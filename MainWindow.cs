using System.Diagnostics;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace VelocityDownload;

internal sealed class MainForm : Form
{
    private static readonly Color WindowBg = Color.FromArgb(243, 243, 243);
    private static readonly Color Surface = Color.FromArgb(251, 251, 251);
    private static readonly Color Stroke = Color.FromArgb(229, 229, 229);
    private static readonly Color TextMain = Color.FromArgb(31, 31, 31);
    private static readonly Color TextMuted = Color.FromArgb(96, 96, 96);
    private static readonly Color Accent = Color.FromArgb(0, 103, 192);

    private readonly TextBox _url = new();
    private readonly TextBox _folder = new();
    private readonly FluentButton _add = new("Ð”Ð¾Ð±Ð°Ð²Ð¸Ñ‚ÑŒ", Accent, Color.White);
    private readonly FluentButton _paste = new("Ð’ÑÑ‚Ð°Ð²Ð¸Ñ‚ÑŒ", Color.FromArgb(238, 238, 238), TextMain);
    private readonly FluentButton _browse = new("Ð’Ñ‹Ð±Ñ€Ð°Ñ‚ÑŒ Ð¿Ð°Ð¿ÐºÑƒ", Color.FromArgb(238, 238, 238), TextMain);
    private readonly FluentButton _clear = new("Ð£Ð±Ñ€Ð°Ñ‚ÑŒ Ð·Ð°Ð²ÐµÑ€ÑˆÑ‘Ð½Ð½Ñ‹Ðµ", Color.Transparent, Accent) { BorderColor = Stroke };
    private readonly ComboBox _connections = new();
    private readonly TextBox _internetMbps = new();
    private readonly Label _summary = MakeLabel("ÐŸÐ¾ÐºÐ° Ð½ÐµÑ‚ Ð·Ð°Ð³Ñ€ÑƒÐ·Ð¾Ðº", 9.5f, TextMuted);
    private readonly Label _diagnosticTitle = MakeLabel("Ð”Ð¸Ð°Ð³Ð½Ð¾ÑÑ‚Ð¸ÐºÐ° ÑÐºÐ¾Ñ€Ð¾ÑÑ‚Ð¸", 9.5f, TextMain, FontStyle.Bold);
    private readonly Label _diagnostics = MakeLabel("Ð”Ð¸Ð°Ð³Ð½Ð¾ÑÑ‚Ð¸ÐºÐ° ÑÐºÐ¾Ñ€Ð¾ÑÑ‚Ð¸ Ð¿Ð¾ÑÐ²Ð¸Ñ‚ÑÑ Ð²Ð¾ Ð²Ñ€ÐµÐ¼Ñ Ð·Ð°Ð³Ñ€ÑƒÐ·ÐºÐ¸", 9f, TextMuted);
    private readonly RoundedPanel _diagnosticSurface = new() { FillColor = Color.FromArgb(248, 248, 248), BorderColor = Stroke, Radius = 8 };
    private readonly Label _empty = MakeLabel("Ð—Ð´ÐµÑÑŒ Ð¿Ð¾ÑÐ²ÑÑ‚ÑÑ Ð²Ð°ÑˆÐ¸ Ð·Ð°Ð³Ñ€ÑƒÐ·ÐºÐ¸\n\nÐ’ÑÑ‚Ð°Ð²ÑŒÑ‚Ðµ Ð¿Ñ€ÑÐ¼ÑƒÑŽ ÑÑÑ‹Ð»ÐºÑƒ Ð²Ñ‹ÑˆÐµ â€” Ð·Ð°Ð³Ñ€ÑƒÐ·ÐºÐ° Ð½Ð°Ñ‡Ð½Ñ‘Ñ‚ÑÑ ÑÑ€Ð°Ð·Ñƒ", 11, TextMuted);
    private readonly SmoothFlowPanel _list = new();
    private readonly Panel _listHost = new() { Dock = DockStyle.Fill, BackColor = WindowBg };
    private readonly System.Windows.Forms.Timer _diagnosticTimer = new() { Interval = 500 };
    private readonly Queue<double> _speedSamples = new();
    private double _peakAggregateSpeed;
    private bool _hadActiveDownloads;

    public MainForm()
    {
        Text = "Velocity Download";
        ClientSize = new Size(960, 690);
        MinimumSize = new Size(790, 570);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = WindowBg;
        ForeColor = TextMain;
        Font = UiFont(10);
        Icon = FluentWindow.CreateAppIcon();
        DoubleBuffered = true;
        ResizeRedraw = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        BuildLayout();
        ConfigureInputs();
        WireEvents();
    }

    public int ActiveDownloadCount => _list.Controls.OfType<DownloadCard>().Count(x => x.IsActive);
    public int CompletedDownloadCount => _list.Controls.OfType<DownloadCard>().Count(x => x.IsCompleted);

    internal void AddPreviewCards()
    {
        var first = new DownloadCard(new Uri("https://example.com/Windows_11_24H2.iso"), _folder.Text, 8) { Width = CardWidth() };
        first.SetPreview("Windows_11_24H2.iso", "Ð—Ð°Ð³Ñ€ÑƒÐ·ÐºÐ° Â· 8 ÑÐ¾ÐµÐ´Ð¸Ð½ÐµÐ½Ð¸Ð¹", .64, "3.29 Ð“Ð‘ / 5.14 Ð“Ð‘", "3.1 ÐœÐ‘/Ñ", "ÐžÑÑ‚Ð°Ð»Ð¾ÑÑŒ 10 Ð¼Ð¸Ð½", false, 3.1 * 1024 * 1024);
        var second = new DownloadCard(new Uri("https://example.com/Project-assets.zip"), _folder.Text, 8) { Width = CardWidth() };
        second.SetPreview("Project-assets.zip", "Ð—Ð°Ð²ÐµÑ€ÑˆÐµÐ½Ð¾", 1, "842 ÐœÐ‘ / 842 ÐœÐ‘", "", "", true);
        foreach (var card in new[] { first, second })
        {
            card.RemoveRequested += async (_, _) => await RemoveCardAsync(card); card.StatusChanged += (_, _) => UpdateSummary(); _list.Controls.Add(card);
        }
        UpdateEmptyState();
        for (var i = 0; i < 14; i++) UpdateDiagnostics();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        FluentWindow.Apply(Handle);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = WindowBg,
            Padding = new Padding(34, 26, 34, 26),
            ColumnCount = 1,
            RowCount = 6
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = WindowBg };
        var icon = new FluentAppIcon { Location = new Point(0, 5), Size = new Size(44, 44) };
        var title = MakeLabel("Velocity Download", 20, TextMain, FontStyle.Bold);
        title.Location = new Point(58, 0); title.Size = new Size(360, 38);
        var subtitle = MakeLabel("Ð‘Ñ‹ÑÑ‚Ñ€Ñ‹Ðµ Ð¿Ð°Ñ€Ð°Ð»Ð»ÐµÐ»ÑŒÐ½Ñ‹Ðµ Ð·Ð°Ð³Ñ€ÑƒÐ·ÐºÐ¸", 9.5f, TextMuted);
        subtitle.Location = new Point(60, 38); subtitle.Size = new Size(330, 24);
        header.Controls.AddRange([icon, title, subtitle]);

