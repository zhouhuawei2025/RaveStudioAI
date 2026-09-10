using RaveStudioAI.Common;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RaveStudioAI.Pages;

public partial class LogsPage : UserControl
{
    public LogsPage() => InitializeComponent();

    private void LogsPage_Loaded(object sender, RoutedEventArgs e) => RefreshLogs();
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshLogs();
    private void CategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) RefreshLogs();
    }

    private void RefreshLogs()
    {
        LogManager.EnsureDirectories();
        var selected = (CategoryComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
        var directories = selected == "All"
            ? Enum.GetValues<LogCategory>().Select(LogManager.GetDirectory)
            : [LogManager.GetDirectory(Enum.Parse<LogCategory>(selected))];

        LogsGrid.ItemsSource = directories
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory)
                .Select(path => new LogFileItem(path, Path.GetFileName(directory))))
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path }) OpenPath(path);
    }

    private void LogsGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LogsGrid.SelectedItem is LogFileItem item) OpenPath(item.FullPath);
    }

    private void OpenRoot_Click(object sender, RoutedEventArgs e)
    {
        LogManager.EnsureDirectories();
        OpenPath(LogManager.RootDirectory);
    }

    private static void OpenPath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private sealed class LogFileItem
    {
        public LogFileItem(string path, string category)
        {
            var info = new FileInfo(path);
            FullPath = path;
            FileName = info.Name;
            Category = category;
            UpdatedAt = info.LastWriteTime;
            UpdatedAtText = UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            SizeText = info.Length < 1024 ? $"{info.Length} B" : $"{info.Length / 1024d:F1} KB";
        }

        public string FullPath { get; }
        public string FileName { get; }
        public string Category { get; }
        public DateTime UpdatedAt { get; }
        public string UpdatedAtText { get; }
        public string SizeText { get; }
    }
}
