using Lumina.Core;
using Xunit;

namespace Lumina.Tests;

public class ImageTypeTests
{
    [Theory]
    [InlineData("photo.HEIC", true)]
    [InlineData("photo.cr3", true)]
    [InlineData("photo.dng", true)]
    [InlineData("photo.jpg.lumina.xmp", false)]
    [InlineData("photo.xmp", false)]
    [InlineData("notes.txt", false)]
    public void Recognizes_extensions(string name, bool expected) =>
        Assert.Equal(expected, ImageTypes.IsSupported(name));

    [Theory]
    [InlineData("a.heic", true)]
    [InlineData("a.NEF", true)]
    [InlineData("a.jpg", false)]
    [InlineData("a.png", false)]
    public void Foreign_decoder_only_for_camera_formats(string name, bool expected) =>
        Assert.Equal(expected, ImageTypes.NeedsForeignDecoder(name));
}

public class SidecarTests
{
    [Fact]
    public void Roundtrip_rating_and_favorite_without_touching_the_image()
    {
        var dir = Directory.CreateTempSubdirectory("lumina-side");
        try
        {
            var image = Path.Combine(dir.FullName, "img.jpg");
            File.WriteAllBytes(image, [1, 2, 3, 4]);
            Assert.True(SidecarStore.TryWrite(image, 4, true));

            var note = SidecarStore.TryRead(image);
            Assert.Equal(4, note?.Rating);
            Assert.True(note?.Favorite);
            Assert.True(note?.OwnsFavorite);
            Assert.Equal(4, new FileInfo(image).Length);
            Assert.True(File.Exists(image + ".lumina.xmp"));
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void Clearing_stars_is_remembered()
    {
        var dir = Directory.CreateTempSubdirectory("lumina-clear");
        try
        {
            var image = Path.Combine(dir.FullName, "img.jpg");
            File.WriteAllBytes(image, [1]);
            SidecarStore.TryWrite(image, 0, false);
            var note = SidecarStore.TryRead(image);
            Assert.Equal(0, note?.Rating);
            Assert.False(note?.Favorite);
            Assert.True(note?.OwnsFavorite);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void Reads_adobe_rating_and_does_not_rewrite_it()
    {
        var dir = Directory.CreateTempSubdirectory("lumina-adobe");
        try
        {
            var image = Path.Combine(dir.FullName, "img.jpg");
            File.WriteAllBytes(image, [1]);
            var adobe = SidecarStore.AdobePath(image);
            File.WriteAllText(adobe, """
                <?xml version="1.0" encoding="utf-8"?>
                <x:xmpmeta xmlns:x="adobe:ns:meta/">
                  <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
                    <rdf:Description xmlns:xmp="http://ns.adobe.com/xap/1.0/" xmp:Rating="5" rdf:about=""/>
                  </rdf:RDF>
                </x:xmpmeta>
                """);
            var before = File.ReadAllText(adobe);
            var note = SidecarStore.TryRead(image);
            Assert.Equal(5, note?.Rating);
            Assert.False(note?.OwnsFavorite);
            Assert.Equal(before, File.ReadAllText(adobe));
            Assert.False(File.Exists(SidecarStore.OwnPath(image)));
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void Own_sidecar_wins_over_adobe()
    {
        var dir = Directory.CreateTempSubdirectory("lumina-both");
        try
        {
            var image = Path.Combine(dir.FullName, "img.jpg");
            File.WriteAllBytes(image, [1]);
            File.WriteAllText(SidecarStore.AdobePath(image), """<rdf:Description xmp:Rating="5"/>""");
            SidecarStore.TryWrite(image, 2, true);
            var note = SidecarStore.TryRead(image);
            Assert.Equal(2, note?.Rating);
            Assert.True(note?.Favorite);
        }
        finally
        {
            dir.Delete(true);
        }
    }
}
