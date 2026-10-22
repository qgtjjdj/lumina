using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lumina.Core;
using SkiaSharp;

namespace Lumina.Services;

public readonly record struct RawBitmap(int Width, int Height, int RowBytes, byte[] Pixels, bool HasAlpha);

public sealed class ThumbPack
{
    public int OriginalWidth { get; init; }
    public int OriginalHeight { get; init; }
    public RawBitmap Image { get; init; }
    public int[] Histogram { get; init; } = [];
    public (byte R, byte G, byte B)[] Palette { get; init; } = [];
}

public sealed class FramePack
{
    public RawBitmap Image { get; init; }
    public int DurationMs { get; init; }
}

public sealed class FullPack
{
    public int OriginalWidth { get; init; }
    public int OriginalHeight { get; init; }
    public bool Subsampled { get; init; }
    public bool IsPreview { get; init; }
    public bool Animated { get; init; }
    public RawBitmap Image { get; init; }
    public List<FramePack> Frames { get; init; } = [];
    public PhotoDetails? Details { get; init; }
}

public static class Imaging
{
    private const long SharpMaxPixels = 64_000_000;
    private const int PreviewEdge = 2048;
    private const int ThumbEdge = 360;
    private const int AnimEdge = 1400;
    private const int MaxFrames = 80;
    private static readonly SKColorSpace Srgb = SKColorSpace.CreateSrgb();

    public static ThumbPack? Thumbnail(string path)
    {
        if (ForeignImaging.Handles(path))
        {
            var foreign = ForeignImaging.Decode(path, ThumbEdge, SharpMaxPixels);
            if (foreign is null) return null;
            return new ThumbPack
            {
                OriginalWidth = foreign.Value.OriginalWidth,
                OriginalHeight = foreign.Value.OriginalHeight,
                Image = foreign.Value.Image,
                Histogram = BuildHistogram(foreign.Value.Image),
                Palette = BuildPalette(foreign.Value.Image)
            };
        }

        using var codec = SKCodec.Create(path);
        if (codec is null) return null;

        var (ow, oh) = OrientedSize(codec.Info.Width, codec.Info.Height, codec.EncodedOrigin);
        using var decoded = DecodeScaled(codec, ThumbEdge);
        if (decoded is null) return null;
        using var oriented = Orient(decoded, codec.EncodedOrigin);
        var raw = FromSk(oriented);
        return new ThumbPack
        {
            OriginalWidth = ow,
            OriginalHeight = oh,
            Image = raw,
            Histogram = BuildHistogram(raw),
            Palette = BuildPalette(raw)
        };
    }

    public static FullPack? Preview(string path) => Load(path, preview: true);

    public static FullPack? Sharp(string path) => Load(path, preview: false);

    private static FullPack? Load(string path, bool preview)
    {
        if (ForeignImaging.Handles(path))
        {
            var foreignEdge = preview ? PreviewEdge : 0;
            var foreign = ForeignImaging.Decode(path, foreignEdge, SharpMaxPixels);
            if (foreign is null) return null;
            var soft = foreign.Value.Subsampled;
            return new FullPack
            {
                OriginalWidth = foreign.Value.OriginalWidth,
                OriginalHeight = foreign.Value.OriginalHeight,
                Subsampled = soft,
                IsPreview = preview && soft,
                Image = foreign.Value.Image,
                Details = preview ? PhotoMetadata.Read(path) : null
            };
        }

        using var codec = SKCodec.Create(path);
        if (codec is null) return null;
        if (!preview && codec.FrameCount > 1) return null;

        var details = preview ? PhotoMetadata.Read(path) : null;
        var (ow, oh) = OrientedSize(codec.Info.Width, codec.Info.Height, codec.EncodedOrigin);

        if (codec.FrameCount > 1)
        {
            var frames = DecodeFrames(codec);
            if (frames.Count > 0)
            {
                return new FullPack
                {
                    OriginalWidth = ow,
                    OriginalHeight = oh,
                    Image = frames[0].Image,
                    Frames = frames,
                    Details = details,
                    Subsampled = true,
                    Animated = true
                };
            }
        }

        var longest = Math.Max(codec.Info.Width, codec.Info.Height);
        var edge = TargetEdge(codec.Info.Width, codec.Info.Height);
        if (preview) edge = Math.Min(edge, PreviewEdge);
        using var decoded = DecodeScaled(codec, edge);
        if (decoded is null) return null;
        using var oriented = Orient(decoded, codec.EncodedOrigin);
        var subsampled = edge < longest;
        return new FullPack
        {
            OriginalWidth = ow,
            OriginalHeight = oh,
            Subsampled = subsampled,
            IsPreview = preview && subsampled,
            Image = FromSk(oriented),
            Details = details
        };
    }

