namespace Lumina.Core;

public sealed record ImageFile(string Path, string Name, long Length, DateTime ModifiedUtc)
{
    public string Extension => System.IO.Path.GetExtension(Name);
}

public enum ImageSort
{
    Name,
    Date,
    Size,
    Type
}

public static class ImageTypes
{
    public static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".ico", ".avif",
        ".heic", ".heif",
        ".cr2", ".cr3", ".nef", ".nrw", ".arw", ".orf", ".rw2", ".dng", ".raf", ".pef", ".srw", ".raw",
        ".3fr", ".fff", ".iiq", ".rwl", ".x3f"
    };

    private static readonly HashSet<string> Foreign = new(StringComparer.OrdinalIgnoreCase)
    {
        ".heic", ".heif",
        ".cr2", ".cr3", ".nef", ".nrw", ".arw", ".orf", ".rw2", ".dng", ".raf", ".pef", ".srw", ".raw",
        ".3fr", ".fff", ".iiq", ".rwl", ".x3f"
    };

    public static IReadOnlyList<string> Patterns { get; } = Extensions.Select(ext => "*" + ext).ToArray();

    public static bool IsSupported(string path) =>
        Extensions.Contains(System.IO.Path.GetExtension(path));

    public static bool NeedsForeignDecoder(string path) =>
        Foreign.Contains(System.IO.Path.GetExtension(path));
}
