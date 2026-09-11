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
    private string _normalizedLogicText = string.Empty;
    private string _validationMessage = string.Empty;
    public string QueryOid { get; set; } = string.Empty;
    public string FolderOid { get; set; } = string.Empty;
    public string FormOid { get; set; } = string.Empty;
    public string FieldOid { get; set; } = string.Empty;
    public string LogicText { get; set; } = string.Empty;
    public string NormalizedLogicText { get => _normalizedLogicText; set { _normalizedLogicText = value; OnPropertyChanged(); } }
    public string ValidationMessage { get => _validationMessage; set { _validationMessage = value; OnPropertyChanged(); } }
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
