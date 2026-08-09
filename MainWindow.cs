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
    private readonly FluentButton _add = new("Добавить", Accent, Color.White);
    private readonly FluentButton _paste = new("Вставить", Color.FromArgb(238, 238, 238), TextMain);
    private readonly FluentButton _browse = new("Выбрать папку", Color.FromArgb(238, 238, 238), TextMain);
    private readonly FluentButton _clear = new("Убрать завершённые", Color.Transparent, Accent) { BorderColor = Stroke };
    private readonly ComboBox _connections = new();
    private readonly TextBox _internetMbps = new();
    private readonly Label _summary = MakeLabel("Пока нет загрузок", 9.5f, TextMuted);
    private readonly Label _diagnosticTitle = MakeLabel("Диагностика скорости", 9.5f, TextMain, FontStyle.Bold);
    private readonly Label _diagnostics = MakeLabel("Диагностика скорости появится во время загрузки", 9f, TextMuted);
    private readonly RoundedPanel _diagnosticSurface = new() { FillColor = Color.FromArgb(248, 248, 248), BorderColor = Stroke, Radius = 8 };
    private readonly Label _empty = MakeLabel("Здесь появятся ваши загрузки\n\nВставьте прямую ссылку выше — загрузка начнётся сразу", 11, TextMuted);
    private readonly SmoothFlowPanel _list = new();
    private readonly Panel _listHost = new() { Dock = DockStyle.Fill, BackColor = WindowBg };
    private readonly System.Windows.Forms.Timer _diagnosticTimer = new() { Interval = 500 };
    private readonly Queue<double> _speedSamples = new();
    private double _peakAggregateSpeed;
    private bool _hadActiveDownloads;

    public MainForm()
    {
        Text = "Velocity - Download Manager";
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
        first.SetPreview("Windows_11_24H2.iso", "Загрузка · 8 соединений", .64, "3.29 ГБ / 5.14 ГБ", "3.1 МБ/с", "Осталось 10 мин", false, 3.1 * 1024 * 1024);
        var second = new DownloadCard(new Uri("https://example.com/Project-assets.zip"), _folder.Text, 8) { Width = CardWidth() };
        second.SetPreview("Project-assets.zip", "Завершено", 1, "842 МБ / 842 МБ", "", "", true);
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
        var title = MakeLabel("Velocity - Download Manager", 20, TextMain, FontStyle.Bold);
        title.Location = new Point(58, 0); title.Size = new Size(520, 38);
        var subtitle = MakeLabel("Быстрые параллельные загрузки", 9.5f, TextMuted);
        subtitle.Location = new Point(60, 38); subtitle.Size = new Size(460, 24);
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
        var connectionLabel = MakeLabel("Соединения", 8.5f, TextMuted); connectionLabel.Dock = DockStyle.Fill; connectionLabel.TextAlign = ContentAlignment.MiddleLeft;
        _connections.Dock = DockStyle.Fill; _connections.Margin = new Padding(0); connectionGrid.Controls.Add(connectionLabel, 0, 0); connectionGrid.Controls.Add(_connections, 1, 0); connectionSurface.Controls.Add(connectionGrid);
        var tariffSurface = new RoundedPanel { Width = 220, Height = 40, FillColor = Surface, BorderColor = Stroke, Radius = 7, Padding = new Padding(11, 4, 8, 4), Margin = new Padding(0) };
        var tariffGrid = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Surface, ColumnCount = 3, RowCount = 1 };
        tariffGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52)); tariffGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42)); tariffGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
        var tariffLabel = MakeLabel("Тариф", 8.5f, TextMuted); tariffLabel.Dock = DockStyle.Fill; tariffLabel.TextAlign = ContentAlignment.MiddleLeft;
        var tariffUnit = MakeLabel("Мбит/с", 8f, TextMuted); tariffUnit.Dock = DockStyle.Fill; tariffUnit.TextAlign = ContentAlignment.MiddleLeft;
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
        ConfigureTextBox(_url, "Вставьте одну или несколько прямых ссылок…");
        ConfigureTextBox(_folder, "Папка для сохранения");
        _folder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        _connections.DropDownStyle = ComboBoxStyle.DropDownList;
        _connections.Items.AddRange(["Авто · 8", "4 потока", "8 потоков", "16 потоков", "1 поток"]);
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
            MessageBox.Show(this, "Вставьте прямую ссылку, начинающуюся с http:// или https://", "Velocity - Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try { Directory.CreateDirectory(_folder.Text.Trim()); }
        catch (Exception ex) { MessageBox.Show(this, "Не удалось открыть папку: " + ex.Message, "Velocity - Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        _url.Clear();
        foreach (var link in links)
        {
            if (_list.Controls.OfType<DownloadCard>().Any(x => x.SourceUrl == link && !x.IsCompleted)) continue;
            var card = new DownloadCard(link, _folder.Text.Trim(), GetConnectionCount());
            card.Width = CardWidth();
            card.RemoveRequested += async (_, _) => await RemoveCardAsync(card);
            card.StatusChanged += (_, _) => UpdateSummary();
            _list.Controls.Add(card);
            card.BringToFront();
            _ = card.StartAsync();
            await Task.Yield();
        }
        UpdateEmptyState();
    }

    private async Task RemoveCardAsync(DownloadCard card)
    {
        if (!card.IsCompleted)
        {
            var result = MessageBox.Show(this, "Удалить эту загрузку?\n\nЗагруженная часть и данные для продолжения будут удалены без возможности восстановления.", "Velocity - Download Manager", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;
        }
        _list.Controls.Remove(card);
        UpdateEmptyState();
        await card.DiscardAsync();
        card.Dispose();
    }

    private void ClearCompleted()
    {
        foreach (var card in _list.Controls.OfType<DownloadCard>().Where(x => x.IsCompleted).ToArray())
        {
            _list.Controls.Remove(card); card.Dispose();
        }
        UpdateEmptyState();
    }

    private void UpdateSummary()
    {
        var total = _list.Controls.OfType<DownloadCard>().Count();
        var active = ActiveDownloadCount;
        var done = CompletedDownloadCount;
        _summary.Text = total == 0 ? "Пока нет загрузок" : $"{active} активных   ·   {done} завершено   ·   {total} всего";
        _clear.Enabled = done > 0;
    }

    private void UpdateDiagnostics()
    {
        var active = _list.Controls.OfType<DownloadCard>().Where(x => x.IsActive).ToArray();
        if (active.Length == 0)
        {
            if (_hadActiveDownloads) { _speedSamples.Clear(); _peakAggregateSpeed = 0; }
            _hadActiveDownloads = false;
            _diagnosticTitle.Text = "Диагностика скорости";
            _diagnostics.Text = "Начните загрузку — здесь появится анализ канала и сервера";
            _diagnosticTitle.ForeColor = TextMain;
            _diagnostics.ForeColor = TextMuted;
            return;
        }

        _hadActiveDownloads = true;
        var current = active.Sum(x => x.CurrentBytesPerSecond);
        _peakAggregateSpeed = Math.Max(_peakAggregateSpeed, current);
        if (current > 0)
        {
            _speedSamples.Enqueue(current);
            while (_speedSamples.Count > 24) _speedSamples.Dequeue();
        }

        var tariffMbps = double.TryParse(_internetMbps.Text, out var parsedTariff) && parsedTariff > 0 ? parsedTariff : 200;
        var tariffBytes = tariffMbps * 1_000_000 / 8;
        var ratio = tariffBytes > 0 ? current / tariffBytes : 0;
        var prefix = $"Сейчас {ValueFormat.Bytes((long)current)}/с ({current * 8 / 1_000_000:0.#} Мбит/с)   ·   Пик {ValueFormat.Bytes((long)_peakAggregateSpeed)}/с   ·   {ratio * 100:0}% тарифа";
        if (_speedSamples.Count < 12)
        {
            _diagnosticTitle.Text = "Анализируем соединение…";
            _diagnostics.Text = prefix;
            _diagnosticTitle.ForeColor = Accent;
            _diagnostics.ForeColor = TextMuted;
            return;
        }

        var recent = _speedSamples.TakeLast(16).ToArray();
        var average = recent.Average();
        var deviation = average > 0 ? Math.Sqrt(recent.Average(x => Math.Pow(x - average, 2))) / average : 1;
        var averageRatio = tariffBytes > 0 ? average / tariffBytes : 0;
        if (deviation < .18 && averageRatio < .70)
        {
            _diagnosticTitle.Text = "Вероятно ограничено сервером";
            _diagnostics.Text = prefix + $"   ·   Наблюдаемый предел ≈{ValueFormat.Bytes((long)average)}/с";
            _diagnosticTitle.ForeColor = Color.FromArgb(157, 93, 0); _diagnostics.ForeColor = TextMuted;
        }
        else if (averageRatio >= .85)
        {
            _diagnosticTitle.Text = "Канал используется почти полностью";
            _diagnostics.Text = prefix;
            _diagnosticTitle.ForeColor = Color.FromArgb(15, 123, 75); _diagnostics.ForeColor = TextMuted;
        }
        else
        {
            _diagnosticTitle.Text = "Скорость нестабильна";
            _diagnostics.Text = prefix + "   ·   Возможны Wi-Fi, диск или сервер";
            _diagnosticTitle.ForeColor = TextMain;
            _diagnostics.ForeColor = TextMuted;
        }
    }

    private void UpdateEmptyState()
    {
        var empty = _list.Controls.Count == 0;
        _list.Visible = !empty; _empty.Visible = empty;
        if (empty) _empty.BringToFront(); else _list.BringToFront();
        UpdateSummary();
    }

    private void ResizeCards()
    {
        var width = CardWidth();
        foreach (Control card in _list.Controls) card.Width = width;
    }

    private int CardWidth() => Math.Max(560, _listHost.ClientSize.Width - 32);

    private void PasteLinks()
    {
        try { if (Clipboard.ContainsText()) _url.Text = Clipboard.GetText().Trim(); }
        catch { }
        _url.Focus(); _url.SelectionStart = _url.TextLength;
    }

    private void TryFillClipboard()
    {
        try
        {
            if (!Clipboard.ContainsText()) return;
            var value = Clipboard.GetText().Trim();
            if (value.Split(['\r', '\n', ' '], StringSplitOptions.RemoveEmptyEntries).Any(x => Uri.TryCreate(x, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https"))) _url.Text = value;
        }
        catch { }
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Выберите папку для загрузок", SelectedPath = _folder.Text, UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) _folder.Text = dialog.SelectedPath;
    }

    private int GetConnectionCount() => _connections.SelectedIndex switch { 1 => 4, 2 => 8, 3 => 16, 4 => 1, _ => 8 };

    private static void ConfigureTextBox(TextBox box, string placeholder)
    {
        box.BorderStyle = BorderStyle.None; box.BackColor = Surface; box.ForeColor = TextMain; box.Font = UiFont(10.5f); box.PlaceholderText = placeholder;
    }

    internal static Font UiFont(float size, FontStyle style = FontStyle.Regular) => new("Segoe UI Variable Text", size, style);
    internal static Label MakeLabel(string text, float size, Color color, FontStyle style = FontStyle.Regular) => new() { Text = text, ForeColor = color, BackColor = Color.Transparent, Font = UiFont(size, style), AutoSize = false };
}

internal enum CardState { Connecting, Running, Pausing, Paused, Completed, Failed }

internal sealed class DownloadCard : UserControl
{
    private static readonly Color CardBg = Color.FromArgb(251, 251, 251);
    private static readonly Color Border = Color.FromArgb(229, 229, 229);
    private static readonly Color TextMain = Color.FromArgb(31, 31, 31);
    private static readonly Color Muted = Color.FromArgb(96, 96, 96);
    private static readonly Color Accent = Color.FromArgb(0, 103, 192);
    private static readonly Color Success = Color.FromArgb(15, 123, 75);
    private static readonly Color Error = Color.FromArgb(196, 43, 28);

    private readonly Uri _url;
    private readonly string _folder;
    private readonly int _connections;
    private readonly Label _name = MainForm.MakeLabel("Подключение…", 11.5f, TextMain, FontStyle.Bold);
    private readonly Label _status = MainForm.MakeLabel("Проверяем сервер", 9, Muted);
    private readonly Label _size = MainForm.MakeLabel("0 Б / —", 9, Muted);
    private readonly Label _speed = MainForm.MakeLabel("", 9, Muted);
    private readonly Label _eta = MainForm.MakeLabel("", 9, Muted);
    private readonly FluentProgress _progress = new();
    private readonly FluentButton _pause = new("Пауза", Color.FromArgb(238, 238, 238), TextMain);
    private readonly FluentButton _open = new("Папка", Color.FromArgb(238, 238, 238), TextMain);
    private readonly FluentButton _remove = new("×", Color.Transparent, Muted) { BorderColor = Border };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 350 };
    private DownloadEngine? _engine;
    private CancellationTokenSource? _cts;
    private string? _completedPath;
    private CardState _state = CardState.Paused;
    private Task? _currentRun;
    private bool _discardRequested;
    private bool _closing;
    private double _currentBytesPerSecond;
    private double _peakBytesPerSecond;

    public event EventHandler? RemoveRequested;
    public event EventHandler? StatusChanged;
    public Uri SourceUrl => _url;
    public bool IsActive => _state is CardState.Connecting or CardState.Running or CardState.Pausing;
    public bool IsCompleted => _state == CardState.Completed;
    public double CurrentBytesPerSecond => IsActive ? _currentBytesPerSecond : 0;
    public double PeakBytesPerSecond => _peakBytesPerSecond;

    public DownloadCard(Uri url, string folder, int connections)
    {
        _url = url; _folder = folder; _connections = connections;
        Height = 126; BackColor = Color.FromArgb(243, 243, 243); Margin = new Padding(0, 0, 0, 12); DoubleBuffered = true;
        var initialName = Uri.UnescapeDataString(Path.GetFileName(url.LocalPath));
        _name.Text = string.IsNullOrWhiteSpace(initialName) ? url.Host : initialName;
        _name.AutoEllipsis = true; _status.AutoEllipsis = true;
        Controls.AddRange([_name, _status, _size, _speed, _eta, _progress, _pause, _open, _remove]);
        _pause.Click += async (_, _) => await TogglePauseAsync();
        _open.Click += (_, _) => OpenFolder();
        _remove.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);
        _timer.Tick += (_, _) => RefreshProgress();
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    public Task StartAsync()
    {
        if (IsActive) return _currentRun ?? Task.CompletedTask;
        _currentRun = RunCoreAsync();
        return _currentRun;
    }

    private async Task RunCoreAsync()
    {
        _state = CardState.Connecting;
        _status.Text = "Подключение к серверу…"; _status.ForeColor = Muted;
        _pause.Text = "Пауза"; _pause.Enabled = true;
        _engine = new DownloadEngine(_url, _folder, _connections);
        _cts = new CancellationTokenSource();
        _timer.Start(); StatusChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            _state = CardState.Running;
            _completedPath = await _engine.RunAsync(_cts.Token);
            _state = CardState.Completed;
            _progress.Value = 1;
            _name.Text = Path.GetFileName(_completedPath);
            _status.Text = "Завершено"; _status.ForeColor = Success;
            _pause.Text = "Готово"; _pause.Enabled = false;
            _open.Enabled = true;
        }
        catch (OperationCanceledException)
        {
            if (_discardRequested || _closing) return;
            _state = CardState.Paused;
            _status.Text = "Приостановлено — прогресс сохранён"; _status.ForeColor = Muted;
            _pause.Text = "Продолжить"; _pause.Enabled = true;
        }
        catch (Exception ex)
        {
            _state = CardState.Failed;
            _status.Text = "Ошибка: " + ex.Message; _status.ForeColor = Error;
            _pause.Text = "Повторить"; _pause.Enabled = true;
        }
        finally
        {
            _timer.Stop();
            if (!_discardRequested && !_closing) { RefreshProgress(); StatusChanged?.Invoke(this, EventArgs.Empty); }
        }
    }

    public void PauseForExit()
    {
        _closing = true;
        _cts?.Cancel();
    }

    public async Task DiscardAsync()
    {
        _discardRequested = true;
        _cts?.Cancel();
        if (_currentRun is not null)
        {
            try { await _currentRun; }
            catch { }
        }
        await DownloadEngine.DeleteCachedDownloadAsync(_url, _folder);
    }

    internal void SetPreview(string name, string status, double progress, string size, string speed, string eta, bool completed, double bytesPerSecond = 0)
    {
        _name.Text = name; _status.Text = status; _progress.Value = progress; _size.Text = size; _speed.Text = speed; _eta.Text = eta;
        _currentBytesPerSecond = bytesPerSecond; _peakBytesPerSecond = bytesPerSecond;
        _state = completed ? CardState.Completed : CardState.Running;
        _status.ForeColor = completed ? Success : Accent;
        _pause.Text = completed ? "Готово" : "Пауза"; _pause.Enabled = !completed;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        var actionsWidth = 236;
        _name.SetBounds(20, 15, Math.Max(120, Width - actionsWidth - 34), 25);
        _status.SetBounds(20, 42, Math.Max(120, Width - actionsWidth - 34), 22);
        _remove.SetBounds(Width - 48, 16, 28, 28);
        _open.SetBounds(Width - 126, 16, 70, 30);
        _pause.SetBounds(Width - 224, 16, 90, 30);
        _progress.SetBounds(20, 72, Math.Max(100, Width - 40), 7);
        _size.SetBounds(20, 88, 230, 22);
        _speed.SetBounds(Math.Max(255, Width / 2 - 80), 88, 160, 22);
        _eta.SetBounds(Math.Max(430, Width - 250), 88, 230, 22); _eta.TextAlign = ContentAlignment.MiddleRight;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = DrawingUtil.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1, Height - 2), 10);
        using var fill = new SolidBrush(CardBg); using var pen = new Pen(Border);
        e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(pen, path);
    }

    private async Task TogglePauseAsync()
    {
        if (_state is CardState.Paused or CardState.Failed) { await StartAsync(); return; }
        if (!IsActive) return;
        _state = CardState.Pausing; _pause.Enabled = false; _status.Text = "Сохраняем прогресс…";
        _cts?.Cancel(); StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshProgress()
    {
        if (_engine is null) return;
        var p = _engine.GetProgress();
        _currentBytesPerSecond = p.BytesPerSecond;
        _peakBytesPerSecond = Math.Max(_peakBytesPerSecond, p.BytesPerSecond);
        if (!string.IsNullOrWhiteSpace(p.FileName)) _name.Text = p.FileName;
        _size.Text = $"{ValueFormat.Bytes(p.Downloaded)} / {(p.Total > 0 ? ValueFormat.Bytes(p.Total) : "неизвестно")}";
        _speed.Text = p.BytesPerSecond > 0 ? $"{ValueFormat.Bytes((long)p.BytesPerSecond)}/с" : "";
        if (p.Total > 0)
        {
            _progress.Value = Math.Clamp((double)p.Downloaded / p.Total, 0, 1);
            if (p.BytesPerSecond > 1 && p.Downloaded < p.Total)
                _eta.Text = "Осталось " + ValueFormat.Duration(TimeSpan.FromSeconds((p.Total - p.Downloaded) / p.BytesPerSecond));
        }
        if (_state == CardState.Running)
        {
            _status.Text = p.UsingRanges ? $"Загрузка · {Math.Max(1, p.ActiveConnections)} соединений" : "Загрузка · один поток";
            _status.ForeColor = Accent;
        }
    }

    private void OpenFolder()
    {
        var folder = _completedPath is not null ? Path.GetDirectoryName(_completedPath)! : _folder;
        if (Directory.Exists(folder)) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }
}

internal sealed class FluentButton : Control
{
    private bool _hover;
    private bool _pressed;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Color FillColor { get; set; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Color HoverColor { get; set; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Color TextColor { get; set; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Color BorderColor { get; set; } = Color.Transparent;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int Radius { get; set; } = 6;

    public FluentButton(string text, Color fill, Color textColor)
    {
        Text = text; FillColor = fill; HoverColor = fill == Color.Transparent ? Color.FromArgb(235, 235, 235) : ControlPaint.Dark(fill, .04f); TextColor = textColor;
        Font = MainForm.UiFont(9, FontStyle.Regular); Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.PushButton; TabStop = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode is Keys.Enter or Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);
        var fill = !Enabled ? Color.FromArgb(235, 235, 235) : _pressed ? ControlPaint.Dark(FillColor == Color.Transparent ? HoverColor : FillColor, .08f) : _hover ? HoverColor : FillColor;
        var text = Enabled ? TextColor : Color.FromArgb(150, 150, 150);
        using var path = DrawingUtil.RoundRect(new RectangleF(.5f, .5f, Width - 1, Height - 1), Radius);
        if (fill != Color.Transparent) { using var brush = new SolidBrush(fill); e.Graphics.FillPath(brush, path); }
        if (BorderColor != Color.Transparent) { using var pen = new Pen(BorderColor); e.Graphics.DrawPath(pen, path); }
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3));
    }
}

internal sealed class RoundedPanel : Panel
{
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Color FillColor { get; set; } = Color.White;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Color BorderColor { get; set; } = Color.Transparent;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int Radius { get; set; } = 8;
    public RoundedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
    }
    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        if (Width <= 0 || Height <= 0) return;
        using var path = DrawingUtil.RoundRect(new RectangleF(0, 0, Width, Height), Radius);
        var oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
        Invalidate();
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(FillColor);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (BorderColor == Color.Transparent) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = DrawingUtil.RoundRect(new RectangleF(.5f, .5f, Width - 1.5f, Height - 1.5f), Radius);
        using var pen = new Pen(BorderColor); e.Graphics.DrawPath(pen, path);
    }
}

