namespace Lumina.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public int StageIndex { get; set; }
    public bool Recursive { get; set; }
    public int SortIndex { get; set; }
    public bool SortDescending { get; set; }
    public double SlideshowSeconds { get; set; } = 4;
    public bool SlideshowLoop { get; set; } = true;
    public bool ShowFilmstrip { get; set; } = true;
    public bool ShowSidebar { get; set; } = true;
    public bool ShowInfo { get; set; }
    public bool KeepZoom { get; set; }
    public double WindowWidth { get; set; } = 1320;
    public double WindowHeight { get; set; } = 860;
    public bool WindowMaximized { get; set; }
    public string? LastFolder { get; set; }
    public List<string> Recents { get; set; } = [];
    public List<string> Favorites { get; set; } = [];
    public Dictionary<string, int> Ratings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
