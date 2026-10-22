namespace Lumina.Core;

public static class CatalogScanner
{
    public static IReadOnlyList<ImageFile> Scan(string folder, bool recursive)
    {
        var result = new List<ImageFile>();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return result;

        var stack = new Stack<string>();
        stack.Push(folder);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(dir);
            }
            catch
            {
                continue;
            }

            foreach (var entry in entries)
            {
                try
                {
                    var name = Path.GetFileName(entry);
                    if (name.StartsWith('.')) continue;

                    var attr = File.GetAttributes(entry);
                    if ((attr & FileAttributes.Directory) != 0)
                    {
                        if (recursive) stack.Push(entry);
                        continue;
                    }

                    if (!ImageTypes.IsSupported(entry)) continue;
                    var info = new FileInfo(entry);
                    result.Add(new ImageFile(info.FullName, info.Name, info.Length, info.LastWriteTimeUtc));
                }
                catch
                {
                    // Unreadable entries are skipped; a viewer should not die on one file.
                }
            }
        }

        return result;
    }

    public static List<ImageFile> FromPaths(IEnumerable<string> paths)
    {
        var result = new List<ImageFile>();
        foreach (var path in paths)
        {
            try
            {
                if (!File.Exists(path) || !ImageTypes.IsSupported(path)) continue;
                var info = new FileInfo(path);
                result.Add(new ImageFile(info.FullName, info.Name, info.Length, info.LastWriteTimeUtc));
            }
            catch
            {
                // ignore
            }
        }

        return result;
    }

    public static List<ImageFile> Sort(IEnumerable<ImageFile> files, ImageSort sort, bool descending)
    {
        IEnumerable<ImageFile> ordered = sort switch
        {
            ImageSort.Date => descending
                ? files.OrderByDescending(f => f.ModifiedUtc).ThenBy(f => f.Name, NaturalStringComparer.Instance)
                : files.OrderBy(f => f.ModifiedUtc).ThenBy(f => f.Name, NaturalStringComparer.Instance),
            ImageSort.Size => descending
                ? files.OrderByDescending(f => f.Length).ThenBy(f => f.Name, NaturalStringComparer.Instance)
                : files.OrderBy(f => f.Length).ThenBy(f => f.Name, NaturalStringComparer.Instance),
            ImageSort.Type => descending
                ? files.OrderByDescending(f => f.Extension, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Name, NaturalStringComparer.Instance)
                : files.OrderBy(f => f.Extension, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Name, NaturalStringComparer.Instance),
            _ => descending
                ? files.OrderByDescending(f => f.Name, NaturalStringComparer.Instance)
                : files.OrderBy(f => f.Name, NaturalStringComparer.Instance)
        };

        return ordered.ToList();
    }
}
