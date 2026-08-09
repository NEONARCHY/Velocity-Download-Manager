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

    private readonly TextBox _url = MakeTextBox("Вставьте прямую ссылку https://...");
    private readonly TextBox _folder = MakeTextBox("");
    private readonly ComboBox _connections = new();
    private readonly Button _start = MakeButton("СКАЧАТЬ", Accent, Color.White);
    private readonly Button _pause = MakeButton("ПАУЗА", Input, PrimaryText);
    private readonly Button _paste = MakeButton("ВСТАВИТЬ", Input, PrimaryText);
    private readonly Button _browse = MakeButton("ОБЗОР", Input, PrimaryText);
    private readonly Button _open = MakeButton("ОТКРЫТЬ ПАПКУ", Input, PrimaryText);
    private readonly Label _fileName = MakeLabel("Готов к загрузке", 16, FontStyle.Bold, PrimaryText);
    private readonly Label _status = MakeLabel("Вставьте прямую ссылку и нажмите «Скачать»", 10, FontStyle.Regular, SecondaryText);
    private readonly Label _percent = MakeLabel("0%", 20, FontStyle.Bold, PrimaryText);
    private readonly Label _downloaded = MakeLabel("0 Б / —", 10, FontStyle.Regular, SecondaryText);
    private readonly Label _speed = MakeLabel("0 Б/с", 13, FontStyle.Bold, Accent2);
    private readonly Label _eta = MakeLabel("Осталось: —", 10, FontStyle.Regular, SecondaryText);
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
        _connections.Items.AddRange(["Авто (8 потоков)", "4 потока", "8 потоков", "16 потоков", "1 поток"]);
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
        var bolt = MakeLabel("⚡", 28, FontStyle.Regular, Accent2); bolt.AutoSize = true; bolt.Location = new Point(0, 3);
        var title = MakeLabel("Velocity Download", 20, FontStyle.Bold, PrimaryText); title.AutoSize = true; title.Location = new Point(48, 3);
        var subtitle = MakeLabel("Быстрый загрузчик прямых ссылок", 9.5f, FontStyle.Regular, SecondaryText); subtitle.AutoSize = true; subtitle.Location = new Point(51, 38);
        titlePanel.Controls.AddRange([bolt, title, subtitle]);

        var urlRow = MakeInputRow(_url, _paste, "ПРЯМАЯ ССЫЛКА");
        var folderRow = MakeInputRow(_folder, _browse, "ПАПКА ДЛЯ СОХРАНЕНИЯ");

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
        combo.BackColor = Input; combo.ForeColor = PrimaryText; combo.FlatStyle = FlatStyle.Flat;
        combo.Font = new Font("Segoe UI", 9.5f); combo.Dock = DockStyle.Fill; combo.Margin = new Padding(0, 0, 8, 0);
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.ItemHeight = 28;
        combo.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            var selected = (e.State & DrawItemState.Selected) != 0;
            using var background = new SolidBrush(selected ? Accent : Input);
            using var foreground = new SolidBrush(PrimaryText);
            e.Graphics.FillRectangle(background, e.Bounds);
            e.Graphics.DrawString(combo.Items[e.Index]?.ToString(), combo.Font, foreground, e.Bounds.X + 8, e.Bounds.Y + 5);
        };
    }

    private async Task StartDownloadAsync()
    {
        if (_engine?.IsRunning == true) return;
        if (!Uri.TryCreate(_url.Text.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            ShowError("Нужна корректная прямая ссылка, начинающаяся с http:// или https://"); return;
        }

        var folder = _folder.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder)) { ShowError("Выберите папку для сохранения."); return; }
        try { Directory.CreateDirectory(folder); }
        catch (Exception ex) { ShowError("Не удалось открыть папку: " + ex.Message); return; }

        _cts = new CancellationTokenSource();
        _engine = new DownloadEngine(uri, folder, GetConnectionCount());
        SetDownloading(true);
        _fileName.Text = "Подключение…";
        _status.Text = "Проверяем сервер и параметры файла";
        _progress.Value = 0;
        _uiTimer.Start();

        try
        {
            var result = await _engine.RunAsync(_cts.Token);
            _lastCompletedFile = result;
            _fileName.Text = Path.GetFileName(result);
            _status.Text = "Готово — файл успешно загружен";
            _percent.Text = "100%"; _progress.Value = 1000;
            System.Media.SystemSounds.Asterisk.Play();
        }
        catch (OperationCanceledException)
        {
            if (!_closing) _status.Text = "На паузе — нажмите «Продолжить»";
        }
        catch (Exception ex)
        {
            _status.Text = "Ошибка загрузки";
            ShowError(ex.Message);
        }
        finally
        {
            _uiTimer.Stop(); RefreshProgress(); SetDownloading(false);
        }
    }

    private void PauseDownload()
    {
        if (_engine?.IsRunning != true) return;
        _status.Text = "Сохраняем прогресс…";
        _cts?.Cancel();
    }

    private void RefreshProgress()
    {
        if (_engine is null) return;
        var p = _engine.GetProgress();
        if (!string.IsNullOrWhiteSpace(p.FileName)) _fileName.Text = p.FileName;
        _downloaded.Text = $"{FormatBytes(p.Downloaded)} / {(p.Total > 0 ? FormatBytes(p.Total) : "неизвестно")}";
        _speed.Text = p.BytesPerSecond > 0 ? $"{FormatBytes((long)p.BytesPerSecond)}/с" : "0 Б/с";
        if (p.Total > 0)
        {
            var fraction = Math.Clamp((double)p.Downloaded / p.Total, 0, 1);
            _percent.Text = $"{fraction * 100:0.0}%";
            _progress.Value = (int)(fraction * 1000);
            var remaining = p.BytesPerSecond > 1 ? TimeSpan.FromSeconds((p.Total - p.Downloaded) / p.BytesPerSecond) : TimeSpan.MaxValue;
            _eta.Text = remaining != TimeSpan.MaxValue ? "Осталось: " + FormatDuration(remaining) : "Осталось: —";
        }
        else { _percent.Text = "—"; _eta.Text = "Осталось: —"; }
        if (_engine.IsRunning) _status.Text = p.UsingRanges ? $"Загрузка в {p.ActiveConnections} потоков" : "Загрузка одним потоком (ограничение сервера)";
    }

    private void SetDownloading(bool active)
    {
        _start.Enabled = !active; _url.Enabled = !active; _folder.Enabled = !active; _browse.Enabled = !active; _paste.Enabled = !active; _connections.Enabled = !active;
        _pause.Enabled = active; _pause.Text = "ПАУЗА"; _start.Text = active ? "ЗАГРУЗКА…" : ((_engine is not null && _engine.GetProgress().Downloaded > 0 && _lastCompletedFile is null) ? "ПРОДОЛЖИТЬ" : "СКАЧАТЬ");
    }

    private int GetConnectionCount() => _connections.SelectedIndex switch { 1 => 4, 2 => 8, 3 => 16, 4 => 1, _ => 8 };

    private void PasteUrl()
    {
        try { if (Clipboard.ContainsText()) _url.Text = Clipboard.GetText().Trim(); }
        catch { }
    }

    private void TryFillFromClipboard()
    {
        if (!string.IsNullOrWhiteSpace(_url.Text)) return;
        try
        {
            var text = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : "";
            if (Uri.TryCreate(text, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https")) _url.Text = text;
        }
        catch { }
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Куда сохранить файл?", SelectedPath = _folder.Text, UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) _folder.Text = dialog.SelectedPath;
    }

    private void OpenFolder()
    {
        var folder = _lastCompletedFile is not null ? Path.GetDirectoryName(_lastCompletedFile)! : _folder.Text;
        if (Directory.Exists(folder)) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _closing = true; _cts?.Cancel();
    }

    private void ShowError(string text) => MessageBox.Show(this, text, "Velocity Download", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private static string FormatBytes(long value)
    {
        string[] units = ["Б", "КБ", "МБ", "ГБ", "ТБ"];
        double size = value; var i = 0;
        while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
        return i == 0 ? $"{size:0} {units[i]}" : $"{size:0.##} {units[i]}";
    }

    private static string FormatDuration(TimeSpan t)
    {
        if (t.TotalDays >= 1) return $"{(int)t.TotalDays} д {t.Hours} ч";
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours} ч {t.Minutes} мин";
        if (t.TotalMinutes >= 1) return $"{(int)t.TotalMinutes} мин {t.Seconds} сек";
        return $"{Math.Max(0, t.Seconds)} сек";
    }
}

internal sealed class DownloadEngine
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly object DestinationLock = new();
    private static readonly HashSet<string> ReservedDestinations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Uri _source;
    private readonly string _folder;
    private readonly int _requestedConnections;
    private readonly object _sync = new();
    private DownloadState? _state;
    private long _sessionStartBytes;
    private Stopwatch _sessionWatch = new();
    private DateTime _lastStateSave = DateTime.MinValue;
    private int _saving;
    private Task? _pendingStateSave;
    private readonly SemaphoreSlim _stateSaveGate = new(1, 1);

    public bool IsRunning { get; private set; }

    public DownloadEngine(Uri source, string folder, int requestedConnections)
    {
        _source = source; _folder = folder; _requestedConnections = requestedConnections;
    }

    public async Task<string> RunAsync(CancellationToken token)
    {
        IsRunning = true;
        try
        {
            var remote = await ProbeAsync(token);
            var safeName = MakeSafeFileName(remote.FileName);
            var cache = GetCachePaths(_source, _folder);
            Directory.CreateDirectory(cache.Directory);
            var statePath = cache.StatePath;
            var partialPath = cache.PartialPath;
            var finalPath = GetCachedFinalPath(statePath, partialPath, remote) ?? GetAvailableDestinationPath(_folder, safeName);

            _state = await LoadOrCreateStateAsync(statePath, finalPath, partialPath, remote, token);
            _sessionStartBytes = _state.Segments.Sum(x => x.Downloaded);
            _sessionWatch.Restart();

            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
            if (remote.SupportsRanges && remote.Length > 0 && _requestedConnections > 1)
                await DownloadRangedAsync(statePath, token);
            else
                await DownloadSingleAsync(statePath, token);

            await SaveStateAsync(statePath, CancellationToken.None);
            await WaitForPendingStateSaveAsync();
            if (_state.TotalLength > 0 && _state.Segments.Sum(x => x.Downloaded) < _state.TotalLength)
                throw new IOException("Загрузка завершилась раньше ожидаемого размера. Попробуйте продолжить.");

            finalPath = await FinalizeDownloadAsync(partialPath, _state.FinalPath);
            File.Delete(statePath);
            File.Delete(statePath + ".tmp");
            return finalPath;
        }
        catch (OperationCanceledException)
        {
            if (_state is not null) await SaveStateAsync(_state.StatePath, CancellationToken.None);
            await WaitForPendingStateSaveAsync();
            throw;
        }
        finally
        {
            await WaitForPendingStateSaveAsync();
            IsRunning = false; _sessionWatch.Stop();
        }
    }

    public DownloadProgress GetProgress()
    {
        lock (_sync)
        {
            if (_state is null) return new DownloadProgress("", 0, 0, 0, false, 0);
            var done = _state.Segments.Sum(x => x.Downloaded);
            var elapsed = _sessionWatch.Elapsed.TotalSeconds;
            var speed = elapsed > .2 ? Math.Max(0, done - _sessionStartBytes) / elapsed : 0;
            return new DownloadProgress(Path.GetFileName(_state.FinalPath), done, _state.TotalLength, speed, _state.UsingRanges, _state.Segments.Count(x => !x.Complete));
        }
    }

    private async Task DownloadRangedAsync(string statePath, CancellationToken token)
    {
        if (_state is null) return;
        _state.UsingRanges = true;
        await using (var init = new FileStream(_state.PartialPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite, 1, FileOptions.Asynchronous))
            init.SetLength(_state.TotalLength);

        using var handle = File.OpenHandle(_state.PartialPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, FileOptions.Asynchronous | FileOptions.RandomAccess);
        var tasks = _state.Segments.Where(s => !s.Complete).Select(segment => DownloadSegmentWithRetriesAsync(segment, handle, statePath, token));
        await Task.WhenAll(tasks);
    }

    private async Task DownloadSegmentWithRetriesAsync(SegmentState segment, Microsoft.Win32.SafeHandles.SafeFileHandle handle, string statePath, CancellationToken token)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { await DownloadSegmentAsync(segment, handle, statePath, token); return; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                last = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt * attempt), token);
            }
        }
        throw new IOException($"Не удалось загрузить часть файла после нескольких попыток: {last?.Message}", last);
    }

    private async Task DownloadSegmentAsync(SegmentState segment, Microsoft.Win32.SafeHandles.SafeFileHandle handle, string statePath, CancellationToken token)
    {
        var from = segment.Start + segment.Downloaded;
        if (from > segment.End) { segment.Complete = true; return; }
        using var request = new HttpRequestMessage(HttpMethod.Get, _source);
        request.Headers.Range = new RangeHeaderValue(from, segment.End);
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode != HttpStatusCode.PartialContent)
            throw new IOException("Сервер перестал поддерживать многопоточную загрузку.");
        await using var input = await response.Content.ReadAsStreamAsync(token);
        var buffer = new byte[128 * 1024];
        long offset = from;
        while (true)
        {
            var read = await input.ReadAsync(buffer, token);
            if (read == 0) break;
            var allowed = (int)Math.Min(read, segment.End - offset + 1);
            await RandomAccess.WriteAsync(handle, buffer.AsMemory(0, allowed), offset, token);
            offset += allowed;
            lock (_sync) segment.Downloaded += allowed;
            MaybeSaveState(statePath);
            if (offset > segment.End) break;
        }
        if (offset <= segment.End) throw new EndOfStreamException("Сервер прервал соединение.");
        segment.Complete = true;
    }

    private async Task DownloadSingleAsync(string statePath, CancellationToken token)
    {
        if (_state is null) return;
        _state.UsingRanges = false;
        var segment = _state.Segments[0];
        var existing = File.Exists(_state.PartialPath) ? new FileInfo(_state.PartialPath).Length : 0;
        segment.Downloaded = _state.TotalLength > 0 ? Math.Min(existing, _state.TotalLength) : existing;
        var requestedFrom = segment.Downloaded;

        using var request = new HttpRequestMessage(HttpMethod.Get, _source);
        if (requestedFrom > 0) request.Headers.Range = new RangeHeaderValue(requestedFrom, null);
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var canAppend = requestedFrom > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (!canAppend) { requestedFrom = 0; segment.Downloaded = 0; }

        await using var output = new FileStream(_state.PartialPath, canAppend ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read, 256 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var input = await response.Content.ReadAsStreamAsync(token);
        var buffer = new byte[256 * 1024];
        while (true)
        {
            var read = await input.ReadAsync(buffer, token);
            if (read == 0) break;
            await output.WriteAsync(buffer.AsMemory(0, read), token);
            lock (_sync) segment.Downloaded += read;
            MaybeSaveState(statePath);
        }
        segment.Complete = _state.TotalLength <= 0 || segment.Downloaded >= _state.TotalLength;
    }

    private async Task<RemoteInfo> ProbeAsync(CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _source);
        request.Headers.Range = new RangeHeaderValue(0, 0);
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var supportsRanges = response.StatusCode == HttpStatusCode.PartialContent && response.Content.Headers.ContentRange?.Length is > 0;
        var length = response.Content.Headers.ContentRange?.Length ?? response.Content.Headers.ContentLength ?? -1;
        var name = GetFileName(response, _source);
        return new RemoteInfo(name, length, supportsRanges);
    }

    private async Task<DownloadState> LoadOrCreateStateAsync(string statePath, string finalPath, string partialPath, RemoteInfo remote, CancellationToken token)
    {
        if (File.Exists(statePath) && File.Exists(partialPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(statePath, token);
                var loaded = JsonSerializer.Deserialize<DownloadState>(json);
                if (loaded is not null && loaded.Url == _source.ToString() && loaded.TotalLength == remote.Length)
                {
                    loaded.FinalPath = finalPath;
                    loaded.PartialPath = partialPath;
                    loaded.StatePath = statePath;
                    foreach (var s in loaded.Segments) s.Downloaded = Math.Clamp(s.Downloaded, 0, s.End - s.Start + 1);
                    return loaded;
                }
            }
            catch { }
        }

        var state = new DownloadState { Url = _source.ToString(), FinalPath = finalPath, PartialPath = partialPath, StatePath = statePath, TotalLength = remote.Length, UsingRanges = remote.SupportsRanges && _requestedConnections > 1 };
        if (state.UsingRanges && remote.Length > 0)
        {
            var count = (int)Math.Min(_requestedConnections, Math.Max(1, remote.Length / (1024 * 1024)));
            var chunk = remote.Length / count;
            for (var i = 0; i < count; i++)
            {
                var start = i * chunk;
                var end = i == count - 1 ? remote.Length - 1 : ((i + 1) * chunk) - 1;
                state.Segments.Add(new SegmentState { Start = start, End = end });
            }
        }
        else state.Segments.Add(new SegmentState { Start = 0, End = remote.Length > 0 ? remote.Length - 1 : long.MaxValue - 1 });
        await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state), token);
        return state;
    }

    private void MaybeSaveState(string statePath)
    {
        if ((DateTime.UtcNow - _lastStateSave).TotalSeconds < 1 || Interlocked.Exchange(ref _saving, 1) == 1) return;
        _lastStateSave = DateTime.UtcNow;
        _pendingStateSave = Task.Run(async () =>
        {
            try { await SaveStateAsync(statePath, CancellationToken.None); }
            catch { }
            finally { Interlocked.Exchange(ref _saving, 0); }
        });
    }

    private async Task SaveStateAsync(string statePath, CancellationToken token)
    {
        if (_state is null) return;
        await _stateSaveGate.WaitAsync(token);
        try
        {
            string json;
            lock (_sync) json = JsonSerializer.Serialize(_state);
            var temp = statePath + ".tmp";
            await File.WriteAllTextAsync(temp, json, token);
            File.Move(temp, statePath, true);
        }
        finally { _stateSaveGate.Release(); }
    }

    private async Task WaitForPendingStateSaveAsync()
    {
        var pending = _pendingStateSave;
        if (pending is null) return;
        try { await pending; }
        catch { }
    }

    internal static async Task<bool> DeleteCachedDownloadAsync(Uri source, string folder)
    {
        var cache = GetCachePaths(source, folder);
        var files = new[] { cache.StatePath + ".tmp", cache.StatePath, cache.PartialPath };
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var failed = false;
            foreach (var file in files)
            {
                try { if (File.Exists(file)) File.Delete(file); }
                catch (IOException) { failed = true; }
                catch (UnauthorizedAccessException) { failed = true; }
            }
            if (!failed && files.All(file => !File.Exists(file))) return true;
            await Task.Delay(100 + attempt * 100);
        }
        return files.All(file => !File.Exists(file));
    }

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            MaxConnectionsPerServer = 32,
            ConnectTimeout = TimeSpan.FromSeconds(20),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            UseProxy = true
        };
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) VelocityDownload/2.4");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("identity");
        return client;
    }

    private static string GetFileName(HttpResponseMessage response, Uri original)
    {
        var cd = response.Content.Headers.ContentDisposition;
        var name = cd?.FileNameStar ?? cd?.FileName;
        if (!string.IsNullOrWhiteSpace(name)) return name.Trim('"');
        var finalUri = response.RequestMessage?.RequestUri ?? original;
        var pathName = Uri.UnescapeDataString(Path.GetFileName(finalUri.LocalPath));
        return string.IsNullOrWhiteSpace(pathName) ? "download.bin" : pathName;
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(name) ? "download.bin" : name;
    }

    private string? GetCachedFinalPath(string statePath, string partialPath, RemoteInfo remote)
    {
        if (!File.Exists(statePath) || !File.Exists(partialPath)) return null;
        try
        {
            var state = JsonSerializer.Deserialize<DownloadState>(File.ReadAllText(statePath));
            if (state is null || state.Url != _source.ToString() || state.TotalLength != remote.Length || string.IsNullOrWhiteSpace(state.FinalPath)) return null;
            var requestedFolder = Path.GetFullPath(_folder).TrimEnd(Path.DirectorySeparatorChar);
            var stateFolder = Path.GetFullPath(Path.GetDirectoryName(state.FinalPath)!).TrimEnd(Path.DirectorySeparatorChar);
            if (!string.Equals(requestedFolder, stateFolder, StringComparison.OrdinalIgnoreCase)) return null;
            lock (DestinationLock) ReservedDestinations.Add(state.FinalPath);
            return state.FinalPath;
        }
        catch { return null; }
    }

    private static string GetAvailableDestinationPath(string folder, string name)
    {
        lock (DestinationLock)
        {
            var stem = Path.GetFileNameWithoutExtension(name); var ext = Path.GetExtension(name);
            for (var i = 0; i < 10_000; i++)
            {
                var candidate = Path.Combine(folder, i == 0 ? name : $"{stem} ({i}){ext}");
                if (File.Exists(candidate) || File.Exists(candidate + ".velocity.part") || File.Exists(candidate + ".velocity.json") || ReservedDestinations.Contains(candidate)) continue;
                ReservedDestinations.Add(candidate);
                return candidate;
            }
            var fallback = Path.Combine(folder, $"{stem}-{Guid.NewGuid():N}{ext}");
            ReservedDestinations.Add(fallback);
            return fallback;
        }
    }

    private static CachePaths GetCachePaths(Uri source, string folder)
    {
        var normalizedFolder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(source + "\n" + normalizedFolder));
        var key = Convert.ToHexString(keyBytes).ToLowerInvariant();
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VelocityDownload", "Cache");
        return new CachePaths(directory, Path.Combine(directory, key + ".json"), Path.Combine(directory, key + ".part"));
    }

    private static async Task<string> FinalizeDownloadAsync(string partialPath, string requestedPath)
    {
        var destination = requestedPath;
        if (File.Exists(destination)) destination = GetAvailableDestinationPath(Path.GetDirectoryName(destination)!, Path.GetFileName(destination));
        var sourceRoot = Path.GetPathRoot(Path.GetFullPath(partialPath));
        var destinationRoot = Path.GetPathRoot(Path.GetFullPath(destination));
        if (string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(partialPath, destination, false);
            return destination;
        }

        var staging = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.velocity-finalizing");
        try
        {
            await using (var input = new FileStream(partialPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                try { File.SetAttributes(staging, FileAttributes.Hidden | FileAttributes.Temporary); } catch { }
                await input.CopyToAsync(output);
                await output.FlushAsync();
            }
            try { File.SetAttributes(staging, FileAttributes.Normal); } catch { }
            File.Move(staging, destination, false);
            File.Delete(partialPath);
            return destination;
        }
        catch
        {
            if (File.Exists(staging)) File.Delete(staging);
            throw;
        }
    }
}

