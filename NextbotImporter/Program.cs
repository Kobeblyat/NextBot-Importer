using System.Globalization;
using System.Text;

namespace NextbotImporter;

internal enum AppLanguage
{
    Chinese,
    English
}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        AppLanguage initialLanguage = LanguageSettings.Load() ??
            (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
                ? AppLanguage.Chinese
                : AppLanguage.English);

        using var notice = new ResponsibleUseForm(initialLanguage);
        if (notice.ShowDialog() != DialogResult.OK) return;

        Application.Run(new LanguageApplicationContext(initialLanguage));
    }
}

internal sealed class LanguageApplicationContext : ApplicationContext
{
    private Form? _current;
    private bool _switching;

    public LanguageApplicationContext(AppLanguage language) => ShowLanguage(language);

    private void ShowLanguage(AppLanguage language)
    {
        LanguageSettings.Save(language);
        _switching = true;
        Form? previous = _current;
        _current = language == AppLanguage.Chinese
            ? new MainForm(() => ShowLanguage(AppLanguage.English))
            : new EnglishMainForm(() => ShowLanguage(AppLanguage.Chinese));

        _current.FormClosed += (_, _) =>
        {
            if (!_switching) ExitThread();
        };
        MainForm = _current;
        _current.Show();
        previous?.Close();
        previous?.Dispose();
        _switching = false;
    }
}

internal static class LanguageSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Kobeblyat",
        "NextBotImporter");
    private static readonly string SettingsFile = Path.Combine(SettingsDirectory, "language.txt");

    public static AppLanguage? Load()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return null;
            string value = File.ReadAllText(SettingsFile, Encoding.UTF8).Trim();
            return value.Equals("zh", StringComparison.OrdinalIgnoreCase)
                ? AppLanguage.Chinese
                : value.Equals("en", StringComparison.OrdinalIgnoreCase)
                    ? AppLanguage.English
                    : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(AppLanguage language)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(
                SettingsFile,
                language == AppLanguage.Chinese ? "zh" : "en",
                new UTF8Encoding(false));
        }
        catch
        {
            // A read-only profile should not prevent the application from running.
        }
    }
}

internal sealed class ResponsibleUseForm : Form
{
    private static readonly Color Navy = Color.FromArgb(15, 23, 42);
    private static readonly Color Blue = Color.FromArgb(37, 99, 235);
    private readonly AppLanguage _language;

    public ResponsibleUseForm(AppLanguage language)
    {
        _language = language;
        Text = language == AppLanguage.Chinese ? "使用提醒" : "Please use responsibly";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(640, 360);
        BackColor = Color.FromArgb(241, 245, 249);
        Font = new Font(language == AppLanguage.Chinese ? "Microsoft YaHei UI" : "Segoe UI", 10F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Controls.Add(BuildContent());
    }

    private Control BuildContent()
    {
        bool zh = _language == AppLanguage.Chinese;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Navy, Padding = new Padding(26, 12, 26, 10) };
        header.Controls.Add(new Label
        {
            Text = zh ? "请负责任地使用这个工具" : "Please use this tool responsibly",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font(Font.FontFamily, 17F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });

        var message = new Label
        {
            Text = zh
                ? "尽管这个软件很方便，但我不建议你利用这个完成NextBot大量水作往创意工坊搬，这很让人不舒服"
                : "Although this tool is convenient, please do not use it to flood the Steam Workshop with large numbers of low-effort NextBots. It makes the community unpleasant.",
            Dock = DockStyle.Fill,
            ForeColor = Navy,
            Font = new Font(Font.FontFamily, 12F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(34, 28, 34, 24)
        };

        var footer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(0, 14, 24, 14) };
        var continueButton = new Button
        {
            Text = zh ? "我知道了，继续" : "I understand — continue",
            DialogResult = DialogResult.OK,
            Dock = DockStyle.Right,
            Width = 190,
            BackColor = Blue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(Font.FontFamily, 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        continueButton.FlatAppearance.BorderSize = 0;
        footer.Controls.Add(continueButton);
        AcceptButton = continueButton;

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(message, 0, 1);
        root.Controls.Add(footer, 0, 2);
        return root;
    }
}
