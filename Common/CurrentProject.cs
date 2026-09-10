using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RaveStudioAI.Common;

public sealed class CurrentProject : INotifyPropertyChanged
{
    public static CurrentProject Instance { get; } = new();

    private string _sdsPath = string.Empty;
    private DateTime? _loadedAt;

    public string SdsPath
    {
        get => _sdsPath;
        private set { _sdsPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSds)); }
    }

    public DateTime? LoadedAt
    {
        get => _loadedAt;
        private set { _loadedAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(LoadedAtText)); }
    }

    public bool HasSds => !string.IsNullOrWhiteSpace(SdsPath);
    public string LoadedAtText => LoadedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "尚未上传";
    public List<ProjectForm> Forms { get; private set; } = [];
    public List<ProjectField> Fields { get; private set; } = [];
    public List<ProjectFolder> Folders { get; private set; } = [];
    public int FormCount => Forms.Count;
    public int FieldCount => Fields.Count;
    public int FolderCount => Folders.Count;

    public void Replace(string path, SdsReadResult result)
    {
        Forms = result.Forms;
        Fields = result.Fields;
        Folders = result.Folders;
        SdsPath = path;
        LoadedAt = DateTime.Now;
        OnPropertyChanged(nameof(Forms));
        OnPropertyChanged(nameof(Fields));
        OnPropertyChanged(nameof(Folders));
        OnPropertyChanged(nameof(FormCount));
        OnPropertyChanged(nameof(FieldCount));
        OnPropertyChanged(nameof(FolderCount));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class ProjectForm
{
    public string Oid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> FolderOids { get; set; } = [];
}

public sealed class ProjectField
{
    public string FormOid { get; set; } = string.Empty;
    public string FieldOid { get; set; } = string.Empty;
    public string VariableOid { get; set; } = string.Empty;
    public bool IsLog { get; set; }
}

public sealed class ProjectFolder
{
    public string Oid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ParentFolderOid { get; set; } = string.Empty;
    public bool IsReusable { get; set; }
}

public sealed class SdsReadResult
{
    public List<ProjectForm> Forms { get; set; } = [];
    public List<ProjectField> Fields { get; set; } = [];
    public List<ProjectFolder> Folders { get; set; } = [];
}
