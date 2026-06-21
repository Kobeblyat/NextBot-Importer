using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NextbotImporter;

internal enum ImageFitMode
{
    Contain,
    Cover
}

internal sealed record NextbotOptions(
    string DisplayName,
    string InternalId,
    string Category,
    string ImagePath,
    string ChaseSoundPath,
    string KillSoundPath,
    string JumpSoundPath,
    string OutputAddonsPath,
    int Speed,
    int SpriteSize,
    int Damage,
    int AttackDistance,
    ImageFitMode ImageFit,
    bool English,
    bool AdminOnly,
    bool SmashProps);

internal sealed record BuildResult(string AddonPath, int FrameCount);

internal static class AddonBuilder
{
    private static readonly HashSet<string> SupportedAudio =
        new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav" };
    private static readonly HashSet<string> SupportedImages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".gif", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff"
        };

    public static string SanitizeId(string value)
    {
        value = value.Trim().ToLowerInvariant();
        value = Regex.Replace(value, @"[^a-z0-9_]+", "_");
        value = Regex.Replace(value, @"_+", "_").Trim('_');
        if (value.Length > 40) value = value[..40].TrimEnd('_');
        return value;
    }

    public static BuildResult Build(NextbotOptions options, IProgress<string>? progress = null)
    {
        Validate(options);

        string id = SanitizeId(options.InternalId);
        string className = "npc_" + id;
        string addonFolderName = id + "_nextbot";
        string outputRoot = Path.GetFullPath(options.OutputAddonsPath);
        string addonPath = Path.Combine(outputRoot, addonFolderName);
        if (!string.Equals(Path.GetDirectoryName(addonPath), outputRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("导出路径不安全。");
        string tempPath = addonPath + ".building_" + Guid.NewGuid().ToString("N")[..8];

        progress?.Report(options.English ? "Creating addon folders…" : "正在创建 addon 目录…");
        Directory.CreateDirectory(tempPath);

        try
        {
            string luaDir = Path.Combine(tempPath, "lua", "entities");
            string entityMaterialDir = Path.Combine(tempPath, "materials", "entities");
            string spriteMaterialDir = Path.Combine(tempPath, "materials", className);
            string soundDir = Path.Combine(tempPath, "sound", className);
            Directory.CreateDirectory(luaDir);
            Directory.CreateDirectory(entityMaterialDir);
            Directory.CreateDirectory(spriteMaterialDir);
            Directory.CreateDirectory(soundDir);

            progress?.Report(options.English ? "Processing images and GIF frames…" : "正在处理图片与 GIF 帧…");
            var frames = ExportFrames(
                options.ImagePath, spriteMaterialDir, entityMaterialDir, className, options.ImageFit);

            var sounds = new Dictionary<string, string?>();
            sounds["chase"] = CopySound(options.ChaseSoundPath, soundDir, "chase");
            sounds["kill"] = CopySound(options.KillSoundPath, soundDir, "kill");
            sounds["jump"] = CopySound(options.JumpSoundPath, soundDir, "jump");

            progress?.Report(options.English ? "Generating NextBot Lua…" : "正在生成 Nextbot Lua…");
            string lua = CreateLua(options, className, frames.DelaysMs, sounds);
            File.WriteAllText(Path.Combine(luaDir, className + ".lua"), lua, new UTF8Encoding(false));

            var addonJson = new
            {
                title = options.DisplayName + " Nextbot",
                type = "npc",
                tags = new[] { "fun", "cartoon" },
                ignore = Array.Empty<string>()
            };
            File.WriteAllText(
                Path.Combine(tempPath, "addon.json"),
                JsonSerializer.Serialize(addonJson, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));

            File.WriteAllText(
                Path.Combine(tempPath, "README_使用说明.txt"),
                CreateReadme(options, className),
                new UTF8Encoding(true));

            if (Directory.Exists(addonPath))
            {
                progress?.Report(options.English ? "Replacing the previous addon…" : "正在替换旧版本 addon…");
                Directory.Delete(addonPath, true);
            }

            Directory.Move(tempPath, addonPath);
            progress?.Report(options.English ? "Done!" : "完成！");
            return new BuildResult(addonPath, frames.DelaysMs.Count);
        }
        catch
        {
            if (Directory.Exists(tempPath))
                Directory.Delete(tempPath, true);
            throw;
        }
    }

    private static void Validate(NextbotOptions o)
    {
        if (string.IsNullOrWhiteSpace(o.DisplayName))
            throw new InvalidOperationException(o.English ? "Enter a NextBot display name." : "请输入 Nextbot 显示名称。");
        if (SanitizeId(o.InternalId).Length < 2)
            throw new InvalidOperationException(o.English ? "The internal ID must contain at least two letters or numbers." : "内部 ID 至少需要 2 个英文字母或数字。");
        if (!File.Exists(o.ImagePath))
            throw new FileNotFoundException(o.English ? "The image file was not found." : "找不到图片文件。", o.ImagePath);
        if (!SupportedImages.Contains(Path.GetExtension(o.ImagePath)))
            throw new InvalidOperationException(o.English ? "Supported images: PNG, GIF, JPG/JPEG, BMP, and TIF/TIFF." : "图片支持 PNG、GIF、JPG/JPEG、BMP、TIF/TIFF。");
        ValidateOptionalSound(o.ChaseSoundPath, o.English ? "chase audio" : "追逐音效", o.English);
        ValidateOptionalSound(o.KillSoundPath, o.English ? "death audio" : "死亡音效", o.English);
        ValidateOptionalSound(o.JumpSoundPath, o.English ? "jump audio" : "跳跃音效", o.English);
        if (string.IsNullOrWhiteSpace(o.OutputAddonsPath))
            throw new InvalidOperationException(o.English ? "Select the garrysmod/addons folder." : "请选择 garrysmod/addons 文件夹。");
        Directory.CreateDirectory(o.OutputAddonsPath);
    }

    private static void ValidateOptionalSound(string path, string label, bool english)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!File.Exists(path))
            throw new FileNotFoundException(english ? $"The {label} file was not found." : $"找不到{label}。", path);
        if (!SupportedAudio.Contains(Path.GetExtension(path)))
            throw new InvalidOperationException(english ? $"{label} supports MP3 or WAV only." : $"{label}仅支持 MP3 或 WAV。");
    }

    private sealed record FrameExportResult(List<int> DelaysMs);

    private static FrameExportResult ExportFrames(
        string sourcePath,
        string frameDir,
        string iconDir,
        string className,
        ImageFitMode fitMode)
    {
        const int TextureSize = 1024;
        using var image = Image.FromFile(sourcePath);
        var dimension = new FrameDimension(image.FrameDimensionsList[0]);
        int count = Math.Max(1, image.GetFrameCount(dimension));
        var delays = ReadGifDelays(image, count);

        for (int i = 0; i < count; i++)
        {
            if (count > 1) image.SelectActiveFrame(dimension, i);
            using var frame = RenderToSquare(image, TextureSize, fitMode);
            frame.Save(Path.Combine(frameDir, $"frame_{i:000}.png"), ImageFormat.Png);

            if (i == 0)
            {
                using var icon = RenderToSquare(frame, 512, ImageFitMode.Contain);
                icon.Save(Path.Combine(iconDir, className + ".png"), ImageFormat.Png);
            }
        }

        return new FrameExportResult(delays);
    }

    private static List<int> ReadGifDelays(Image image, int count)
    {
        var result = Enumerable.Repeat(100, count).ToList();
        try
        {
            const int FrameDelayProperty = 0x5100;
            PropertyItem? item = image.GetPropertyItem(FrameDelayProperty);
            if (item?.Value is null) return result;
            for (int i = 0; i < count && i * 4 + 3 < item.Value.Length; i++)
            {
                int hundredths = BitConverter.ToInt32(item.Value, i * 4);
                result[i] = Math.Clamp(hundredths * 10, 20, 10000);
            }
        }
        catch (ArgumentException)
        {
            // PNG and GIFs without delay metadata use 100ms.
        }
        return result;
    }

    internal static Bitmap CreatePreview(string sourcePath, ImageFitMode fitMode, int size = 720)
    {
        using var image = Image.FromFile(sourcePath);
        return RenderToSquare(image, size, fitMode);
    }

    private static Bitmap RenderToSquare(Image source, int size, ImageFitMode fitMode)
    {
        var output = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(output);
        g.Clear(Color.Transparent);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        float scale = fitMode == ImageFitMode.Cover
            ? Math.Max((float)size / source.Width, (float)size / source.Height)
            : Math.Min((float)size / source.Width, (float)size / source.Height);
        int width = Math.Max(1, (int)(source.Width * scale));
        int height = Math.Max(1, (int)(source.Height * scale));
        g.DrawImage(source, (size - width) / 2, (size - height) / 2, width, height);
        return output;
    }

    private static string? CopySound(string source, string targetDir, string baseName)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        string extension = Path.GetExtension(source).ToLowerInvariant();
        string fileName = baseName + extension;
        File.Copy(source, Path.Combine(targetDir, fileName), true);
        return fileName;
    }

    private static string LuaString(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\r", "").Replace("\n", "\\n") + "\"";

    private static string CreateLua(
        NextbotOptions o,
        string className,
        IReadOnlyList<int> frameDelays,
        IReadOnlyDictionary<string, string?> sounds)
    {
        string id = SanitizeId(o.InternalId);
        string frameDelayLua = string.Join(", ", frameDelays.Select(ms =>
            (ms / 1000.0).ToString("0.###", CultureInfo.InvariantCulture)));
        string chase = sounds["chase"] is null ? "nil" : LuaString($"{className}/{sounds["chase"]}");
        string kill = sounds["kill"] is null ? "nil" : LuaString($"{className}/{sounds["kill"]}");
        string jump = sounds["jump"] is null ? "nil" : LuaString($"{className}/{sounds["jump"]}");
        string display = LuaString(o.DisplayName);
        string category = LuaString(string.IsNullOrWhiteSpace(o.Category) ? "Nextbot" : o.Category.Trim());
        string audioError = o.English ? "Audio failed to load: " : "音效加载失败: ";
        string chaseError = o.English ? "Chase music failed to load: " : "追逐音乐加载失败: ";

        return $$"""
AddCSLuaFile()

ENT.Base = "base_nextbot"
ENT.PrintName = {{display}}
ENT.Category = {{category}}
ENT.Spawnable = true
ENT.AdminOnly = {{o.AdminOnly.ToString().ToLowerInvariant()}}
ENT.AutomaticFrameAdvance = false
ENT.PhysgunDisabled = true

local CLASS = "{{className}}"
local CHASE_SOUND = {{chase}}
local KILL_SOUND = {{kill}}
local JUMP_SOUND = {{jump}}

if SERVER then
    local cvSpeed = CreateConVar(CLASS .. "_speed", "{{o.Speed}}", FCVAR_ARCHIVE, "Nextbot movement speed")
    local cvDamage = CreateConVar(CLASS .. "_damage", "{{o.Damage}}", FCVAR_ARCHIVE, "Nextbot attack damage")
    local cvAttackDistance = CreateConVar(CLASS .. "_attack_distance", "{{o.AttackDistance}}", FCVAR_ARCHIVE, "Nextbot attack range")
    local cvMusicDistance = CreateConVar(CLASS .. "_music_distance", "1200", FCVAR_ARCHIVE, "Chase music range")

    util.AddNetworkString(CLASS .. "_music")
    util.AddNetworkString(CLASS .. "_oneshot")

    function ENT:Initialize()
        self:SetHealth(1000000)
        self:SetBloodColor(DONT_BLEED)
        self:SetCollisionBounds(Vector(-13, -13, 0), Vector(13, 13, 72))
        self:SetRenderMode(RENDERMODE_TRANSALPHA)
        self:SetColor(Color(255, 255, 255, 1))
        self.loco:SetDesiredSpeed(cvSpeed:GetFloat())
        self.loco:SetAcceleration(1200)
        self.loco:SetDeceleration(800)
        self.loco:SetJumpHeight(300)
        self.NextAttack = 0
        self.NextMusicUpdate = 0
    end

    function ENT:OnInjured(dmg)
        dmg:SetDamage(0)
    end

    local function validTarget(ent)
        if not IsValid(ent) then return false end
        if ent:IsPlayer() then return ent:Alive() and not GetConVar("ai_ignoreplayers"):GetBool() end
        return ent:IsNPC() and ent:Health() > 0 and ent:GetClass() ~= CLASS
    end

    function ENT:FindTarget()
        local nearest, nearestDist = nil, math.huge
        for _, ent in ipairs(ents.FindInSphere(self:GetPos(), 100000)) do
            if validTarget(ent) then
                local dist = self:GetPos():DistToSqr(ent:GetPos())
                if dist < nearestDist then
                    nearest, nearestDist = ent, dist
                end
            end
        end
        return nearest
    end

    function ENT:AttackNearby()
        if CurTime() < self.NextAttack then return end
        local radius = cvAttackDistance:GetFloat()
        for _, ent in ipairs(ents.FindInSphere(self:GetPos() + Vector(0, 0, 36), radius)) do
            if validTarget(ent) then
                local direction = (ent:GetPos() - self:GetPos()):GetNormalized()
                local dmg = DamageInfo()
                dmg:SetAttacker(self)
                dmg:SetInflictor(self)
                dmg:SetDamage(cvDamage:GetFloat())
                dmg:SetDamageType(DMG_CRUSH)
                dmg:SetDamageForce((direction * 1400 + Vector(0, 0, 500)) * 100)
                ent:TakeDamageInfo(dmg)
                ent:SetVelocity(direction * 1400 + Vector(0, 0, 500))
                if KILL_SOUND then
                    self:EmitSound(KILL_SOUND, 100, 100)
                    net.Start(CLASS .. "_oneshot")
                        net.WriteUInt(1, 2)
                    net.Broadcast()
                end
                self.NextAttack = CurTime() + 0.35
            elseif {{o.SmashProps.ToString().ToLowerInvariant()}} and ent:GetMoveType() == MOVETYPE_VPHYSICS then
                local phys = ent:GetPhysicsObject()
                if IsValid(phys) and phys:IsMotionEnabled() then
                    constraint.RemoveAll(ent)
                    phys:ApplyForceCenter((ent:GetPos() - self:GetPos()):GetNormalized() * math.max(phys:GetMass(), 20) * 1000)
                end
            end
        end
    end

    function ENT:UpdateMusic()
        if CurTime() < self.NextMusicUpdate then return end
        self.NextMusicUpdate = CurTime() + 0.2
        local distance = cvMusicDistance:GetFloat()
        for _, ply in ipairs(player.GetHumans()) do
            if ply:GetPos():DistToSqr(self:GetPos()) <= distance * distance then
                net.Start(CLASS .. "_music")
                    net.WriteEntity(self)
                    net.WriteFloat(distance)
                net.Send(ply)
            end
        end
    end

    function ENT:RunBehaviour()
        while true do
            local target = self:FindTarget()
            if IsValid(target) then
                local path = Path("Follow")
                path:SetMinLookAheadDistance(300)
                path:SetGoalTolerance(20)
                path:Compute(self, target:GetPos())

                if path:IsValid() then
                    while path:IsValid() and IsValid(target) and validTarget(target) do
                        path:Update(self)
                        self:AttackNearby()
                        self:UpdateMusic()

                        if self.loco:IsStuck() then
                            self:HandleStuck()
                            return
                        end

                        if target:GetPos().z - self:GetPos().z > 60 and self:IsOnGround() then
                            self.loco:Jump()
                            if JUMP_SOUND then
                                self:EmitSound(JUMP_SOUND, 90, 100)
                                net.Start(CLASS .. "_oneshot")
                                    net.WriteUInt(2, 2)
                                net.Broadcast()
                            end
                        end

                        if path:GetAge() > 0.12 then path:Compute(self, target:GetPos()) end
                        coroutine.yield()
                    end
                end
            else
                coroutine.wait(0.2)
            end
            coroutine.yield()
        end
    end

    function ENT:HandleStuck()
        self:SetPos(self:GetPos() + Vector(0, 0, 24))
        self.loco:ClearStuck()
    end
else
    local FRAME_COUNT = {{frameDelays.Count}}
    local FRAME_DELAYS = { {{frameDelayLua}} }
    local MATERIALS = {}
    for i = 0, FRAME_COUNT - 1 do
        MATERIALS[i + 1] = Material(CLASS .. "/frame_" .. string.format("%03d", i) .. ".png", "smooth")
    end

    killicon.Add(CLASS, "entities/" .. CLASS .. ".png", color_white)
    language.Add(CLASS, {{display}})

    ENT.RenderGroup = RENDERGROUP_TRANSLUCENT

    function ENT:Initialize()
        self.FrameIndex = 1
        self.NextFrameAt = CurTime() + FRAME_DELAYS[1]
        local size = {{o.SpriteSize}}
        self:SetRenderBounds(Vector(-size, -size, 0), Vector(size, size, size * 2))
    end

    function ENT:DrawTranslucent()
        if FRAME_COUNT > 1 and CurTime() >= self.NextFrameAt then
            self.FrameIndex = self.FrameIndex % FRAME_COUNT + 1
            self.NextFrameAt = CurTime() + FRAME_DELAYS[self.FrameIndex]
        end

        local size = {{o.SpriteSize}}
        local pos = self:GetPos() + Vector(0, 0, size / 2)
        local normal = EyePos() - pos
        normal:Normalize()
        local xy = Vector(normal.x, normal.y, 0)
        xy:Normalize()
        local pitch = math.acos(math.Clamp(normal:Dot(xy), -1, 1)) / 3
        local c = math.cos(pitch)
        normal = Vector(xy.x * c, xy.y * c, math.sin(pitch))

        render.SetMaterial(MATERIALS[self.FrameIndex])
        render.DrawQuadEasy(pos, normal, size, size, color_white, 180)
    end

    local music
    local musicLoading = false
    local musicEntity
    local lastSeen = 0
    local musicDistance = 1200

    local oneShotChannels = {}
    local function playOneShot(path)
        sound.PlayFile("sound/" .. path, "noblock", function(channel, errorId, errorName)
            if not IsValid(channel) then
                chat.AddText(Color(255, 100, 100), "[" .. CLASS .. "] {{audioError}}" .. tostring(errorName))
                return
            end
            table.insert(oneShotChannels, channel)
            channel:SetVolume(1)
            channel:Play()
            timer.Simple(15, function()
                if IsValid(channel) then channel:Stop() end
                table.RemoveByValue(oneShotChannels, channel)
            end)
        end)
    end

    net.Receive(CLASS .. "_oneshot", function()
        local soundType = net.ReadUInt(2)
        if soundType == 1 and KILL_SOUND then
            playOneShot(KILL_SOUND)
        elseif soundType == 2 and JUMP_SOUND then
            playOneShot(JUMP_SOUND)
        end
    end)

    net.Receive(CLASS .. "_music", function()
        musicEntity = net.ReadEntity()
        musicDistance = net.ReadFloat()
        lastSeen = CurTime()
        if CHASE_SOUND and not IsValid(music) and not musicLoading then
            musicLoading = true
            sound.PlayFile("sound/" .. CHASE_SOUND, "noblock", function(channel, errorId, errorName)
                musicLoading = false
                if not IsValid(channel) then
                    chat.AddText(Color(255, 100, 100), "[" .. CLASS .. "] {{chaseError}}" .. tostring(errorName))
                    return
                end
                music = channel
                music:EnableLooping(true)
                music:SetVolume(0.05)
                music:Play()
            end)
        end
    end)

    hook.Add("Think", CLASS .. "_music_think", function()
        if not IsValid(music) then return end
        if not IsValid(musicEntity) or CurTime() - lastSeen > 0.5 or not LocalPlayer():Alive() then
            music:SetVolume(0)
            if CurTime() - lastSeen > 2 then
                music:Stop()
                music = nil
            end
            return
        end
        local distance = LocalPlayer():GetPos():Distance(musicEntity:GetPos())
        local volume = math.Clamp(1 - distance / musicDistance, 0.03, 1)
        music:Play()
        music:SetVolume(volume)
    end)
end

list.Set("NPC", CLASS, {
    Name = {{display}},
    Class = CLASS,
    Category = {{category}},
    AdminOnly = {{o.AdminOnly.ToString().ToLowerInvariant()}}
})
""";
    }

    private static string CreateReadme(NextbotOptions o, string className) => o.English ? $"""
{o.DisplayName} NextBot
========================

Entity class: {className}

Installation:
This folder is already under garrysmod/addons and does not need to be moved.

In-game:
1. Restart Garry's Mod or change map.
2. Enter Sandbox and open the spawn menu (Q by default).
3. Find “{o.DisplayName}” under NPCs → {o.Category}.
4. The map needs a Nav Mesh for NextBots to move.

If the map has no Nav Mesh:
Run nav_generate in the console. It may take a long time and restart the map.

Console variables:
{className}_speed
{className}_damage
{className}_attack_distance
{className}_music_distance
""" : $"""
{o.DisplayName} Nextbot
========================

实体类名：{className}

安装位置：
此文件夹已经位于 garrysmod/addons 中，无需再次移动。

游戏内使用：
1. 重启 Garry's Mod 或切换地图。
2. 进入沙盒模式，打开生成菜单（默认 Q）。
3. 在 NPC → {o.Category} 中找到“{o.DisplayName}”。
4. 地图必须带有 Nav Mesh，Nextbot 才能移动。

如果地图没有 Nav Mesh：
在控制台执行 nav_generate（可能耗时较久，并会重启地图）。

可调控制台变量：
{className}_speed
{className}_damage
{className}_attack_distance
{className}_music_distance
""";
}
