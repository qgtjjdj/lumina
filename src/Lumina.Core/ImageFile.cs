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
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".ico", ".avif"
    };

    public static bool IsSupported(string path) =>
        Extensions.Contains(System.IO.Path.GetExtension(path));
}
