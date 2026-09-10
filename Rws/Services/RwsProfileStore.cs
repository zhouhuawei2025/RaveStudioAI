using RaveStudioAI.Rws.Models;
using RaveStudioAI.Common;
using System.IO;
using System.Text.Json;

namespace RaveStudioAI.Rws.Services;

public static class RwsProfileStore
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "Config", "rws-configs.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static RwsProfileFile Load()
    {
        EnsureFile();
        try
        {
            return JsonSerializer.Deserialize<RwsProfileFile>(File.ReadAllText(FilePath), Options)
                   ?? CreateDefault();
        }
        catch (Exception ex)
        {
            LogManager.WriteException(LogCategory.Rws, "rws.log", ex, "读取 RWS 配置失败");
            return CreateDefault();
        }
    }

    public static void Save(RwsProfileFile configuration)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(configuration, Options));
    }

    private static void EnsureFile()
    {
        if (File.Exists(FilePath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(CreateDefault(), Options));
    }

    private static RwsProfileFile CreateDefault() => new()
    {
        Tenants = new Dictionary<string, RwsTenantProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["your-tenant"] = new()
        }
    };
}
