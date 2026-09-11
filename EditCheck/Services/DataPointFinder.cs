using RaveStudioAI.Common;

namespace RaveStudioAI.EditCheck.Services;

public sealed class DataPointFinder
{
    public static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "=", "and", "or", ">", "<", "＞", "＜", "≥", "≤", ">=", "<=", "＞=", "＜=", "<>", "!=", "＜＞",
        "Add", "AddDay", "AddMin", "AddHour", "AddMonth", "IsEmpty", "IsNotEmpty", "TimeSpan", "IsPresent"
    };

    public ProjectField? CurrentField { get; private set; }

    public bool TryFindField(string formOid, string fieldOid)
    {
        CurrentField = CurrentProject.Instance.Fields.FirstOrDefault(x =>
            x.FormOid.Equals(formOid, StringComparison.OrdinalIgnoreCase) &&
            x.FieldOid.Equals(fieldOid, StringComparison.OrdinalIgnoreCase));
        return CurrentField is not null;
    }

    public string NormalizeFolder(string value)
    {
        var folder = CurrentProject.Instance.Folders.FirstOrDefault(x =>
            x.Oid.Equals(value, StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
        return folder?.Oid ?? value;
    }

    public bool IsReusableFolder(string oid) => CurrentProject.Instance.Folders.Any(x =>
        x.Oid.Equals(oid, StringComparison.OrdinalIgnoreCase) && x.IsReusable);

    public bool TryGetUniqueFolderOid(string formOid, out string uniqueFolderOid)
    {
        uniqueFolderOid = string.Empty;
        var form = CurrentProject.Instance.Forms.FirstOrDefault(x =>
            x.Oid.Equals(formOid, StringComparison.OrdinalIgnoreCase));
        if (form?.FolderOids.Count != 1) return false;

        uniqueFolderOid = form.FolderOids[0];
        return true;
    }
}
