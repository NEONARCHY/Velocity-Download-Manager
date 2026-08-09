using System.Diagnostics;
using System.ComponentModel;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VelocityDownload;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 4 && args[0] == "--self-test")
        {
            RunSelfTest(args[1], args[2], args[3]).GetAwaiter().GetResult();
            return;
        }
        if (args.Length == 4 && args[0] == "--resume-test")
        {
            RunResumeTest(args[1], args[2], args[3]).GetAwaiter().GetResult();
            return;
        }
        if (args.Length == 5 && args[0] == "--multi-test")
        {
            RunMultiTest(args[1], int.Parse(args[2]), args[3], args[4]).GetAwaiter().GetResult();
            return;
        }
        if (args.Length == 4 && args[0] == "--discard-test")
        {
            RunDiscardTest(args[1], args[2], args[3]).GetAwaiter().GetResult();
            return;
        }
        ApplicationConfiguration.Initialize();
        if (args.Length == 2 && (args[0] == "--render-ui" || args[0] == "--render-ui-small"))
        {
            var preview = new MainForm();
            if (args[0] == "--render-ui-small") preview.ClientSize = new Size(800, 560);
            preview.AddPreviewCards();
            preview.Shown += async (_, _) =>
            {
                try
                {
                    preview.TopMost = true;
                    preview.Activate();
                    preview.BringToFront();
                    if (args[0] == "--render-ui-small")
                    {
                        foreach (var width in new[] { 920, 810, 980, 800 })
                        {
                            preview.ClientSize = new Size(width, 560);
                            await Task.Delay(80);
                        }
                    }
                    await Task.Delay(700);
                    using var bitmap = new Bitmap(preview.Width, preview.Height);
                    using (var graphics = Graphics.FromImage(bitmap))
                        graphics.CopyFromScreen(preview.Location, Point.Empty, preview.Size);
                    bitmap.Save(args[1]);
                }
                catch (Exception ex) { File.WriteAllText(args[1] + ".error.txt", ex.ToString()); }
                finally { preview.Close(); }
            };
            Application.Run(preview);
            return;
        }
        Application.Run(new MainForm());
    }

    private static async Task RunSelfTest(string url, string folder, string resultFile)
    {
        try
        {
            var engine = new DownloadEngine(new Uri(url), folder, 8);
            var output = await engine.RunAsync(CancellationToken.None);
            await File.WriteAllTextAsync(resultFile, "OK\n" + output);
        }
        catch (Exception ex) { await File.WriteAllTextAsync(resultFile, "ERROR\n" + ex); }
    }

    private static async Task RunResumeTest(string url, string folder, string resultFile)
    {
        try
        {
            var first = new DownloadEngine(new Uri(url), folder, 8);
            using var pause = new CancellationTokenSource(TimeSpan.FromMilliseconds(350));
            try { await first.RunAsync(pause.Token); }
            catch (OperationCanceledException) { }
            var cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VelocityDownload", "Cache");
            var saved = Directory.Exists(cacheFolder) && Directory.EnumerateFiles(cacheFolder, "*.json").Any(path =>
            {
                try { return JsonSerializer.Deserialize<DownloadState>(File.ReadAllText(path))?.Url == url; }
                catch { return false; }
            });
            var second = new DownloadEngine(new Uri(url), folder, 8);
            var output = await second.RunAsync(CancellationToken.None);
            await File.WriteAllTextAsync(resultFile, $"OK\nSTATE_SAVED={saved}\n{output}");
        }
        catch (Exception ex) { await File.WriteAllTextAsync(resultFile, "ERROR\n" + ex); }
    }

    private static async Task RunMultiTest(string baseUrl, int count, string folder, string resultFile)
    {
        try
        {
            var jobs = Enumerable.Range(1, count)
                .Select(i => new DownloadEngine(new Uri(baseUrl.TrimEnd('/') + $"/payload-{i}.bin"), folder, 8).RunAsync(CancellationToken.None));
            var outputs = await Task.WhenAll(jobs);
            await File.WriteAllTextAsync(resultFile, "OK\n" + string.Join('\n', outputs));
        }
        catch (Exception ex) { await File.WriteAllTextAsync(resultFile, "ERROR\n" + ex); }
    }

    private static async Task RunDiscardTest(string url, string folder, string resultFile)
    {
        try
        {
            var engine = new DownloadEngine(new Uri(url), folder, 8);
            using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(350));
            try { await engine.RunAsync(cancel.Token); }
            catch (OperationCanceledException) { }
            var deleted = await DownloadEngine.DeleteCachedDownloadAsync(new Uri(url), folder);
            await File.WriteAllTextAsync(resultFile, $"OK\nCACHE_DELETED={deleted}\nTARGET_FILES={Directory.EnumerateFiles(folder).Count()}");
        }
        catch (Exception ex) { await File.WriteAllTextAsync(resultFile, "ERROR\n" + ex); }
    }
}

