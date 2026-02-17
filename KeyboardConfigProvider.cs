using System.Text.Json;

namespace OmenKeyboardService;

public sealed class KeyboardConfigProvider
{
    private readonly string _configPath;

    public KeyboardConfigProvider()
    {
        _configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
    }

    public string ConfigPath => _configPath;

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public KeyboardConfig LoadOrCreateDefault()
    {
        if (!File.Exists(_configPath))
        {
            CreateDefaultConfig();
        }

        var json = File.ReadAllText(_configPath);
        var config = JsonSerializer.Deserialize<KeyboardConfig>(json, JsonOptions) ?? new KeyboardConfig();
        config.Validate();
        return config;
    }

    public async Task<KeyboardConfig> LoadOrCreateDefaultAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_configPath))
        {
            CreateDefaultConfig();
        }

        var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
        var config = JsonSerializer.Deserialize<KeyboardConfig>(json, JsonOptions) ?? new KeyboardConfig();
        config.Validate();
        return config;
    }

    public void CreateDefaultConfig()
    {
        var defaultConfig = new KeyboardConfig
        {
            ProfileName = "Gaming",
            LogLevel = "Warning",
            RefreshIntervalSeconds = null,
            KvmMode = false,
            Profile = new Dictionary<string, string>
            {
                ["fps"] = "FF0000",
                ["arrows"] = "00FF00",
                ["fkeys"] = "0000FF",
                ["pkeys"] = "FF00FF",
                ["media"] = "FFFF00",
                ["numpad"] = "00FFFF",
                ["windows"] = "FF6600"
            }
        };

        var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_configPath, json);
    }
}
