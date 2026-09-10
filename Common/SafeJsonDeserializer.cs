using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RaveStudioAI.Common;

/// <summary>
/// 从 AI 返回文本中提取 JSON，并安全地反序列化为对象列表。
/// </summary>
public static class SafeJsonDeserializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// AI 可以返回 JSON 数组、单个 JSON 对象，或带 Markdown 包装的 JSON。
    /// 单个对象会自动包装成 List。logPath 为空时不写日志。
    /// </summary>
    public static bool TryDeserializeFromAiText<T>(
        string rawText,
        out List<T> result,
        string? logPath = null)
    {
        result = [];
        WriteLog(logPath, $"=============开始进行 JSON 反序列化：{DateTime.Now}\r\n");

        if (string.IsNullOrWhiteSpace(rawText))
        {
            WriteDiagnostic(logPath, "错误：AI 返回文本为空。\r\n");
            return false;
        }

        try
        {
            var cleanedText = rawText.Trim('\r', '\n', '\t', ' ', '　', '"', '\'', '`', ':', '-', '=');
            var arrayMatch = Regex.Match(
                cleanedText,
                @"\[(?:[^\[\]]|(?<open>\[)|(?<-open>\]))*(?(open)(?!))\]",
                RegexOptions.Singleline);

            string json;
            if (arrayMatch.Success)
            {
                json = arrayMatch.Value;
            }
            else
            {
                var objectMatch = Regex.Match(
                    cleanedText,
                    @"\{(?:[^\{\}]|(?<open>\{)|(?<-open>\}))*(?(open)(?!))\}",
                    RegexOptions.Singleline);
                if (!objectMatch.Success)
                {
                    WriteDiagnostic(logPath, "错误：未从 AI 返回文本中找到有效 JSON。\r\n");
                    return false;
                }
                json = $"[{objectMatch.Value}]";
            }

            WriteLog(logPath, $"提取到的 JSON：\r\n{json}\r\n------------------------\r\n");
            var deserialized = JsonSerializer.Deserialize<List<T>>(json, JsonOptions);
            if (deserialized is null) return false;

            result = deserialized;
            return true;
        }
        catch (JsonException ex)
        {
            WriteDiagnostic(logPath,
                $"反序列化失败：{ex.Message}\r\n位置：Line {ex.LineNumber}, Column {ex.BytePositionInLine}\r\n");
            return false;
        }
        catch (Exception ex)
        {
            WriteDiagnostic(logPath, $"JSON 处理失败：{ex.Message}\r\n");
            return false;
        }
    }

    private static void WriteDiagnostic(string? path, string message)
    {
        Debug.WriteLine(message);
        WriteLog(path, message);
    }

    private static void WriteLog(string? path, string message)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.AppendAllText(path, message);
    }
}
