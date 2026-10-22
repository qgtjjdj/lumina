using Lumina.Core;
using Xunit;

namespace Lumina.Tests;

public class NaturalSortTests
{
    [Fact]
    public void Orders_embedded_numbers()
    {
        var names = new[] { "img10.jpg", "img2.jpg", "img1.jpg", "IMG3.jpg" };
        var sorted = names.OrderBy(name => name, NaturalStringComparer.Instance).ToArray();
        Assert.Equal(["img1.jpg", "img2.jpg", "IMG3.jpg", "img10.jpg"], sorted);
    }

    [Theory]
    [InlineData("img2.png", "img10.png", -1)]
    [InlineData("a", "a", 0)]
    [InlineData("b", "a", 1)]
    public void Compares_pairs(string left, string right, int sign)
    {
        var cmp = NaturalStringComparer.Instance.Compare(left, right);
        Assert.Equal(sign, Math.Sign(cmp));
    }
}

public class FormatTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(5_242_880, "5 MB")]
    public void Bytes(long value, string expected) =>
        Assert.Equal(expected, Formatters.Bytes(value));
}

public class CatalogTests
{
    [Fact]
    public void Scan_keeps_images_and_skips_other_files()
    {
        var dir = Directory.CreateTempSubdirectory("lumina-scan");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "note.txt"), "x");
            File.WriteAllBytes(Path.Combine(dir.FullName, "b.JPG"), [1, 2, 3]);
            File.WriteAllBytes(Path.Combine(dir.FullName, "c.png"), [1]);
            File.WriteAllBytes(Path.Combine(dir.FullName, ".hidden.png"), [1]);
            var nested = Directory.CreateDirectory(Path.Combine(dir.FullName, "sub"));
            File.WriteAllBytes(Path.Combine(nested.FullName, "d.webp"), [1]);

            var top = CatalogScanner.Scan(dir.FullName, recursive: false);
            Assert.Equal(2, top.Count);

            var all = CatalogScanner.Scan(dir.FullName, recursive: true);
            Assert.Equal(3, all.Count);
            Assert.Contains(all, file => file.Name == "d.webp");
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void Sort_by_size_then_name()
    {
        var files = new[]
        {
            new ImageFile("a.jpg", "a.jpg", 50, DateTime.UnixEpoch),
            new ImageFile("b.jpg", "b.jpg", 10, DateTime.UnixEpoch),
            new ImageFile("c.jpg", "c.jpg", 30, DateTime.UnixEpoch)
        };

        var sorted = CatalogScanner.Sort(files, ImageSort.Size, descending: false);
        Assert.Equal(["b.jpg", "c.jpg", "a.jpg"], sorted.Select(file => file.Name).ToArray());

        var names = CatalogScanner.Sort(
            [
                new ImageFile("img10.jpg", "img10.jpg", 1, DateTime.UnixEpoch),
                new ImageFile("img2.jpg", "img2.jpg", 1, DateTime.UnixEpoch)
            ],
            ImageSort.Name,
            descending: false);
        Assert.Equal("img2.jpg", names[0].Name);
    }
}
