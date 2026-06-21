using System.Diagnostics;

namespace NextbotImporter;

internal sealed class EnglishMainForm : Form
{
    private readonly Action _switchLanguage;
    private static readonly Color Navy = Color.FromArgb(15, 23, 42);
    private static readonly Color Blue = Color.FromArgb(37, 99, 235);
    private static readonly Color Pale = Color.FromArgb(241, 245, 249);
    private static readonly Color Muted = Color.FromArgb(100, 116, 139);

    private readonly TextBox _name = Input("Example: Smiley");
    private readonly TextBox _id = Input("Example: smiley (letters, numbers, underscores)");
    private readonly TextBox _category = Input("Example: Kobeblyat NextBots");
    private readonly FilePickerEn _image = new(
        "Character image (required)",
        "Supported images|*.png;*.gif;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|All files|*.*",
        true);
    private readonly FilePickerEn _chase = new("Chase music", "Audio|*.mp3;*.wav|All files|*.*");
    private readonly FilePickerEn _kill = new("Attack / death sound", "Audio|*.mp3;*.wav|All files|*.*");
    private readonly FilePickerEn _jump = new("Jump sound (optional)", "Audio|*.mp3;*.wav|All files|*.*");
    private readonly TextBox _output = Input(@"Select GarrysMod\garrysmod\addons");
    private readonly NumericUpDown _speed = NumberBox(50, 2000, 500, 10);
    private readonly NumericUpDown _size = NumberBox(32, 1024, 128, 8);
    private readonly NumericUpDown _damage = NumberBox(1, 1_000_000, 1_000_000, 10);
    private readonly NumericUpDown _attackDistance = NumberBox(20, 500, 80, 5);
    private readonly ComboBox _imageFit = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 9.5F)
    };
    private readonly CheckBox _adminOnly = Check("Admin-only spawning", false);
    private readonly CheckBox _smashProps = Check("Smash physics props", true);
    private readonly PictureBox _preview = new()
    {
        SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = Color.FromArgb(8, 15, 30),
        Dock = DockStyle.Fill
    };
    private readonly Label _status = new()
    {
        Text = "Ready · PNG / GIF / JPG / JPEG / BMP / TIFF supported",
        Dock = DockStyle.Fill,
        ForeColor = Muted,
        TextAlign = ContentAlignment.MiddleLeft,
        AutoEllipsis = true
    };
    private readonly Button _build = PrimaryButton("Generate NextBot Addon");
    private readonly Button _openOutput = SecondaryButton("Open addons folder");

    public EnglishMainForm(Action switchLanguage)
    {
        _switchLanguage = switchLanguage;
        Text = "Garry's Mod NextBot Importer--Made by Kobeblyat";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 780);
        Size = new Size(1320, 900);
        Font = new Font("Segoe UI", 9F);
        BackColor = Pale;
        ForeColor = Navy;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        _category.Text = "Kobeblyat NextBots";
        _imageFit.Items.AddRange(new object[]
        {
            "Fit entire image (recommended, no crop)",
            "Fill canvas (may crop edges)"
        });
        _imageFit.SelectedIndex = 0;
        _imageFit.SelectedIndexChanged += (_, _) => LoadPreview();
        _output.Text = DetectGmodAddonsPath() ?? "";
        _image.PathChanged += (_, _) => LoadPreview();
        _name.TextChanged += (_, _) =>
        {
            if (!_id.Focused && string.IsNullOrWhiteSpace(_id.Text))
                _id.Text = AddonBuilder.SanitizeId(_name.Text);
        };
        _build.Click += async (_, _) => await BuildAsync();
        _openOutput.Click += (_, _) => OpenFolder(_output.Text);
        Controls.Add(BuildPage());
    }

    private Control BuildPage()
    {
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, BackColor = Pale };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        page.Controls.Add(BuildHeader(), 0, 0);
        page.Controls.Add(BuildBody(), 0, 1);
        var statusBar = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(22, 0, 22, 0) };
        statusBar.Controls.Add(_status);
        page.Controls.Add(statusBar, 0, 2);
        return page;
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Navy,
            Padding = new Padding(28, 15, 28, 12),
            ColumnCount = 2,
            RowCount = 2
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        header.Controls.Add(new Label
        {
            Text = "Garry's Mod NextBot Importer",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        header.Controls.Add(new Label
        {
            Text = "Image + audio → a playable Garry's Mod addon in one click",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 10F),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);
        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0, 10, 0, 10)
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        var language = HeaderButton("🌐  中文");
        language.Click += (_, _) => _switchLanguage();
        var author = HeaderButton("Bilibili · Kobeblyat");
        author.Click += (_, _) => OpenUrl("https://space.bilibili.com/3546897006463032");
        var github = HeaderButton("★ Star on GitHub");
        github.Click += (_, _) => OpenUrl("https://github.com/Kobeblyat/NextBot-Importer");
        actions.Controls.Add(language, 0, 0);
        actions.Controls.Add(author, 1, 0);
        actions.Controls.Add(github, 2, 0);
        header.Controls.Add(actions, 1, 0);
        header.SetRowSpan(actions, 2);
        return header;
    }

    private Control BuildBody()
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        body.Controls.Add(BuildFormCard(), 0, 0);
        body.Controls.Add(BuildPreviewCard(), 1, 0);
        return body;
    }

    private Control BuildFormCard()
    {
        var card = Card();
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(22, 14, 22, 20) };
        var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4 };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        int row = 0;
        Section(form, ref row, "01  Identity and menu");
        Field(form, row, 0, "Display name", _name);
        Field(form, row++, 2, "Internal ID", _id);
        WideField(form, ref row, "NPC menu category (custom)", _category);
        Section(form, ref row, "02  Image and audio");
        WidePicker(form, ref row, _image);
        WideField(form, ref row, "Image fit mode", _imageFit);
        WidePicker(form, ref row, _chase);
        WidePicker(form, ref row, _kill);
        WidePicker(form, ref row, _jump);
        Section(form, ref row, "03  NextBot settings");
        Field(form, row, 0, "Movement speed", _speed);
        Field(form, row++, 2, "Sprite size", _size);
        Field(form, row, 0, "Attack damage", _damage);
        Field(form, row++, 2, "Attack range", _attackDistance);
        var checks = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 34, Padding = new Padding(0, 5, 0, 0) };
        checks.Controls.Add(_adminOnly);
        checks.Controls.Add(_smashProps);
        WideField(form, ref row, "Behavior", checks);
        Section(form, ref row, "04  Export");
        var browse = SecondaryButton("Browse…");
        browse.Dock = DockStyle.Fill;
        browse.Click += (_, _) => BrowseOutput();
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        form.Controls.Add(LabelFor("addons folder"), 0, row);
        form.Controls.Add(_output, 1, row);
        form.SetColumnSpan(_output, 2);
        form.Controls.Add(browse, 3, row);
        scroll.Controls.Add(form);
        card.Controls.Add(scroll);
        return card;
    }

    private Control BuildPreviewCard()
    {
        var card = Card();
        card.Padding = new Padding(18);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.Controls.Add(new Label
        {
            Text = "Export preview",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        var previewFrame = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(203, 213, 225), Padding = new Padding(1) };
        previewFrame.Controls.Add(_preview);
        layout.Controls.Add(previewFrame, 0, 1);
        layout.Controls.Add(new Label
        {
            Text = "GIF animation is preserved; other formats are converted to game-ready PNG textures.\r\nThe preview uses the same fitting mode as the exported addon.",
            Dock = DockStyle.Fill,
            ForeColor = Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 8, 0, 4)
        }, 0, 2);
        _build.Dock = DockStyle.Fill;
        _openOutput.Dock = DockStyle.Fill;
        layout.Controls.Add(_build, 0, 3);
        layout.Controls.Add(_openOutput, 0, 4);
        card.Controls.Add(layout);
        return card;
    }

    private static Panel Card() => new() { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(6), Padding = new Padding(1) };
    private static TextBox Input(string placeholder) => new() { PlaceholderText = placeholder, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.5F), Dock = DockStyle.Fill };
    private static CheckBox Check(string text, bool value) => new() { Text = text, Checked = value, AutoSize = true, Margin = new Padding(0, 3, 22, 0) };
    private static Button PrimaryButton(string text) => new() { Text = text, BackColor = Blue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Cursor = Cursors.Hand };

    private static Button SecondaryButton(string text)
    {
        var button = new Button { Text = text, BackColor = Color.White, ForeColor = Navy, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F), Cursor = Cursors.Hand };
        button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        return button;
    }

    private static Button HeaderButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.FromArgb(226, 232, 240),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            AutoEllipsis = true
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(71, 85, 105);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(51, 65, 85);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(37, 99, 235);
        return button;
    }

    private static Label LabelFor(string text) => new() { Text = text, Dock = DockStyle.Fill, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft };

    private static void Section(TableLayoutPanel panel, ref int row, string text)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        var label = new Label { Text = text, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Blue, TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(0, 0, 0, 8) };
        panel.Controls.Add(label, 0, row);
        panel.SetColumnSpan(label, 4);
        row++;
    }

    private static void Field(TableLayoutPanel panel, int row, int col, string label, Control control)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.Controls.Add(LabelFor(label), col, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 4, 12, 4);
        panel.Controls.Add(control, col + 1, row);
    }

    private static void WideField(TableLayoutPanel panel, ref int row, string label, Control control)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.Controls.Add(LabelFor(label), 0, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 4, 0, 4);
        panel.Controls.Add(control, 1, row);
        panel.SetColumnSpan(control, 3);
        row++;
    }

    private static void WidePicker(TableLayoutPanel panel, ref int row, FilePickerEn picker)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.Controls.Add(LabelFor(picker.Label), 0, row);
        panel.Controls.Add(picker, 1, row);
        panel.SetColumnSpan(picker, 3);
        row++;
    }

    private static NumericUpDown NumberBox(decimal min, decimal max, decimal value, decimal increment) => new()
    {
        Minimum = min, Maximum = max, Value = value, Increment = increment,
        ThousandsSeparator = true, BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI", 9.5F)
    };

    private void LoadPreview()
    {
        _preview.Image?.Dispose();
        _preview.Image = null;
        if (!File.Exists(_image.Value)) return;
        try
        {
            using var source = Image.FromFile(_image.Value);
            _preview.Image = AddonBuilder.CreatePreview(_image.Value, SelectedFitMode());
            _status.Text = $"Loaded {Path.GetFileName(_image.Value)}  ·  {source.Width} × {source.Height}  ·  {Path.GetExtension(_image.Value).ToUpperInvariant()}";
        }
        catch (Exception ex) { _status.Text = "Preview failed: " + ex.Message; }
    }

    private async Task BuildAsync()
    {
        _build.Enabled = false;
        try
        {
            string safeId = AddonBuilder.SanitizeId(_id.Text);
            string existingPath = Path.Combine(_output.Text.Trim(), safeId + "_nextbot");
            if (safeId.Length >= 2 && Directory.Exists(existingPath))
            {
                var overwrite = MessageBox.Show($"An addon already exists:\n{existingPath}\n\nOverwrite it?", "Confirm overwrite", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (overwrite != DialogResult.Yes) return;
            }
            var options = new NextbotOptions(
                _name.Text.Trim(), _id.Text.Trim(), _category.Text.Trim(),
                _image.Value, _chase.Value, _kill.Value, _jump.Value,
                _output.Text.Trim(), (int)_speed.Value, (int)_size.Value,
                (int)_damage.Value, (int)_attackDistance.Value, SelectedFitMode(),
                true,
                _adminOnly.Checked, _smashProps.Checked);
            var progress = new Progress<string>(message => _status.Text = message);
            BuildResult result = await Task.Run(() => AddonBuilder.Build(options, progress));
            _status.Text = $"Export complete · {result.AddonPath}";
            MessageBox.Show(
                $"NextBot exported successfully!\n\nCategory: {_category.Text.Trim()}\nLocation: {result.AddonPath}\nImage frames: {result.FrameCount}\n\nRestart Garry's Mod or change map, then look under NPCs and your custom category.",
                "Export complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenFolder(result.AddonPath);
        }
        catch (Exception ex)
        {
            _status.Text = "Export failed · " + ex.Message;
            MessageBox.Show(ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { _build.Enabled = true; }
    }

    private void BrowseOutput()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = @"Select the GarrysMod\garrysmod\addons folder",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_output.Text) ? _output.Text : ""
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _output.Text = dialog.SelectedPath;
    }

    private static void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            MessageBox.Show("The folder does not exist. Please select a valid location.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    private ImageFitMode SelectedFitMode() => _imageFit.SelectedIndex == 1 ? ImageFitMode.Cover : ImageFitMode.Contain;

    private static string? DetectGmodAddonsPath()
    {
        var candidates = new List<string>();
        string x86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(x86))
            candidates.Add(Path.Combine(x86, "Steam", "steamapps", "common", "GarrysMod", "garrysmod", "addons"));
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, "SteamLibrary", "steamapps", "common", "GarrysMod", "garrysmod", "addons"));
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, "Steam", "steamapps", "common", "GarrysMod", "garrysmod", "addons"));
        }
        return candidates.FirstOrDefault(Directory.Exists);
    }
}

internal sealed class FilePickerEn : UserControl
{
    private readonly TextBox _path = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.5F) };
    public string Label { get; }
    public string Value => _path.Text.Trim();
    public event EventHandler? PathChanged;

    public FilePickerEn(string label, string filter, bool required = false)
    {
        Label = label;
        Dock = DockStyle.Fill;
        Height = 34;
        Margin = new Padding(0, 3, 0, 3);
        var button = new Button { Text = "Choose file…", Dock = DockStyle.Right, Width = 100, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand };
        button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        button.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK) _path.Text = dialog.FileName;
        };
        _path.PlaceholderText = required ? "Required" : "Optional";
        _path.TextChanged += (_, _) => PathChanged?.Invoke(this, EventArgs.Empty);
        Controls.Add(_path);
        Controls.Add(button);
    }
}
