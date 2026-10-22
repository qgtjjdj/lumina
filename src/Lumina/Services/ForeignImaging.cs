using ImageMagick;
using Lumina.Core;

namespace Lumina.Services;

public readonly record struct ForeignResult(int OriginalWidth, int OriginalHeight, RawBitmap Image, bool Subsampled);

public static class ForeignImaging
{
    private static readonly SemaphoreSlim Gate = new(2, 2);

    public static bool Handles(string path) => ImageTypes.NeedsForeignDecoder(path);

    public static ForeignResult? Decode(string path, int maxEdge, long maxPixels)
    {
        Gate.Wait();
        try
        {
            using var image = new MagickImage(path);
            image.AutoOrient();
            ApplySrgb(image);

            var originalWidth = Math.Max(1, (int)image.Width);
            var originalHeight = Math.Max(1, (int)image.Height);
            var edge = Edge(originalWidth, originalHeight, maxEdge, maxPixels);
            var subsampled = false;
            if (Math.Max(image.Width, image.Height) > (uint)edge)
            {
                image.Resize(new MagickGeometry($"{edge}x{edge}>"));
                subsampled = true;
            }

            image.ColorType = ColorType.TrueColorAlpha;
            using var pixels = image.GetPixels();
            var packed = pixels.ToByteArray(PixelMapping.BGRA);
            var width = Math.Max(1, (int)image.Width);
            var height = Math.Max(1, (int)image.Height);
            if (packed is null || packed.Length < width * height * 4) return null;

            var tight = width * height * 4;
            if (packed.Length != tight)
                packed = packed[..tight];

            return new ForeignResult(originalWidth, originalHeight, new RawBitmap(width, height, width * 4, packed, false), subsampled);
        }
        catch
        {
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static void ApplySrgb(MagickImage image)
    {
        try
        {
            var embedded = image.GetColorProfile();
            if (embedded is not null)
            {
                image.TransformColorSpace(embedded, ColorProfiles.SRGB);
                return;
            }
        }
        catch
        {
            // A missing or odd profile should not hide the picture.
        }

        if (image.ColorSpace != ColorSpace.sRGB)
            image.ColorSpace = ColorSpace.sRGB;
    }

    private static int Edge(int width, int height, int maxEdge, long maxPixels)
    {
        var longest = Math.Max(1, Math.Max(width, height));
        var edge = longest;
        if (maxEdge > 0)
            edge = Math.Min(edge, maxEdge);
        var pixels = (long)width * height;
        if (maxPixels > 0 && pixels > maxPixels)
        {
            var scale = Math.Sqrt(maxPixels / (double)pixels);
            edge = Math.Min(edge, Math.Max(1, (int)Math.Round(longest * scale)));
        }

        return Math.Max(1, edge);
    }
}
