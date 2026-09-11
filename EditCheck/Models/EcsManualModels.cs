using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using RaveStudioAI.Common;

namespace RaveStudioAI.EditCheck.Models;

public sealed class EcsConditionRow : INotifyPropertyChanged
{
    private string _type = "Data Value";
    private string _logicalRecordPosition = "None";
    private string _staticValue = string.Empty;
    private string _dataFormat = "StandardValue";
    private string _formOid = string.Empty;
    private string _fieldOid = string.Empty;
    private string _variableOid = string.Empty;
    private string _recordPosition = string.Empty;
    public string Type
    {
        get => _type;
        set { _type = value; Changed(); Changed(nameof(IsDataValue)); Changed(nameof(IsConstant)); Changed(nameof(IsFunction)); }
    }
    public bool IsDataValue => Type.Equals("Data Value", StringComparison.OrdinalIgnoreCase);
    public bool IsConstant => Type.Equals("Constant", StringComparison.OrdinalIgnoreCase);
    public bool IsFunction => Type.Equals("Check Function", StringComparison.OrdinalIgnoreCase);
    public string CheckFunction { get; set; } = string.Empty;
    public string StaticValue
    {
        get => _staticValue;
        set
        {
            _staticValue = value;
            if (IsConstant) DataFormat = InferFormat(value);
            Changed();
        }
    }
    public string DataFormat { get => _dataFormat; set { _dataFormat = value; Changed(); } }
    public string VariableOid { get => _variableOid; set { _variableOid = value; Changed(); } }
    public string FolderOid { get; set; } = string.Empty;
    public string FormOid
    {
        get => _formOid;
        set
        {
            if (string.Equals(_formOid, value, StringComparison.OrdinalIgnoreCase)) return;
            _formOid = value ?? string.Empty;
            _fieldOid = string.Empty;
            VariableOid = string.Empty;
            RecordPosition = string.Empty;
            Changed(); Changed(nameof(FieldOid)); Changed(nameof(AvailableFieldOids));
        }
    }
    public IEnumerable<string> AvailableFieldOids => CurrentProject.Instance.Fields
        .Where(x => x.FormOid.Equals(FormOid, StringComparison.OrdinalIgnoreCase))
        .Select(x => x.FieldOid).Distinct(StringComparer.OrdinalIgnoreCase);
    public string FieldOid
    {
        get => _fieldOid;
        set
        {
            _fieldOid = value ?? string.Empty;
            var field = CurrentProject.Instance.Fields.FirstOrDefault(x =>
                x.FormOid.Equals(FormOid, StringComparison.OrdinalIgnoreCase) &&
                x.FieldOid.Equals(_fieldOid, StringComparison.OrdinalIgnoreCase));
            VariableOid = field?.VariableOid ?? string.Empty;
            RecordPosition = field is null ? string.Empty : field.IsLog ? string.Empty : "0";
            Changed();
        }
    }
    public string RecordPosition { get => _recordPosition; set { _recordPosition = value; Changed(); } }
    public string CustomFunction { get; set; } = string.Empty;
    public string LogicalRecordPosition
    {
        get => _logicalRecordPosition;
        set
        {
            _logicalRecordPosition = value;
            if (!ShowScope) Scope = string.Empty;
            if (!ShowOrderBy) OrderBy = string.Empty;
            Changed(); Changed(nameof(ShowScope)); Changed(nameof(ShowOrderBy)); Changed(nameof(Scope)); Changed(nameof(OrderBy));
        }
    }
    public bool ShowScope => !string.IsNullOrWhiteSpace(LogicalRecordPosition) &&
                             !LogicalRecordPosition.Equals("None", StringComparison.OrdinalIgnoreCase);
    public bool ShowOrderBy => LogicalRecordPosition.Equals("Previous", StringComparison.OrdinalIgnoreCase) ||
                               LogicalRecordPosition.Equals("Next", StringComparison.OrdinalIgnoreCase);
    public string Scope { get; set; } = string.Empty;
    public string OrderBy { get; set; } = string.Empty;
    public string FormRepeatNumber { get; set; } = string.Empty;
    public string FolderRepeatNumber { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static string InferFormat(string value)
    {
        if (value == "00:00") return "HH:nn";
        if (int.TryParse(value, out var integer)) return integer.ToString().Length.ToString();
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var number))
        {
            var normalized = number.ToString(CultureInfo.InvariantCulture);
            var decimalDigits = normalized.Contains('.') ? normalized.Split('.')[1].Length : 0;
            return $"{normalized.Length}.{decimalDigits}";
        }
        return string.IsNullOrEmpty(value) ? string.Empty : $"${value.Length}";
    }
}

