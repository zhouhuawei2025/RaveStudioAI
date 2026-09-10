using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RaveStudioAI.EditCheck.Models;

public abstract class EditCheckRow : INotifyPropertyChanged
{
    private bool _hasError;
    public bool HasError { get => _hasError; set { _hasError = value; OnPropertyChanged(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class QueryRow : EditCheckRow
{
    public string QueryOid { get; set; } = string.Empty;
    public string FolderOid { get; set; } = string.Empty;
    public string FormOid { get; set; } = string.Empty;
    public string FieldOid { get; set; } = string.Empty;
    public string LogicText { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
}

public sealed class BlindRow : EditCheckRow
{
    public string BlindOid { get; set; } = string.Empty;
    public string FolderOid { get; set; } = string.Empty;
    public string FormOid { get; set; } = string.Empty;
    public string FieldOid { get; set; } = string.Empty;
    public string LogicText { get; set; } = string.Empty;
}
