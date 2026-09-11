namespace RaveStudioAI.Rws.Models;

public sealed class FormPreviewResult
{
    public string FormOID { get; init; } = string.Empty;
    public int RowCount { get; init; }
    public string Header => $"{FormOID} ({RowCount})";
}
