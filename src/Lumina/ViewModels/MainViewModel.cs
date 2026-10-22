using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Core;
using Lumina.Models;
using Lumina.Services;
using Lumina.Views;

namespace Lumina.ViewModels;

public partial class StarVm : ObservableObject
{
    public StarVm(int value, Action<int> rate)
    {
        Value = value;
        Rate = new RelayCommand(() => rate(value));
    }

    public int Value { get; }
    public IRelayCommand Rate { get; }

    [ObservableProperty] private bool filled;

    public string Glyph => Filled ? "★" : "☆";

    partial void OnFilledChanged(bool value) => OnPropertyChanged(nameof(Glyph));
}

public sealed class RecentEntry
{
    public RecentEntry(string path, string label, IRelayCommand open)
    {
        Path = path;
        Label = label;
        Open = open;
    }

    public string Path { get; }
    public string Label { get; }
    public IRelayCommand Open { get; }
}

public partial class MainViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly ConcurrentDictionary<string, FullPack> _cache = new();
    private readonly DispatcherTimer _clock;
    private readonly Stopwatch _slideWatch = new();
    private readonly List<GalleryItem> _all = [];
    private DispatcherTimer? _anim;
    private List<Bitmap> _frames = [];
    private List<int> _durations = [];
    private Bitmap? _still;
    private int _frame;
    private int _session;
    private int _loadToken;
    private string? _folder;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _watchCts;
    private CancellationTokenSource? _saveCts;
    private bool _ready;
    private IStorageProvider? _storage;

    public MainViewModel(AppSettings settings)
    {
        _settings = settings;
        ShowSidebar = settings.ShowSidebar;
        ShowFilmstrip = settings.ShowFilmstrip;
        ShowInfo = settings.ShowInfo;
        KeepZoom = settings.KeepZoom;
        Recursive = settings.Recursive;
        SortIndex = Math.Clamp(settings.SortIndex, 0, 3);
        SortDescending = settings.SortDescending;
        SlideshowSeconds = Math.Clamp(settings.SlideshowSeconds, 1, 30);
        SlideshowLoop = settings.SlideshowLoop;
        StageIndex = Math.Clamp(settings.StageIndex, 0, 3);
        Stage = (StageBackdrop)StageIndex;
        Stars = Enumerable.Range(1, 5).Select(i => new StarVm(i, Rate)).ToArray();
        RebuildSavedLists();

        _clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _clock.Tick += (_, _) => OnClock();
    }

    public AppSettings Settings => _settings;

    public ObservableCollection<GalleryItem> Visible { get; } = [];
    public ObservableCollection<RecentEntry> Recents { get; } = [];
    public ObservableCollection<RecentEntry> Favorites { get; } = [];

    public IReadOnlyList<StarVm> Stars { get; }

    [ObservableProperty] private GalleryItem? selected;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string busyText = "Einen Moment…";
    [ObservableProperty] private bool isDragOver;
    [ObservableProperty] private bool showSidebar;
    [ObservableProperty] private bool showFilmstrip;
    [ObservableProperty] private bool showInfo;
    [ObservableProperty] private bool showHelp;
    [ObservableProperty] private bool isFullscreen;
    [ObservableProperty] private bool isSlideshow;
    [ObservableProperty] private double slideshowProgress;
    [ObservableProperty] private double slideshowSeconds = 4;
    [ObservableProperty] private bool slideshowLoop = true;
    [ObservableProperty] private bool keepZoom;
    [ObservableProperty] private bool recursive;
    [ObservableProperty] private bool sortDescending;
    [ObservableProperty] private int sortIndex;
    [ObservableProperty] private int stageIndex;
    [ObservableProperty] private string folderTitle = "Lumina";
    [ObservableProperty] private string positionText = "Keine Bilder";
    [ObservableProperty] private string statusMessage = "";
    [ObservableProperty] private bool hasStatus;
    [ObservableProperty] private bool isFavorite;
    [ObservableProperty] private int rating;
    [ObservableProperty] private IReadOnlyList<MetaRow> meta = [];
    [ObservableProperty] private int[] histogram = [];
    [ObservableProperty] private IReadOnlyList<Swatch> palette = [];
    [ObservableProperty] private string detailTitle = "";
    [ObservableProperty] private string detailPath = "";
    [ObservableProperty] private string subsampledNote = "";
    [ObservableProperty] private bool hasSubsampled;
    [ObservableProperty] private Bitmap? currentImage;
    [ObservableProperty] private int rotation;
    [ObservableProperty] private bool flipH;
    [ObservableProperty] private bool flipV;
    [ObservableProperty] private StageBackdrop stage;
    [ObservableProperty] private bool hasImages;
    [ObservableProperty] private bool hasSelection;
    [ObservableProperty] private bool showWelcome = true;
    [ObservableProperty] private bool hasRecents;
    [ObservableProperty] private bool hasFavorites;
    [ObservableProperty] private bool hasPalette;
    [ObservableProperty] private bool hasHistogram;

    public event Action<bool>? FullscreenRequested;
    public event Action? FitRequested;
    public event Action<GalleryItem?>? ScrollRequested;

    public void Attach(IStorageProvider storage) => _storage = storage;

    public async Task InitializeAsync(string[] args)
    {
        _ready = true;
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            var path = Path.GetFullPath(args[0]);
            if (Directory.Exists(path))
            {
                await LoadFolderAsync(path);
                return;
            }

            if (File.Exists(path))
            {
                await LoadFilesAsync([path], scanSiblings: true);
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
            await LoadFolderAsync(_settings.LastFolder);
        else
            UpdateChrome();
    }

    public void Shutdown()
    {
        _ready = false;
        StopWatch();
        StopAnim(dispose: true);
        _clock.Stop();
        _still?.Dispose();
        _still = null;
        SaveSettings();
    }

    public void SaveSettings() => SettingsService.Save(_settings);

    partial void OnSelectedChanged(GalleryItem? value)
    {
        UpdateChrome();
        BindDetails(value);
        ScrollRequested?.Invoke(value);
        _ = LoadSelectedAsync(value);
    }

    partial void OnSearchTextChanged(string value) => RebuildVisible(keepSelection: true);

    partial void OnSortIndexChanged(int value)
    {
        _settings.SortIndex = value;
        Resort();
        SaveSoon();
    }

    public bool ShowWindowButtons => !IsFullscreen;
    public bool ShowPlayIcon => !IsSlideshow;
    public string SortLabel => SortDescending ? "Z–A" : "A–Z";

    partial void OnIsFullscreenChanged(bool value) => OnPropertyChanged(nameof(ShowWindowButtons));

    partial void OnIsSlideshowChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowPlayIcon));
        if (value)
        {
            _slideWatch.Restart();
            SlideshowProgress = 0;
            _clock.Start();
        }
        else
        {
            SlideshowProgress = 0;
            _clock.Stop();
        }
    }

    partial void OnSortDescendingChanged(bool value)
    {
        OnPropertyChanged(nameof(SortLabel));
        _settings.SortDescending = value;
        Resort();
        SaveSoon();
    }

    partial void OnRecursiveChanged(bool value)
    {
        _settings.Recursive = value;
        SaveSoon();
        if (_ready && _folder is not null)
            _ = LoadFolderAsync(_folder);
    }

    partial void OnStageIndexChanged(int value)
    {
        Stage = (StageBackdrop)Math.Clamp(value, 0, 3);
        _settings.StageIndex = StageIndex;
        SaveSoon();
    }

    partial void OnShowSidebarChanged(bool value)
    {
        _settings.ShowSidebar = value;
        SaveSoon();
    }

    partial void OnShowFilmstripChanged(bool value)
    {
        _settings.ShowFilmstrip = value;
        SaveSoon();
    }

    partial void OnShowInfoChanged(bool value)
    {
        _settings.ShowInfo = value;
        SaveSoon();
    }

    partial void OnKeepZoomChanged(bool value)
    {
        _settings.KeepZoom = value;
        SaveSoon();
    }

    partial void OnSlideshowSecondsChanged(double value)
    {
        _settings.SlideshowSeconds = value;
        SaveSoon();
    }

    partial void OnSlideshowLoopChanged(bool value)
    {
        _settings.SlideshowLoop = value;
        SaveSoon();
    }

    [RelayCommand]
    private async Task OpenFolder()
    {
        if (_storage is null) return;
        var result = await _storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Ordner öffnen",
            AllowMultiple = false
        });
        var path = result.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            await LoadFolderAsync(path);
    }

    [RelayCommand]
    private async Task OpenFiles()
    {
        if (_storage is null) return;
        var result = await _storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Bilder öffnen",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Bilder")
                {
                    Patterns = ["*.jpg", "*.jpeg", "*.png", "*.gif", "*.bmp", "*.webp", "*.tif", "*.tiff", "*.ico", "*.avif"]
                }
            ]
        });
        var paths = result
            .Select(item => item.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToList();
        if (paths.Count == 0) return;
        await LoadFilesAsync(paths, scanSiblings: paths.Count == 1);
    }

    [RelayCommand] private void ToggleSidebar() => ShowSidebar = !ShowSidebar;
    [RelayCommand] private void ToggleFilmstrip() => ShowFilmstrip = !ShowFilmstrip;
    [RelayCommand] private void ToggleInfo() => ShowInfo = !ShowInfo;
    [RelayCommand] private void ToggleHelp() => ShowHelp = !ShowHelp;

    [RelayCommand]
    private void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
        FullscreenRequested?.Invoke(IsFullscreen);
    }

    public void SetFullscreen(bool value)
    {
        if (IsFullscreen == value) return;
        IsFullscreen = value;
    }

    [RelayCommand]
    private void ToggleSlideshow()
    {
        if (Visible.Count == 0) return;
        IsSlideshow = !IsSlideshow;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var app = Avalonia.Application.Current;
        if (app is null) return;
        var light = app.RequestedThemeVariant == Avalonia.Styling.ThemeVariant.Light;
        app.RequestedThemeVariant = light
            ? Avalonia.Styling.ThemeVariant.Dark
            : Avalonia.Styling.ThemeVariant.Light;
        _settings.Theme = light ? "Dark" : "Light";
        SaveSoon();
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (Selected is null) return;
        var path = Selected.File.Path;
        if (_settings.Favorites.Contains(path, StringComparer.OrdinalIgnoreCase))
            _settings.Favorites.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        else
            _settings.Favorites.Insert(0, path);
        IsFavorite = _settings.Favorites.Contains(path, StringComparer.OrdinalIgnoreCase);
        RebuildSavedLists();
        SaveSoon();
        Toast(IsFavorite ? "Zu Favoriten gelegt" : "Favorit entfernt");
    }

    [RelayCommand] private void ToggleSortDirection() => SortDescending = !SortDescending;

    [RelayCommand] private void Next() => Advance(slideshow: false, forward: true);
    [RelayCommand] private void Prev() => Advance(slideshow: false, forward: false);
    [RelayCommand] private void First() { if (Visible.Count > 0) Selected = Visible[0]; }
    [RelayCommand] private void Last() { if (Visible.Count > 0) Selected = Visible[^1]; }

    [RelayCommand]
    private void RotateCw() => Rotation = (Rotation + 90) % 360;

    [RelayCommand]
    private void RotateCcw() => Rotation = (Rotation + 270) % 360;

    [RelayCommand] private void FlipHorizontal() => FlipH = !FlipH;
    [RelayCommand] private void FlipVertical() => FlipV = !FlipV;

    [RelayCommand]
    private void RemoveFromSession()
    {
        if (Selected is null) return;
        var item = Selected;
        var index = Visible.IndexOf(item);
        _all.Remove(item);
        Visible.Remove(item);
        item.Thumbnail?.Dispose();
        _cache.TryRemove(item.File.Path, out _);
        Selected = Visible.Count == 0 ? null : Visible[Math.Clamp(index, 0, Visible.Count - 1)];
        UpdateChrome();
        Toast("Aus der Sitzung genommen — Datei bleibt erhalten");
    }

    [RelayCommand]
    private async Task CopyPath()
    {
        if (Selected is null) return;
        var clipboard = ResolveClipboard();
        if (clipboard is null) return;
        await clipboard.SetTextAsync(Selected.File.Path);
        Toast("Pfad kopiert");
    }

    [RelayCommand]
    private void Reveal()
    {
        if (Selected is null) return;
        try
        {
            var path = Selected.File.Path;
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo("open", $"-R \"{path}\"") { UseShellExecute = true });
            }
            else
            {
                var folder = Path.GetDirectoryName(path) ?? path;
                Process.Start(new ProcessStartInfo("xdg-open", folder) { UseShellExecute = true });
            }
        }
        catch
        {
            Toast("Ordner konnte nicht geöffnet werden");
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        if (_folder is null)
        {
            Toast("Kein Ordner zum Aktualisieren");
            return;
        }

        var keep = Selected?.File.Path;
        await LoadFolderAsync(_folder);
        if (keep is not null)
        {
            var match = _all.FirstOrDefault(i => string.Equals(i.File.Path, keep, StringComparison.OrdinalIgnoreCase));
            if (match is not null) Selected = match;
        }
    }

    [RelayCommand]
    private void Shuffle()
    {
        if (_all.Count < 2) return;
        var rng = new Random();
        var mixed = _all.OrderBy(_ => rng.Next()).ToList();
        _all.Clear();
        _all.AddRange(mixed);
        RebuildVisible(keepSelection: true);
        Toast("Reihenfolge gemischt");
    }

    public async Task OpenDroppedAsync(IReadOnlyList<string> paths)
    {
        var folders = paths.Where(Directory.Exists).ToList();
        var files = paths.Where(File.Exists).ToList();
        if (folders.Count > 0)
        {
            await LoadFolderAsync(folders[0]);
            return;
        }

        if (files.Count > 0)
            await LoadFilesAsync(files, scanSiblings: files.Count == 1);
    }

    private async Task LoadFolderAsync(string folder)
    {
        IsBusy = true;
        BusyText = "Ordner wird gelesen…";
        UpdateChrome();
        try
        {
            var recursive = Recursive;
            var sort = CurrentSort;
            var descending = SortDescending;
            var files = await Task.Run(() => CatalogScanner.Sort(CatalogScanner.Scan(folder, recursive), sort, descending));
            _folder = folder;
            FolderTitle = PrettyName(folder);
            ReplaceAll(files);
            RememberRecent(folder);
            _settings.LastFolder = folder;
            Watch(folder);
            SaveSoon();
            if (files.Count == 0) Toast("Keine Bilder in diesem Ordner");
        }
        finally
        {
            IsBusy = false;
            UpdateChrome();
        }
    }

    private async Task LoadFilesAsync(IReadOnlyList<string> paths, bool scanSiblings)
    {
        IsBusy = true;
        BusyText = "Bilder werden geöffnet…";
        UpdateChrome();
        try
        {
            if (scanSiblings && paths.Count == 1)
            {
                var full = Path.GetFullPath(paths[0]);
                var folder = Path.GetDirectoryName(full);
                if (folder is null) return;
                var sort = CurrentSort;
                var descending = SortDescending;
                var files = await Task.Run(() => CatalogScanner.Sort(CatalogScanner.Scan(folder, false), sort, descending));
                _folder = folder;
                FolderTitle = PrettyName(folder);
                ReplaceAll(files);
                var match = _all.FirstOrDefault(i => string.Equals(i.File.Path, full, StringComparison.OrdinalIgnoreCase));
                if (match is not null) Selected = match;
                RememberRecent(folder);
                _settings.LastFolder = folder;
                Watch(folder);
                SaveSoon();
                return;
            }

            var sortMany = CurrentSort;
            var descendingMany = SortDescending;
            var picked = await Task.Run(() => CatalogScanner.Sort(CatalogScanner.FromPaths(paths), sortMany, descendingMany));
            _folder = null;
            FolderTitle = "Auswahl";
            StopWatch();
            ReplaceAll(picked);
        }
        finally
        {
            IsBusy = false;
            UpdateChrome();
        }
    }

    private void ReplaceAll(IReadOnlyList<ImageFile> files)
    {
        _session++;
        StopAnim(dispose: true);
        _still?.Dispose();
        _still = null;
        CurrentImage = null;
        foreach (var old in _all)
            old.Thumbnail?.Dispose();
        _all.Clear();
        _cache.Clear();
        _all.AddRange(files.Select(file => new GalleryItem(file)));
        RebuildVisible(keepSelection: false);
        StartThumbs(_session, _all.ToList());
    }

    private void Resort()
    {
        if (_all.Count == 0) return;
        var sorted = CatalogScanner.Sort(_all.Select(item => item.File), CurrentSort, SortDescending);
        var lookup = _all.ToDictionary(item => item.File.Path, StringComparer.OrdinalIgnoreCase);
        _all.Clear();
        foreach (var file in sorted)
        {
            if (lookup.TryGetValue(file.Path, out var item))
                _all.Add(item);
        }

        RebuildVisible(keepSelection: true);
    }

    private void RebuildVisible(bool keepSelection)
    {
        var query = SearchText.Trim();
        IEnumerable<GalleryItem> filtered = _all;
        if (query.Length > 0)
        {
            filtered = _all.Where(item =>
                item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.File.Path.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var keep = keepSelection ? Selected : null;
        Visible.Clear();
        foreach (var item in filtered)
            Visible.Add(item);

        if (keep is not null && Visible.Contains(keep))
            Selected = keep;
        else if (!keepSelection)
            Selected = Visible.FirstOrDefault();
        else if (Selected is not null && !Visible.Contains(Selected))
            Selected = Visible.FirstOrDefault();

        UpdateChrome();
    }

    private void StartThumbs(int session, List<GalleryItem> items)
    {
        _ = Task.Run(() =>
        {
            Parallel.ForEach(items, new ParallelOptions { MaxDegreeOfParallelism = 3 }, item =>
            {
                if (session != _session) return;
                ThumbPack? pack = null;
                try
                {
                    pack = Imaging.Thumbnail(item.File.Path);
                }
                catch
                {
                    pack = null;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (session != _session) return;
                    if (pack is null)
                    {
                        item.MarkFailed();
                        return;
                    }

                    try
                    {
                        item.PixelWidth = pack.OriginalWidth;
                        item.PixelHeight = pack.OriginalHeight;
                        item.Histogram = pack.Histogram;
                        item.Palette = pack.Palette;
                        item.Thumbnail = BitmapUtil.Create(pack.Image);
                        if (Selected == item) BindDetails(item);
                    }
                    catch
                    {
                        item.MarkFailed();
                    }
                });
            });
        });
    }

    private async Task LoadSelectedAsync(GalleryItem? item)
    {
        var token = ++_loadToken;
        StopAnim(dispose: true);
        if (item is null)
        {
            _still?.Dispose();
            _still = null;
            CurrentImage = null;
            return;
        }

        try
        {
            var pack = await GetFullAsync(item.File.Path);
            if (token != _loadToken) return;
            if (pack is null)
            {
                CurrentImage = null;
                HasSubsampled = false;
                Toast("Bild konnte nicht geöffnet werden");
                return;
            }

            item.PixelWidth = pack.OriginalWidth;
            item.PixelHeight = pack.OriginalHeight;
            item.Details = pack.Details;
            HasSubsampled = pack.Subsampled;
            SubsampledNote = pack.Subsampled ? "Für die Anzeige etwas verkleinert" : "";

            if (pack.Frames.Count > 1)
            {
                var frames = new List<Bitmap>();
                var durations = new List<int>();
                foreach (var frame in pack.Frames)
                {
                    frames.Add(BitmapUtil.Create(frame.Image));
                    durations.Add(frame.DurationMs);
                }

                if (token != _loadToken)
                {
                    foreach (var frame in frames) frame.Dispose();
                    return;
                }

                PlayFrames(frames, durations);
            }
            else
            {
                var still = BitmapUtil.Create(pack.Image);
                if (token != _loadToken)
                {
                    still.Dispose();
                    return;
                }

                SetStill(still);
            }

            BindDetails(item);
            if (!KeepZoom)
            {
                Rotation = 0;
                FlipH = false;
                FlipV = false;
                FitRequested?.Invoke();
            }

            PrefetchNext(item.File.Path);
        }
        catch (Exception ex)
        {
            if (token != _loadToken) return;
            Debug.WriteLine(ex);
            Toast("Bild konnte nicht geöffnet werden");
        }
    }

    private async Task<FullPack?> GetFullAsync(string path)
    {
        if (_cache.TryGetValue(path, out var cached)) return cached;
        var pack = await Task.Run(() => Imaging.Full(path));
        if (pack is null) return null;
        _cache[path] = pack;
        TrimCache(path);
        return pack;
    }

    private void PrefetchNext(string current)
    {
        if (Selected is null) return;
        var index = Visible.IndexOf(Selected);
        if (index < 0 || index + 1 >= Visible.Count) return;
        var next = Visible[index + 1].File.Path;
        if (next == current || _cache.ContainsKey(next)) return;
        _ = Task.Run(() =>
        {
            try
            {
                var pack = Imaging.Full(next);
                if (pack is null) return;
                _cache[next] = pack;
                Dispatcher.UIThread.Post(() => TrimCache(current));
            }
            catch
            {
                // Prefetch is best-effort.
            }
        });
    }

    private void TrimCache(string keep)
    {
        while (_cache.Count > 3)
        {
            var old = _cache.Keys.FirstOrDefault(key => !string.Equals(key, keep, StringComparison.OrdinalIgnoreCase));
            if (old is null || !_cache.TryRemove(old, out _)) break;
        }
    }

    private void SetStill(Bitmap bitmap)
    {
        StopAnim(dispose: true);
        var previous = _still;
        _still = bitmap;
        CurrentImage = bitmap;
        if (previous is not null && !ReferenceEquals(previous, bitmap))
            previous.Dispose();
    }

    private void PlayFrames(List<Bitmap> frames, List<int> durations)
    {
        StopAnim(dispose: true);
        _still?.Dispose();
        _still = null;
        _frames = frames;
        _durations = durations;
        _frame = 0;
        CurrentImage = frames[0];
        if (frames.Count < 2) return;
        _anim = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(20, durations[0])) };
        _anim.Tick += OnAnimTick;
        _anim.Start();
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        if (_frames.Count == 0) return;
        _frame = (_frame + 1) % _frames.Count;
        CurrentImage = _frames[_frame];
        if (_anim is not null && _frame < _durations.Count)
            _anim.Interval = TimeSpan.FromMilliseconds(Math.Max(20, _durations[_frame]));
    }

    private void StopAnim(bool dispose)
    {
        if (_anim is not null)
        {
            _anim.Stop();
            _anim.Tick -= OnAnimTick;
            _anim = null;
        }

        if (!dispose) return;
        foreach (var frame in _frames)
        {
            if (!ReferenceEquals(frame, CurrentImage))
                frame.Dispose();
        }

        _frames.Clear();
        _durations.Clear();
    }

    private void OnClock()
    {
        if (!IsSlideshow) return;
        var total = Math.Max(0.6, SlideshowSeconds);
        var elapsed = _slideWatch.Elapsed.TotalSeconds;
        SlideshowProgress = Math.Clamp(elapsed / total, 0, 1);
        if (elapsed < total) return;
        _slideWatch.Restart();
        Advance(slideshow: true, forward: true);
    }

    private void Advance(bool slideshow, bool forward)
    {
        if (Visible.Count == 0 || Selected is null) return;
        var index = Visible.IndexOf(Selected);
        if (index < 0) index = 0;
        if (forward)
        {
            if (index >= Visible.Count - 1)
            {
                if (slideshow && !SlideshowLoop)
                {
                    IsSlideshow = false;
                    return;
                }

                Selected = Visible[0];
                return;
            }

            Selected = Visible[index + 1];
            return;
        }

        Selected = index <= 0 ? Visible[^1] : Visible[index - 1];
    }

    private void BindDetails(GalleryItem? item)
    {
        HasSelection = item is not null;
        if (item is null)
        {
            DetailTitle = "";
            DetailPath = "";
            Meta = [];
            Histogram = [];
            Palette = [];
            HasPalette = false;
            HasHistogram = false;
            IsFavorite = false;
            Rating = 0;
            UpdateStars();
            return;
        }

        DetailTitle = item.Name;
        DetailPath = item.File.Path;
        IsFavorite = _settings.Favorites.Contains(item.File.Path, StringComparer.OrdinalIgnoreCase);
        Rating = _settings.Ratings.TryGetValue(item.File.Path, out var stars) ? stars : 0;
        UpdateStars();

        var rows = new List<MetaRow>();
        if (item.PixelWidth > 0)
            rows.Add(new MetaRow("Maße", $"{item.PixelWidth} × {item.PixelHeight}"));
        rows.Add(new MetaRow("Größe", Formatters.Bytes(item.File.Length)));
        rows.Add(new MetaRow("Geändert", item.File.ModifiedUtc.ToLocalTime().ToString("g")));
        var details = item.Details;
        if (details is not null)
        {
            Add(rows, "Kamera", details.Camera);
            Add(rows, "Objektiv", details.Lens);
            Add(rows, "Belichtung", details.Exposure);
            Add(rows, "Blende", details.Aperture);
            Add(rows, "ISO", details.Iso);
            Add(rows, "Brennweite", details.FocalLength);
            Add(rows, "Aufgenommen", details.Taken);
        }

        Meta = rows;
        Histogram = item.Histogram;
        HasHistogram = item.Histogram.Length > 0 && item.Histogram.Any(v => v > 1);
        Palette = item.Palette.Select(color => new Swatch(color.R, color.G, color.B)).ToArray();
        HasPalette = Palette.Count > 0;
    }

    private static void Add(List<MetaRow> rows, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            rows.Add(new MetaRow(label, value));
    }

    private void Rate(int value)
    {
        if (Selected is null) return;
        var path = Selected.File.Path;
        var next = Rating == value ? 0 : value;
        if (next == 0) _settings.Ratings.Remove(path);
        else _settings.Ratings[path] = next;
        Rating = next;
        UpdateStars();
        SaveSoon();
    }

    private void UpdateStars()
    {
        foreach (var star in Stars)
            star.Filled = Rating >= star.Value;
    }

    private void UpdateChrome()
    {
        HasImages = _all.Count > 0;
        HasSelection = Selected is not null;
        ShowWelcome = !HasImages && !IsBusy;
        if (Selected is null || Visible.Count == 0)
            PositionText = HasImages ? "0 Treffer" : "Keine Bilder";
        else
            PositionText = $"{Visible.IndexOf(Selected) + 1} / {Visible.Count}";
    }

    private ImageSort CurrentSort => (ImageSort)Math.Clamp(SortIndex, 0, 3);

    private void RememberRecent(string folder)
    {
        _settings.Recents.RemoveAll(path => string.Equals(path, folder, StringComparison.OrdinalIgnoreCase));
        _settings.Recents.Insert(0, folder);
        if (_settings.Recents.Count > 8)
            _settings.Recents.RemoveRange(8, _settings.Recents.Count - 8);
        _settings.LastFolder = folder;
        RebuildSavedLists();
    }

    private void RebuildSavedLists()
    {
        Recents.Clear();
        foreach (var path in _settings.Recents.Where(Directory.Exists))
            Recents.Add(new RecentEntry(path, PrettyName(path), new RelayCommand(() => _ = OpenRecent(path))));
        HasRecents = Recents.Count > 0;

        Favorites.Clear();
        foreach (var path in _settings.Favorites.Where(File.Exists))
        {
            Favorites.Add(new RecentEntry(path, Path.GetFileName(path), new RelayCommand(() => _ = OpenFavorite(path))));
        }

        HasFavorites = Favorites.Count > 0;
    }

    private async Task OpenRecent(string path)
    {
        if (!Directory.Exists(path))
        {
            _settings.Recents.RemoveAll(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
            RebuildSavedLists();
            SaveSoon();
            Toast("Ordner gibt es nicht mehr");
            return;
        }

        await LoadFolderAsync(path);
    }

    private async Task OpenFavorite(string path)
    {
        if (!File.Exists(path))
        {
            _settings.Favorites.RemoveAll(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
            RebuildSavedLists();
            SaveSoon();
            Toast("Datei gibt es nicht mehr");
            return;
        }

        await LoadFilesAsync([path], scanSiblings: true);
    }

    private void Watch(string folder)
    {
        StopWatch();
        try
        {
            _watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = Recursive,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            FileSystemEventHandler changed = (_, _) => DebounceRescan();
            _watcher.Created += changed;
            _watcher.Deleted += changed;
            _watcher.Changed += changed;
            _watcher.Renamed += (_, _) => DebounceRescan();
        }
        catch
        {
            _watcher = null;
        }
    }

    private void DebounceRescan()
    {
        _watchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _watchCts = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(600, cts.Token);
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (_folder is null || cts.IsCancellationRequested) return;
                    var keep = Selected?.File.Path;
                    await LoadFolderAsync(_folder);
                    if (keep is null) return;
                    var match = _all.FirstOrDefault(item => string.Equals(item.File.Path, keep, StringComparison.OrdinalIgnoreCase));
                    if (match is not null) Selected = match;
                });
            }
            catch (TaskCanceledException)
            {
                // A newer change replaced this refresh.
            }
        });
    }

    private void StopWatch()
    {
        _watchCts?.Cancel();
        if (_watcher is null) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    private void SaveSoon()
    {
        if (!_ready) return;
        _saveCts?.Cancel();
        var cts = new CancellationTokenSource();
        _saveCts = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, cts.Token);
                SettingsService.Save(_settings);
            }
            catch (TaskCanceledException)
            {
                // A later edit will save.
            }
        });
    }

    private CancellationTokenSource? _toastCts;

    private void Toast(string message)
    {
        StatusMessage = message;
        HasStatus = true;
        _toastCts?.Cancel();
        var cts = new CancellationTokenSource();
        _toastCts = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2200, cts.Token);
                await Dispatcher.UIThread.InvokeAsync(() => HasStatus = false);
            }
            catch (TaskCanceledException)
            {
                // replaced
            }
        });
    }

    private static string PrettyName(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private static IClipboard? ResolveClipboard()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow?.Clipboard;
        return null;
    }
}
