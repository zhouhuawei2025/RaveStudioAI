using Microsoft.Win32;
using RaveStudioAI.Common;
using RaveStudioAI.EditCheck.Models;
using RaveStudioAI.EditCheck.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RaveStudioAI.Pages;

public partial class SetDataPointVisiblePage : UserControl
{
    public ObservableCollection<BlindRow> Rows { get; } = [];
    private readonly string _outputDirectory = Path.Combine(AppContext.BaseDirectory, "Output", "EditCheck");
    private bool _showingExamples = true;
    private bool _isRunning;

    public SetDataPointVisiblePage()
    {
        InitializeComponent();
        DataContext = this;
        Rows.Add(new BlindRow { BlindOid = "SV_BL_001", FieldOid = "VISDAT/SVREASND", FolderOid = "UNS", FormOid = "SV", LogicText = "SVOCCUR = 1, set VISDAT/SVREASND visible." });
        Rows.Add(new BlindRow { BlindOid = "VS2_BL_001", FieldOid = "SYSBPAE/SYSBPMH/SYSBPOTH", FolderOid = "All visit", FormOid = "VS2", LogicText = "VSPERF = 1 and SBPCLSIG >=3, set SYSBPAE/SYSBPMH/SYSBPOTH visible." });
        AttachRows();
        ProgressText.Text = "格式示例：上传 Excel 后会自动替换";
        RunButton.IsEnabled = false;
        Loaded += (_, _) => RefreshStatus();
    }

    private void Upload_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureSds()) return;
        var dialog = new OpenFileDialog { Title = "选择 Blind Excel", Filter = "Excel|*.xlsx;*.xlsm" };
        if (dialog.ShowDialog() != true) return;
        LogManager.BeginRun(LogCategory.EditCheck, "editcheck.log", "读取并校验 Blind Excel");
        try
        {
            var values = EditCheckExcelReader.ReadBlinds(dialog.FileName);
            BlindValidator.ValidateAndNormalize(values);
            DetachRows();
            Rows.Clear();
            foreach (var row in values) Rows.Add(row);
            AttachRows();
            _showingExamples = false;
            UpdateActionState();

            var errors = Rows.Count(x => x.HasError);
            var suggestions = Rows.Count(x => x.HasSuggestion);
            ProgressText.Text = $"共 {Rows.Count} 条｜错误 {errors} 条｜建议补全 {suggestions} 条";
            if (Rows.Count == 0) Notice.Warning("Blind Excel 中没有数据行。");
            else if (errors == 0) Notice.Success("Blind 基础校验完成，请审阅建议补全结果。");
            else Notice.Warning($"发现 {errors} 条错误；可导出审阅，或接受风险后直接强制生成。");
        }
        catch (Exception ex)
        {
            RunButton.IsEnabled = false;
            LogManager.WriteException(LogCategory.EditCheck, "editcheck.log", ex, "Blind Excel 读取或校验失败");
            Notice.Error($"Blind Excel 读取或校验失败：{ex.Message}");
        }
    }

    private void ExportReview_Click(object sender, RoutedEventArgs e)
    {
        if (_showingExamples || Rows.Count == 0) { Notice.Warning("请先上传并校验 Blind 文件。"); return; }
        try
        {
            var path = Path.Combine(_outputDirectory, "BlindValidation.xlsx");
            BlindExcelExporter.Export(Rows, path);
            Notice.Success($"审阅文件已导出：{path}");
            OpenOutputDirectory();
        }
        catch (Exception ex)
        {
            LogManager.WriteException(LogCategory.EditCheck, "editcheck.log", ex, "导出 Blind 审阅文件失败");
            Notice.Error($"导出 Blind 审阅文件失败：{ex.Message}");
        }
    }

    private void OpenOutputDirectory()
    {
        Directory.CreateDirectory(_outputDirectory);
        Process.Start(new ProcessStartInfo { FileName = _outputDirectory, UseShellExecute = true });
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        if (_showingExamples) { Notice.Warning("当前显示的是格式示例，请先上传 Blind Excel。"); return; }
        if (!EnsureSds() || Rows.Count == 0) return;

        var forcedErrors = Rows.Count(x => x.HasError);
        LogManager.BeginRun(LogCategory.EditCheck, "blind.log", forcedErrors > 0
            ? $"SetDataPointVisible 强制生成（{forcedErrors} 条错误）"
            : "SetDataPointVisible 批量生成");
        _isRunning = true;
        RunButton.IsEnabled = false;
        var output = new StringBuilder();
        var failed = 0;
        var index = 0;
        try
        {
            foreach (var row in Rows)
            {
                ProgressText.Text = $"正在处理 {++index}/{Rows.Count}：{row.BlindOid}";
                try
                {
                    output.AppendLine(EditCheckConverter.ConvertBlind(row));
                }
                catch (Exception ex)
                {
                    failed++;
                    row.HasError = true;
                    row.ValidationMessage = $"生成失败：{ex.Message}";
                    output.AppendLine($"|{row.BlindOid}|生成失败：{ex.Message}");
                    LogManager.WriteException(LogCategory.EditCheck, "blind.log", ex, row.BlindOid);
                }
                output.AppendLine("------------------------------------------------------------------------");
                ResultTextBox.Text = output.ToString();
            }
            ProgressText.Text = $"处理完成：{Rows.Count - failed} 成功，{failed} 失败";
        }
        finally
        {
            _isRunning = false;
            UpdateActionState();
        }
    }

    private void AttachRows() { foreach (var row in Rows) row.PropertyChanged += Row_PropertyChanged; }
    private void DetachRows() { foreach (var row in Rows) row.PropertyChanged -= Row_PropertyChanged; }
    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditCheckRow.HasError)) UpdateActionState();
    }

    private void UpdateActionState()
    {
        var errors = Rows.Count(x => x.HasError);
        RunButton.Content = errors > 0 ? $"强制生成 ECS（{errors} 条错误）" : "生成 ECS";
        if (errors > 0)
        {
            RunButton.Background = new SolidColorBrush(Color.FromRgb(220, 38, 38));
            RunButton.BorderBrush = new SolidColorBrush(Color.FromRgb(185, 28, 28));
            RunButton.Foreground = Brushes.White;
        }
        else
        {
            RunButton.ClearValue(Button.BackgroundProperty);
            RunButton.ClearValue(Button.BorderBrushProperty);
            RunButton.ClearValue(Button.ForegroundProperty);
        }
        RunButton.IsEnabled = !_isRunning && !_showingExamples && Rows.Count > 0;
    }

    private void ClearRows_Click(object sender, RoutedEventArgs e)
    {
        DetachRows();
        Rows.Clear();
        _showingExamples = false;
        UpdateActionState();
        ProgressText.Text = string.Empty;
    }

    private void ClearResult_Click(object sender, RoutedEventArgs e) => ResultTextBox.Clear();
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ResultTextBox.Text)) Clipboard.SetText(ResultTextBox.Text);
    }

    private void RefreshStatus() => SdsStatusText.Text = CurrentProject.Instance.HasSds
        ? $"当前 SDS：{CurrentProject.Instance.FormCount} Forms · {CurrentProject.Instance.FieldCount} Fields"
        : "请先从项目入口上传人工确认的 SDS。";

    private bool EnsureSds()
    {
        RefreshStatus();
        if (CurrentProject.Instance.HasSds) return true;
        Notice.Warning("请先从项目入口上传人工确认的 SDS。");
        return false;
    }
}
