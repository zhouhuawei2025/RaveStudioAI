namespace RaveStudioAI.EditCheck.Models;

public sealed class EcsConditionRow
{
    public string Type { get; set; } = "Data Value";
    public string CheckFunction { get; set; } = string.Empty;
    public string StaticValue { get; set; } = string.Empty;
    public string DataFormat { get; set; } = "StandardValue";
    public string VariableOid { get; set; } = string.Empty;
    public string FolderOid { get; set; } = string.Empty;
    public string FormOid { get; set; } = string.Empty;
    public string FieldOid { get; set; } = string.Empty;
    public string RecordPosition { get; set; } = string.Empty;
    public string CustomFunction { get; set; } = string.Empty;
    public string LogicalRecordPosition { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string OrderBy { get; set; } = string.Empty;
    public string FormRepeatNumber { get; set; } = string.Empty;
    public string FolderRepeatNumber { get; set; } = string.Empty;
}

public sealed class EcsActionRow
{
    public string FolderOid { get; set; } = string.Empty;
    public string FormOid { get; set; } = string.Empty;
    public string FieldOid { get; set; } = string.Empty;
    public string VariableOid { get; set; } = string.Empty;
    public string RecordPosition { get; set; } = string.Empty;
    public string FormRepeatNumber { get; set; } = string.Empty;
    public string FolderRepeatNumber { get; set; } = string.Empty;
    public string LogicalRecordPosition { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string OrderBy { get; set; } = string.Empty;
    public string ActionType { get; set; } = "IsPresent";
    public string ActionString { get; set; } = string.Empty;
    public string ActionOptions { get; set; } = "0";
    public string ActionScript { get; set; } = string.Empty;
    public string ActionHeader { get; set; } = string.Empty;
}