internal sealed class LegacyMainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(15, 18, 25);
    private static readonly Color Card = Color.FromArgb(25, 30, 40);
    private static readonly Color Input = Color.FromArgb(34, 40, 52);
    private static readonly Color Accent = Color.FromArgb(83, 109, 254);
    private static readonly Color Accent2 = Color.FromArgb(69, 214, 163);
    private static readonly Color PrimaryText = Color.FromArgb(239, 242, 248);
    private static readonly Color SecondaryText = Color.FromArgb(148, 158, 178);

    private readonly TextBox _url = MakeTextBox("Ð’ÑÑ‚Ð°Ð²ÑŒÑ‚Ðµ Ð¿Ñ€ÑÐ¼ÑƒÑŽ ÑÑÑ‹Ð»ÐºÑƒ https://...");
    private readonly TextBox _folder = MakeTextBox("");
    private readonly ComboBox _connections = new();
    private readonly Button _start = MakeButton("Ð¡ÐšÐÐ§ÐÐ¢Ð¬", Accent, Color.White);
    private readonly Button _pause = MakeButton("ÐŸÐÐ£Ð—Ð", Input, PrimaryText);
    private readonly Button _paste = MakeButton("Ð’Ð¡Ð¢ÐÐ’Ð˜Ð¢Ð¬", Input, PrimaryText);
    private readonly Button _browse = MakeButton("ÐžÐ‘Ð—ÐžÐ ", Input, PrimaryText);
    private readonly Button _open = MakeButton("ÐžÐ¢ÐšÐ Ð«Ð¢Ð¬ ÐŸÐÐŸÐšÐ£", Input, PrimaryText);
    private readonly Label _fileName = MakeLabel("Ð“Ð¾Ñ‚Ð¾Ð² Ðº Ð·Ð°Ð³Ñ€ÑƒÐ·ÐºÐµ", 16, FontStyle.Bold, PrimaryText);
    private readonly Label _status = MakeLabel("Ð’ÑÑ‚Ð°Ð²ÑŒÑ‚Ðµ Ð¿Ñ€ÑÐ¼ÑƒÑŽ ÑÑÑ‹Ð»ÐºÑƒ Ð¸ Ð½Ð°Ð¶Ð¼Ð¸Ñ‚Ðµ Â«Ð¡ÐºÐ°Ñ‡Ð°Ñ‚ÑŒÂ»", 10, FontStyle.Regular, SecondaryText);
    private readonly Label _percent = MakeLabel("0%", 20, FontStyle.Bold, PrimaryText);
    private readonly Label _downloaded = MakeLabel("0 Ð‘ / â€”", 10, FontStyle.Regular, SecondaryText);
    private readonly Label _speed = MakeLabel("0 Ð‘/Ñ", 13, FontStyle.Bold, Accent2);
    private readonly Label _eta = MakeLabel("ÐžÑÑ‚Ð°Ð»Ð¾ÑÑŒ: â€”", 10, FontStyle.Regular, SecondaryText);
    private readonly SmoothProgressBar _progress = new() { Height = 9, Dock = DockStyle.Fill, BackColor = Input, ForeColor = Accent2 };
    private readonly System.Windows.Forms.Timer _uiTimer = new() { Interval = 350 };

    private DownloadEngine? _engine;
    private CancellationTokenSource? _cts;
    private string? _lastCompletedFile;
    private bool _closing;

    public LegacyMainForm()
    {
        Text = "Velocity Download";
        ClientSize = new Size(760, 560);
        MinimumSize = new Size(680, 530);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg;
        ForeColor = PrimaryText;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BuildUi();

        _folder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        _connections.DropDownStyle = ComboBoxStyle.DropDownList;
        _connections.Items.AddRange(["ÐÐ²Ñ‚Ð¾ (8 Ð¿Ð¾Ñ‚Ð¾ÐºÐ¾Ð²)", "4 Ð¿Ð¾Ñ‚Ð¾ÐºÐ°", "8 Ð¿Ð¾Ñ‚Ð¾ÐºÐ¾Ð²", "16 Ð¿Ð¾Ñ‚Ð¾ÐºÐ¾Ð²", "1 Ð¿Ð¾Ñ‚Ð¾Ðº"]);
        _connections.SelectedIndex = 0;

        _start.Click += async (_, _) => await StartDownloadAsync();
        _pause.Click += (_, _) => PauseDownload();
        _paste.Click += (_, _) => PasteUrl();
        _browse.Click += (_, _) => BrowseFolder();
        _open.Click += (_, _) => OpenFolder();
        _url.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await StartDownloadAsync(); } };
        _uiTimer.Tick += (_, _) => RefreshProgress();
        FormClosing += OnFormClosing;
        Shown += (_, _) => TryFillFromClipboard();
        SetDownloading(false);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(34, 26, 34, 28), RowCount = 5, ColumnCount = 1, BackColor = Bg };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var titlePanel = new Panel { Dock = DockStyle.Fill };
        var bolt = MakeLabel("âš¡", 28, FontStyle.Regular, Accent2); bolt.AutoSize = true; bolt.Location = new Point(0, 3);
        var title = MakeLabel("Velocity Download", 20, FontStyle.Bold, PrimaryText); title.AutoSize = true; title.Location = new Point(48, 3);
        var subtitle = MakeLabel("Ð‘Ñ‹ÑÑ‚Ñ€Ñ‹Ð¹ Ð·Ð°Ð³Ñ€ÑƒÐ·Ñ‡Ð¸Ðº Ð¿Ñ€ÑÐ¼Ñ‹Ñ… ÑÑÑ‹Ð»Ð¾Ðº", 9.5f, FontStyle.Regular, SecondaryText); subtitle.AutoSize = true; subtitle.Location = new Point(51, 38);
        titlePanel.Controls.AddRange([bolt, title, subtitle]);

        var urlRow = MakeInputRow(_url, _paste, "ÐŸÐ Ð¯ÐœÐÐ¯ Ð¡Ð¡Ð«Ð›ÐšÐ");
        var folderRow = MakeInputRow(_folder, _browse, "ÐŸÐÐŸÐšÐ Ð”Ð›Ð¯ Ð¡ÐžÐ¥Ð ÐÐÐ•ÐÐ˜Ð¯");

        var card = new Panel { Dock = DockStyle.Fill, BackColor = Card, Padding = new Padding(24, 20, 24, 20), Margin = new Padding(0, 8, 0, 10) };
        var cardGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5 };
        cardGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
        cardGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        cardGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        cardGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        cardGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        cardGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        cardGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        _fileName.Dock = DockStyle.Fill; _fileName.TextAlign = ContentAlignment.MiddleLeft;
        _percent.Dock = DockStyle.Fill; _percent.TextAlign = ContentAlignment.MiddleRight;
        _status.Dock = DockStyle.Fill; _status.TextAlign = ContentAlignment.MiddleLeft;
        _downloaded.Dock = DockStyle.Fill; _downloaded.TextAlign = ContentAlignment.MiddleLeft;
        _speed.Dock = DockStyle.Fill; _speed.TextAlign = ContentAlignment.MiddleRight;
        _eta.Dock = DockStyle.Fill; _eta.TextAlign = ContentAlignment.MiddleRight;
        cardGrid.Controls.Add(_fileName, 0, 0); cardGrid.Controls.Add(_percent, 1, 0);
        cardGrid.Controls.Add(_status, 0, 1); cardGrid.SetColumnSpan(_status, 2);
        cardGrid.Controls.Add(_progress, 0, 2); cardGrid.SetColumnSpan(_progress, 2);
        cardGrid.Controls.Add(_downloaded, 0, 3); cardGrid.Controls.Add(_speed, 1, 3);
        cardGrid.Controls.Add(_eta, 1, 4);
        card.Controls.Add(cardGrid);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        StyleCombo(_connections);
        _pause.Margin = new Padding(8, 0, 8, 0); _open.Margin = new Padding(0, 0, 8, 0); _start.Margin = new Padding(0);
        actions.Controls.Add(_connections, 0, 0); actions.Controls.Add(_pause, 1, 0); actions.Controls.Add(_open, 2, 0); actions.Controls.Add(_start, 3, 0);

        root.Controls.Add(titlePanel, 0, 0);
        root.Controls.Add(urlRow, 0, 1);
        root.Controls.Add(folderRow, 0, 2);
        root.Controls.Add(card, 0, 3);
        root.Controls.Add(actions, 0, 4);
        Controls.Add(root);
    }

    private static Control MakeInputRow(TextBox box, Button button, string caption)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = new Padding(0, 3, 0, 5) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        var label = MakeLabel(caption, 8.5f, FontStyle.Bold, SecondaryText); label.Dock = DockStyle.Fill; label.TextAlign = ContentAlignment.MiddleLeft;
        box.Dock = DockStyle.Fill; box.Margin = new Padding(0, 0, 8, 0); button.Dock = DockStyle.Fill; button.Margin = new Padding(0);
        panel.Controls.Add(label, 0, 0); panel.SetColumnSpan(label, 2); panel.Controls.Add(box, 0, 1); panel.Controls.Add(button, 1, 1);
        return panel;
    }

    private static TextBox MakeTextBox(string placeholder) => new()
    {
        BackColor = Input, ForeColor = PrimaryText, BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI", 10.5F), PlaceholderText = placeholder, Padding = new Padding(10)
    };

    private static Button MakeButton(string text, Color bg, Color fg) => new Button()
    {
        Text = text, BackColor = bg, ForeColor = fg, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold), UseVisualStyleBackColor = false, Dock = DockStyle.Fill
    }.With(b => { b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg, .08f); });

    private static Label MakeLabel(string text, float size, FontStyle style, Color color) => new() { Text = text, ForeColor = color, Font = new Font("Segoe UI", size, style), AutoSize = false };

    private static void StyleCombo(ComboBox combo)
    {
        combo.BackColor = Input; combo.ForeColor = Primã]¸¶‰žËkºwµçUÍÐ°!ÑÑÁ½µÁ±•Ñ¥½¹=ÁÑ¥½¸¹I•ÍÁ½¹Í•!•…‘•ÉÍI•…°Ñ½­•¸¤ì(€€€€€€€¥˜€¡É•ÍÁ½¹Í”¹MÑ…ÑÕÍ½‘”€„ô!ÑÑÁMÑ…ÑÕÍ½‘”¹A…ÉÑ¥…±½¹Ñ•¹Ð¤(€€€€€€€€€€€Ñ¡É½Ü¹•Ü%=á•ÁÑ¥½¸ ‹B‡B×FBËB×F ƒBÿB×FB×FFBÃBìƒBÿBûBÓBÓB×FBÛBãBËBÃFF0ƒBóB÷BûBÏBûBÿBûFBûFB÷FF8ƒBßBÃBÏFFBßBëF¸ˆ¤ì(€€€€€€€…Ý…¥ÐÕÍ¥¹œÙ…È¥¹ÁÕÐ€ô…Ý…¥ÐÉ•ÍÁ½¹Í”¹½¹Ñ•¹Ð¹I•…‘ÍMÑÉ•…µÍå¹Œ¡Ñ½­•¸¤ì(€€€€€€€Ù…È‰Õ™™•È€ô¹•Ü‰åÑ•lÄÈà€¨€ÄÀÈÑtì(€€€€€€€±½¹œ½™™Í•Ð€ô™É½´ì(€€€€€€€Ý¡¥±”€¡ÑÉÕ”¤(€€€€€€€ì(€€€€€€€€€€€Ù…ÈÉ•…€ô…Ý…¥Ð¥¹ÁÕÐ¹I•…‘Íå¹Œ¡‰Õ™™•È°Ñ½­•¸¤ì(€€€€€€€€€€€¥˜€¡É•…€ôô€À¤‰É•…¬ì(€€€€€€€€€€€Ù…È…±±½Ý•€ô€¡¥¹Ð¥5…Ñ ¹5¥¸¡É•…°Í•µ•¹Ð¹¹€´½™™Í•Ð€¬€Ä¤ì(€€€€€€€€€€€…Ý…¥ÐI…¹‘½µ•ÍÌ¹]É¥Ñ•Íå¹Œ¡¡…¹‘±”°‰Õ™™•È¹Í5•µ½Éä À°…±±½Ý•¤°½™™Í•Ð°Ñ½­•¸¤ì(€€€€€€€€€€€½™™Í•Ð€¬ô…±±½Ý•ì(€€€€€€€€€€€±½¬€¡}Íå¹Œ¤Í•µ•¹Ð¹½Ý¹±½…‘•€¬ô…±±½Ý•ì(€€€€€€€€€€€5…å‰•M…Ù•MÑ…Ñ”¡ÍÑ…Ñ•A…Ñ ¤ì(€€€€€€€€€€€¥˜€¡½™™Í•Ð€øÍ•µ•¹Ð¹¹¤‰É•…¬ì(€€€€€€€ô(€€€€€€€¥˜€¡½™™Í•Ð€ðôÍ•µ•¹Ð¹¹¤Ñ¡É½Ü¹•Ü¹‘=™MÑÉ•…µá•ÁÑ¥½¸ ‹B‡B×FBËB×F ƒBÿFB×FBËBÃBìƒFBûB×BÓBãB÷B×B÷BãBÔ¸ˆ¤ì(€€€€€€€Í•µ•¹Ð¹½µÁ±•Ñ”€ôÑÉÕ”ì(€€€ô((€€€ÁÉ¥Ù…Ñ”…Íå¹ŒQ…Í¬½Ý¹±½…‘M¥¹±•Íå¹Œ¡ÍÑÉ¥¹œÍÑ…Ñ•A…Ñ °…¹•±±…Ñ¥½¹Q½­•¸Ñ½­•¸¤(€€€ì(€€€€€€€¥˜€¡}ÍÑ…Ñ”¥Ì¹Õ±°¤É•ÑÕÉ¸ì(€€€€€€€}ÍÑ…Ñ”¹UÍ¥¹I…¹•Ì€ô™…±Í”ì(€€€€€€€Ù…ÈÍ•µ•¹Ð€ô}ÍÑ…Ñ”¹M•µ•¹ÑÍlÁtì(€€€€€€€Ù…È•á¥ÍÑ¥¹œ€ô¥±”¹á¥ÍÑÌ¡}ÍÑ…Ñ”¹A…ÉÑ¥…±A…Ñ ¤€ü¹•Ü¥±•%¹™¼¡}ÍÑ…Ñ”¹A…ÉÑ¥…±A…Ñ ¤¹1•¹Ñ €è€Àì(€€€€€€€Í•µ•¹Ð¹½Ý¹±½…‘•€ô}ÍÑ…Ñ”¹Q½Ñ…±1•¹Ñ €ø€À€ü5…Ñ ¹5¥¸¡•á¥ÍÑ¥¹œ°}ÍÑ…Ñ”¹Q½Ñ…±1•¹Ñ ¤€è•á¥ÍÑ¥¹œì(€€€€€€€Ù…ÈÉ•ÅÕ•ÍÑ•‘É½´€ôÍ•µ•¹Ð¹½Ý¹±½…‘•ì((€€€€€€€ÕÍ¥¹œÙ…ÈÉ•ÅÕ•ÍÐ€ô¹•Ü!ÑÑÁI•ÅÕ•ÍÑ5•ÍÍ…”¡!ÑÑÁ5•Ñ¡½¹•Ð°}Í½ÕÉ”¤ì(€€€€€€€¥˜€¡É•ÅÕ•ÍÑ•‘É½´€ø€À¤É•ÅÕ•ÍÐ¹!•…‘•ÉÌ¹I…¹”€ô¹•ÜI…¹•!•…‘•ÉY…±Õ”¡É•ÅÕ•ÍÑ•‘É½´°¹Õ±°¤ì(€€€€€€€ÕÍ¥¹œÙ…ÈÉ•ÍÁ½¹Í”€ô…Ý…¥Ð±¥•¹Ð¹M•¹‘Íå¹Œ¡É•ÅÕ•ÍÐ°!ÑÑÁ½µÁ±•Ñ¥½¹=ÁÑ¥½¸¹I•ÍÁ½¹Í•!•…‘•ÉÍI•…°Ñ½­•¸¤ì(€€€€€€€É•ÍÁ½¹Í”¹¹ÍÕÉ•MÕ•ÍÍMÑ…ÑÕÍ½‘” ¤ì(€€€€€€€Ù…È…¹ÁÁ•¹€ôÉ•ÅÕ•ÍÑ•‘É½´€ø€À€˜˜É•ÍÁ½¹Í”¹MÑ…ÑÕÍ½‘”€ôô!ÑÑÁMÑ…ÑÕÍ½‘”¹A…ÉÑ¥…±½¹Ñ•¹Ðì(€€€€€€€¥˜€ ……¹ÁÁ•¹¤ìÉ•ÅÕ•ÍÑ•‘É½´€ô€ÀìÍ•µ•¹Ð¹½Ý¹±½…‘•€ô€Àìô((€€€€€€€…Ý…¥ÐÕÍ¥¹œÙ…È½ÕÑÁÕÐ€ô¹•Ü¥±•MÑÉ•…´¡}ÍÑ…Ñ”¹A…ÉÑ¥…±A…Ñ °…¹ÁÁ•¹€ü¥±•5½‘”¹ÁÁ•¹€è¥±•5½‘”¹É•…Ñ”°¥±••ÍÌ¹]É¥Ñ”°¥±•M¡…É”¹I•…°€ÈÔØ€¨€ÄÀÈÐ°¥±•=ÁÑ¥½¹Ì¹Íå¹¡É½¹½ÕÌð¥±•=ÁÑ¥½¹Ì¹M•ÅÕ•¹Ñ¥…±M…¸¤ì(€€€€€€€…Ý…¥ÐÕÍ¥¹œÙ…È¥¹ÁÕÐ€ô…Ý…¥ÐÉ•ÍÁ½¹Í”¹½¹Ñ•¹Ð¹I•…‘ÍMÑÉ•…µÍå¹Œ¡Ñ½­•¸¤ì(€€€€€€€Ù…È‰Õ™™•È€ô¹•Ü‰åÑ•lÈÔØ€¨€ÄÀÈÑtì(€€€€€€€Ý¡¥±”€¡ÑÉÕ”¤(€€€€€€€ì(€€€€€€€€€€€Ù…ÈÉ•…€ô…Ý…¥Ð¥¹ÁÕÐ¹I•…‘Íå¹Œ¡‰Õ™™•È°Ñ½­•¸¤ì(€€€€€€€€€€€¥˜€¡É•…€ôô€À¤‰É•…¬ì(€€€€€€€€€€€…Ý…¥Ð½ÕÑÁÕÐ¹]É¥Ñ•Íå¹Œ¡‰Õ™™•È¹Í5•µ½Éä À°É•…¤°Ñ½­•¸¤ì(€€€€€€€€€€€±½¬€¡}Íå¹Œ¤Í•µ•¹Ð¹½Ý¹±½…‘•€¬ôÉ•…ì(€€€€€€€€€€€5…å‰•M…Ù•MÑ…Ñ”¡ÍÑ…Ñ•A…Ñ ¤ì(€€€€€€€ô(€€€€€€€Í•µ•¹Ð¹½µÁ±•Ñ”€ô}ÍÑ…Ñ”¹Q½Ñ…±1•¹Ñ €ðô€ÀñðÍ•µ•¹Ð¹½Ý¹±½…‘•€øô}ÍÑ…Ñ”¹Q½Ñ…±1•¹Ñ ì(€€€ô((€€€ÁÉ¥Ù…Ñ”…Íå¹ŒQ…Í¬ñI•µ½Ñ•%¹™¼øAÉ½‰•Íå¹Œ¡…¹•±±…Ñ¥½¹Q½­•¸Ñ½­•¸¤(€€€ì(€€€€€€€ÕÍ¥¹œÙ…ÈÉ•ÅÕ•ÍÐ€ô¹•Ü!ÑÑÁI•ÅÕ•ÍÑ5•ÍÍ…”¡!ÑÑÁ5•Ñ¡½¹•Ð°}Í½ÕÉ”¤ì(€€€€€€€É•ÅÕ•ÍÐ¹!•…‘•ÉÌ¹I…¹”€ô¹•ÜI…¹•!•…‘•ÉY…±Õ” À°€À¤ì(€€€€€€€ÕÍ¥¹œÙ…ÈÉ•ÍÁ½¹Í”€ô…Ý…¥Ð±¥•¹Ð¹M•¹‘Íå¹Œ¡É•ÅÕ•ÍÐ°!ÑÑÁ½µÁ±•Ñ¥½¹=ÁÑ¥½¸¹I•ÍÁ½¹Í•!•…‘•ÉÍI•…°Ñ½­•¸¤ì(€€€€€€€É•ÍÁ½¹Í”¹¹ÍÕÉ•MÕ•ÍÍMÑ…ÑÕÍ½‘” ¤ì(€€€€€€€Ù…ÈÍÕÁÁ½ÉÑÍI…¹•Ì€ôÉ•ÍÁ½¹Í”¹MÑ…ÑÕÍ½‘”€ôô!ÑÑÁMÑ…ÑÕÍ½‘”¹A…ÉÑ¥…±½¹Ñ•¹Ð€˜˜É•ÍÁ½¹Í”¹½¹Ñ•¹Ð¹!•…‘•ÉÌ¹½¹Ñ•¹ÑI…¹”ü¹1•¹Ñ ¥Ì€ø€Àì(€€€€€€€Ù…È±•¹Ñ €ôÉ•ÍÁ½¹Í”¹½¹Ñ•¹Ð¹!•…‘•ÉÌ¹½¹Ñ•¹ÑI…¹”ü¹1•¹Ñ €üüÉ•ÍÁ½¹Í”¹½¹Ñ•¹Ð¹!•…‘•ÉÌ¹½¹Ñ•¹Ñ1•¹Ñ €üü€´Äì(€€€€€€€Ù…È¹…µ”€ô•Ñ¥±•9…µ”¡É•ÍÁ½¹Í”°}Í½ÕÉ”¤ì(€€€€€€€É•ÑÕÉ¸¹•ÜI•µ½Ñ•%¹™¼¡¹…µ”°±•¹Ñ °ÍÕÁÁ½ÉÑÍI…¹•Ì¤ì(€€€ô((€€€ÁÉ¥Ù…Ñ”…Íå¹ŒQ…Í¬ñ½Ý¹±½…‘MÑ…Ñ”ø1½…‘=ÉÉ•…Ñ•MÑ…Ñ•Íå¹Œ¡ÍÑÉ¥¹œÍÑ…Ñ•A…Ñ °ÍÑÉ¥¹œ™¥¹…±A…Ñ °ÍÑÉ¥¹œÁ…ÉÑ¥…±A…Ñ °I•µ½Ñ•%¹™¼É•µ½Ñ”°…¹•±±…Ñ¥½¹Q½­•¸Ñ½­•¸¤(€€€ì(€€€€€€€¥˜€¡¥±”¹á¥ÍÑÌ¡ÍÑ…Ñ•A…Ñ ¤€˜˜¥±”¹á¥ÍÑÌ¡Á…ÉÑ¥…±A…Ñ ¤¤(€€€€€€€ì(€€€€€€€€€€€ÑÉä(€€€€€€€€€€€ì(€€€€€€€€€€€€€€€Ù…È©Í½¸€ô…Ý…¥Ð¥±”¹I•…‘±±Q•áÑÍå¹Œ¡ÍÑ…Ñ•A…Ñ °Ñ½­•¸¤ì(€€€€€€€€€€€€€€€Ù…È±½…‘•€ô)Í½¹M•É¥…±¥é•È¹•Í•É¥…±¥é”ñ½Ý¹±½…‘MÑ…Ñ”ø¡©Í½¸¤ì(€€€€€€€€€€€€€€€¥˜€¡±½…‘•¥Ì¹½Ð¹Õ±°€˜˜±½…‘•¹UÉ°€ôô}Í½ÕÉ”¹Q½MÑÉ¥¹œ ¤€˜˜±½…‘•¹Q½Ñ…±1•¹Ñ €ôôÉ•µ½Ñ”¹1•¹Ñ ¤(€€€€€€€€€€€€€€€ì(€€€€€€€€€€€€€€€€€€€±½…‘•¹¥¹…±A…Ñ €ô™¥¹…±A…Ñ ì(€€€€€€€€€€€€€€€€€€€±½…‘•¹A…ÉÑ¥…±A…Ñ €ôÁ…ÉÑ¥…±A…Ñ ì(€€€€€€€€€€€€€€€€€€€±½…‘•¹MÑ…Ñ•A…Ñ €ôÍÑ…Ñ•A…Ñ ì(€€€€€€€€€€€€€€€€€€€™½É•… €¡Ù…ÈÌ¥¸±½…‘•¹M•µ•¹ÑÌ¤Ì¹½Ý¹±½…‘•€ô5…Ñ ¹±…µÀ¡Ì¹½Ý¹±½…‘•°€À°Ì¹¹€´Ì¹MÑ…ÉÐ€¬€Ä¤ì(€€€€€€€€€€€€€€€€€€€É•ÑÕÉ¸±½…‘•ì(€€€€€€€€€€€€€€€ô(€€€€€€€€€€€ô(€€€€€€€€€€€…Ñ ìô(€€€€€€€ô((€€€€€€€Ù…ÈÍÑ…Ñ”€ô¹•Ü½Ý¹±½…‘MÑ…Ñ”ìUÉ°€ô}Í½ÕÉ”¹Q½MÑÉ¥¹œ ¤°¥¹…±A…Ñ €ô™¥¹…±A…Ñ °A…ÉÑ¥…±A…Ñ €ôÁ…ÉÑ¥…±A…Ñ °MÑ…Ñ•A…Ñ €ôÍÑ…Ñ•A…Ñ °Q½Ñ…±1•¹Ñ €ôÉ•µ½Ñ”¹1•¹Ñ °UÍ¥¹I…¹•Ì€ôÉ•µ½Ñ”¹MÕÁÁ½ÉÑÍI…¹•Ì€˜˜}É•ÅÕ•ÍÑ•‘½¹¹•Ñ¥½¹Ì€ø€Äôì(€€€€€€€¥˜€¡ÍÑ…Ñ”¹UÍ¥¹I…¹•Ì€˜˜É•µ½Ñ”¹1•¹Ñ €ø€À¤(€€€€€€€ì(€€€€€€€€€€€Ù…È½Õ¹Ð€ô€¡¥¹Ð¥5…Ñ ¹5¥¸¡}É•ÅÕ•ÍÑ•‘½¹¹•Ñ¥½¹Ì°5…Ñ ¹5…à Ä°É•µ½Ñ”¹1•¹Ñ €¼€ ÄÀÈÐ€¨€ÄÀÈÐ¤¤¤ì(€€€€€€€€€€€Ù…È¡Õ¹¬€ôÉ•µ½Ñ”¹1•¹Ñ €¼½Õ¹Ðì(€€€€€€€€€€€™½È€¡Ù…È¤€ô€Àì¤€ð½Õ¹Ðì¤¬¬¤(€€€€€€€€€€€ì(€€€€€€€€€€€€€€€Ù…ÈÍÑ…ÉÐ€ô¤€¨¡Õ¹¬ì(€€€€€€€€€€€€€€€Ù…È•¹€ô¤€ôô½Õ¹Ð€´€Ä€üÉ•µ½Ñ”¹1•¹Ñ €´€Ä€è€ ¡¤€¬€Ä¤€¨¡Õ¹¬¤€´€Äì(€€€€€€€€€€€€€€€ÍÑ…Ñ”¹M•µ•¹ÑÌ¹‘¡¹•ÜM•µ•¹ÑMÑ…Ñ”ìMÑ…ÉÐ€ôÍÑ…ÉÐ°¹€ô•¹ô¤ì(€€€€€€€€€€€ô(€€€€€€€ô(€€€€€€€•±Í”ÍÑ…Ñ”¹M•µ•¹ÑÌ¹‘¡¹•ÜM•µ•¹ÑMÑ…Ñ”ìMÑ…ÉÐ€ô€À°¹€ôÉ•µ½Ñ”¹1•¹Ñ €ø€À€üÉ•µ½Ñ”¹1•¹Ñ €´€Ä€è±½¹œ¹5…áY…±Õ”€´€Äô¤ì(€€€€€€€…Ý…¥Ð¥±”¹]É¥Ñ•±±Q•áÑÍå¹Œ¡ÍÑ…Ñ•A…Ñ °)Í½¹M•É¥…±¥é•È¹M•É¥…±¥é”¡ÍÑ…Ñ”¤°Ñ½­•¸¤ì(€€€€€€€É•ÑÕÉ¸ÍÑ…Ñ”ì(€€€ô((€€€ÁÉ¥Ù…Ñ”Ù½¥5…å‰•M…Ù•MÑ…Ñ”¡ÍÑÉ¥¹œÍÑ…Ñ•A…Ñ ¤(€€€ì(€€€€€€€¥˜€ ¡…Ñ•Q¥µ”¹UÑ9½Ü€´}±…ÍÑMÑ…Ñ•M…Ù”¤¹Q½Ñ…±M•½¹‘Ì€ð€Äñð%¹Ñ•É±½­•¹á¡…¹”¡É•˜}Í…Ù¥¹œ°€Ä¤€ôô€Ä¤É•ÑÕÉ¸ì(€€€€€€€}±…ÍÑMÑ…Ñ•M…Ù”€ô…Ñ•Q¥µ”¹UÑ9½Üì(€€€€€€€}Á•¹‘¥¹MÑ…Ñ•M…Ù”€ôQ…Í¬¹IÕ¸¡…Íå¹Œ€ ¤€ôø(€€€€€€€ì(€€€€€€€€€€€ÑÉäì…Ý…¥ÐM…Ù•MÑ…Ñ•Íå¹Œ¡ÍÑ…Ñ•A…Ñ °…¹•±±…Ñ¥½¹Q½­•¸¹9½¹”¤ìô(€€€€€€€€€€€…Ñ ìô(€€€€€€€€€€€™¥¹…±±äì%¹Ñ•É±½­•¹á¡…¹”¡É•˜}Í…Ù¥¹œ°€À¤ìô(€€€€€€€ô¤ì(€€€ô((€€€ÁÉ¥Ù…Ñ”…Íå¹ŒQ…Í¬M…Ù•MÑ…Ñ•Íå¹Œ¡ÍÑÉ¥¹œÍÑ…Ñ•A…Ñ °…¹•±±…Ñ¥½¹Q½­•¸Ñ½­•¸¤(€€€ì(€€€€€€€¥˜€¡}ÍÑ…Ñ”¥Ì¹Õ±°¤É•ÑÕÉ¸ì(€€€€€€€…Ý…¥Ð}ÍÑ…Ñ•M…Ù•…Ñ”¹]…¥ÑÍå¹Œ¡Ñ½­•¸¤ì(€€€€€€€ÑÉä(€€€€€€€ì(€€€€€€€€€€€ÍÑÉ¥¹œ©Í½¸ì(€€€€€€€€€€€±½¬€¡}Íå¹Œ¤©Í½¸€ô)Í½¹M•É¥…±¥é•È¹M•É¥…±¥é”¡}ÍÑ…Ñ”¤ì(€€€€€€€€€€€Ù…ÈÑ•µÀ€ôÍÑ…Ñ•A…Ñ €¬€ˆ¹ÑµÀˆì(€€€€€€€€€€€…Ý…¥Ð¥±”¹]É¥Ñ•±±Q•áÑÍå¹Œ¡Ñ•µÀ°©Í½¸°Ñ½­•¸¤ì(€€€€€€€€€€€¥±”¹5½Ù”¡Ñ•µÀ°ÍÑ…Ñ•A…Ñ °ÑÉÕ”¤ì(€€€€€€€ô(€€€€€€€™¥¹…±±äì}ÍÑ…Ñ•M…Ù•…Ñ”¹I•±•…Í” ¤ìô(€€€ô((€€€ÁÉ¥Ù…Ñ”…Íå¹ŒQ…Í¬]…¥Ñ½ÉA•¹‘¥¹MÑ…Ñ•M…Ù•Íå¹Œ ¤(€€€ì(€€€€€€€Ù…ÈÁ•¹‘¥¹œ€ô}Á•¹‘¥¹MÑ…Ñ•M…Ù”ì(€€€€€€€¥˜€¡Á•¹‘¥¹œ¥Ì¹Õ±°¤É•ÑÕÉ¸ì(€€€€€€€ÑÉäì…Ý…¥ÐÁ•¹‘¥¹œìô(€€€€€€€…Ñ ìô(€€€ô((€€€¥¹Ñ•É¹…°ÍÑ…Ñ¥Œ…Íå¹ŒQ…Í¬ñ‰½½°ø•±•Ñ•…¡•‘½Ý¹±½…‘Íå¹Œ¡UÉ¤Í½ÕÉ”°ÍÑÉ¥¹œ™½±‘•È¤(€€€ì(€€€€€€€Ù…È…¡”€ô•Ñ…¡•A…Ñ¡Ì¡Í½ÕÉ”°™½±‘•È¤ì(€€€€€€€Ù…È™¥±•Ì€ô¹•Ýmtì…¡”¹MÑ…Ñ•A…Ñ €¬€ˆ¹ÑµÀˆ°…¡”¹MÑ…Ñ•A…Ñ °…¡”¹A…ÉÑ¥…±A…Ñ ôì(€€€€€€€™½È€¡Ù…È…ÑÑ•µÁÐ€ô€Àì…ÑÑ•µÁÐ€ð€àì…ÑÑ•µÁÐ¬¬¤(€€€€€€€ì(€€€€€€€€€€€Ù…È™…¥±•€ô™…±Í”ì(€€€€€€€€€€€™½É•… €¡Ù…È™¥±”¥¸™¥±•Ì¤(€€€€€€€€€€€ì(€€€€€€€€€€€€€€€ÑÉäì¥˜€¡¥±”¹á¥ÍÑÌ¡™¥±”¤¤¥±”¹•±•Ñ”¡™¥±”¤ìô(€€€€€€€€€€€€€€€…Ñ €¡%=á•ÁÑ¥½¸¤ì™…¥±•€ôÑÉÕ”ìô(€€€€€€€€€€€€€€€…Ñ €¡U¹…ÕÑ¡½É¥é•‘•ÍÍá•ÁÑ¥½¸¤ì™…¥±•€ôÑÉÕ”ìô(€€€€€€€€€€€ô(€€€€€€€€€€€¥˜€ …™…¥±•€˜˜™¥±•Ì¹±°¡™¥±”€ôø€…¥±”¹á¥ÍÑÌ¡™¥±”¤¤¤É•ÑÕÉ¸ÑÉÕ”ì(€€€€€€€€€€€…Ý…¥ÐQ…Í¬¹•±…ä ÄÀÀ€¬…ÑÑ•µÁÐ€¨€ÄÀÀ¤ì(€€€€€€€ô(€€€€€€€É•ÑÕÉ¸™¥±•Ì¹±°¡™¥±”€ôø€…¥±”¹á¥ÍÑÌ¡™¥±”¤¤ì(€€€ô((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥Œ!ÑÑÁ±¥•¹ÐÉ•…Ñ•±¥•¹Ð ¤(€€€ì(€€€€€€€Ù…È¡…¹‘±•È€ô¹•ÜM½­•ÑÍ!ÑÑÁ!…¹‘±•È(€€€€€€€ì(€€€€€€€€€€€ÕÑ½µ…Ñ¥•½µÁÉ•ÍÍ¥½¸€ô•½µÁÉ•ÍÍ¥½¹5•Ñ¡½‘Ì¹9½¹”°(€€€€€€€€€€€±±½ÝÕÑ½I•‘¥É•Ð€ôÑÉÕ”°(€€€€€€€€€€€5…áÕÑ½µ…Ñ¥I•‘¥É•Ñ¥½¹Ì€ô€ÄÀ°(€€€€€€€€€€€5…á½¹¹•Ñ¥½¹ÍA•ÉM•ÉÙ•È€ô€ÌÈ°(€€€€€€€€€€€½¹¹•ÑQ¥µ•½ÕÐ€ôQ¥µ•MÁ…¸¹É½µM•½¹‘Ì ÈÀ¤°(€€€€€€€€€€€A½½±•‘½¹¹•Ñ¥½¹1¥™•Ñ¥µ”€ôQ¥µ•MÁ…¸¹É½µ5¥¹ÕÑ•Ì ÄÀ¤°(€€€€€€€€€€€UÍ•AÉ½áä€ôÑÉÕ”(€€€€€€€ôì(€€€€€€€Ù…È±¥•¹Ð€ô¹•Ü!ÑÑÁ±¥•¹Ð¡¡…¹‘±•È¤ìQ¥µ•½ÕÐ€ôQ¥µ•½ÕÐ¹%¹™¥¹¥Ñ•Q¥µ•MÁ…¸ôì(€€€€€€€±¥•¹Ð¹•™…Õ±ÑI•ÅÕ•ÍÑ!•…‘•ÉÌ¹UÍ•É•¹Ð¹A…ÉÍ•‘ ‰5½é¥±±„¼Ô¸À€¡]¥¹‘½ÝÌ9P€ÄÀ¸Àì]¥¸ØÐìàØÐ¤Y•±½¥Ñå½Ý¹±½…¼È¸Ðˆ¤ì(€€€€€€€±¥•¹Ð¹•™…Õ±ÑI•ÅÕ•ÍÑ!•…‘•ÉÌ¹•ÁÐ¹‘¡¹•Ü5•‘¥…QåÁ•]¥Ñ¡EÕ…±¥Ñå!•…‘•ÉY…±Õ” ˆ¨¼¨ˆ¤¤ì(€€€€€€€±¥•¹Ð¹•™…Õ±ÑI•ÅÕ•ÍÑ!•…‘•ÉÌ¹•ÁÑ¹½‘¥¹œ¹A…ÉÍ•‘ ‰¥‘•¹Ñ¥Ñäˆ¤ì(€€€€€€€É•ÑÕÉ¸±¥•¹Ðì(€€€ô((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒÍÑÉ¥¹œ•Ñ¥±•9…µ”¡!ÑÑÁI•ÍÁ½¹Í•5•ÍÍ…”É•ÍÁ½¹Í”°UÉ¤½É¥¥¹…°¤(€€€ì(€€€€€€€Ù…È€ôÉ•ÍÁ½¹Í”¹½¹Ñ•¹Ð¹!•…‘•ÉÌ¹½¹Ñ•¹Ñ¥ÍÁ½Í¥Ñ¥½¸ì(€€€€€€€Ù…È¹…µ”€ôü¹¥±•9…µ•MÑ…È€üüü¹¥±•9…µ”ì(€€€€€€€¥˜€ …ÍÑÉ¥¹œ¹%Í9Õ±±=É]¡¥Ñ•MÁ…”¡¹…µ”¤¤É•ÑÕÉ¸¹…µ”¹QÉ¥´ œˆœ¤ì(€€€€€€€Ù…È™¥¹…±UÉ¤€ôÉ•ÍÁ½¹Í”¹I•ÅÕ•ÍÑ5•ÍÍ…”ü¹I•ÅÕ•ÍÑUÉ¤€üü½É¥¥¹…°ì(€€€€€€€Ù…ÈÁ…Ñ¡9…µ”€ôUÉ¤¹U¹•Í…Á•…Ñ…MÑÉ¥¹œ¡A…Ñ ¹•Ñ¥±•9…µ”¡™¥¹…±UÉ¤¹1½…±A…Ñ ¤¤ì(€€€€€€€É•ÑÕÉ¸ÍÑÉ¥¹œ¹%Í9Õ±±=É]¡¥Ñ•MÁ…”¡Á…Ñ¡9…µ”¤€ü€‰‘½Ý¹±½…¹‰¥¸ˆ€èÁ…Ñ¡9…µ”ì(€€€ô((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒÍÑÉ¥¹œ5…­•M…™•¥±•9…µ”¡ÍÑÉ¥¹œ¹…µ”¤(€€€ì(€€€€€€€™½É•… €¡Ù…ÈŒ¥¸A…Ñ ¹•Ñ%¹Ù…±¥‘¥±•9…µ•¡…ÉÌ ¤¤¹…µ”€ô¹…µ”¹I•Á±…”¡Œ°€|œ¤ì(€€€€€€€¹…µ”€ô¹…µ”¹QÉ¥´ ¤¹QÉ¥µ¹ œ¸œ¤ì(€€€€€€€É•ÑÕÉ¸ÍÑÉ¥¹œ¹%Í9Õ±±=É]¡¥Ñ•MÁ…”¡¹…µ”¤€ü€‰‘½Ý¹±½…¹‰¥¸ˆ€è¹…µ”ì(€€€ô((€€€ÁÉ¥Ù…Ñ”ÍÑÉ¥¹œü•Ñ…¡•‘¥¹…±A…Ñ ¡ÍÑÉ¥¹œÍÑ…Ñ•A…Ñ °ÍÑÉ¥¹œÁ…ÉÑ¥…±A…Ñ °I•µ½Ñ•%¹™¼É•µ½Ñ”¤(€€€ì(€€€€€€€¥˜€ …¥±”¹á¥ÍÑÌ¡ÍÑ…Ñ•A…Ñ ¤ñð€…¥±”¹á¥ÍÑÌ¡Á…ÉÑ¥…±A…Ñ ¤¤É•ÑÕÉ¸¹Õ±°ì(€€€€€€€ÑÉä(€€€€€€€ì(€€€€€€€€€€€Ù…ÈÍÑ…Ñ”€ô)Í½¹M•É¥…±¥é•È¹•Í•É¥…±¥é”ñ½Ý¹±½…‘MÑ…Ñ”ø¡¥±”¹I•…‘±±Q•áÐ¡ÍÑ…Ñ•A…Ñ ¤¤ì(€€€€€€€€€€€¥˜€¡ÍÑ…Ñ”¥Ì¹Õ±°ñðÍÑ…Ñ”¹UÉ°€„ô}Í½ÕÉ”¹Q½MÑÉ¥¹œ ¤ñðÍÑ…Ñ”¹Q½Ñ…±1•¹Ñ €„ôÉ•µ½Ñ”¹1•¹Ñ ñðÍÑÉ¥¹œ¹%Í9Õ±±=É]¡¥Ñ•MÁ…”¡ÍÑ…Ñ”¹¥¹…±A…Ñ ¤¤É•ÑÕÉ¸¹Õ±°ì(€€€€€€€€€€€Ù…ÈÉ•ÅÕ•ÍÑ•‘½±‘•È€ôA…Ñ ¹•ÑÕ±±A…Ñ ¡}™½±‘•È¤¹QÉ¥µ¹¡A…Ñ ¹¥É•Ñ½ÉåM•Á…É…Ñ½É¡…È¤ì(€€€€€€€€€€€Ù…ÈÍÑ…Ñ•½±‘•È€ôA…Ñ ¹•ÑÕ±±A…Ñ ¡A…Ñ ¹•Ñ¥É•Ñ½Éå9…µ”¡ÍÑ…Ñ”¹¥¹…±A…Ñ ¤„¤¹QÉ¥µ¹¡A…Ñ ¹¥É•Ñ½ÉåM•Á…É…Ñ½É¡…È¤ì(€€€€€€€€€€€¥˜€ …ÍÑÉ¥¹œ¹ÅÕ…±Ì¡É•ÅÕ•ÍÑ•‘½±‘•È°ÍÑ…Ñ•½±‘•È°MÑÉ¥¹½µÁ…É¥Í½¸¹=É‘¥¹…±%¹½É•…Í”¤¤É•ÑÕÉ¸¹Õ±°ì(€€€€€€€€€€€±½¬€¡•ÍÑ¥¹…Ñ¥½¹1½¬¤I•Í•ÉÙ•‘•ÍÑ¥¹…Ñ¥½¹Ì¹‘¡ÍÑ…Ñ”¹¥¹…±A…Ñ ¤ì(€€€€€€€€€€€É•ÑÕÉ¸ÍÑ…Ñ”¹¥¹…±A…Ñ ì(€€€€€€€ô(€€€€€€€…Ñ ìÉ•ÑÕÉ¸¹Õ±°ìô(€€€ô((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒÍÑÉ¥¹œ•ÑÙ…¥±…‰±••ÍÑ¥¹…Ñ¥½¹A…Ñ ¡ÍÑÉ¥¹œ™½±‘•È°ÍÑÉ¥¹œ¹…µ”¤(€€€ì(€€€€€€€±½¬€¡•ÍÑ¥¹…Ñ¥½¹1½¬¤(€€€€€€€ì(€€€€€€€€€€€Ù…ÈÍÑ•´€ôA…Ñ ¹•Ñ¥±•9…µ•]¥Ñ¡½ÕÑáÑ•¹Í¥½¸¡¹…µ”¤ìÙ…È•áÐ€ôA…Ñ ¹•ÑáÑ•¹Í¥½¸¡¹…µ”¤ì(€€€€€€€€€€€™½È€¡Ù…È¤€ô€Àì¤€ð€ÄÁ|ÀÀÀì¤¬¬¤(€€€€€€€€€€€ì(€€€€€€€€€€€€€€€Ù…È…¹‘¥‘…Ñ”€ôA…Ñ ¹½µ‰¥¹”¡™½±‘•È°¤€ôô€À€ü¹…µ”€è€‰íÍÑ•µô€¡í¥ô¥í•áÑôˆ¤ì(€€€€€€€€€€€€€€€¥˜€¡¥±”¹á¥ÍÑÌ¡…¹‘¥‘…Ñ”¤ñð¥±”¹á¥ÍÑÌ¡…¹‘¥‘…Ñ”€¬€ˆ¹Ù•±½¥Ñä¹Á…ÉÐˆ¤ñð¥±”¹á¥ÍÑÌ¡…¹‘¥‘…Ñ”€¬€ˆ¹Ù•±½¥Ñä¹©Í½¸ˆ¤ñðI•Í•ÉÙ•‘•ÍÑ¥¹…Ñ¥½¹Ì¹½¹Ñ…¥¹Ì¡…¹‘¥‘…Ñ”¤¤½¹Ñ¥¹Õ”ì(€€€€€€€€€€€€€€€I•Í•ÉÙ•‘•ÍÑ¥¹…Ñ¥½¹Ì¹‘¡…¹‘¥‘…Ñ”¤ì(€€€€€€€€€€€€€€€É•ÑÕÉ¸…¹‘¥‘…Ñ”ì(€€€€€€€€€€€ô(€€€€€€€€€€€Ù…È™…±±‰…¬€ôA…Ñ ¹½µ‰¥¹”¡™½±‘•È°€‰íÍÑ•µôµíÕ¥¹9•ÝÕ¥ ¤é9õí•áÑôˆ¤ì(€€€€€€€€€€€I•Í•ÉÙ•‘•ÍÑ¥¹…Ñ¥½¹Ì¹‘¡™…±±‰…¬¤ì(€€€€€€€€€€€É•ÑÕÉ¸™…±±‰…¬ì(€€€€€€€ô(€€€ô((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥Œ…¡•A…Ñ¡Ì•Ñ…¡•A…Ñ¡Ì¡UÉ¤Í½ÕÉ”°ÍÑÉ¥¹œ™½±‘•È¤(€€€ì(€€€€€€€Ù…È¹½Éµ…±¥é•‘½±‘•È€ôA…Ñ ¹•ÑÕ±±A…Ñ ¡™½±‘•È¤¹QÉ¥µ¹¡A…Ñ ¹¥É•Ñ½ÉåM•Á…É…Ñ½É¡…È¤¹Q½UÁÁ•É%¹Ù…É¥…¹Ð ¤ì(€€€€€€€Ù…È­•å	åÑ•Ì€ôM!ÈÔØ¹!…Í¡…Ñ„¡¹½‘¥¹œ¹UQà¹•Ñ	åÑ•Ì¡Í½ÕÉ”€¬€‰q¸ˆ€¬¹½Éµ…±¥é•‘½±‘•È¤¤ì(€€€€€€€Ù…È­•ä€ô½¹Ù•ÉÐ¹Q½!•áMÑÉ¥¹œ¡­•å	åÑ•Ì¤¹Q½1½Ý•É%¹Ù…É¥…¹Ð ¤ì(€€€€€€€Ù…È‘¥É•Ñ½Éä€ôA…Ñ ¹½µ‰¥¹”¡¹Ù¥É½¹µ•¹Ð¹•Ñ½±‘•ÉA…Ñ ¡¹Ù¥É½¹µ•¹Ð¹MÁ•¥…±½±‘•È¹1½…±ÁÁ±¥…Ñ¥½¹…Ñ„¤°€‰Y•±½¥Ñå½Ý¹±½…ˆ°€‰…¡”ˆ¤ì(€€€€€€€É•ÑÕÉ¸¹•Ü…¡•A…Ñ¡Ì¡‘¥É•Ñ½Éä°A…Ñ ¹½µ‰¥¹”¡‘¥É•Ñ½Éä°­•ä€¬€ˆ¹©Í½¸ˆ¤°A…Ñ ¹½µ‰¥¹”¡‘¥É•Ñ½Éä°­•ä€¬€ˆ¹Á…ÉÐˆ¤¤ì(€€€ô((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥Œ…Íå¹ŒQ…Í¬ñÍÑÉ¥¹œø¥¹…±¥é•½Ý¹±½…‘Íå¹Œ¡ÍÑÉ¥¹œÁ…ÉÑ¥…±A…Ñ °ÍÑÉ¥¹œÉ•ÅÕ•ÍÑ•‘A…Ñ ¤(€€€ì(€€€€€€€Ù…È‘•ÍÑ¥¹…Ñ¥½¸€ôÉ•ÅÕ•ÍÑ•‘A…Ñ ì(€€€€€€€¥˜€¡¥±”¹á¥ÍÑÌ¡‘•ÍÑ¥¹…Ñ¥½¸¤¤‘•ÍÑ¥¹…Ñ¥½¸€ô•ÑÙ…¥±…‰±••ÍÑ¥¹…Ñ¥½¹A…Ñ ¡A…Ñ ¹•Ñ¥É•Ñ½Éå9…µ”¡‘•ÍÑ¥¹…Ñ¥½¸¤„°A…Ñ ¹•Ñ¥±•9…µ”¡‘•ÍÑ¥¹…Ñ¥½¸¤¤ì(€€€€€€€Ù…ÈÍ½ÕÉ•I½½Ð€ôA…Ñ ¹•ÑA…Ñ¡I½½Ð¡A…Ñ ¹•ÑÕ±±A…Ñ ¡Á…ÉÑ¥…±A…Ñ ¤¤ì(€€€€€€€Ù…È‘•ÍÑ¥¹…Ñ¥½¹I½½Ð€ôA…Ñ ¹•ÑA…Ñ¡I½½Ð¡A…Ñ ¹•ÑÕ±±A…Ñ ¡‘•ÍÑ¥¹…Ñ¥½¸¤¤ì(€€€€€€€¥˜€¡ÍÑÉ¥¹œ¹ÅÕ…±Ì¡Í½ÕÉ•I½½Ð°‘•ÍÑ¥¹…Ñ¥½¹I½½Ð°MÑÉ¥¹½µÁ…É¥Í½¸¹=É‘¥¹…±%¹½É•…Í”¤¤(€€€€€€€ì(€€€€€€€€€€€¥±”¹5½Ù”¡Á…ÉÑ¥…±A…Ñ °‘•ÍÑ¥¹…Ñ¥½¸°™…±Í”¤ì(€€€€€€€€€€€É•ÑÕÉ¸‘•ÍÑ¥¹…Ñ¥½¸ì(€€€€€€€ô((€€€€€€€Ù…ÈÍÑ…¥¹œ€ôA…Ñ ¹½µ‰¥¹”¡A…Ñ ¹•Ñ¥É•Ñ½Éå9…µ”¡‘•ÍÑ¥¹…Ñ¥½¸¤„°€ˆ¹íA…Ñ ¹•Ñ¥±•9…µ”¡‘•ÍÑ¥¹…Ñ¥½¸¥ô¹íÕ¥¹9•ÝÕ¥ ¤é9ô¹Ù•±½¥Ñäµ™¥¹…±¥é¥¹œˆ¤ì(€€€€€€€ÑÉä(€€€€€€€ì(€€€€€€€€€€€…Ý…¥ÐÕÍ¥¹œ€¡Ù…È¥¹ÁÕÐ€ô¹•Ü¥±•MÑÉ•…´¡Á…ÉÑ¥…±A…Ñ °¥±•5½‘”¹=Á•¸°¥±••ÍÌ¹I•…°¥±•M¡…É”¹I•…°€ÄÀÈÐ€¨€ÄÀÈÐ°¥±•=ÁÑ¥½¹Ì¹Íå¹¡É½¹½ÕÌð¥±•=ÁÑ¥½¹Ì¹M•ÅÕ•¹Ñ¥…±M…¸¤¤(€€€€€€€€€€€…Ý…¥ÐÕÍ¥¹œ€¡Ù…È½ÕÑÁÕÐ€ô¹•Ü¥±•MÑÉ•…´¡ÍÑ…¥¹œ°¥±•5½‘”¹É•…Ñ•9•Ü°¥±••ÍÌ¹]É¥Ñ”°¥±•M¡…É”¹I•…°€ÄÀÈÐ€¨€ÄÀÈÐ°¥±•=ÁÑ¥½¹Ì¹Íå¹¡É½¹½ÕÌð¥±•=ÁÑ¥½¹Ì¹M•ÅÕ•¹Ñ¥…±M…¸¤¤(€€€€€€€€€€€ì(€€€€€€€€€€€€€€€ÑÉäì¥±”¹M•ÑÑÑÉ¥‰ÕÑ•Ì¡ÍÑ…¥¹œ°¥±•ÑÑÉ¥‰ÕÑ•Ì¹!¥‘‘•¸ð¥±•ÑÑÉ¥‰ÕÑ•Ì¹Q•µÁ½É…Éä¤ìô…Ñ ìô(€€€€€€€€€€€€€€€…Ý…¥Ð¥¹ÁÕÐ¹½ÁåQ½Íå¹Œ¡½ÕÑÁÕÐ¤ì(€€€€€€€€€€€€€€€…Ý…¥Ð½ÕÑÁÕÐ¹±ÕÍ¡Íå¹Œ ¤ì(€€€€€€€€€€€ô(€€€€€€€€€€€ÑÉäì¥±”¹M•ÑÑÑÉ¥‰ÕÑ•Ì¡ÍÑ…¥¹œ°¥±•ÑÑÉ¥‰ÕÑ•Ì¹9½Éµ…°¤ìô…Ñ ìô(€€€€€€€€€€€¥±”¹5½Ù”¡ÍÑ…¥¹œ°‘•ÍÑ¥¹…Ñ¥½¸°™…±Í”¤ì(€€€€€€€€€€€¥±”¹•±•Ñ”¡Á…ÉÑ¥…±A…Ñ ¤ì(€€€€€€€€€€€É•ÑÕÉ¸‘•ÍÑ¥¹…Ñ¥½¸ì(€€€€€€€ô(€€€€€€€…Ñ (€€€€€€€ì(€€€€€€€€€€€¥˜€¡¥±”¹á¥ÍÑÌ¡ÍÑ…¥¹œ¤¤¥±”¹•±•Ñ”¡ÍÑ…¥¹œ¤ì(€€€€€€€€€€€Ñ¡É½Üì(€€€€€€€ô(€€€ô)ô()¥¹Ñ•É¹…°Í•…±•±…ÍÌ½Ý¹±½…‘MÑ…Ñ”)ì(€€€ÁÕ‰±¥ŒÍÑÉ¥¹œUÉ°ì•ÐìÍ•Ðìô€ô€ˆˆì(€€€ÁÕ‰±¥ŒÍÑÉ¥¹œ¥¹…±A…Ñ ì•ÐìÍ•Ðìô€ô€ˆˆì(€€€ÁÕ‰±¥ŒÍÑÉ¥¹œA…ÉÑ¥…±A…Ñ ì•ÐìÍ•Ðìô€ô€ˆˆì(€€€ÁÕ‰±¥ŒÍÑÉ¥¹œMÑ…Ñ•A…Ñ ì•ÐìÍ•Ðìô€ô€ˆˆì(€€€ÁÕ‰±¥Œ±½¹œQ½Ñ…±1•¹Ñ ì•ÐìÍ•Ðìô(€€€ÁÕ‰±¥Œ‰½½°UÍ¥¹I…¹•Ìì•ÐìÍ•Ðìô(€€€ÁÕ‰±¥Œ1¥ÍÐñM•µ•¹ÑMÑ…Ñ”øM•µ•¹ÑÌì•ÐìÍ•Ðìô€ômtì)ô()¥¹Ñ•É¹…°Í•…±•±…ÍÌM•µ•¹ÑMÑ…Ñ”)ì(€€€ÁÕ‰±¥Œ±½¹œMÑ…ÉÐì•ÐìÍ•Ðìô(€€€ÁÕ‰±¥Œ±½¹œ¹ì•ÐìÍ•Ðìô(€€€ÁÕ‰±¥Œ±½¹œ½Ý¹±½…‘•ì•ÐìÍ•Ðìô(€€€ÁÕ‰±¥Œ‰½½°½µÁ±•Ñ”ì•ÐìÍ•Ðìô)ô()¥¹Ñ•É¹…°É•…‘½¹±äÉ•½ÉÍÑÉÕÐI•µ½Ñ•%¹™¼¡ÍÑÉ¥¹œ¥±•9…µ”°±½¹œ1•¹Ñ °‰½½°MÕÁÁ½ÉÑÍI…¹•Ì¤ì)¥¹Ñ•É¹…°É•…‘½¹±äÉ•½ÉÍÑÉÕÐ…¡•A…Ñ¡Ì¡ÍÑÉ¥¹œ¥É•Ñ½Éä°ÍÑÉ¥¹œMÑ…Ñ•A…Ñ °ÍÑÉ¥¹œA…ÉÑ¥…±A…Ñ ¤ì)¥¹Ñ•É¹…°É•…‘½¹±äÉ•½ÉÍÑÉÕÐ½Ý¹±½…‘AÉ½É•ÍÌ¡ÍÑÉ¥¹œ¥±•9…µ”°±½¹œ½Ý¹±½…‘•°±½¹œQ½Ñ…°°‘½Õ‰±”	åÑ•ÍA•ÉM•½¹°‰½½°UÍ¥¹I…¹•Ì°¥¹ÐÑ¥Ù•½¹¹•Ñ¥½¹Ì¤ì()¥¹Ñ•É¹…°Í•…±•±…ÍÌMµ½½Ñ¡AÉ½É•ÍÍ	…È€è½¹ÑÉ½°)ì(€€€ÁÉ¥Ù…Ñ”¥¹Ð}Ù…±Õ”ì(€€€m	É½ÝÍ…‰±”¡™…±Í”¥t(€€€m•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¡•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¹!¥‘‘•¸¥t(€€€ÁÕ‰±¥Œ¥¹ÐY…±Õ”ì•Ð€ôø}Ù…±Õ”ìÍ•Ðì}Ù…±Õ”€ô5…Ñ ¹±…µÀ¡Ù…±Õ”°€À°€ÄÀÀÀ¤ì%¹Ù…±¥‘…Ñ” ¤ìôô(€€€ÁÕ‰±¥ŒMµ½½Ñ¡AÉ½É•ÍÍ	…È ¤ìM•ÑMÑå±”¡½¹ÑÉ½±MÑå±•Ì¹±±A…¥¹Ñ¥¹%¹]µA…¥¹Ðð½¹ÑÉ½±MÑå±•Ì¹UÍ•ÉA…¥¹Ðð½¹ÑÉ½±MÑå±•Ì¹=ÁÑ¥µ¥é•‘½Õ‰±•	Õ™™•È°ÑÉÕ”¤ìô(€€€ÁÉ½Ñ•Ñ•½Ù•ÉÉ¥‘”Ù½¥=¹A…¥¹Ð¡A…¥¹ÑÙ•¹ÑÉÌ”¤(€€€ì(€€€€€€€”¹É…Á¡¥Ì¹±•…È¡	…­½±½È¤ì(€€€€€€€¥˜€¡}Ù…±Õ”€ðô€À¤É•ÑÕÉ¸ì(€€€€€€€Ù…ÈÝ¥‘Ñ €ô€¡¥¹Ð¤¡±¥•¹ÑM¥é”¹]¥‘Ñ €¨€¡}Ù…±Õ”€¼€ÄÀÀÁ¤¤ì(€€€€€€€ÕÍ¥¹œÙ…È‰ÉÕÍ €ô¹•ÜM½±¥‘	ÉÕÍ ¡½É•½±½È¤ì(€€€€€€€”¹É…Á¡¥Ì¹¥±±I•Ñ…¹±”¡‰ÉÕÍ °€À°€À°Ý¥‘Ñ °±¥•¹ÑM¥é”¹!•¥¡Ð¤ì(€€€ô)ô()¥¹Ñ•É¹…°ÍÑ…Ñ¥Œ±…ÍÌ½¹ÑÉ½±áÑ•¹Í¥½¹Ì)ì(€€€ÁÕ‰±¥ŒÍÑ…Ñ¥ŒP]¥Ñ ñPø¡Ñ¡¥ÌPÙ…±Õ”°Ñ¥½¸ñPø…Ñ¥½¸¤ì…Ñ¥½¸¡Ù…±Õ”¤ìÉ•ÑÕÉ¸Ù…±Õ”ìô)ô(