using NextbotImporter;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: NextbotImporter.Tests <workspace>");
    return 2;
}

string root = Path.GetFullPath(args[0]);
string source = Path.Combine(root, "obunga_nextbot");
string output = Path.Combine(root, "test_output", "addons");
string jpgInput = Path.Combine(root, "test_output", "input_test.jpg");
Directory.CreateDirectory(Path.GetDirectoryName(jpgInput)!);
using (var sourceImage = System.Drawing.Image.FromFile(
    Path.Combine(source, "materials", "entities", "npc_obunga.png")))
{
    using var largeWide = new System.Drawing.Bitmap(5000, 1000);
    using var graphics = System.Drawing.Graphics.FromImage(largeWide);
    graphics.Clear(System.Drawing.Color.White);
    graphics.DrawImage(sourceImage, 0, 0, 1000, 1000);
    graphics.FillRectangle(System.Drawing.Brushes.Red, 4000, 0, 1000, 1000);
    largeWide.Save(jpgInput, System.Drawing.Imaging.ImageFormat.Jpeg);
}

var options = new NextbotOptions(
    "Obunga 测试",
    "obunga_test",
    "Kobeblyat 自定义分类",
    jpgInput,
    Path.Combine(source, "sound", "npc_obunga", "panic.mp3.mp3"),
    Path.Combine(source, "sound", "npc_obunga", "pieceofcake.mp3.mp3"),
    Path.Combine(source, "sound", "npc_obunga", "jump.mp3"),
    output,
    500,
    128,
    1_000_000,
    80,
    ImageFitMode.Contain,
    false,
    false,
    true);

BuildResult result = AddonBuilder.Build(options, new Progress<string>(Console.WriteLine));

string addon = result.AddonPath;
string lua = Path.Combine(addon, "lua", "entities", "npc_obunga_test.lua");
string icon = Path.Combine(addon, "materials", "entities", "npc_obunga_test.png");
string frame = Path.Combine(addon, "materials", "npc_obunga_test", "frame_000.png");
string addonJson = Path.Combine(addon, "addon.json");

foreach (string required in new[] { lua, icon, frame, addonJson })
{
    if (!File.Exists(required)) throw new Exception("Missing generated file: " + required);
}

string luaText = File.ReadAllText(lua);
foreach (string expected in new[]
{
    "ENT.Base = \"base_nextbot\"",
    "local CLASS = \"npc_obunga_test\"",
    "Material(CLASS .. \"/frame_\"",
    "sound.PlayFile(\"sound/\" .. CHASE_SOUND",
    "playOneShot(KILL_SOUND)",
    "net.Broadcast()",
    "Category = \"Kobeblyat 自定义分类\"",
    "list.Set(\"NPC\", CLASS"
})
{
    if (!luaText.Contains(expected, StringComparison.Ordinal))
        throw new Exception("Generated Lua missing: " + expected);
}

using (var generatedFrame = System.Drawing.Image.FromFile(frame))
{
    if (generatedFrame.Width != 1024 || generatedFrame.Height != 1024)
        throw new Exception($"Generated texture is not 1024x1024: {generatedFrame.Size}");
}

Console.WriteLine($"PASS: {addon}");
Console.WriteLine($"Frames: {result.FrameCount}");
return 0;
