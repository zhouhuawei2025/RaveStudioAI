using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RaveStudioAI.Matrix.Models;

public readonly record struct MatrixCellKey(string VisitOid, string FormOid);

public sealed class MatrixDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Oid { get; set; } = string.Empty;
    public List<string> VisitOids { get; set; } = [];
    public List<string> FormOids { get; set; } = [];
    public HashSet<MatrixCellKey> MarkedCells { get; set; } = [];
    public string DisplayName => string.IsNullOrWhiteSpace(Oid) ? Name : $"{Name} ({Oid})";
    public int MarkedCellCount => MarkedCells.Count;
}

public sealed class MatrixPreviewRow
{
    public string FormOid { get; set; } = string.Empty;
    public Dictionary<string, string> Cells { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MatrixScenarioRow
{
    public int SourceRowNumber { get; set; }
    public string MatrixName { get; set; } = string.Empty;
    public string MatrixOid { get; set; } = string.Empty;
    public string VisitOid { get; set; } = string.Empty;
    public string FormOid { get; set; } = string.Empty;
}

public sealed class MatrixValidationIssue
{
    public string Level { get; set; } = "Warning";
    public int SourceRowNumber { get; set; }
    public string Message { get; set; } = string.Empty;
    public string DisplayText => SourceRowNumber > 0
        ? $"[{Level}] 第 {SourceRowNumber} 行：{Message}"
        : $"[{Level}] {Message}";
}

public sealed class MatrixWorkbook
{
    public List<string> VisitOids { get; set; } = [];
    public List<string> FormOids { get; set; } = [];
    public List<MatrixDefinition> Matrices { get; set; } = [];
    public List<MatrixValidationIssue> Issues { get; set; } = [];
}

public sealed class OidTemplate
{
    public List<string> VisitOids { get; set; } = [];
    public List<string> FormOids { get; set; } = [];
}

public sealed class MatrixViewModel : INotifyPropertyChanged
{
    public ObservableCollection<string> FormOids { get; } = [];
    public ObservableCollection<string> VisitOids { get; } = [];
    public ObservableCollection<MatrixDefinition> Matrices { get; } = [];
    public ObservableCollection<MatrixValidationIssue> Issues { get; } = [];

    private MatrixDefinition? _selectedMatrix;
    public MatrixDefinition? SelectedMatrix
    {
        get => _selectedMatrix;
        set
        {
            _selectedMatrix = value;
            OnPropertyChanged(nameof(SelectedMatrix));
            OnPropertyChanged(nameof(SelectedMatrixSummary));
        }
    }

    private string _templatePath = string.Empty;
    public string TemplatePath
    {
        get => _templatePath;
        set { _templatePath = value; OnPropertyChanged(nameof(TemplatePath)); }
    }

    private string _scenarioPath = string.Empty;
    public string ScenarioPath
    {
        get => _scenarioPath;
        set { _scenarioPath = value; OnPropertyChanged(nameof(ScenarioPath)); }
    }

    public string SelectedMatrixSummary => SelectedMatrix is null
        ? "未选择 Matrix"
        : $"{SelectedMatrix.FormOids.Count} 个表单，{SelectedMatrix.VisitOids.Count} 个访视，{SelectedMatrix.MarkedCellCount} 个 X";

    public void ReplaceForms(IEnumerable<string> values)
    {
        FormOids.Clear();
        foreach (var value in values) FormOids.Add(value);
    }

    public void ReplaceVisits(IEnumerable<string> values)
    {
        VisitOids.Clear();
        foreach (var value in values) VisitOids.Add(value);
    }

    public void ReplaceWorkbook(MatrixWorkbook workbook)
    {
        Matrices.Clear();
        Issues.Clear();
        foreach (var matrix in workbook.Matrices) Matrices.Add(matrix);
        foreach (var issue in workbook.Issues) Issues.Add(issue);
        SelectedMatrix = Matrices.FirstOrDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