    private static List<FramePack> DecodeFrames(SKCodec codec)
    {
        var frames = new List<FramePack>();
        try
        {
            var count = Math.Min(codec.FrameCount, MaxFrames);
            var w = Math.Max(1, codec.Info.Width);
            var h = Math.Max(1, codec.Info.Height);
            var scale = Math.Min(1f, AnimEdge / (float)Math.Max(w, h));
            var info = new SKImageInfo(
                Math.Max(1, (int)(w * scale)),
                Math.Max(1, (int)(h * scale)),
                SKColorType.Bgra8888,
                SKAlphaType.Premul,
                Srgb);

            using var canvasBitmap = new SKBitmap(info);
            for (var i = 0; i < count; i++)
            {
                var result = codec.GetPixels(canvasBitmap.Info, canvasBitmap.GetPixels(), new SKCodecOptions(i));
                if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                    continue;

                using var oriented = Orient(canvasBitmap, codec.EncodedOrigin);
                var duration = 100;
                if (i < codec.FrameInfo.Length)
                    duration = Math.Max(20, codec.FrameInfo[i].Duration);
                frames.Add(new FramePack { Image = FromSk(oriented), DurationMs = duration });
            }
        }
        catch
        {
            return [];
        }

        return frames;
    }

    private static int TargetEdge(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        var pixels = (long)width * height;
        if (pixels <= SharpMaxPixels) return Math.Max(width, height);
        var scale = Math.Sqrt(SharpMaxPixels / (double)pixels);
        return Math.Max(1, (int)Math.Round(Math.Max(width, height) * scale));
    }

    private static SKBitmap? DecodeScaled(SKCodec codec, int maxEdge)
    {
        var width = Math.Max(1, codec.Info.Width);
        var height = Math.Max(1, codec.Info.Height);
        var sample = 1;
        while (width / sample > maxEdge * 2 || height / sample > maxEdge * 2)
            sample *= 2;

        var info = new SKImageInfo(
            Math.Max(1, width / sample),
            Math.Max(1, height / sample),
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            Srgb);

        var bitmap = SKBitmap.Decode(codec, info);
        if (bitmap is null) return null;

        var longest = Math.Max(bitmap.Width, bitmap.Height);
        if (longest <= maxEdge || maxEdge <= 0) return bitmap;

        var scale = maxEdge / (float)longest;
        var resized = bitmap.Resize(
            new SKImageInfo(
                Math.Max(1, (int)(bitmap.Width * scale)),
                Math.Max(1, (int)(bitmap.Height * scale)),
                SKColorType.Bgra8888,
                SKAlphaType.Premul,
                Srgb),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        bitmap.Dispose();
        return resized;
    }

    private static (int W, int H) OrientedSize(int width, int height, SKEncodedOrigin origin)
    {
        if (Swaps(origin)) return (height, width);
        return (Math.Max(1, width), Math.Max(1, height));
    }

    private static bool Swaps(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;

    private static SKBitmap Orient(SKBitmap src, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft)
            return src.Copy() ?? src;

        var swap = Swaps(origin);
        var dst = new SKBitmap(swap ? src.Height : src.Width, swap ? src.Width : src.Height, src.ColorType, src.AlphaType);
        using var canvas = new SKCanvas(dst);
        canvas.Clear(SKColors.Transparent);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(src.Width, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(src.Width, src.Height);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, src.Height);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(src.Height, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, src.Width);
                canvas.RotateDegrees(270);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.Translate(0, 0);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                canvas.Translate(-src.Width, 0);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(src.Height, src.Width);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            default:
                canvas.DrawBitmap(src, 0, 0);
                return dst;
        }

        canvas.DrawBitmap(src, 0, 0);
        return dst;
    }

