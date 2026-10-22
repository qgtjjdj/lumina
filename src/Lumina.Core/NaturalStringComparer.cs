namespace Lumina.Core;

public sealed class NaturalStringComparer : IComparer<string>
{
    public static readonly NaturalStringComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var i = 0;
        var j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                var i0 = i;
                var j0 = j;
                while (i < x.Length && char.IsDigit(x[i])) i++;
                while (j < y.Length && char.IsDigit(y[j])) j++;

                var iz = i0;
                var jz = j0;
                while (iz < i - 1 && x[iz] == '0') iz++;
                while (jz < j - 1 && y[jz] == '0') jz++;

                var ilen = i - iz;
                var jlen = j - jz;
                if (ilen != jlen) return ilen.CompareTo(jlen);

                var cmp = string.Compare(x, iz, y, jz, ilen, StringComparison.Ordinal);
                if (cmp != 0) return cmp;
                continue;
            }

            var a = char.ToUpperInvariant(x[i]);
            var b = char.ToUpperInvariant(y[j]);
            if (a != b) return a.CompareTo(b);
            i++;
            j++;
        }

        return (x.Length - i).CompareTo(y.Length - j);
    }
}
