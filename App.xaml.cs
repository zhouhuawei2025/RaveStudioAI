using RaveStudioAI.Common;
using System.Windows;

namespace RaveStudioAI;

public partial class App : Application
{
    public static string? StartupWarning { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LogManager.EnsureDirectories();

        try
        {
            AIConfigStore.Load();
        }
        catch (Exception ex)
        {
            LogManager.WriteException(LogCategory.App, "app.log", ex, "AI 配置读取失败");
            StartupWarning = $"AI 配置读取失败，将使用默认配置：{ex.Message}";
            AIConfigStore.UseDefaults();
        }
    }
}
