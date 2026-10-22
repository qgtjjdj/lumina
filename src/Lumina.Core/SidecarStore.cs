using System.Xml.Linq;

namespace Lumina.Core;

public readonly record struct SidecarNote(int Rating, bool Favorite, bool OwnsFavorite);

/// <summary>
/// Ratings travel with the picture, but the picture itself is never rewritten.
/// Our file is <c>photo.jpg.lumina.xmp</c>. An existing Adobe <c>photo.xmp</c> is read and left alone.
/// </summary>
public static class SidecarStore
{
    public static string OwnPath(string imagePath) => imagePath + ".lumina.xmp";

    public static string AdobePath(string imagePath)
    {
        var directory = Path.GetDirectoryName(imagePath);
        var name = Path.GetFileNameWithoutExtension(imagePath) + ".xmp";
        return string.IsNullOrEmpty(directory) ? name : Path.Combine(directory, name);
    }

    public static SidecarNote? TryRead(string imagePath)
    {
        var own = OwnPath(imagePath);
        if (File.Exists(own))
        {
            var note = ReadFile(own);
            return note is null
                ? new SidecarNote(0, false, true)
                : note.Value with { OwnsFavorite = true };
        }

        var adobe = AdobePath(imagePath);
        if (File.Exists(adobe))
            return ReadFile(adobe);

        return null;
    }

    public static bool TryWrite(string imagePath, int rating, bool favorite)
    {
        try
        {
            var path = OwnPath(imagePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var doc = new XDocument(
                new XElement(XName.Get("xmpmeta", "adobe:ns:meta/"),
                    new XAttribute(XNamespace.Xmlns + "x", "adobe:ns:meta/"),
                    new XAttribute(XNamespace.Xmlns + "lumina", "https://lumina.app/ns/1.0/"),
                    new XElement(XName.Get("RDF", "http://www.w3.org/1999/02/22-rdf-syntax-ns#"),
                        new XAttribute(XNamespace.Xmlns + "rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#"),
                        new XElement(XName.Get("Description", "http://www.w3.org/1999/02/22-rdf-syntax-ns#"),
                            new XAttribute(XName.Get("about", "http://www.w3.org/1999/02/22-rdf-syntax-ns#"), ""),
                            new XAttribute(XNamespace.Xmlns + "xmp", "http://ns.adobe.com/xap/1.0/"),
                            new XAttribute(XName.Get("Rating", "http://ns.adobe.com/xap/1.0/"), Math.Clamp(rating, 0, 5)),
                            new XAttribute(XName.Get("Favorite", "https://lumina.app/ns/1.0/"), favorite ? "True" : "False")))));

            doc.Save(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static SidecarNote? ReadFile(string path)
    {
        try
        {
            var doc = XDocument.Load(path);
            var rating = 0;
            var favorite = false;
            var ownsFavorite = false;
            foreach (var element in doc.Descendants())
            {
                foreach (var attribute in element.Attributes())
                {
                    if (attribute.Name.LocalName == "Rating" && int.TryParse(attribute.Value, out var parsed))
                        rating = Math.Clamp(parsed, 0, 5);

                    if (attribute.Name.LocalName == "Favorite")
                    {
                        ownsFavorite = true;
                        favorite = attribute.Value is "True" or "true" or "1";
                    }
                }

                if (element.Name.LocalName == "li" &&
                    element.Value.Contains("Lumina:Favorite", StringComparison.OrdinalIgnoreCase))
                {
                    ownsFavorite = true;
                    favorite = true;
                }
            }

            return new SidecarNote(rating, favorite, ownsFavorite);
        }
        catch
        {
            return null;
        }
    }
}
