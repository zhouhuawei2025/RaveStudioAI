using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace RaveStudioAI.Common;

public static class AIConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string ConfigPath = Path.Combine(
        AppContext.BaseDirectory, "Config", "ai-configs.json");

    public static ObservableCollection<AIConfigProfile> Profiles { get; } = [];
    public static AIConfigProfile Current { get; private set; } = new();

    public static void Load()
    {
        if (!File.Exists(ConfigPath))
        {
            UseDefaults();
            Save();
            return;
        }

        var content = File.ReadAllText(ConfigPath);
        var config = JsonSerializer.Deserialize<AIConfigFile>(content, JsonOptions)
                     ?? throw new InvalidDataException("AI 配置文件内容为空。");

        Profiles.Clear();
        foreach (var profile in config.Profiles)
        {
            Profiles.Add(profile);
        }

        if (Profiles.Count == 0)
        {
            UseDefaults();
            return;
        }

        Current = Profiles.FirstOrDefault(x =>
                      x.Name.Equals(config.SelectedProfile, StringComparison.OrdinalIgnoreCase))
                  ?? Profiles[0];
    }

    public static void Apply(AIConfigProfile profile)
    {
        Validate(profile);
        Current = profile;
    }

    public static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var config = new AIConfigFile
        {
            SelectedProfile = Current.Name,
            Profiles = Profiles.Select(x => x.Copy()).ToList()
        };
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
    }

    public static void UseDefaults()
    {
        Profiles.Clear();
        Profiles.Add(new AIConfigProfile
        {
            Name = "公司 AI",
            Url = "http://192.168.8.58:30008/v1",
            Model = "openai/gpt-oss-120b",
            ApiKey = string.Empty,
            BatchSize = 20
        });
        Profiles.Add(new AIConfigProfile
        {
            Name = "备用配置",
            Url = "https://api.example.com/v1",
            Model = "model-name",
            ApiKey = string.Empty,
            BatchSize = 20
        });
        Current = Profiles[0];
    }

    private static void Validate(AIConfigProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
            throw new InvalidDataException("配置名称不能为空。");
        if (!Uri.TryCreate(profile.Url, UriKind.Absolute, out _))
            throw new InvalidDataException("AI 服务地址不是有效的 URL。");
        if (string.IsNullOrWhiteSpace(profile.Model))
            throw new InvalidDataException("模型名称不能为空。");
        if (profile.BatchSize <= 0)
            throw new InvalidDataException("批次大小必须大于 0。");
    }
}