internal sealed class FluentProgress : Control
{
    private double _value;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public double Value { get => _value; set { _value = Math.Clamp(value, 0, 1); Invalidate(); } }
    public FluentProgress() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Color.Transparent);
        using var track = DrawingUtil.RoundRect(ClientRectangle, Height / 2f); using var trackBrush = new SolidBrush(Color.FromArgb(225, 225, 225)); e.Graphics.FillPath(trackBrush, track);
        if (_value <= 0) return;
        var width = Math.Max(Height, (float)(Width * _value));
        using var fill = DrawingUtil.RoundRect(new RectangleF(0, 0, width, Height), Height / 2f); using var accent = new SolidBrush(Color.FromArgb(0, 103, 192)); e.Graphics.FillPath(accent, fill);
    }
}

internal sealed class FluentAppIcon : Control
{
    public FluentAppIcon() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var bgPath = DrawingUtil.RoundRect(new RectangleF(1, 1, Width - 2, Height - 2), 10); using var bg = new SolidBrush(Color.FromArgb(0, 103, 192)); e.Graphics.FillPath(bg, bgPath);
        using var pen = new Pen(Color.White, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawLine(pen, Width / 2f, 10, Width / 2f, 28);
        e.Graphics.DrawLine(pen, 15, 23, Width / 2f, 31);
        e.Graphics.DrawLine(pen, Width / 2f, 31, 29, 23);
        e.Graphics.DrawLine(pen, 14, 35, 30, 35);
    }
}

