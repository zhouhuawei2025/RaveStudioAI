using Microsoft.Win32;
using RaveStudioAI.Common;
using RaveStudioAI.EditCheck.Models;
using RaveStudioAI.EditCheck.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace RaveStudioAI.Pages;

public partial class EditCheckPage : UserControl
{
    public ObservableCollection<QueryRow> Queries { get; } = [];
    public ObservableCollection<BlindRow> Blinds { get; } = [];
    private readonly string _instruction;

    public EditCheckPage()
    {
        InitializeComponent();
        DataContext = this;
        _instruction = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Prompts", "EditCheck", "Rave提示词.txt"));
        Loaded += (_, _) => RefreshSdsStatus();
    }

    private void UploadQueries_Click(object sender, RoutedEventArgs e)
    {
        var path = PickExcel("选择 Query Excel");
        if (path is null) return;
        try
        {
            Replace(Queries, EditCheckExcelReader.ReadQueries(path));
            QueryProgressText.Text = $"已读取 {Queries.Count} 条 Query";
        }
        catch (Exception ex) { ShowError("Query Excel 读取失败", ex); }
    }

    private async void RunQueries_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureSds() || Queries.Count == 0) return;
        RunQueriesButton.IsEnabled = false;
        var result = new StringBuilder();
        var completed = 0;
        try
        {
            foreach (var query in Queries)
            {
                query.HasError = false;
                QueryProgressText.Text = $"正在处理 {++completed}/{Queries.Count}：{query.QueryOid}";
                try
                {
                    result.AppendLine(await EditCheckConverter.ConvertQueryAsync(query, _instruction));
                }
                catch (Exception ex)
                {
                    query.HasError = true;
                    result.AppendLine($"|{query.QueryOid}|生成失败：{ex.Message}");
                    LogManager.WriteException(LogCategory.EditCheck, "query.log", ex, query.QueryOid);
                }
                result.AppendLine("------------------------------------------------------------------------");
                QueryResultTextBox.Text = result.ToString();
            }
            QueryProgressText.Text = $"处理完成：{Queries.Count - Queries.Count(x => x.HasError)} 成功，{Queries.Count(x => x.HasError)} 失败";
        }
        finally { RunQueriesButton.IsEnabled = true; }
    }

    private void UploadBlinds_Click(object sender, RoutedEventArgs e)
    {
        var path = PickExcel("选择 Blind Excel");
        if (path is null) return;
        try
        {
            Replace(Blinds, EditCheckExcelReader.ReadBlinds(path));
            BlindProgressText.Text = $"已读取 {Blinds.Count} 条 Blind";
        }
        catch (Exception ex) { ShowError("Blind Excel 读取失败", ex); }
    }

    private void RunBlinds_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureSds() || Blinds.Count == 0) return;
        var result = new StringBuilder();
        var completed = 0;
        foreach (var blind in Blinds)
        {
            blind.HasError = false;
            BlindProgressText.Text = $"正在处理 {++completed}/{Blinds.Count}：{blind.BlindOid}";
            try { result.AppendLine(EditCheckConverter.ConvertBlind(blind)); }
            catch (Exception ex)
            {
                blind.HasError = true;
                result.AppendLine($"|{blind.BlindOid}|生成失败：{ex.Message}");
                LogManager.WriteException(LogCategory.EditCheck, "blind.log", ex, blind.BlindOid);
            }
            result.AppendLine("------------------------------------------------------------------------");
        }
        BlindResultTextBox.Text = result.ToString();
        BlindProgressText.Text = $"处理完成：{Blinds.Count - Blinds.Count(x => x.HasError)} 成功，{Blinds.Count(x => x.HasError)} 失败";
    }

    private void ClearQueries_Click(object sender, RoutedEventArgs e) { Queries.Clear(); QueryResultTextBox.Clear(); QueryProgressText.Text = ""; }
    private void ClearBlinds_Click(object sender, RoutedEventArgs e) { Blinds.Clear(); BlindResultTextBox.Clear(); BlindProgressText.Text = ""; }
    private void CopyQueryResult_Click(object sender, RoutedEventArgs e) => Copy(QueryResultTextBox.Text);
    private void CopyBlindResult_Click(object sender, RoutedEventArgs e) => Copy(BlindResultTextBox.Text);

    private void RefreshSdsStatus()
    {
        var project = CurrentProject.Instance;
        SdsStatusText.Text = project.HasSds
            ? $"当前 SDS：{project.FormCount} Forms · {project.FieldCount} Fields · {project.FolderCount} Folders"
            : "请先从项目入口上传人工确认的 SDS。";
    }

    private bool EnsureSds()
    {
        RefreshSdsStatus();
        if (CurrentProject.Instance.HasSds) return true;
        Notice.Warning("请先从项目入口上传人工确认的 SDS。");
        return false;
    }

    private static string? PickExcel(string title)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = "Excel|*.xlsx;*.xlsm", Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private static void Copy(string text)
    {
        if (!string.IsNullOrWhiteSpace(text)) Clipboard.SetText(text);
    }

    private static void ShowError(string title, Exception ex)
    {
        LogManager.WriteException(LogCategory.EditCheck, "editcheck.log", ex, title);
        Notice.Error($"{title}：{ex.Message}");
    }
}
