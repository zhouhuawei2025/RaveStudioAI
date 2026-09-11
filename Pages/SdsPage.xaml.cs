using Microsoft.Win32;
using RaveStudioAI.Common;
using RaveStudioAI.Sds.Models;
using RaveStudioAI.Sds.Services;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Xceed.Words.NET;

namespace RaveStudioAI.Pages;

public partial class SdsPage : UserControl
{
    private readonly List<Xceed.Document.NET.Table> _tables = [];
    private readonly List<Form> _forms = [];
    private readonly List<DataDictionary> _dictionaries = [];
    private readonly List<List<Field>> _fieldGroups = [];
    private readonly string _outputDirectory = Path.Combine(AppContext.BaseDirectory, "Output", "Sds");
    private readonly string _codeListPrompt;
    private readonly string _normalPrompt;
    private readonly string _addPrompt;
    private readonly string _fixPrompt;
    private readonly string _labPrompt;

    public SdsPage()
    {
        InitializeComponent();
        Directory.CreateDirectory(_outputDirectory);

        var promptDirectory = Path.Combine(AppContext.BaseDirectory, "Prompts", "Sds");
        _codeListPrompt = File.ReadAllText(Path.Combine(promptDirectory, "codelist解析.txt"));
        _normalPrompt = File.ReadAllText(Path.Combine(promptDirectory, "普通表单的field解析.txt"));
        _addPrompt = File.ReadAllText(Path.Combine(promptDirectory, "ADD类型表单的field解析.txt"));
        _fixPrompt = File.ReadAllText(Path.Combine(promptDirectory, "FIX类型表单的field解析.txt"));
        _labPrompt = File.ReadAllText(Path.Combine(promptDirectory, "LAB类型表单的field解析.txt")) + LoadAnalytes();
    }

    private void Upload_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Word 文件|*.docx", Title = "选择需要解析的 CRF 文件", Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;

