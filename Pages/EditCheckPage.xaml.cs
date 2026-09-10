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
    public ObservableCollection<EcsConditionRow> EcsConditions { get; } = [];
    public ObservableCollection<EcsActionRow> EcsActions { get; } = [];
    public IEnumerable<string> EcsFolders => CurrentProject.Instance.Folders.Select(x => x.Oid);
    public IEnumerable<string> EcsForms => CurrentProject.Instance.Forms.Select(x => x.Oid);
    public IEnumerable<string> EcsFields => CurrentProject.Instance.Fields.Select(x => x.FieldOid).Distinct(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> EcsFunctions { get; } = ["Add", "AddDay", "AddMin", "AddMonth", "And", "Contains",
        "CustomFunction", "IsActive", "IsEmpty", "IsEqualTo", "IsGreaterThan", "IsGreaterThanOrEqualTo",
        "IsLessThan", "IsLessThanOrEqualTo", "IsNonConformant", "IsNotEmpty", "IsNotEqualTo", "IsPresent", "Not", "Or", "TimeSpan"];
    public IReadOnlyList<string> EcsActionTypes { get; } = ["AddForm", "CustomFunction", "IsPresent", "MrgMatrix", "OpenQuery",
        "SetDataPointVisible", "SetDynamicSearchList", "SetSiteInformation", "SetSubjectName", "SetSubjectStatus", "UniqueSubjectName"];
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
    private void CopyEcsResult_Click(object sender, RoutedEventArgs e) => Copy(EcsResultTextBox.Text);

    private void AddEcsDataPoint_Click(object sender, RoutedEventArgs e) =>
        EcsConditions.Add(new EcsConditionRow { Type = "Data Value", DataFormat = "StandardValue" });

    private void AddEcsConstant_Click(object sender, RoutedEventArgs e) =>
        EcsConditions.Add(new EcsConditionRow { Type = "Constant", DataFormat = string.Empty });

    private void AddEcsFunction_Click(object sender, RoutedEventArgs e) =>
        EcsConditions.Add(new EcsConditionRow { Type = "Check Function" });

    private void AddEcsFieldConstantPreset_Click(object sender, RoutedEventArgs e)
    {
        AddEcsDataPoint_Click(sender, e);
        AddEcsConstant_Click(sender, e);
        EcsConditions.Add(new EcsConditionRow { Type = "Check Function", CheckFunction = "IsEqualTo" });
    }

    private void AddEcsFieldFieldPreset_Click(object sender, RoutedEventArgs e)
    {
        AddEcsDataPoint_Click(sender, e);
        AddEcsDataPoint_Click(sender, e);
        EcsConditions.Add(new EcsConditionRow { Type = "Check Function", CheckFunction = "IsEqualTo" });
    }

    private void RemoveEcsCondition_Click(object sender, RoutedEventArgs e)
    {
        if (EcsConditions.Count > 0) EcsConditions.RemoveAt(EcsConditions.Count - 1);
    }

    private void AddEcsAction_Click(object sender, RoutedEventArgs e) => EcsActions.Add(new EcsActionRow());

    private void RemoveEcsAction_Click(object sender, RoutedEventArgs e)
    {
        if (EcsActions.Count > 0) EcsActions.RemoveAt(EcsActions.Count - 1);
    }

    private void ClearEcs_Click(object sender, RoutedEventArgs e)
    {
        EcsConditions.Clear();
        EcsActions.Clear();
        EcsResultTextBox.Clear();
    }

    private void GenerateEcs_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureSds()) return;
        EcsConditionsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        EcsConditionsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        EcsActionsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        EcsActionsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (EcsConditions.Count == 0 || EcsActions.Count == 0)
        {
            Notice.Warning("请至少添加一个条件步骤和一个动作。");
            return;
        }

        try
        {
            var lines = EcsConditions.Select(BuildConditionLine).ToList();
            lines.Add(string.Empty);
            lines.AddRange(EcsActions.Select(BuildActionLine));
            EcsResultTextBox.Text = string.Join(Environment.NewLine, lines);
            Notice.Success("ECS 已生成。");
        }
        catch (Exception ex)
        {
            LogManager.WriteException(LogCategory.EditCheck, "ecs-manual.log", ex, "ECS 手动生成失败");
            Notice.Error($"ECS 生成失败：{ex.Message}");
        }
    }

    private static string BuildConditionLine(EcsConditionRow row)
    {
        var type = row.Type.Trim();
        if (type.Equals("Data Value", StringComparison.OrdinalIgnoreCase))
            (row.VariableOid, row.RecordPosition) = EnrichDataPoint(
                row.FormOid, row.FieldOid, row.VariableOid, row.RecordPosition);
        if (type.Equals("Constant", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(row.DataFormat))
            row.DataFormat = InferConstantFormat(row.StaticValue);

        var values = type.ToLowerInvariant() switch
        {
            "data value" => new[] { "", "", row.DataFormat, row.VariableOid, row.FolderOid, row.FormOid, row.FieldOid,
                row.RecordPosition, row.CustomFunction, row.LogicalRecordPosition, row.Scope, row.OrderBy,
                row.FormRepeatNumber, row.FolderRepeatNumber },
            "constant" => new[] { "", row.StaticValue, row.DataFormat, "", "", "", "", "", "", "", "", "", "", "" },
            "check function" => new[] { Required(row.CheckFunction, "Check Function"), "", "", "", "", "", "", "", "", "", "", "", "", "" },
            _ => throw new InvalidDataException($"无法识别条件类型：{row.Type}")
        };
        return string.Join("|", values);
    }

    private static string BuildActionLine(EcsActionRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.FormOid) || !string.IsNullOrWhiteSpace(row.FieldOid))
            (row.VariableOid, row.RecordPosition) = EnrichDataPoint(
                row.FormOid, row.FieldOid, row.VariableOid, row.RecordPosition);
        var actionType = Required(row.ActionType, "ActionType");
        if (string.IsNullOrWhiteSpace(row.ActionOptions)) row.ActionOptions = actionType switch
        {
            "IsPresent" => "0",
            "SetDataPointVisible" => "TRUE,FALSE",
            "OpenQuery" => "Site from System,RequiresResponse,RequiresManualClose",
            _ => string.Empty
        };
        return string.Join("|", new[] { row.FolderOid, row.FormOid, row.FieldOid, row.VariableOid,
            row.RecordPosition, row.FormRepeatNumber, row.FolderRepeatNumber, row.LogicalRecordPosition,
            row.Scope, row.OrderBy, actionType, row.ActionString, row.ActionOptions, row.ActionScript, row.ActionHeader });
    }

    private static (string VariableOid, string RecordPosition) EnrichDataPoint(
        string formOid, string fieldOid, string variableOid, string recordPosition)
    {
        Required(formOid, "FormOID");
        Required(fieldOid, "FieldOID");
        var field = CurrentProject.Instance.Fields.FirstOrDefault(x =>
            x.FormOid.Equals(formOid, StringComparison.OrdinalIgnoreCase) &&
            x.FieldOid.Equals(fieldOid, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"SDS 中找不到字段 {formOid}.{fieldOid}。");
        if (string.IsNullOrWhiteSpace(variableOid)) variableOid = field.VariableOid;
        if (string.IsNullOrWhiteSpace(recordPosition)) recordPosition = field.IsLog ? string.Empty : "0";
        return (variableOid, recordPosition);
    }

    private static string InferConstantFormat(string value)
    {
        if (value == "00:00") return "HH:nn";
        if (int.TryParse(value, out var integer)) return integer.ToString().Length.ToString();
        if (double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out _))
            return $"{value.Length}.{(value.Contains('.') ? value[(value.IndexOf('.') + 1)..].Length : 0)}";
        return string.IsNullOrEmpty(value) ? string.Empty : $"${value.Length}";
    }

    private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value)
        ? value.Trim()
        : throw new InvalidDataException($"{name} 不能为空。");

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