internal sealed class DownloadState
{
    public string Url { get; set; } = "";
    public string FinalPath { get; set; } = "";
    public string PartialPath { get; set; } = "";
    public string StatePath { get; set; } = "";
    public long TotalLength { get; set; }
    public bool UsingRanges { get; set; }
    public List<SegmentState> Segments { get; set; } = [];
}

internal sealed class SegmentState
{
    public long Start { get; set; }
    public long End { get; set; }
    public long Downloaded { get; set; }
    public bool Complete { get; set; }
}

internal readonly record struct RemoteInfo(string FileName, long Length, bool SupportsRanges);
internal readonly record struct CachePaths(string Directory, string StatePath, string PartialPath);
internal readonly record struct DownloadProgress(string FileName, long Downloaded, long Total, double BytesPerSecond, bool UsingRanges, int ActiveConnections);

internal sealed class SmoothProgressBar : Control
{
    private int _value;
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value { get => _value; set { _value = Math.Clamp(value, 0, 1000); Invalidate(); } }
    public SmoothProgressBar() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (_value <= 0) return;
        var width = (int)(ClientSize.Width * (_value / 1000d));
        using var brush = new SolidBrush(ForeColor);
        e.Graphics.FillRectangle(brush, 0, 0, width, ClientSize.Height);
    }
}

internal static class ControlExtensions
{
    public static T With<T>(this T value, Action<T> action) { action(value); return value; }
}