        ResetResults();
        SetBusy(true);
        try
        {
            var log = new StringBuilder();
            using var document = DocX.Load(dialog.FileName);
            foreach (var table in document.Tables)
            {
                if (table.Rows.Count == 0 || table.Rows[0].Cells.Count == 1) continue;
                log.AppendLine(FormatCheckUtils.CheckCrfFormat(table));
                _tables.Add(table);
            }

            var message = log.ToString();
            File.WriteAllText(LogManager.GetPath(LogCategory.Sds, "CRFchecklog.txt"), message);
            if (message.Contains("错误", StringComparison.OrdinalIgnoreCase))
            {
                LogText.Text = "CRF 格式检查发现问题，请修正后重新上传：\n\n" + message;
                return;
            }

            LogText.Text = $"CRF 格式检查通过，共读取 {_tables.Count} 张表单。\n可以继续提取 Forms、CodeLists 和 Fields。";
            FormButton.IsEnabled = CodeListButton.IsEnabled = FieldButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            _tables.Clear();
            LogText.Text = $"CRF 解析失败：{ex.Message}";
        }
        finally { SetBusy(false); }
    }

    private void ExtractForms_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureTables()) return;
        SetBusy(true);
        try
        {
            _forms.Clear();
            foreach (var table in _tables)
            {
                var form = CRFAnalyzeUtils.GetForm(table);
                if (form is not null) _forms.Add(form);
            }
            var path = OutputPath("Forms.xlsx");
            SdsExcelExporter.ExportForms(_forms, path);
            LogText.Text = $"成功提取 {_forms.Count} 个 Form。\n输出文件：{path}";
        }
        catch (Exception ex) { LogText.Text = $"Form 提取失败：{ex.Message}"; }
        finally { SetBusy(false); }
    }

    private async void ExtractCodeLists_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureTables()) return;
        SetBusy(true);
        var logPath = LogManager.GetPath(LogCategory.Sds, "codelistLog.txt");
        LogManager.BeginRun(LogCategory.Sds, "codelistLog.txt", "CodeList 解析");
        try
        {
            _dictionaries.Clear();
            var layouts = _tables.SelectMany(CRFAnalyzeUtils.GetDataDictionary).ToList();
            if (layouts.Count == 0)
            {
                LogText.Text = "未从 CRF 中提取到有效 CodeList。";
                return;
            }

            var batchSize = Math.Max(1, AIConfigStore.Current.BatchSize);
            var total = (int)Math.Ceiling((double)layouts.Count / batchSize);
            for (var index = 0; index < total; index++)
            {
                var batch = layouts.Skip(index * batchSize).Take(batchSize).ToList();
                LogText.Text = $"CodeList 共 {total} 批，正在处理第 {index + 1} 批...";
                var response = await CRFAnalyzeUtils.UsingAiTransferListToJson(_codeListPrompt, batch, logPath);
                if (!SafeJsonDeserializer.TryDeserializeFromAiText<DataDictionary>(response, out var result, logPath))
                    throw new InvalidDataException($"第 {index + 1} 批 AI 返回内容无法解析。");
                _dictionaries.AddRange(result);
            }

            var unique = _dictionaries.GroupBy(x => x.OID).Select(x => x.First()).ToList();
            var path = OutputPath("DataDictionary.xlsx");
            SdsExcelExporter.ExportDataDictionaries(unique, path);
            LogText.Text = $"成功提取 {unique.Count} 个 CodeList。\n输出文件：{path}";
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"{DateTime.Now}: {ex}\r\n");
            LogText.Text = $"CodeList 提取失败：{ex.Message}";
        }
        finally { SetBusy(false); }
    }

    private async void ExtractFields_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureTables()) return;
        SetBusy(true);
        var logPath = LogManager.GetPath(LogCategory.Sds, "fieldlistLog.txt");
        LogManager.BeginRun(LogCategory.Sds, "fieldlistLog.txt", "Field 解析");
        try
        {
            _fieldGroups.Clear();

            foreach (var table in _tables)
            {
                if (table.Rows[0].Cells.Count < 2) continue;
                var formText = TextExtractor.GetCellValue(table, 0, 0);
                if (!TextExtractor.ExtractNameAndOid(formText, out _, out var formOid)) continue;

                var crfType = table.Rows[0].Cells[1].Paragraphs[0].Text.Trim();
                var (rawFields, prompt) = GetFieldsAndPrompt(table, formOid, crfType);
                var batchSize = Math.Max(1, AIConfigStore.Current.BatchSize);
                var total = (int)Math.Ceiling((double)rawFields.Count / batchSize);
                var formFields = new List<Field>();

                for (var index = 0; index < total; index++)
                {
                    LogText.Text = $"正在提取 {formText}：第 {index + 1}/{total} 批...";
                    var batch = rawFields.Skip(index * batchSize).Take(batchSize).ToList();
                    var response = await CRFAnalyzeUtils.UsingAiTransferListToJson(prompt, batch, logPath);
                    if (!SafeJsonDeserializer.TryDeserializeFromAiText<Field>(response, out var result, logPath))
                        throw new InvalidDataException($"{formText} 第 {index + 1} 批 AI 返回内容无法解析。");
                    foreach (var field in result) field.UpdateField();
                    formFields.AddRange(result);
                }
                if (formFields.Count > 0) _fieldGroups.Add(formFields);
            }

            var path = OutputPath("Fields.xlsx");
            SdsExcelExporter.ExportFields(_fieldGroups, path);
            LogText.Text = $"成功提取 {_fieldGroups.Sum(x => x.Count)} 个 Field。\n输出文件：{path}";
        }
        catch (Exception ex)
        {
            if (_fieldGroups.Count > 0) SdsExcelExporter.ExportFields(_fieldGroups, OutputPath("Fields_partial.xlsx"));
            File.AppendAllText(logPath, $"{DateTime.Now}: {ex}\r\n");
            LogText.Text = $"Field 提取失败：{ex.Message}";
        }
        finally { SetBusy(false); }
    }

    private (List<string> Fields, string Prompt) GetFieldsAndPrompt(
        Xceed.Document.NET.Table table, string formOid, string crfType) => crfType switch
        {
            "ADD1" => (CRFAnalyzeUtils.GetFieldInADD1Form(table, formOid), _addPrompt),
            "ADD2" => (CRFAnalyzeUtils.GetFieldInADD2Form(table, formOid), _addPrompt),
            "FIX1" => (CRFAnalyzeUtils.GetFieldInFIX1Form(table, formOid), _fixPrompt),
            "FIX2" => (CRFAnalyzeUtils.GetFieldInFIX2Form(table, formOid), _fixPrompt),
            "LAB1" => (CRFAnalyzeUtils.GetFieldInLAB1Form(table, formOid), _labPrompt),
            "LAB2" => (CRFAnalyzeUtils.GetFieldInLAB2Form(table, formOid), _labPrompt),
            _ => (CRFAnalyzeUtils.GetFieldInNormalForm(table, formOid), _normalPrompt)
        };

    private string LoadAnalytes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Config", "analytes.json");
        var values = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? [];
        return "\n" + string.Join("\n", values) +
               "\n##现在请根据下面的信息直接返回json，以[开始，以]结尾，不要输出额外信息。";
    }

    private void OpenOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_outputDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", _outputDirectory) { UseShellExecute = true });
    }

    private bool EnsureTables()
    {
        if (_tables.Count > 0) return true;
        LogText.Text = "请先上传并通过 CRF 格式检查。";
        return false;
    }

    private void ResetResults()
    {
        _tables.Clear(); _forms.Clear(); _dictionaries.Clear(); _fieldGroups.Clear();
        FormButton.IsEnabled = CodeListButton.IsEnabled = FieldButton.IsEnabled = false;
    }

    private void SetBusy(bool busy)
    {
        ProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        UploadButton.IsEnabled = !busy;
        if (busy) FormButton.IsEnabled = CodeListButton.IsEnabled = FieldButton.IsEnabled = false;
        else if (_tables.Count > 0) FormButton.IsEnabled = CodeListButton.IsEnabled = FieldButton.IsEnabled = true;
    }

    private string OutputPath(string fileName) => Path.Combine(_outputDirectory, fileName);
}