        var urlSurface = new RoundedPanel { Dock = DockStyle.Fill, FillColor = Surface, BorderColor = Stroke, Radius = 9, Padding = new Padding(16, 12, 12, 12), Margin = new Padding(0, 0, 0, 10) };
        var urlGrid = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Surface, ColumnCount = 3, RowCount = 1 };
        urlGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        urlGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        urlGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        _url.Dock = DockStyle.Fill; _url.Margin = new Padding(4, 6, 10, 0);
        _paste.Dock = DockStyle.Fill; _add.Dock = DockStyle.Fill;
        _paste.Margin = new Padding(0, 0, 8, 0); _add.Margin = new Padding(0);
        urlGrid.Controls.Add(_url, 0, 0); urlGrid.Controls.Add(_paste, 1, 0); urlGrid.Controls.Add(_add, 2, 0);
        urlSurface.Controls.Add(urlGrid);

        var settings = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = WindowBg, ColumnCount = 2, RowCount = 2, Margin = new Padding(0) };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        var folderSurface = new RoundedPanel { Dock = DockStyle.Fill, FillColor = Surface, BorderColor = Stroke, Radius = 7, Padding = new Padding(13, 8, 10, 8), Margin = new Padding(0, 0, 10, 8) };
        _folder.Dock = DockStyle.Fill; _folder.Margin = new Padding(0, 5, 0, 0);
        folderSurface.Controls.Add(_folder);
        var secondarySettings = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = WindowBg, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
        var connectionSurface = new RoundedPanel { Width = 240, Height = 40, FillColor = Surface, BorderColor = Stroke, Radius = 7, Padding = new Padding(11, 4, 7, 4), Margin = new Padding(0, 0, 10, 0) };
        var connectionGrid = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Surface, ColumnCount = 2, RowCount = 1 };
        connectionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54)); connectionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        var connectionLabel = MakeLabel("Ð¡Ð¾ÐµÐ´Ð¸Ð½ÐµÐ½Ð¸Ñ", 8.5f, TextMuted); connectionLabel.Dock = DockStyle.Fill; connectionLabel.TextAlign = ContentAlignment.MiddleLeft;
        _connections.Dock = DockStyle.Fill; _connections.Margin = new Padding(0); connectionGrid.Controls.Add(connectionLabel, 0, 0); connectionGrid.Controls.Add(_connections, 1, 0); connectionSurface.Controls.Add(connectionGrid);
        var tariffSurface = new RoundedPanel { Width = 220, Height = 40, FillColor = Surface, BorderColor = Stroke, Radius = 7, Padding = new Padding(11, 4, 8, 4), Margin = new Padding(0) };
        var tariffGrid = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Surface, ColumnCount = 3, RowCount = 1 };
        tariffGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52)); tariffGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42)); tariffGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
        var tariffLabel = MakeLabel("Ð¢Ð°Ñ€Ð¸Ñ„", 8.5f, TextMuted); tariffLabel.Dock = DockStyle.Fill; tariffLabel.TextAlign = ContentAlignment.MiddleLeft;
        var tariffUnit = MakeLabel("ÐœÐ±Ð¸Ñ‚/Ñ", 8f, TextMuted); tariffUnit.Dock = DockStyle.Fill; tariffUnit.TextAlign = ContentAlignment.MiddleLeft;
        _internetMbps.Dock = DockStyle.Fill; _internetMbps.Margin = new Padding(0, 6, 3, 0);
        tariffGrid.Controls.Add(tariffLabel, 0, 0); tariffGrid.Controls.Add(_internetMbps, 1, 0); tariffGrid.Controls.Add(tariffUnit, 2, 0); tariffSurface.Controls.Add(tariffGrid);
        secondarySettings.Controls.Add(connectionSurface); secondarySettings.Controls.Add(tariffSurface);
        _browse.Dock = DockStyle.Fill; _browse.Margin = new Padding(0, 0, 0, 8);
        settings.Controls.Add(folderSurface, 0, 0); settings.Controls.Add(_browse, 1, 0); settings.Controls.Add(secondarySettings, 0, 1); settings.SetColumnSpan(secondarySettings, 2);

        var listHeader = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = WindowBg, ColumnCount = 2, RowCount = 1 };
        listHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); listHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
        _summary.Dock = DockStyle.Fill; _summary.TextAlign = ContentAlignment.MiddleLeft;
        _clear.Dock = DockStyle.Fill; _clear.Margin = new Padding(0, 7, 0, 5);
        listHeader.Controls.Add(_summary, 0, 0); listHeader.Controls.Add(_clear, 1, 0);

        _diagnosticSurface.Dock = DockStyle.Fill; _diagnosticSurface.Margin = new Padding(0, 2, 0, 8); _diagnosticSurface.Padding = new Padding(14, 5, 14, 5);
        var diagnosticGrid = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = _diagnosticSurface.FillColor, ColumnCount = 1, RowCount = 2 };
        diagnosticGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 21)); diagnosticGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _diagnosticTitle.Dock = DockStyle.Fill; _diagnosticTitle.TextAlign = ContentAlignment.MiddleLeft;
        _diagnostics.Dock = DockStyle.Fill; _diagnostics.TextAlign = ContentAlignment.MiddleLeft;
        diagnosticGrid.Controls.Add(_diagnosticTitle, 0, 0); diagnosticGrid.Controls.Add(_diagnostics, 0, 1); _diagnosticSurface.Controls.Add(diagnosticGrid);

        _list.Dock = DockStyle.Fill; _list.AutoScroll = true; _list.FlowDirection = FlowDirection.TopDown; _list.WrapContents = false; _list.BackColor = WindowBg; _list.Padding = new Padding(0, 0, 8, 0);
        _empty.Dock = DockStyle.Fill; _empty.TextAlign = ContentAlignment.MiddleCenter;
        _listHost.Controls.Add(_empty); _listHost.Controls.Add(_list);
        _list.BringToFront();

        root.Controls.Add(header, 0, 0); root.Controls.Add(urlSurface, 0, 1); root.Controls.Add(settings, 0, 2); root.Controls.Add(listHeader, 0, 3); root.Controls.Add(_diagnosticSurface, 0, 4); root.Controls.Add(_listHost, 0, 5);
        Controls.Add(root);
    }

    private void ConfigureInputs()
    {
        ConfigureTextBox(_url, "Ð’ÑÑ‚Ð°Ð²ÑŒÑ‚Ðµ Ð¾Ð´Ð½Ñƒ Ð¸Ð»Ð¸ Ð½ÐµÑÐºÐ¾Ð»ÑŒÐºÐ¾ Ð¿Ñ€ÑÐ¼Ñ‹Ñ… ÑÑÑ‹Ð»Ð¾Ðºâ€¦");
        ConfigureTextBox(_folder, "ÐŸÐ°Ð¿ÐºÐ° Ð´Ð»Ñ ÑÐ¾Ñ…Ñ€Ð°Ð½ÐµÐ½Ð¸Ñ");
        _folder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        _connections.DropDownStyle = ComboBoxStyle.DropDownList;
        _connections.Items.AddRange(["ÐÐ²Ñ‚Ð¾ Â· 8", "4 Ð¿Ð¾Ñ‚Ð¾ÐºÐ°", "8 Ð¿Ð¾Ñ‚Ð¾ÐºÐ¾Ð²", "16 Ð¿Ð¾Ñ‚Ð¾ÐºÐ¾Ð²", "1 Ð¿Ð¾Ñ‚Ð¾Ðº"]);
        _connections.SelectedIndex = 0;
        _connections.Font = UiFont(9.5f);
        _connections.BackColor = Surface; _connections.ForeColor = TextMain; _connections.FlatStyle = FlatStyle.Flat;
        _connections.DrawMode = DrawMode.OwnerDrawFixed; _connections.ItemHeight = 30;
        _connections.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            var selected = (e.State & DrawItemState.Selected) != 0;
            using var bg = new SolidBrush(selected ? Color.FromArgb(225, 239, 252) : Surface);
            using var fg = new SolidBrush(TextMain);
            e.Graphics.FillRectangle(bg, e.Bounds);
            e.Graphics.DrawString(_connections.Items[e.Index]!.ToString(), _connections.Font, fg, e.Bounds.X + 10, e.Bounds.Y + 6);
        };

        _internetMbps.Text = "200"; _internetMbps.MaxLength = 6; _internetMbps.TextAlign = HorizontalAlignment.Right;
        _internetMbps.BorderStyle = BorderStyle.None; _internetMbps.BackColor = Surface; _internetMbps.ForeColor = TextMain; _internetMbps.Font = UiFont(9.5f);

        UpdateEmptyState();
    }

    private void WireEvents()
    {
        _add.Click += async (_, _) => await AddFromInputAsync();
        _paste.Click += (_, _) => PasteLinks();
        _browse.Click += (_, _) => BrowseFolder();
        _clear.Click += (_, _) => ClearCompleted();
        _url.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await AddFromInputAsync(); } };
        _listHost.Resize += (_, _) => ResizeCards();
        _internetMbps.KeyPress += (_, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
        _internetMbps.TextChanged += (_, _) => { _speedSamples.Clear(); _peakAggregateSpeed = 0; };
        _diagnosticTimer.Tick += (_, _) => UpdateDiagnostics();
        _diagnosticTimer.Start();
        FormClosing += (_, _) => { foreach (var card in _list.Controls.OfType<DownloadCard>()) card.PauseForExit(); };
        FormClosed += (_, _) => _diagnosticTimer.Stop();
    }

    private async Task AddFromInputAsync()
    {
        var links = _url.Text.Split(['\r', '\n', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Uri.TryCreate(x, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https") ? uri : null)
            .Where(x => x is not null).Cast<Uri>().DistinctBy(x => x.ToString()).ToList();
        if (links.Count == 0)
        {
            MessageBox.Show(this, "Ð’ÑÑ‚Ð°Ð²ÑŒÑ‚Ðµ Ð¿Ñ€ÑÐ¼ÑƒÑŽ ÑÑÑ‹Ð»ÐºÑƒ, Ð½Ð°Ñ‡Ð¸Ð½Ð°ÑŽÑ‰ÑƒÑŽÑÑ Ñ http:// Ð¸Ð»Ð¸ https://", "Velocity Download", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
ß®»¶‰žËkºwµç}É•Íå¹Œ ¤(€€€ì(€€€€€€€}ÍÑ…Ñ”€ô…É‘MÑ…Ñ”¹½¹¹•Ñ¥¹œì(€€€€€€€}ÍÑ…ÑÕÌ¹Q•áÐ€ô€‹BBûBÓBëBïF;FB×B÷BãBÔƒBèƒFB×FBËB×FFŠ˜ˆì}ÍÑ…ÑÕÌ¹½É•½±½È€ô5ÕÑ•ì(€€€€€€€}Á…ÕÍ”¹Q•áÐ€ô€‹BBÃFBßBÀˆì}Á…ÕÍ”¹¹…‰±•€ôÑÉÕ”ì(€€€€€€€}•¹¥¹”€ô¹•Ü½Ý¹±½…‘¹¥¹”¡}ÕÉ°°}™½±‘•È°}½¹¹•Ñ¥½¹Ì¤ì(€€€€€€€}ÑÌ€ô¹•Ü…¹•±±…Ñ¥½¹Q½­•¹M½ÕÉ” ¤ì(€€€€€€€}Ñ¥µ•È¹MÑ…ÉÐ ¤ìMÑ…ÑÕÍ¡…¹•ü¹%¹Ù½­”¡Ñ¡¥Ì°Ù•¹ÑÉÌ¹µÁÑä¤ì(€€€€€€€ÑÉä(€€€€€€€ì(€€€€€€€€€€€}ÍÑ…Ñ”€ô…É‘MÑ…Ñ”¹IÕ¹¹¥¹œì(€€€€€€€€€€€}½µÁ±•Ñ•‘A…Ñ €ô…Ý…¥Ð}•¹¥¹”¹IÕ¹Íå¹Œ¡}ÑÌ¹Q½­•¸¤ì(€€€€€€€€€€€}ÍÑ…Ñ”€ô…É‘MÑ…Ñ”¹½µÁ±•Ñ•ì(€€€€€€€€€€€}ÁÉ½É•ÍÌ¹Y…±Õ”€ô€Äì(€€€€€€€€€€€}¹…µ”¹Q•áÐ€ôA…Ñ ¹•Ñ¥±•9…µ”¡}½µÁ±•Ñ•‘A…Ñ ¤ì(€€€€€€€€€€€}ÍÑ…ÑÕÌ¹Q•áÐ€ô€‹B_BÃBËB×FF#B×B÷Bøˆì}ÍÑ…ÑÕÌ¹½É•½±½È€ôMÕ•ÍÌì(€€€€€€€€€€€}Á…ÕÍ”¹Q•áÐ€ô€‹BOBûFBûBËBøˆì}Á…ÕÍ”¹¹…‰±•€ô™…±Í”ì(€€€€€€€€€€€}½Á•¸¹¹…‰±•€ôÑÉÕ”ì(€€€€€€€ô(€€€€€€€…Ñ €¡=Á•É…Ñ¥½¹…¹•±•‘á•ÁÑ¥½¸¤(€€€€€€€ì(€€€€€€€€€€€¥˜€¡}‘¥Í…É‘I•ÅÕ•ÍÑ•ñð}±½Í¥¹œ¤É•ÑÕÉ¸ì(€€€€€€€€€€€}ÍÑ…Ñ”€ô…É‘MÑ…Ñ”¹A…ÕÍ•ì(€€€€€€€€€€€}ÍÑ…ÑÕÌ¹Q•áÐ€ô€‹BFBãBûFFBÃB÷BûBËBïB×B÷BøƒŠPƒBÿFBûBÏFB×FFƒFBûFFBÃB÷FGBôˆì}ÍÑ…ÑÕÌ¹½É•½±½È€ô5ÕÑ•ì(€€€€€€€€€€€}Á…ÕÍ”¹Q•áÐ€ô€‹BFBûBÓBûBïBÛBãFF0ˆì}Á…ÕÍ”¹¹…‰±•€ôÑÉÕ”ì(€€€€€€€ô(€€€€€€€…Ñ €¡á•ÁÑ¥½¸•à¤(€€€€€€€ì(€€€€€€€€€€€}ÍÑ…Ñ”€ô…É‘MÑ…Ñ”¹…¥±•ì(€€€€€€€€€€€}ÍÑ…ÑÕÌ¹Q•áÐ€ô€‹B{F#BãBÇBëBÀè€ˆ€¬•à¹5•ÍÍ…”ì}ÍÑ…ÑÕÌ¹½É•½±½È€ôÉÉ½Èì(€€€€€€€€€€€}Á…ÕÍ”¹Q•áÐ€ô€‹BBûBËFBûFBãFF0ˆì}Á…ÕÍ”¹¹…‰±•€ôÑÉÕ”ì(€€€€€€€ô(€€€€€€€™¥¹…±±ä(€€€€€€€ì(€€€€€€€€€€€}Ñ¥µ•È¹MÑ½À ¤ì(€€€€€€€€€€€¥˜€ …}‘¥Í…É‘I•ÅÕ•ÍÑ•€˜˜€…}±½Í¥¹œ¤ìI•™É•Í¡AÉ½É•ÍÌ ¤ìMÑ…ÑÕÍ¡…¹•ü¹%¹Ù½­”¡Ñ¡¥Ì°Ù•¹ÑÉÌ¹µÁÑä¤ìô(€€€€€€€ô(€€€ô((€€€ÁÕ‰±¥ŒÙ½¥A…ÕÍ•½Éá¥Ð ¤(€€€ì(€€€€€€€}±½Í¥¹œ€ôÑÉÕ”ì(€€€€€€€}ÑÌü¹…¹•° ¤ì(€€€ô((€€€ÁÕ‰±¥Œ…Íå¹ŒQ…Í¬¥Í…É‘Íå¹Œ ¤(€€€ì(€€€€€€€}‘¥Í…É‘I•ÅÕ•ÍÑ•€ôÑÉÕ”ì(€€€€€€€}ÑÌü¹…¹•° ¤ì(€€€€€€€¥˜€¡}ÕÉÉ•¹ÑIÕ¸¥Ì¹½Ð¹Õ±°¤(€€€€€€€ì(€€€€€€€€€€€ÑÉäì…Ý…¥Ð}ÕÉÉ•¹ÑIÕ¸ìô(€€€€€€€€€€€…Ñ ìô(€€€€€€€ô(€€€€€€€…Ý…¥Ð½Ý¹±½…‘¹¥¹”¹•±•Ñ•…¡•‘½Ý¹±½…‘Íå¹Œ¡}ÕÉ°°}™½±‘•È¤ì(€€€ô((€€€¥¹Ñ•É¹…°Ù½¥M•ÑAÉ•Ù¥•Ü¡ÍÑÉ¥¹œ¹…µ”°ÍÑÉ¥¹œÍÑ…ÑÕÌ°‘½Õ‰±”ÁÉ½É•ÍÌ°ÍÑÉ¥¹œÍ¥é”°ÍÑÉ¥¹œÍÁ••°ÍÑÉ¥¹œ•Ñ„°‰½½°½µÁ±•Ñ•°‘½Õ‰±”‰åÑ•ÍA•ÉM•½¹€ô€À¤(€€€ì(€€€€€€€}¹…µ”¹Q•áÐ€ô¹…µ”ì}ÍÑ…ÑÕÌ¹Q•áÐ€ôÍÑ…ÑÕÌì}ÁÉ½É•ÍÌ¹Y…±Õ”€ôÁÉ½É•ÍÌì}Í¥é”¹Q•áÐ€ôÍ¥é”ì}ÍÁ••¹Q•áÐ€ôÍÁ••ì}•Ñ„¹Q•áÐ€ô•Ñ„ì(€€€€€€€}ÕÉÉ•¹Ñ	åÑ•ÍA•ÉM•½¹€ô‰åÑ•ÍA•ÉM•½¹ì}Á•…­	åÑ•ÍA•ÉM•½¹€ô‰åÑ•ÍA•ÉM•½¹ì(€€€€€€€}ÍÑ…Ñ”€ô½µÁ±•Ñ•€ü…É‘MÑ…Ñ”¹½µÁ±•Ñ•€è…É‘MÑ…Ñ”¹IÕ¹¹¥¹œì(€€€€€€€}ÍÑ…ÑÕÌ¹½É•½±½È€ô½µÁ±•Ñ•€üMÕ•ÍÌ€è•¹Ðì(€€€€€€€}Á…ÕÍ”¹Q•áÐ€ô½µÁ±•Ñ•€ü€‹BOBûFBûBËBøˆ€è€‹BBÃFBßBÀˆì}Á…ÕÍ”¹¹…‰±•€ô€…½µÁ±•Ñ•ì(€€€ô((€€€ÁÉ½Ñ•Ñ•½Ù•ÉÉ¥‘”Ù½¥=¹1…å½ÕÐ¡1…å½ÕÑÙ•¹ÑÉÌ”¤(€€€ì(€€€€€€€‰…Í”¹=¹1…å½ÕÐ¡”¤ì(€€€€€€€Ù…È…Ñ¥½¹Í]¥‘Ñ €ô€ÈÌØì(€€€€€€€}¹…µ”¹M•Ñ	½Õ¹‘Ì ÈÀ°€ÄÔ°5…Ñ ¹5…à ÄÈÀ°]¥‘Ñ €´…Ñ¥½¹Í]¥‘Ñ €´€ÌÐ¤°€ÈÔ¤ì(€€€€€€€}ÍÑ…ÑÕÌ¹M•Ñ	½Õ¹‘Ì ÈÀ°€ÐÈ°5…Ñ ¹5…à ÄÈÀ°]¥‘Ñ €´…Ñ¥½¹Í]¥‘Ñ €´€ÌÐ¤°€ÈÈ¤ì(€€€€€€€}É•µ½Ù”¹M•Ñ	½Õ¹‘Ì¡]¥‘Ñ €´€Ðà°€ÄØ°€Èà°€Èà¤ì(€€€€€€€}½Á•¸¹M•Ñ	½Õ¹‘Ì¡]¥‘Ñ €´€ÄÈØ°€ÄØ°€ÜÀ°€ÌÀ¤ì(€€€€€€€}Á…ÕÍ”¹M•Ñ	½Õ¹‘Ì¡]¥‘Ñ €´€ÈÈÐ°€ÄØ°€äÀ°€ÌÀ¤ì(€€€€€€€}ÁÉ½É•ÍÌ¹M•Ñ	½Õ¹‘Ì ÈÀ°€ÜÈ°5…Ñ ¹5…à ÄÀÀ°]¥‘Ñ €´€ÐÀ¤°€Ü¤ì(€€€€€€€}Í¥é”¹M•Ñ	½Õ¹‘Ì ÈÀ°€àà°€ÈÌÀ°€ÈÈ¤ì(€€€€€€€}ÍÁ••¹M•Ñ	½Õ¹‘Ì¡5…Ñ ¹5…à ÈÔÔ°]¥‘Ñ €¼€È€´€àÀ¤°€àà°€ÄØÀ°€ÈÈ¤ì(€€€€€€€}•Ñ„¹M•Ñ	½Õ¹‘Ì¡5…Ñ ¹5…à ÐÌÀ°]¥‘Ñ €´€ÈÔÀ¤°€àà°€ÈÌÀ°€ÈÈ¤ì}•Ñ„¹Q•áÑ±¥¸€ô½¹Ñ•¹Ñ±¥¹µ•¹Ð¹5¥‘‘±•I¥¡Ðì(€€€ô((€€€ÁÉ½Ñ•Ñ•½Ù•ÉÉ¥‘”Ù½¥=¹A…¥¹Ð¡A…¥¹ÑÙ•¹ÑÉÌ”¤(€€€ì(€€€€€€€”¹É…Á¡¥Ì¹Mµ½½Ñ¡¥¹5½‘”€ôMµ½½Ñ¡¥¹5½‘”¹¹Ñ¥±¥…Ìì(€€€€€€€ÕÍ¥¹œÙ…ÈÁ…Ñ €ôÉ…Ý¥¹UÑ¥°¹I½Õ¹‘I•Ð¡¹•ÜI•Ñ…¹±• À¸Õ˜°€À¸Õ˜°]¥‘Ñ €´€Ä°!•¥¡Ð€´€È¤°€ÄÀ¤ì(€€€€€€€ÕÍ¥¹œÙ…È™¥±°€ô¹•ÜM½±¥‘	ÉÕÍ ¡…É‘	œ¤ìÕÍ¥¹œÙ…ÈÁ•¸€ô¹•ÜA•¸¡	½É‘•È¤ì(€€€€€€€”¹É…Á¡¥Ì¹¥±±A…Ñ ¡™¥±°°Á…Ñ ¤ì”¹É…Á¡¥Ì¹É…ÝA…Ñ ¡Á•¸°Á…Ñ ¤ì(€€€ô((€€€ÁÉ¥Ù…Ñ”…Íå¹ŒQ…Í¬Q½±•A…ÕÍ•Íå¹Œ ¤(€€€ì(€€€€€€€¥˜€¡}ÍÑ…Ñ”¥Ì…É‘MÑ…Ñ”¹A…ÕÍ•½È…É‘MÑ…Ñ”¹…¥±•¤ì…Ý…¥ÐMÑ…ÉÑÍå¹Œ ¤ìÉ•ÑÕÉ¸ìô(€€€€€€€¥˜€ …%ÍÑ¥Ù”¤É•ÑÕÉ¸ì(€€€€€€€}ÍÑ…Ñ”€ô…É‘MÑ…Ñ”¹A…ÕÍ¥¹œì}Á…ÕÍ”¹¹…‰±•€ô™…±Í”ì}ÍÑ…ÑÕÌ¹Q•áÐ€ô€‹B‡BûFFBÃB÷F?B×BðƒBÿFBûBÏFB×FFŠ˜ˆì(€€€€€€€}ÑÌü¹…¹•° ¤ìMÑ…ÑÕÍ¡…¹•ü¹%¹Ù½­”¡Ñ¡¥Ì°Ù•¹ÑÉÌ¹µÁÑä¤ì(€€€ô((€€€ÁÉ¥Ù…Ñ”Ù½¥I•™É•Í¡AÉ½É•ÍÌ ¤(€€€ì(€€€€€€€¥˜€¡}•¹¥¹”¥Ì¹Õ±°¤É•ÑÕÉ¸ì(€€€€€€€Ù…ÈÀ€ô}•¹¥¹”¹•ÑAÉ½É•ÍÌ ¤ì(€€€€€€€}ÕÉÉ•¹Ñ	åÑ•ÍA•ÉM•½¹€ôÀ¹	åÑ•ÍA•ÉM•½¹ì(€€€€€€€}Á•…­	åÑ•ÍA•ÉM•½¹€ô5…Ñ ¹5…à¡}Á•…­	åÑ•ÍA•ÉM•½¹°À¹	åÑ•ÍA•ÉM•½¹¤ì(€€€€€€€¥˜€ …ÍÑÉ¥¹œ¹%Í9Õ±±=É]¡¥Ñ•MÁ…”¡À¹¥±•9…µ”¤¤}¹…µ”¹Q•áÐ€ôÀ¹¥±•9…µ”ì(€€€€€€€}Í¥é”¹Q•áÐ€ô€‰íY…±Õ•½Éµ…Ð¹	åÑ•Ì¡À¹½Ý¹±½…‘•¥ô€¼ì¡À¹Q½Ñ…°€ø€À€üY…±Õ•½Éµ…Ð¹	åÑ•Ì¡À¹Q½Ñ…°¤€è€‹B÷B×BãBßBËB×FFB÷Bøˆ¥ôˆì(€€€€€€€}ÍÁ••¹Q•áÐ€ôÀ¹	åÑ•ÍA•ÉM•½¹€ø€À€ü€‰íY…±Õ•½Éµ…Ð¹	åÑ•Ì ¡±½¹œ¥À¹	åÑ•ÍA•ÉM•½¹¥ô¿Fˆ€è€ˆˆì(€€€€€€€¥˜€¡À¹Q½Ñ…°€ø€À¤(€€€€€€€ì(€€€€€€€€€€€}ÁÉ½É•ÍÌ¹Y…±Õ”€ô5…Ñ ¹±…µÀ ¡‘½Õ‰±”¥À¹½Ý¹±½…‘•€¼À¹Q½Ñ…°°€À°€Ä¤ì(€€€€€€€€€€€¥˜€¡À¹	åÑ•ÍA•ÉM•½¹€ø€Ä€˜˜À¹½Ý¹±½…‘•€ðÀ¹Q½Ñ…°¤(€€€€€€€€€€€€€€€}•Ñ„¹Q•áÐ€ô€‹B{FFBÃBïBûFF0€ˆ€¬Y…±Õ•½Éµ…Ð¹ÕÉ…Ñ¥½¸¡Q¥µ•MÁ…¸¹É½µM•½¹‘Ì ¡À¹Q½Ñ…°€´À¹½Ý¹±½…‘•¤€¼À¹	åÑ•ÍA•ÉM•½¹¤¤ì(€€€€€€€ô(€€€€€€€¥˜€¡}ÍÑ…Ñ”€ôô…É‘MÑ…Ñ”¹IÕ¹¹¥¹œ¤(€€€€€€€ì(€€€€€€€€€€€}ÍÑ…ÑÕÌ¹Q•áÐ€ôÀ¹UÍ¥¹I…¹•Ì€ü€‹B_BÃBÏFFBßBëBÀƒ
Üí5…Ñ ¹5…à Ä°À¹Ñ¥Ù•½¹¹•Ñ¥½¹Ì¥ôƒFBûB×BÓBãB÷B×B÷BãBäˆ€è€‹B_BÃBÏFFBßBëBÀƒ
ÜƒBûBÓBãBôƒBÿBûFBûBèˆì(€€€€€€€€€€€}ÍÑ…ÑÕÌ¹½É•½±½È€ô•¹Ðì(€€€€€€€ô(€€€ô((€€€ÁÉ¥Ù…Ñ”Ù½¥=Á•¹½±‘•È ¤(€€€ì(€€€€€€€Ù…È™½±‘•È€ô}½µÁ±•Ñ•‘A…Ñ ¥Ì¹½Ð¹Õ±°€üA…Ñ ¹•Ñ¥É•Ñ½Éå9…µ”¡}½µÁ±•Ñ•‘A…Ñ ¤„€è}™½±‘•Èì(€€€€€€€¥˜€¡¥É•Ñ½Éä¹á¥ÍÑÌ¡™½±‘•È¤¤AÉ½•ÍÌ¹MÑ…ÉÐ¡¹•ÜAÉ½•ÍÍMÑ…ÉÑ%¹™¼ ‰•áÁ±½É•È¹•á”ˆ°€‰p‰í™½±‘•Éõpˆˆ¤ìUÍ•M¡•±±á•ÕÑ”€ôÑÉÕ”ô¤ì(€€€ô)ô()¥¹Ñ•É¹…°Í•…±•±…ÍÌ±Õ•¹Ñ	ÕÑÑ½¸€è½¹ÑÉ½°)ì(€€€ÁÉ¥Ù…Ñ”‰½½°}¡½Ù•Èì(€€€ÁÉ¥Ù…Ñ”‰½½°}ÁÉ•ÍÍ•ì(€€€m	É½ÝÍ…‰±”¡™…±Í”¤°•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¡•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¹!¥‘‘•¸¥tÁÕ‰±¥Œ½±½È¥±±½±½Èì•ÐìÍ•Ðìô(€€€m	É½ÝÍ…‰±”¡™…±Í”¤°•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¡•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¹!¥‘‘•¸¥tÁÕ‰±¥Œ½±½È!½Ù•É½±½Èì•ÐìÍ•Ðìô(€€€m	É½ÝÍ…‰±”¡™…±Í”¤°•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¡•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¹!¥‘‘•¸¥tÁÕ‰±¥Œ½±½ÈQ•áÑ½±½Èì•ÐìÍ•Ðìô(€€€m	É½ÝÍ…‰±”¡™…±Í”¤°•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¡•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¹!¥‘‘•¸¥tÁÕ‰±¥Œ½±½È	½É‘•É½±½Èì•ÐìÍ•Ðìô€ô½±½È¹QÉ…¹ÍÁ…É•¹Ðì(€€€m	É½ÝÍ…‰±”¡™…±Í”¤°•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¡•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¹!¥‘‘•¸¥tÁÕ‰±¥Œ¥¹ÐI…‘¥ÕÌì•ÐìÍ•Ðìô€ô€Øì((€€€ÁÕ‰±¥Œ±Õ•¹Ñ	ÕÑÑ½¸¡ÍÑÉ¥¹œÑ•áÐ°½±½È™¥±°°½±½ÈÑ•áÑ½±½È¤(€€€ì(€€€€€€€Q•áÐ€ôÑ•áÐì¥±±½±½È€ô™¥±°ì!½Ù•É½±½È€ô™¥±°€ôô½±½È¹QÉ…¹ÍÁ…É•¹Ð€ü½±½È¹É½µÉˆ ÈÌÔ°€ÈÌÔ°€ÈÌÔ¤€è½¹ÑÉ½±A…¥¹Ð¹…É¬¡™¥±°°€¸ÀÑ˜¤ìQ•áÑ½±½È€ôÑ•áÑ½±½Èì(€€€€€€€½¹Ð€ô5…¥¹½É´¹U¥½¹Ð ä°½¹ÑMÑå±”¹I•Õ±…È¤ìÕÉÍ½È€ôÕÉÍ½ÉÌ¹!…¹ì•ÍÍ¥‰±•I½±”€ô•ÍÍ¥‰±•I½±”¹AÕÍ¡	ÕÑÑ½¸ìQ…‰MÑ½À€ôÑÉÕ”ì(€€€€€€€M•ÑMÑå±”¡½¹ÑÉ½±MÑå±•Ì¹±±A…¥¹Ñ¥¹%¹]µA…¥¹Ðð½¹ÑÉ½±MÑå±•Ì¹=ÁÑ¥µ¥é•‘½Õ‰±•	Õ™™•Èð½¹ÑÉ½±MÑå±•Ì¹UÍ•ÉA…¥¹Ðð½¹ÑÉ½±MÑå±•Ì¹I•Í¥é•I•‘É…Üð½¹ÑÉ½±MÑå±•Ì¹MÕÁÁ½ÉÑÍQÉ…¹ÍÁ…É•¹Ñ	…­½±½È°ÑÉÕ”¤ì(€€€ô((€€€ÁÉ½Ñ•Ñ•½Ù•ÉÉ¥‘”Ù½¥=¹5½ÕÍ•¹Ñ•È¡Ù•¹ÑÉÌ”¤ì}¡½Ù•È€ôÑÉÕ”ì%¹Ù…±¥‘…Ñ” ¤ì‰…Í”¹=¹5½ÕÍ•¹Ñ•È¡”¤ìô(€€€ÁÉ½Ñ•Ñ•½Ù•ÉÉ¥‘”Ù½¥=¹5½ÕÍ•1•…Ù”¡Ù•¹ÑÉÌ”¤ì}¡½Ù•È€ô}ÁÉ•ÍÍ•€ô™…±Í”ì%¹Ù…±¥‘…Ñ” ¤ì‰…Í”¹=¹5½ÕÍ•1•…Ù”¡”¤ìô(€€€ÁÉ½Ñ•Ñ•½Ù•ÉÉ¥‘”Ù½¥=¹5½ÕÍ•½Ý¸¡5½ÕÍ•Ù•¹ÑÉÌ”¤ì¥˜€¡”¹	ÕÑÑ½¸€ôô5½ÕÍ•	ÕÑÑ½¹Ì¹1•™Ð¤}ÁÉ•ÍÍ•€ôÑÉÕ”ì%¹Ù…±¥‘…Ñ” ¤ì‰…Í”¹=¹5½ÕÍ•½Ý¸¡”¤ìô(€€€ÁÉ½Ñ•Ñ•½Ù•ÉÉ¥‘”Ù½¥=¹5½ÕÍ•UÀ¡5½ÕÍ•Ù•¹ÑÉÌ”¤ì}ÁÉ•ÍÍ•€ô™…±Í”ì%¹Ù…±¥‘…Ñ” ¤ì‰…Í”¹=¹5½ÕÍ•UÀ¡”¤ìô(€€€ÁÉ½Ñ•Ñ•½Ù•ÉÉ¥‘”Ù½¥=¹-•å½Ý¸¡-•åÙ•¹ÑÉÌ”¤ì¥˜€¡”¹-•å½‘”¥Ì-•åÌ¹¹Ñ•È½È-•åÌ¹MÁ…”¤ì=¹±¥¬¡Ù•¹ÑÉÌ¹µÁÑä¤ì”¹!…¹‘±•€ôÑÉÕ”ìô‰…Í”¹=¹-•å½Ý¸¡”¤ìô((€€€ÁÉ½Ñ•Ñ•½Ù•ÉÉ¥‘”Ù½¥=¹A…¥¹Ð¡A…¥¹ÑÙ•¹ÑÉÌ”¤(€€€ì(€€€€€€€”¹É…Á¡¥Ì¹Mµ½½Ñ¡¥¹5½‘”€ôMµ½½Ñ¡¥¹5½‘”¹¹Ñ¥±¥…Ìì(€€€€€€€”¹É…Á¡¥Ì¹±•…È¡A…É•¹Ðü¹	…­½±½È€üüMåÍÑ•µ½±½ÉÌ¹½¹ÑÉ½°¤ì(€€€€€€€Ù…È™¥±°€ô€…¹…‰±•€ü½±½È¹É½µÉˆ ÈÌÔ°€ÈÌÔ°€ÈÌÔ¤€è}ÁÉ•ÍÍ•€ü½¹ÑÉ½±A…¥¹Ð¹…É¬¡¥±±½±½È€ôô½±½È¹QÉ…¹ÍÁ…É•¹Ð€ü!½Ù•É½±½È€è¥±±½±½È°€¸Àá˜¤€è}¡½Ù•È€ü!½Ù•É½±½È€è¥±±½±½Èì(€€€€€€€Ù…ÈÑ•áÐ€ô¹…‰±•€üQ•áÑ½±½È€è½±½È¹É½µÉˆ ÄÔÀ°€ÄÔÀ°€ÄÔÀ¤ì(€€€€€€€ÕÍ¥¹œÙ…ÈÁ…Ñ €ôÉ…Ý¥¹UÑ¥°¹I½Õ¹‘I•Ð¡¹•ÜI•Ñ…¹±• ¸Õ˜°€¸Õ˜°]¥‘Ñ €´€Ä°!•¥¡Ð€´€Ä¤°I…‘¥ÕÌ¤ì(€€€€€€€¥˜€¡™¥±°€„ô½±½È¹QÉ…¹ÍÁ…É•¹Ð¤ìÕÍ¥¹œÙ…È‰ÉÕÍ €ô¹•ÜM½±¥‘	ÉÕÍ ¡™¥±°¤ì”¹É…Á¡¥Ì¹¥±±A…Ñ ¡‰ÉÕÍ °Á…Ñ ¤ìô(€€€€€€€¥˜€¡	½É‘•É½±½È€„ô½±½È¹QÉ…¹ÍÁ…É•¹Ð¤ìÕÍ¥¹œÙ…ÈÁ•¸€ô¹•ÜA•¸¡	½É‘•É½±½È¤ì”¹É…Á¡¥Ì¹É…ÝA…Ñ ¡Á•¸°Á…Ñ ¤ìô(€€€€€€€Q•áÑI•¹‘•É•È¹É…ÝQ•áÐ¡”¹É…Á¡¥Ì°Q•áÐ°½¹Ð°±¥•¹ÑI•Ñ…¹±”°Ñ•áÐ°Q•áÑ½Éµ…Ñ±…Ì¹!½É¥é½¹Ñ…±•¹Ñ•ÈðQ•áÑ½Éµ…Ñ±…Ì¹Y•ÉÑ¥…±•¹Ñ•ÈðQ•áÑ½Éµ…Ñ±…Ì¹¹‘±±¥ÁÍ¥Ì¤ì(€€€€€€€¥˜€¡½ÕÍ•¤½¹ÑÉ½±A…¥¹Ð¹É…Ý½ÕÍI•Ñ…¹±”¡”¹É…Á¡¥Ì°I•Ñ…¹±”¹%¹™±…Ñ”¡±¥•¹ÑI•Ñ…¹±”°€´Ì°€´Ì¤¤ì(€€€ô)ô()¥¹Ñ•É¹…°Í•…±•±…ÍÌI½Õ¹‘•‘A…¹•°€èA…¹•°)ì(€€€m	É½ÝÍ…‰±”¡™…±Í”¤°•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¡•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¹!¥‘‘•¸¥tÁÕ‰±¥Œ½±½È¥±±½±½Èì•ÐìÍ•Ðìô€ô½±½È¹]¡¥Ñ”ì(€€€m	É½ÝÍ…‰±”¡™…±Í”¤°•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¡•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¹!¥‘‘•¸¥tÁÕ‰±¥Œ½±½È	½É‘•É½±½Èì•ÐìÍ•Ðìô€ô½±½È¹QÉ…¹ÍÁ…É•¹Ðì(€€€m	É½ÝÍ…‰±”¡™…±Í”¤°•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¡•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¹!¥‘‘•¸¥tÁÕ‰±¥Œ¥¹ÐI…‘¥ÕÌì•ÐìÍ•Ðìô€ô€àì(€€€ÁÕ‰±¥ŒI½Õ¹‘•‘A…¹•° ¤(€€€ì(€€€€€€€M•ÑMÑå±”¡½¹ÑÉ½±MÑå±•Ì¹±±A…¥¹Ñ¥¹%¹]µA…¥¹Ðð½¹ÑÉ½±MÑå±•Ì¹=ÁÑ¥µ¥é•‘½Õ‰±•	Õ™™•Èð½¹ÑÉ½±MÑå±•Ì¹UÍ•ÉA…¥¹Ðð½¹ÑÉ½±MÑå±•Ì¹I•Í¥é•I•‘É…Ü°ÑÉÕ”¤ì(€€€€€€€½Õ‰±•	Õ™™•É•€ôÑÉÕ”ì(€€€ô(€€€ÁÉ½Ñ•Ñ•½Ù•ÉÉ¥‘”Ù½¥=¹I•Í¥é”¡Ù•¹ÑÉÌ•Ù•¹Ñ…ÉÌ¤(€€€ì(€€€€€€€‰…Í”¹=¹I•Í¥é”¡•Ù•¹Ñ…ÉÌ¤ì(€€€€€€€¥˜€¡]¥‘Ñ €ðô€Àñð!•¥¡Ð€ðô€À¤É•ÑÕÉ¸ì(€€€€€€€ÕÍ¥¹œÙ…ÈÁ…Ñ €ôÉ…Ý¥¹UÑ¥°¹I½Õ¹‘I•Ð¡¹•ÜI•Ñ…¹±• À°€À°]¥‘Ñ °!•¥¡Ð¤°I…‘¥ÕÌ¤ì(€€€€€€€Ù…È½±‘I•¥½¸€ôI•¥½¸ì(€€€€€€€I•¥½¸€ô¹•ÜI•¥½¸¡Á…Ñ ¤ì(€€€€€€€½±‘I•¥½¸ü¹¥ÍÁ½Í” ¤ì(€€€€€€€%¹Ù…±¥‘…Ñ” ¤ì(€€€ô(€€€ÁÉ½Ñ•Ñ•½Ù•ÉÉ¥‘”Ù½¥=¹A…¥¹Ñ	…­É½Õ¹¡A…¥¹ÑÙ•¹ÑÉÌ”¤(€€€ì(€€€€€€€”¹É…Á¡¥Ì¹±•…È¡¥±±½±½È¤ì(€€€ô(€€€ÁÉ½Ñ•Ñ•½Ù•ÉÉ¥‘”Ù½¥=¹A…¥¹Ð¡A…¥¹ÑÙ•¹ÑÉÌ”¤(€€€ì(€€€€€€€‰…Í”¹=¹A…¥¹Ð¡”¤ì(€€€€€€€¥˜€¡	½É‘•É½±½È€ôô½±½È¹QÉ…¹ÍÁ…É•¹Ð¤É•ÑÕÉ¸ì(€€€€€€€”¹É…Á¡¥Ì¹Mµ½½Ñ¡¥¹5½‘”€ôMµ½½Ñ¡¥¹5½‘”¹¹Ñ¥±¥…Ìì(€€€€€€€ÕÍ¥¹œÙ…ÈÁ…Ñ €ôÉ…Ý¥¹UÑ¥°¹I½Õ¹‘I•Ð¡¹•ÜI•Ñ…¹±• ¸Õ˜°€¸Õ˜°]¥‘Ñ €´€Ä¸Õ˜°!•¥¡Ð€´€Ä¸Õ˜¤°I…‘¥ÕÌ¤ì(€€€€€€€ÕÍ¥¹œÙ…ÈÁ•¸€ô¹•ÜA•¸¡	½É‘•É½±½È¤ì”¹É…Á¡¥Ì¹É…ÝA…Ñ ¡Á•¸°Á…Ñ ¤ì(€€€ô)ô()¥¹Ñ•É¹…°Í•…±•±…ÍÌ±Õ•¹ÑAÉ½É•ÍÌ€è½¹ÑÉ½°)ì(€€€ÁÉ¥Ù…Ñ”‘½Õ‰±”}Ù…±Õ”ì(€€€m	É½ÝÍ…‰±”¡™…±Í”¤°•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¡•Í¥¹•ÉM•É¥…±¥é…Ñ¥½¹Y¥Í¥‰¥±¥Ñä¹!¥‘‘•¸¥tÁÕ‰±¥Œ‘½Õ‰±”Y…±Õ”ì•Ð€ôø}Ù…±Õ”ìÍ•Ðì}Ù…±Õ”€ô5…Ñ ¹±…µÀ¡Ù…±Õ”°€À°€Ä¤ì%¹Ù…±¥‘…Ñ” ¤ìôô(€€€ÁÕ‰±¥Œ±Õ•¹ÑAÉ½É•ÍÌ ¤ìM•ÑMÑå±”¡½¹ÑÉ½±MÑå±•Ì¹±±A…¥¹Ñ¥¹%¹]µA…¥¹Ðð½¹ÑÉ½±MÑå±•Ì¹=ÁÑ¥µ¥é•‘½Õ‰±•	Õ™™•Èð½¹ÑÉ½±MÑå±•Ì¹UÍ•ÉA…¥¹Ð°ÑÉÕ”¤ìô(€€€ÁÉ½Ñ•Ñ•½Ù•ÉÉ¥‘”Ù½¥=¹A…¥¹Ð¡A…¥¹ÑÙ•¹ÑÉÌ”¤(€€€ì(€€€€€€€”¹É…Á¡¥Ì¹Mµ½½Ñ¡¥¹5½‘”€ôMµ½½Ñ¡¥¹5½‘”¹¹Ñ¥±¥…Ìì(€€€€€€€”¹É…Á¡¥Ì¹±•…È¡A…É•¹Ðü¹	…­½±½È€üü½±½È¹QÉ…¹ÍÁ…É•¹Ð¤ì(€€€€€€€ÕÍ¥¹œÙ…ÈÑÉ…¬€ôÉ…Ý¥¹UÑ¥°¹I½Õ¹‘I•Ð¡±¥•¹ÑI•Ñ…¹±”°!•¥¡Ð€¼€É˜¤ìÕÍ¥¹œÙ…ÈÑÉ…­	ÉÕÍ €ô¹•ÜM½±¥‘	ÉÕÍ ¡½±½È¹É½µÉˆ ÈÈÔ°€ÈÈÔ°€ÈÈÔ¤¤ì”¹É…Á¡¥Ì¹¥±±A…Ñ ¡ÑÉ…­	ÉÕÍ °ÑÉ…¬¤ì(€€€€€€€¥˜€¡}Ù…±Õ”€ðô€À¤É•ÑÕÉ¸ì(€€€€€€€Ù…ÈÝ¥‘Ñ €ô5…Ñ ¹5…à¡!•¥¡Ð°€¡™±½…Ð¤¡]¥‘Ñ €¨}Ù…±Õ”¤¤ì(€€€€€€€ÕÍ¥¹œÙ…È™¥±°€ôÉ…Ý¥¹UÑ¥°¹I½Õ¹‘I•Ð¡¹•ÜI•Ñ…¹±• À°€À°Ý¥‘Ñ °!•¥¡Ð¤°!•¥¡Ð€¼€É˜¤ìÕÍ¥¹œÙ…È…•¹Ð€ô¹•ÜM½±¥‘	ÉÕÍ ¡½±½È¹É½µÉˆ À°€ÄÀÌ°€ÄäÈ¤¤ì”¹É…Á¡¥Ì¹¥±±A…Ñ ¡…•¹Ð°™¥±°¤ì(€€€ô)ô()¥¹Ñ•É¹…°Í•…±•±…ÍÌ±Õ•¹ÑÁÁ%½¸€è½¹ÑÉ½°)ì(€€€ÁÕ‰±¥Œ±Õ•¹ÑÁÁ%½¸ ¤ìM•ÑMÑå±”¡½¹ÑÉ½±MÑå±•Ì¹±±A…¥¹Ñ¥¹%¹]µA…¥¹Ðð½¹ÑÉ½±MÑå±•Ì¹=ÁÑ¥µ¥é•‘½Õ‰±•	Õ™™•Èð½¹ÑÉ½±MÑå±•Ì¹UÍ•ÉA…¥¹Ðð½¹ÑÉ½±MÑå±•Ì¹MÕÁÁ½ÉÑÍQÉ…¹ÍÁ…É•¹Ñ	…­½±½È°ÑÉÕ”¤ì	…­½±½È€ô½±½È¹QÉ…¹ÍÁ…É•¹Ðìô(€€€ÁÉ½Ñ•Ñ•½Ù•ÉÉ¥‘”Ù½¥=¹A…¥¹Ð¡A…¥¹ÑÙ•¹ÑÉÌ”¤(€€€ì(€€€€€€€”¹É…Á¡¥Ì¹Mµ½½Ñ¡¥¹5½‘”€ôMµ½½Ñ¡¥¹5½‘”¹¹Ñ¥±¥…Ìì(€€€€€€€ÕÍ¥¹œÙ…È‰A…Ñ €ôÉ…Ý¥¹UÑ¥°¹I½Õ¹‘I•Ð¡¹•ÜI•Ñ…¹±• Ä°€Ä°]¥‘Ñ €´€È°!•¥¡Ð€´€È¤°€ÄÀ¤ìÕÍ¥¹œÙ…È‰œ€ô¹•ÜM½±¥‘	ÉÕÍ ¡½±½È¹É½µÉˆ À°€ÄÀÌ°€ÄäÈ¤¤ì”¹É…Á¡¥Ì¹¥±±A…Ñ ¡‰œ°‰A…Ñ ¤ì(€€€€€€€ÕÍ¥¹œÙ…ÈÁ•¸€ô¹•ÜA•¸¡½±½È¹]¡¥Ñ”°€Ì¤ìMÑ…ÉÑ…À€ô1¥¹•…À¹I½Õ¹°¹‘…À€ô1¥¹•…À¹I½Õ¹ôì(€€€€€€€”¹É…Á¡¥Ì¹É…Ý1¥¹”¡Á•¸°]¥‘Ñ €¼€É˜°€ÄÀ°]¥‘Ñ €¼€É˜°€Èà¤ì(€€€€€€€”¹É…Á¡¥Ì¹É…Ý1¥¹”¡Á•¸°€ÄÔ°€ÈÌ°]¥‘Ñ €¼€É˜°€ÌÄ¤ì(€€€€€€€”¹É…Á¡¥Ì¹É…Ý1¥¹”¡Á•¸°]¥‘Ñ €¼€É˜°€ÌÄ°€Èä°€ÈÌ¤ì(€€€€€€€”¹É…Á¡¥Ì¹É…Ý1¥¹”¡Á•¸°€ÄÐ°€ÌÔ°€ÌÀ°€ÌÔ¤ì(€€€ô)ô()¥¹Ñ•É¹…°Í•…±•±…ÍÌMµ½½Ñ¡±½ÝA…¹•°€è±½Ý1…å½ÕÑA…¹•°)ì(€€€ÁÕ‰±¥ŒMµ½½Ñ¡±½ÝA…¹•° ¤ì½Õ‰±•	Õ™™•É•€ôÑÉÕ”ìô)ô()¥¹Ñ•É¹…°ÍÑ…Ñ¥Œ±…ÍÌÉ…Ý¥¹UÑ¥°)ì(€€€ÁÕ‰±¥ŒÍÑ…Ñ¥ŒÉ…Á¡¥ÍA…Ñ I½Õ¹‘I•Ð¡I•Ñ…¹±•É•Ð°™±½…ÐÉ…‘¥ÕÌ¤(€€€ì(€€€€€€€Ù…ÈÁ…Ñ €ô¹•ÜÉ…Á¡¥ÍA…Ñ  ¤ìÙ…È€ô5…Ñ ¹5¥¸¡É…‘¥ÕÌ€¨€È°5…Ñ ¹5¥¸¡É•Ð¹]¥‘Ñ °É•Ð¹!•¥¡Ð¤¤ì(€€€€€€€Á…Ñ ¹‘‘ÉŒ¡É•Ð¹`°É•Ð¹d°°°€ÄàÀ°€äÀ¤ìÁ…Ñ ¹‘‘ÉŒ¡É•Ð¹I¥¡Ð€´°É•Ð¹d°°°€ÈÜÀ°€äÀ¤ì(€€€€€€€Á…Ñ ¹‘‘ÉŒ¡É•Ð¹I¥¡Ð€´°É•Ð¹	½ÑÑ½´€´°°°€À°€äÀ¤ìÁ…Ñ ¹‘‘ÉŒ¡É•Ð¹`°É•Ð¹	½ÑÑ½´€´°°°€äÀ°€äÀ¤ìÁ…Ñ ¹±½Í•¥ÕÉ” ¤ì(€€€€€€€É•ÑÕÉ¸Á…Ñ ì(€€€ô)ô()¥¹Ñ•É¹…°ÍÑ…Ñ¥Œ±…ÍÌY…±Õ•½Éµ…Ð)ì(€€€ÁÕ‰±¥ŒÍÑ…Ñ¥ŒÍÑÉ¥¹œ	åÑ•Ì¡±½¹œÙ…±Õ”¤(€€€ì(€€€€€€€ÍÑÉ¥¹mtÕ¹¥ÑÌ€ôl‹BDˆ°€‹BkBDˆ°€‹BsBDˆ°€‹BOBDˆ°€‹B‹BD‰tì‘½Õ‰±”Í¥é”€ô5…Ñ ¹5…à À°Ù…±Õ”¤ìÙ…È¤€ô€Àì(€€€€€€€Ý¡¥±”€¡Í¥é”€øô€ÄÀÈÐ€˜˜¤€ðÕ¹¥ÑÌ¹1•¹Ñ €´€Ä¤ìÍ¥é”€¼ô€ÄÀÈÐì¤¬¬ìô(€€€€€€€É•ÑÕÉ¸¤€ôô€À€ü€‰íÍ¥é”èÁôíÕ¹¥ÑÍm¥uôˆ€è€‰íÍ¥é”èÀ¸ŒôíÕ¹¥ÑÍm¥uôˆì(€€€ô(€€€ÁÕ‰±¥ŒÍÑ…Ñ¥ŒÍÑÉ¥¹œÕÉ…Ñ¥½¸¡Q¥µ•MÁ…¸Ð¤€ôøÐ¹Q½Ñ…±!½ÕÉÌ€øô€Ä€ü€‰ì¡¥¹Ð¥Ð¹Q½Ñ…±!½ÕÉÍôƒFíÐ¹5¥¹ÕÑ•ÍôƒBóBãBôˆ€èÐ¹Q½Ñ…±5¥¹ÕÑ•Ì€øô€Ä€ü€‰ì¡¥¹Ð¥Ð¹Q½Ñ…±5¥¹ÕÑ•ÍôƒBóBãBôíÐ¹M•½¹‘ÍôƒFB×Bèˆ€è€‰í5…Ñ ¹5…à À°Ð¹M•½¹‘Ì¥ôƒFB×Bèˆì)ô()¥¹Ñ•É¹…°ÍÑ…Ñ¥Œ±…ÍÌ±Õ•¹Ñ]¥¹‘½Ü)ì(€€€m±±%µÁ½ÉÐ ‰‘Ýµ…Á¤¹‘±°ˆ¥tÁÉ¥Ù…Ñ”ÍÑ…Ñ¥Œ•áÑ•É¸¥¹ÐÝµM•Ñ]¥¹‘½ÝÑÑÉ¥‰ÕÑ”¡%¹ÑAÑÈ¡Ý¹°¥¹Ð…ÑÑÉ¥‰ÕÑ”°É•˜¥¹ÐÙ…±Õ”°¥¹ÐÍ¥é”¤ì(€€€m±±%µÁ½ÉÐ ‰ÕÍ•ÈÌÈ¹‘±°ˆ¥tÁÉ¥Ù…Ñ”ÍÑ…Ñ¥Œ•áÑ•É¸‰½½°•ÍÑÉ½å%½¸¡%¹ÑAÑÈ¡…¹‘±”¤ì(€€€ÁÕ‰±¥ŒÍÑ…Ñ¥Œ%½¸É•…Ñ•ÁÁ%½¸ ¤(€€€ì(€€€€€€€ÕÍ¥¹œÙ…È‰¥Ñµ…À€ô¹•Ü	¥Ñµ…À ÌÈ°€ÌÈ¤ì(€€€€€€€ÕÍ¥¹œ€¡Ù…Èœ€ôÉ…Á¡¥Ì¹É½µ%µ…”¡‰¥Ñµ…À¤¤(€€€€€€€ì(€€€€€€€€€€€œ¹Mµ½½Ñ¡¥¹5½‘”€ôMµ½½Ñ¡¥¹5½‘”¹¹Ñ¥±¥…Ììœ¹±•…È¡½±½È¹QÉ…¹ÍÁ…É•¹Ð¤ì(€€€€€€€€€€€ÕÍ¥¹œÙ…ÈÍ¡…Á”€ôÉ…Ý¥¹UÑ¥°¹I½Õ¹‘I•Ð¡¹•ÜI•Ñ…¹±• Ä°€Ä°€ÌÀ°€ÌÀ¤°€Ü¤ìÕÍ¥¹œÙ…È‰±Õ”€ô¹•ÜM½±¥‘	ÉÕÍ ¡½±½È¹É½µÉˆ À°€ÄÀÌ°€ÄäÈ¤¤ìœ¹¥±±A…Ñ ¡‰±Õ”°Í¡…Á”¤ì(€€€€€€€€€€€ÕÍ¥¹œÙ…ÈÁ•¸€ô¹•ÜA•¸¡½±½È¹]¡¥Ñ”°€È¸Õ˜¤ìMÑ…ÉÑ…À€ô1¥¹•…À¹I½Õ¹°¹‘…À€ô1¥¹•…À¹I½Õ¹ôì(€€€€€€€€€€€œ¹É…Ý1¥¹”¡Á•¸°€ÄØ°€Ü°€ÄØ°€ÈÄ¤ìœ¹É…Ý1¥¹”¡Á•¸°€ÄÀ¸Õ˜°€ÄØ°€ÄØ°€ÈÈ¤ìœ¹É…Ý1¥¹”¡Á•¸°€ÄØ°€ÈÈ°€ÈÄ¸Õ˜°€ÄØ¤ìœ¹É…Ý1¥¹”¡Á•¸°€ä°€ÈØ°€ÈÌ°€ÈØ¤ì(€€€€€€€ô(€€€€€€€Ù…È¡…¹‘±”€ô‰¥Ñµ…À¹•Ñ!¥½¸ ¤ì(€€€€€€€ÑÉäìÕÍ¥¹œÙ…È¥½¸€ô%½¸¹É½µ!…¹‘±”¡¡…¹‘±”¤ìÉ•ÑÕÉ¸€¡%½¸¥¥½¸¹±½¹” ¤ìô(€€€€€€€™¥¹…±±äì•ÍÑÉ½å%½¸¡¡…¹‘±”¤ìô(€€€ô(€€€ÁÕ‰±¥ŒÍÑ…Ñ¥ŒÙ½¥ÁÁ±ä¡%¹ÑAÑÈ¡…¹‘±”¤(€€€ì(€€€€€€€¥˜€ …=Á•É…Ñ¥¹MåÍÑ•´¹%Í]¥¹‘½ÝÍY•ÉÍ¥½¹Ñ1•…ÍÐ ÄÀ°€À°€ÈÈÀÀÀ¤¤É•ÑÕÉ¸ì(€€€€€€€ÑÉä(€€€€€€€ì(€€€€€€€€€€€Ù…ÈÉ½Õ¹‘•€ô€ÈìÝµM•Ñ]¥¹‘½ÝÑÑÉ¥‰ÕÑ”¡¡…¹‘±”°€ÌÌ°É•˜É½Õ¹‘•°Í¥é•½˜¡¥¹Ð¤¤ì(€€€€€€€€€€€Ù…Èµ¥„€ô€ÈìÝµM•Ñ]¥¹‘½ÝÑÑÉ¥‰ÕÑ”¡¡…¹‘±”°€Ìà°É•˜µ¥„°Í¥é•½˜¡¥¹Ð¤¤ì(€€€€€€€€€€€Ù…È±¥¡Ð€ô€ÀìÝµM•Ñ]¥¹‘½ÝÑÑÉ¥‰ÕÑ”¡¡…¹‘±”°€ÈÀ°É•˜±¥¡Ð°Í¥é•½˜¡¥¹Ð¤¤ì(€€€€€€€ô(€€€€€€€…Ñ ìô(€€€ô)ô(