    private static RawBitmap FromSk(SKBitmap bitmap)
    {
        var info = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        SKBitmap? converted = null;
        var source = bitmap;
        if (bitmap.ColorType != info.ColorType || bitmap.AlphaType != info.AlphaType)
        {
            converted = bitmap.Copy(SKColorType.Bgra8888);
            if (converted is not null) source = converted;
        }

        try
        {
            var row = source.RowBytes;
            var bytes = new byte[Math.Max(0, row * source.Height)];
            if (bytes.Length > 0)
                Marshal.Copy(source.GetPixels(), bytes, 0, bytes.Length);
            return new RawBitmap(source.Width, source.Height, row, bytes, source.AlphaType != SKAlphaType.Opaque);
        }
        finally
        {
            converted?.Dispose();
        }
    }

    private static int[] BuildHistogram(RawBitmap raw)
    {
        var bins = new int[48];
        if (raw.Width <= 0 || raw.Height <= 0 || raw.Pixels.Length < 4) return bins;
        var step = Math.Max(1, raw.Width * raw.Height / 8000);
        for (var p = 0; p < raw.Width * raw.Height; p += step)
        {
            var y = p / raw.Width;
            var x = p % raw.Width;
            var o = y * raw.RowBytes + x * 4;
            if (o + 2 >= raw.Pixels.Length) break;
            var b = raw.Pixels[o];
            var g = raw.Pixels[o + 1];
            var r = raw.Pixels[o + 2];
            var luma = (r * 299 + g * 587 + b * 114) / 1000;
            bins[Math.Clamp(luma * 47 / 255, 0, 47)]++;
        }

        var max = 1;
        foreach (var bin in bins) max = Math.Max(max, bin);
        var heights = new int[bins.Length];
        for (var i = 0; i < bins.Length; i++)
            heights[i] = (int)Math.Round(bins[i] * 40.0 / max);
        return heights;
    }

    private static (byte R, byte G, byte B)[] BuildPalette(RawBitmap raw)
    {
        var counts = new Dictionary<int, int>();
        if (raw.Width <= 0 || raw.Height <= 0) return [];
        var step = Math.Max(1, raw.Width * raw.Height / 4000);
        for (var p = 0; p < raw.Width * raw.Height; p += step)
        {
            var y = p / raw.Width;
            var x = p % raw.Width;
            var o = y * raw.RowBytes + x * 4;
            if (o + 2 >= raw.Pixels.Length) break;
            var r = raw.Pixels[o + 2] >> 3;
            var g = raw.Pixels[o + 1] >> 3;
            var b = raw.Pixels[o] >> 3;
            var key = (r << 10) | (g << 5) | b;
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        var picked = new List<(byte R, byte G, byte B)>();
        foreach (var kv in counts.OrderByDescending(kv => kv.Value))
        {
            byte r = (byte)(((kv.Key >> 10) & 31) * 255 / 31);
            byte g = (byte)(((kv.Key >> 5) & 31) * 255 / 31);
            byte b = (byte)((kv.Key & 31) * 255 / 31);
            var close = false;
            foreach (var existing in picked)
            {
                if (Math.Abs(existing.R - r) + Math.Abs(existing.G - g) + Math.Abs(existing.B - b) < 48)
                {
                    close = true;
                    break;
                }
            }

            if (close) continue;
            picked.Add((r, g, b));
            if (picked.Count == 5) break;
        }

        return picked.ToArray();
    }
}

public static class BitmapUtil
{
    public static WriteableBitmap Create(in RawBitmap raw)
    {
        var width = Math.Max(1, raw.Width);
        var height = Math.Max(1, raw.Height);
        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            raw.HasAlpha ? AlphaFormat.Premul : AlphaFormat.Opaque);

        using var frame = bitmap.Lock();
        if (raw.Pixels.Length == 0) return bitmap;

        unsafe
        {
            fixed (byte* srcBase = raw.Pixels)
            {
                var dst = (byte*)frame.Address;
                var copy = Math.Min(raw.RowBytes, frame.RowBytes);
                var rows = Math.Min(height, raw.Height);
                for (var y = 0; y < rows; y++)
                {
                    var srcOffset = y * raw.RowBytes;
                    if (srcOffset + copy > raw.Pixels.Length) break;
                    Buffer.MemoryCopy(srcBase + srcOffset, dst + y * frame.RowBytes, frame.RowBytes, copy);
                }
            }
        }

        return bitmap;
    }
}
