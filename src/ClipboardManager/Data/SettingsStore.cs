using System.IO;
using System.Text.Json;
using ClipboardManager.Models;

namespace ClipboardManager.Data;

/// <summary>
/// Kullanici ayarlarini (hotkey, pencere boyutu vb.) JSON dosyasinda saklar.
/// Ayarlar uygulamaya ozel oldugundan VT yerine ayri dosyada tutulur.
/// </summary>
public class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    public SettingsStore()
    {
        _filePath = Path.Combine(App.AppDataDir, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_filePath)) return new AppSettings();
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOpts);
        File.WriteAllText(_filePath, json);
    }
}
