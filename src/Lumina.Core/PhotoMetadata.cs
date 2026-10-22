using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace Lumina.Core;

public sealed record PhotoDetails(
    string? Camera,
    string? Lens,
    string? Exposure,
    string? Aperture,
    string? Iso,
    string? FocalLength,
    string? Taken);

public static class PhotoMetadata
{
    public static PhotoDetails? Read(string path)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);
            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            var sub = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

            string? make = Clean(ifd0?.GetDescription(ExifDirectoryBase.TagMake));
            string? model = Clean(ifd0?.GetDescription(ExifDirectoryBase.TagModel));
            string? camera = Join(make, model);

            string? lens = Clean(sub?.GetDescription(ExifDirectoryBase.TagLensModel))
                           ?? Clean(ifd0?.GetDescription(ExifDirectoryBase.TagLensModel));
            string? exposure = Clean(sub?.GetDescription(ExifDirectoryBase.TagExposureTime));
            string? aperture = Clean(sub?.GetDescription(ExifDirectoryBase.TagFNumber));
            string? iso = Clean(sub?.GetDescription(ExifDirectoryBase.TagIsoEquivalent));
            string? focal = Clean(sub?.GetDescription(ExifDirectoryBase.TagFocalLength));
            string? taken = Clean(sub?.GetDescription(ExifDirectoryBase.TagDateTimeOriginal))
                            ?? Clean(ifd0?.GetDescription(ExifDirectoryBase.TagDateTime));

            if (camera is null && lens is null && exposure is null && aperture is null && iso is null && focal is null && taken is null)
                return null;

            return new PhotoDetails(camera, lens, exposure, aperture, iso, focal, taken);
        }
        catch
        {
            return null;
        }
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 || trimmed == "0" ? null : trimmed;
    }

    private static string? Join(string? a, string? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        if (b.StartsWith(a, StringComparison.OrdinalIgnoreCase)) return b;
        return $"{a} {b}";
    }
}