public sealed class EcsActionRow : INotifyPropertyChanged
{
    private string _logicalRecordPosition = "None";
    private string _actionType = "IsPresent";
    private string _actionOptions = "0";
    private string _formOid = string.Empty;
    private string _fieldOid = string.Empty;
    private string _variableOid = string.Empty;
    private string _recordPosition = string.Empty;
    public string FolderOid { get; set; } = string.Empty;
    public string FormOid
    {
        get => _formOid;
        set
        {
            if (string.Equals(_formOid, value, StringComparison.OrdinalIgnoreCase)) return;
            _formOid = value ?? string.Empty;
            _fieldOid = string.Empty;
            VariableOid = string.Empty;
            RecordPosition = string.Empty;
            Changed(); Changed(nameof(FieldOid)); Changed(nameof(AvailableFieldOids));
        }
    }
    public IEnumerable<string> AvailableFieldOids => CurrentProject.Instance.Fields
        .Where(x => x.FormOid.Equals(FormOid, StringComparison.OrdinalIgnoreCase))
        .Select(x => x.FieldOid).Distinct(StringComparer.OrdinalIgnoreCase);
    public string FieldOid
    {
        get => _fieldOid;
        set
        {
            _fieldOid = value ?? string.Empty;
            var field = CurrentProject.Instance.Fields.FirstOrDefault(x =>
                x.FormOid.Equals(FormOid, StringComparison.OrdinalIgnoreCase) &&
                x.FieldOid.Equals(_fieldOid, StringComparison.OrdinalIgnoreCase));
            VariableOid = field?.VariableOid ?? string.Empty;
            RecordPosition = field is null ? string.Empty : field.IsLog ? string.Empty : "0";
            Changed();
        }
    }
    public string VariableOid { get => _variableOid; set { _variableOid = value; Changed(); } }
    public string RecordPosition { get => _recordPosition; set { _recordPosition = value; Changed(); } }
    public string FormRepeatNumber { get; set; } = string.Empty;
    public string FolderRepeatNumber { get; set; } = string.Empty;
    public string LogicalRecordPosition
    {
        get => _logicalRecordPosition;
        set
        {
            _logicalRecordPosition = value;
            if (!ShowScope) Scope = string.Empty;
            if (!ShowOrderBy) OrderBy = string.Empty;
            Changed(); Changed(nameof(ShowScope)); Changed(nameof(ShowOrderBy)); Changed(nameof(Scope)); Changed(nameof(OrderBy));
        }
    }
    public bool ShowScope => !string.IsNullOrWhiteSpace(LogicalRecordPosition) &&
                             !LogicalRecordPosition.Equals("None", StringComparison.OrdinalIgnoreCase);
    public bool ShowOrderBy => LogicalRecordPosition.Equals("Previous", StringComparison.OrdinalIgnoreCase) ||
                               LogicalRecordPosition.Equals("Next", StringComparison.OrdinalIgnoreCase);
    public string Scope { get; set; } = string.Empty;
    public string OrderBy { get; set; } = string.Empty;
    public string ActionType
    {
        get => _actionType;
        set
        {
            _actionType = value;
            ActionOptions = value switch
            {
                "IsPresent" => "0",
                "SetDataPointVisible" => "TRUE,FALSE",
                "OpenQuery" => "Site from System,RequiresResponse,RequiresManualClose",
                _ => string.Empty
            };
            Changed();
        }
    }
    public string ActionString { get; set; } = string.Empty;
    public string ActionOptions { get => _actionOptions; set { _actionOptions = value; Changed(); } }
    public string ActionScript { get; set; } = string.Empty;
    public string ActionHeader { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
