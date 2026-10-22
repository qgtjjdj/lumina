using System.Text.Json;
using Lumina.Models;

namespace Lumina.Services;

public static class SettingsService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private static string DirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lumina");

    private static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Json));
        }
        catch
        {
            // Settings are a convenience. Never block the viewer on a bad profile path.
        }
    }
}
