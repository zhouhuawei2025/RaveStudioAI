using HandyControl.Controls;
using HandyControl.Data;
using Microsoft.Win32;
using RaveStudioAI.Common;
using RaveStudioAI.Matrix.Models;
using RaveStudioAI.Matrix.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace RaveStudioAI.Pages;

public partial class MatrixPage : UserControl
{
    private readonly MatrixViewModel _viewModel = new();
    private readonly OidListParserService _oidListParser = new();
    private readonly ScenarioParserService _scenarioParser = new();
    private readonly MatrixBuildService _matrixBuilder = new();
    private readonly MatrixExcelExporter _exporter = new();
    private MatrixWorkbook? _currentWorkbook;

    public MatrixPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void MatrixPage_Loaded(object sender, RoutedEventArgs e)
    {
        var currentPath = CurrentProject.Instance.SdsPath;
        if (!string.IsNullOrWhiteSpace(currentPath) &&
            !currentPath.Equals(_viewModel.TemplatePath, StringComparison.OrdinalIgnoreCase))
        {
            LoadTemplate(currentPath, false);
        }
    }

    private void UploadTemplate_Click(object sender, RoutedEventArgs e)
    {
        var path = PickFile("OID 模板 Excel|*.xlsx;*.xlsm|Excel|*.xlsx;*.xlsm");
        if (path is null) return;

        LoadTemplate(path, true);
    }

    private void UseCurrentSds_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CurrentProject.Instance.SdsPath))
        {
            Growl.Warning(new GrowlInfo { Message = "请先在项目入口上传已确认的 SDS。", WaitTime = 3 });
            return;
        }
        LoadTemplate(CurrentProject.Instance.SdsPath, true);
    }

    private void LoadTemplate(string path, bool showMessage)
    {
        SetEnabled(false);
        try
        {
            var template = _oidListParser.ParseTemplateWorkbook(path);
            _viewModel.ReplaceForms(template.FormOids);
            _viewModel.ReplaceVisits(template.VisitOids);
            _viewModel.TemplatePath = path;
            if (showMessage)
            {
                Growl.Success(new GrowlInfo
                {
                    Message = $"已读取 {template.FormOids.Count} 个 Form OID，{template.VisitOids.Count} 个 Visit OID。",
                    WaitTime = 2
                });
            }
            RebuildIfReady();
        }
        catch (Exception ex) { ShowError("读取 OID 模板失败", ex); }
        finally { SetEnabled(true); }
    }

    private void UploadScenario_Click(object sender, RoutedEventArgs e)
    {
        var path = PickFile("Scenario Excel|*.xlsx;*.xlsm|Excel|*.xlsx;*.xlsm");
        if (path is null) return;

        SetEnabled(false);
        try
        {
            _viewModel.ScenarioPath = path;
            RebuildIfReady();
        }
        finally { SetEnabled(true); }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_currentWorkbook is null || _currentWorkbook.Matrices.Count == 0)
        {
            Growl.Warning(new GrowlInfo { Message = "请先上传 OID 模板和 Matrices 文件。", WaitTime = 2 });
            return;
        }

        var dialog = new SaveFileDialog { Filter = "Excel Workbook|*.xlsx", FileName = "rave_matrix.xlsx" };
        if (dialog.ShowDialog() != true) return;

        SetEnabled(false);
        try
        {
            _exporter.Export(_currentWorkbook, dialog.FileName);
            Growl.Success(new GrowlInfo { Message = "Matrix Excel 已导出。", WaitTime = 2 });
        }
        catch (Exception ex) { ShowError("导出 Matrix Excel 失败", ex); }
        finally { SetEnabled(true); }
    }

    private void MatrixSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RenderPreview(_viewModel.SelectedMatrix);

    private void RebuildIfReady()
    {
        if (_viewModel.FormOids.Count == 0 || _viewModel.VisitOids.Count == 0 ||
            string.IsNullOrWhiteSpace(_viewModel.ScenarioPath)) return;

        try
        {
            var rows = _scenarioParser.Parse(_viewModel.ScenarioPath);
            _currentWorkbook = _matrixBuilder.Build(
                _viewModel.FormOids.ToList(), _viewModel.VisitOids.ToList(), rows);
            _viewModel.ReplaceWorkbook(_currentWorkbook);
            RenderPreview(_viewModel.SelectedMatrix);
            Growl.Success(new GrowlInfo { Message = $"已解析 {_currentWorkbook.Matrices.Count} 个 Matrix。", WaitTime = 2 });
        }
        catch (Exception ex) { ShowError("解析 Matrices 文件失败", ex); }
    }

    private void RenderPreview(MatrixDefinition? matrix)
    {
        MatrixDataGrid.Columns.Clear();
        MatrixDataGrid.ItemsSource = null;
        if (matrix is null) return;

        MatrixDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "FormOID", Binding = new Binding(nameof(MatrixPreviewRow.FormOid)), Width = 120
        });
        foreach (var visitOid in matrix.VisitOids)
        {
            MatrixDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = visitOid, Binding = new Binding($"Cells[{visitOid}]"), Width = 80
            });
        }

        MatrixDataGrid.ItemsSource = matrix.FormOids.Select(formOid =>
        {
            var row = new MatrixPreviewRow { FormOid = formOid };
            foreach (var visitOid in matrix.VisitOids)
                row.Cells[visitOid] = matrix.MarkedCells.Contains(new MatrixCellKey(visitOid, formOid)) ? "X" : string.Empty;
            return row;
        }).ToList();
    }

    private void SetEnabled(bool enabled)
    {
        UploadTemplateButton.IsEnabled = enabled;
        UploadScenarioButton.IsEnabled = enabled;
        ExportButton.IsEnabled = enabled;
    }

    private static string? PickFile(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter, Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static void ShowError(string title, Exception ex)
    {
        LogManager.WriteException(LogCategory.Matrix, "matrix.log", ex, title);
        Notice.Error($"{title}：{ex.Message}");
    }
}
