using System.IO;

namespace RaveStudioAI.Common;

public enum LogCategory
{
    App,
    Sds,
    Matrix,
    EditCheck,
    Rws
}

public static class LogManager
{
    public static string RootDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Logs");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        foreach (var category in Enum.GetValues<LogCategory>())
            Directory.CreateDirectory(GetDirectory(category));
    }

    public static string GetDirectory(LogCategory category) =>
        Path.Combine(RootDirectory, category.ToString());

    public static string GetPath(LogCategory category, string fileName)
    {
        var directory = GetDirectory(category);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    public static void Write(LogCategory category, string fileName, string message) =>
        File.AppendAllText(GetPath(category, fileName),
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");

    public static void WriteException(LogCategory category, string fileName, Exception exception, string? context = null)
    {
        var title = string.IsNullOrWhiteSpace(context) ? "发生异常" : context;
        Write(category, fileName, $"{title}：{exception.Message}{Environment.NewLine}{exception}");
    }
}
