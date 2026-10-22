using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Lumina.Core;

namespace Lumina.Models;

public partial class GalleryItem : ObservableObject
{
    public GalleryItem(ImageFile file) => File = file;

    public ImageFile File { get; }
    public string Name => File.Name;
    public string Path => File.Path;
    public string ExtensionLabel => File.Extension.TrimStart('.').ToUpperInvariant();

    [ObservableProperty] private Bitmap? thumbnail;
    [ObservableProperty] private int pixelWidth;
    [ObservableProperty] private int pixelHeight;
    [ObservableProperty] private bool failed;
    [ObservableProperty] private int[] histogram = [];
    [ObservableProperty] private (byte R, byte G, byte B)[] palette = [];
    [ObservableProperty] private PhotoDetails? details;

    public bool ThumbnailMissing => Thumbnail is null;

    partial void OnThumbnailChanged(Bitmap? value) => OnPropertyChanged(nameof(ThumbnailMissing));

    public void MarkFailed() => Failed = true;
}

public sealed record MetaRow(string Label, string Value);

public sealed class Swatch
{
    public Swatch(byte r, byte g, byte b)
    {
        Brush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(r, g, b));
    }

    public Avalonia.Media.IBrush Brush { get; }
}
