using System.IO;
using System.Text.Json;
using BalanceDock.Models;

namespace BalanceDock.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public SettingsService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BalanceDock");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public AppSettings Current { get; private set; } = new();

    public string SettingsPath => _settingsPath;

    public void Load()
    {
        if (!File.Exists(_settingsPath))
        {
            Current = new AppSettings();
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            Current.Balance = Math.Clamp(Current.Balance, -100, 100);
            Current.HotkeyStep = Math.Clamp(Current.HotkeyStep, 1, 25);
        }
        catch
        {
            Current = new AppSettings();
            Save();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
