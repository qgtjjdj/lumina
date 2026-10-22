using System.Globalization;

namespace Lumina.Core;

public static class Formatters
{
    public static string Bytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes < 0 ? 0 : bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        if (unit == 0) return $"{(long)value} B";
        return string.Format(CultureInfo.InvariantCulture, "{0:0.#} {1}", value, units[unit]);
    }
}
