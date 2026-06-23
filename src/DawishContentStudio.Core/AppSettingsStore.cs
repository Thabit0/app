using System.Text.Json;

namespace DawishContentStudio.Core;

public sealed class AppSettingsStore
{
    public string AppFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DawishContentStudio");
    private string SettingsPath => Path.Combine(AppFolder, "settings.json");

    public AppSettings Load()
    {
        try
        {
            Directory.CreateDirectory(AppFolder);
            if (!File.Exists(SettingsPath)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(AppFolder);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
