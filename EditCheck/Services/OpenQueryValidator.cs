using RaveStudioAI.Common;
using RaveStudioAI.EditCheck.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace RaveStudioAI.EditCheck.Services;

public static class OpenQueryValidator
{
    private static readonly Regex FullIn = new(@"\b([A-Z][A-Z0-9_-]*)\s*\.\s*([A-Z][A-Z0-9_-]*)\s*\(\s*IN\s+([A-Z][A-Z0-9_-]*)\s*\)", RegexOptions.IgnoreCase);
    private static readonly Regex FieldIn = new(@"\b([A-Z][A-Z0-9_-]*)\s*\(\s*IN\s+([A-Z][A-Z0-9_-]*)\s*\)", RegexOptions.IgnoreCase);
    private static readonly Regex DataPointToken = new(@"\b[A-Z][A-Z0-9_-]*(?:\.[A-Z][A-Z0-9_-]*){0,2}\b", RegexOptions.IgnoreCase);
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
        { "AND", "OR", "NOT", "NULL", "TRUE", "FALSE", "IS", "EMPTY" };

    public static bool Validate(QueryRow row)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(row.QueryOid)) errors.Add("QueryOID 不能为空");
        if (string.IsNullOrWhiteSpace(row.FolderOid)) errors.Add("FolderOID 不能为空");
        if (string.IsNullOrWhiteSpace(row.FormOid)) errors.Add("FormOID 不能为空");
        if (string.IsNullOrWhiteSpace(row.FieldOid)) errors.Add("FieldOID 不能为空");
        if (string.IsNullOrWhiteSpace(row.LogicText)) errors.Add("LogicText 不能为空");
        if (string.IsNullOrWhiteSpace(row.MessageText)) errors.Add("Message 不能为空");
        var targetFolder = NormalizeFolder(row.FolderOid);
        var targetForm = row.FormOid.Trim();
        var targetField = row.FieldOid.Trim();
        ValidateTarget(targetFolder, targetForm, targetField, errors);

        var normalized = NormalizeOperators(row.LogicText);
        normalized = FullIn.Replace(normalized, match =>
            ResolveAndCollect($"{match.Groups[3].Value}.{match.Groups[1].Value}.{match.Groups[2].Value}", targetFolder, targetForm, targetField, errors));
        normalized = FieldIn.Replace(normalized, match =>
            ResolveAndCollect($"{match.Groups[2].Value}.{targetForm}.{match.Groups[1].Value}", targetFolder, targetForm, targetField, errors));
        normalized = DataPointToken.Replace(normalized, match =>
        {
            var token = match.Value;
            if (Keywords.Contains(token) || IsTimeUnit(token)) return token;
            return ResolveAndCollect(token, targetFolder, targetForm, targetField, errors);
        });

        ValidateSyntax(normalized, errors);
        row.NormalizedLogicText = normalized;
        row.ValidationMessage = string.Join("；", errors.Distinct(StringComparer.OrdinalIgnoreCase));
        row.HasError = errors.Count > 0;
        return !row.HasError;
    }

    public static void AddError(QueryRow row, string message)
    {
        row.ValidationMessage = string.IsNullOrWhiteSpace(row.ValidationMessage)
            ? message : $"{row.ValidationMessage}；{message}";
        row.HasError = true;
    }

    private static string NormalizeOperators(string text)
    {
        var result = (text ?? string.Empty).Replace('（', '(').Replace('）', ')').Replace('，', ',').Replace('–', '-').Replace('—', '-');
        result = Regex.Replace(result, @"\bis\s+not\s+empty\b", "<> null", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bis\s+empty\b", "= null", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\s*\.\s*", ".");
        return Regex.Replace(result, @"\s+", " ").Trim();
    }

    private static string ResolveAndCollect(string token, string targetFolder, string targetForm, string targetField, List<string> errors)
    {
        try { return Resolve(token, targetFolder, targetForm, targetField); }
        catch (InvalidDataException ex) { errors.Add(ex.Message); return token; }
    }

    private static string Resolve(string token, string targetFolder, string targetForm, string targetField)
    {
        var project = CurrentProject.Instance;
        var parts = token.Split('.');
        if (parts.Length == 3) return ValidateAddress(parts[0], parts[1], parts[2]);
        if (parts.Length == 2)
        {
            var first = parts[0]; var field = parts[1];
            var isFolder = HasFolder(first); var isForm = HasForm(first);
            if (isFolder && isForm) return ValidateAddress(first, first, field);
            if (isForm)
            {
                EnsureField(isForm ? first : targetForm, field);
                if (targetFolder == "ALLVISIT" || !FormInFolder(first, targetFolder)) return $"ALLVISIT.{first}.{field}";
                return ValidateAddress(targetFolder, first, field);
            }
            if (isFolder)
            {
                var candidates = project.Forms.Where(form => FormInFolder(form.Oid, first) && FieldExists(form.Oid, field)).ToList();
                var target = candidates.FirstOrDefault(x => x.Oid.Equals(targetForm, StringComparison.OrdinalIgnoreCase));
                if (target is not null) return ValidateAddress(first, target.Oid, field);
                if (candidates.Count == 1) return ValidateAddress(first, candidates[0].Oid, field);
                if (candidates.Count == 0) throw new InvalidDataException($"Folder {first} 中找不到 Field {field}");
                throw new InvalidDataException($"Folder {first} 中的 Field {field} 无法唯一确定 Form");
            }
            throw new InvalidDataException($"{first} 既不是 FolderOID，也不是 FormOID");
        }
        if (parts.Length != 1) throw new InvalidDataException($"无法识别数据点 {token}");

        var bareField = parts[0];
        if (HasForm(targetForm) && FieldExists(targetForm, bareField))
            return targetFolder == "ALLVISIT" ? $"ALLVISIT.{targetForm}.{bareField}" : ValidateAddress(targetFolder, targetForm, bareField);

        var locations = project.Fields.Where(x => x.FieldOid.Equals(bareField, StringComparison.OrdinalIgnoreCase))
            .SelectMany(field => project.Forms.Where(x => x.Oid.Equals(field.FormOid, StringComparison.OrdinalIgnoreCase))
                .SelectMany(form => form.FolderOids.Select(folder => $"{folder}.{form.Oid}.{field.FieldOid}")))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (locations.Count == 1) return locations[0];
        if (locations.Count == 0) throw new InvalidDataException($"EDC 中不存在 Field {bareField}");
        throw new InvalidDataException($"裸字段 {bareField} 无法唯一定位");
    }

    private static void ValidateTarget(string folder, string form, string field, List<string> errors)
    {
        try
        {
            if (folder == "ALLVISIT") EnsureField(form, field);
            else ValidateAddress(folder, form, field);
        }
        catch (InvalidDataException ex) { errors.Add($"目标数据点：{ex.Message}"); }
    }

    private static string ValidateAddress(string folder, string form, string field)
    {
        if (folder.Equals("ALLVISIT", StringComparison.OrdinalIgnoreCase)) { EnsureField(form, field); return $"ALLVISIT.{form}.{field}"; }
        if (!HasFolder(folder)) throw new InvalidDataException($"Folder {folder} 不存在");
        EnsureField(form, field);
        if (!FormInFolder(form, folder)) throw new InvalidDataException($"Form {form} 不位于 Folder {folder}");
        return $"{folder}.{form}.{field}";
    }

    private static void EnsureField(string form, string field)
    {
        if (!HasForm(form)) throw new InvalidDataException($"Form {form} 不存在");
        if (!FieldExists(form, field)) throw new InvalidDataException($"Form {form} 中不存在 Field {field}");
    }

    private static void ValidateSyntax(string expression, List<string> errors)
    {
        var balance = 0;
        foreach (var character in expression) { if (character == '(') balance++; else if (character == ')' && --balance < 0) break; }
        if (balance != 0) errors.Add("表达式括号不匹配");
        if (Regex.IsMatch(expression, @"\b(?:AND|OR)\s+[-+]?\d+(?:\.\d+)?(?:D|H|MIN|MON)?\b", RegexOptions.IgnoreCase))
            errors.Add("逻辑连接词后只有常量，条件书写不完整");
    }

    private static bool IsTimeUnit(string token) => token.Equals("MIN", StringComparison.OrdinalIgnoreCase) || token.Equals("MON", StringComparison.OrdinalIgnoreCase);
    private static string NormalizeFolder(string value) => value.Contains("all", StringComparison.OrdinalIgnoreCase) ? "ALLVISIT" : value.Trim();
    private static bool HasFolder(string oid) => CurrentProject.Instance.Folders.Any(x => x.Oid.Equals(oid, StringComparison.OrdinalIgnoreCase));
    private static bool HasForm(string oid) => CurrentProject.Instance.Forms.Any(x => x.Oid.Equals(oid, StringComparison.OrdinalIgnoreCase));
    private static bool FieldExists(string form, string field) => CurrentProject.Instance.Fields.Any(x => x.FormOid.Equals(form, StringComparison.OrdinalIgnoreCase) && x.FieldOid.Equals(field, StringComparison.OrdinalIgnoreCase));
    private static bool FormInFolder(string form, string folder) => CurrentProject.Instance.Forms.FirstOrDefault(x => x.Oid.Equals(form, StringComparison.OrdinalIgnoreCase))?.FolderOids.Contains(folder, StringComparer.OrdinalIgnoreCase) == true;
}