internal sealed class SmoothFlowPanel : FlowLayoutPanel
{
    public SmoothFlowPanel() { DoubleBuffered = true; }
}

internal static class DrawingUtil
{
    public static GraphicsPath RoundRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath(); var d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        path.AddArc(rect.X, rect.Y, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90); path.CloseFigure();
        return path;
    }
}

internal static class ValueFormat
{
    public static string Bytes(long value)
    {
        string[] units = ["Б", "КБ", "МБ", "ГБ", "ТБ"]; double size = Math.Max(0, value); var i = 0;
        while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
        return i == 0 ? $"{size:0} {units[i]}" : $"{size:0.##} {units[i]}";
    }
    public static string Duration(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours} ч {t.Minutes} мин" : t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes} мин {t.Seconds} сек" : $"{Math.Max(0, t.Seconds)} сек";
}

internal static class FluentWindow
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
    public static Icon CreateAppIcon()
    {
        using var embeddedIcon = typeof(FluentWindow).Assembly.GetManifestResourceStream("VelocityDownload.AppIcon.ico");
        if (embeddedIcon is not null)
        {
            using var icon = new Icon(embeddedIcon);
            return (Icon)icon.Clone();
        }

        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
            using var shape = DrawingUtil.RoundRect(new RectangleF(1, 1, 30, 30), 7); using var blue = new SolidBrush(Color.FromArgb(0, 103, 192)); g.FillPath(blue, shape);
            using var pen = new Pen(Color.White, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, 16, 7, 16, 21); g.DrawLine(pen, 10.5f, 16, 16, 22); g.DrawLine(pen, 16, 22, 21.5f, 16); g.DrawLine(pen, 9, 26, 23, 26);
        }
        var handle = bitmap.GetHicon();
        try { using var icon = Icon.FromHandle(handle); return (Icon)icon.Clone(); }
        finally { DestroyIcon(handle); }
    }
    public static void Apply(IntPtr handle)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        try
        {
            var rounded = 2; DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int));
            var mica = 2; DwmSetWindowAttribute(handle, 38, ref mica, sizeof(int));
            var light = 0; DwmSetWindowAttribute(handle, 20, ref light, sizeof(int));
        }
        catch { }
    }
}
