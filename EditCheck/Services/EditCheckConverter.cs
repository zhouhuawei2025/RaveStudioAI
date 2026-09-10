using RaveStudioAI.Common;
using RaveStudioAI.EditCheck.Models;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RaveStudioAI.EditCheck.Services;

public static class EditCheckConverter
{
    public static async Task<string> ConvertQueryAsync(QueryRow query, string instruction)
    {
        var finder = new DataPointFinder();
        var folderOid = query.FolderOid.Contains("all", StringComparison.OrdinalIgnoreCase)
            ? "ALLVISIT" : finder.NormalizeFolder(query.FolderOid.Trim());
        var prompt = $"{folderOid}##{query.FormOid}##{query.LogicText}";
        LogManager.Write(LogCategory.EditCheck, "query.log", $"开始解析 {query.QueryOid}：{prompt}");

        // 每条 Query 单独等待一次 AI，不使用 BatchSize、Task.WhenAny 或固定延时。
        var aiResult = (await AIHelper.AskAsync(instruction, prompt)).Replace("`", string.Empty).Trim();
        LogManager.Write(LogCategory.EditCheck, "query.log", $"{query.QueryOid} AI 返回：{aiResult}");
        if (string.IsNullOrWhiteSpace(aiResult)) throw new InvalidDataException("AI 返回内容为空。");

        var sections = aiResult.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        CorrectUniqueFolders(sections, folderOid, query.FormOid, finder);
        var repeatFlags = GetFolderRepeatFlags(sections, finder);
        var steps = new List<string>();

        for (var index = 0; index < sections.Length; index++)
        {
            var section = sections[index];
            if (section.Contains(':'))
            {
                var parts = section.Split(':', StringSplitOptions.TrimEntries);
                if (parts.Length != 3) throw new InvalidDataException($"AI 返回的数据点格式不正确：{section}");
                var sectionFolder = parts[0].Equals("ALLVISIT", StringComparison.OrdinalIgnoreCase) ? string.Empty : finder.NormalizeFolder(parts[0]);
                if (!finder.TryFindField(parts[1], parts[2])) throw new InvalidDataException($"SDS 中找不到字段 {parts[1]}.{parts[2]}。");
                steps.Add(ParseField(finder.CurrentField!, sectionFolder, parts[1], repeatFlags[index]));
            }
            else if (DataPointFinder.Functions.Contains(section)) steps.Add(ParseFunction(section));
            else steps.Add(ParseConstant(section));
        }

        if (!finder.TryFindField(query.FormOid, query.FieldOid))
            throw new InvalidDataException($"SDS 中找不到目标字段 {query.FormOid}.{query.FieldOid}。");
        var actionFolder = folderOid == "ALLVISIT" ? string.Empty : folderOid;
        var action = ParseAction(finder.CurrentField!, actionFolder, query.FormOid, "OpenQuery", query.MessageText);
        return $"|{query.QueryOid}|TRUE|TRUE{Environment.NewLine}{Environment.NewLine}" +
               Connect(steps, [action]);
    }

    public static string ConvertBlind(BlindRow blind)
    {
        var finder = new DataPointFinder();
        var folderOid = blind.FolderOid.Contains("all", StringComparison.OrdinalIgnoreCase)
            ? string.Empty : finder.NormalizeFolder(blind.FolderOid.Trim());
        var targets = blind.FieldOid.Split(['、', '/', ',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var logic = blind.LogicText.Split([",", "，", "set", "Set", "SET"], StringSplitOptions.None)[0];
        var tokens = ParseRuleText(logic);
        if (tokens.Count == 0) throw new InvalidDataException("LogicText 为空。");

        var postfix = new List<string>();
        string? presentField = null;
        foreach (var token in tokens)
        {
            if (token is "and" or "or") { postfix.Add(token); continue; }
            var parts = SplitExpression(token) ?? throw new InvalidDataException($"无法解析表达式：{token}");
            postfix.AddRange(parts);
            presentField = parts[0];
        }

        var steps = new List<string>();
        foreach (var token in postfix)
        {
            if (finder.TryFindField(blind.FormOid, token)) steps.Add(ParseAction(finder.CurrentField!, folderOid, blind.FormOid, "Field"));
            else if (DataPointFinder.Functions.Contains(token)) steps.Add(ParseFunction(token));
            else steps.Add(ParseConstant(token));
        }

        var actions = new List<string>();
        if (presentField is not null && finder.TryFindField(blind.FormOid, presentField))
            actions.Add(ParseAction(finder.CurrentField!, folderOid, blind.FormOid, "IsPresent"));
        foreach (var target in targets)
        {
            if (!finder.TryFindField(blind.FormOid, target)) throw new InvalidDataException($"SDS 中找不到目标字段 {blind.FormOid}.{target}。");
            actions.Add(ParseAction(finder.CurrentField!, folderOid, blind.FormOid, "SetDataPointVisible"));
        }

        return $"|{blind.BlindOid}|TRUE|TRUE{Environment.NewLine}{Environment.NewLine}" + Connect(steps, actions);
    }

    private static List<string> ParseRuleText(string input)
    {
        var matches = Regex.Matches(input, @"\s+(and|or)\s+", RegexOptions.IgnoreCase);
        var operators = matches.Select(x => x.Groups[1].Value.ToLowerInvariant()).ToList();
        var conditions = Regex.Split(input, @"\s+(?:and|or)\s+", RegexOptions.IgnoreCase)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
        var result = new List<string>();
        for (var i = 0; i < conditions.Count; i++)
        {
            result.Add(conditions[i]);
            if (i > 0) result.Add(operators[i - 1]);
        }
        return result;
    }

    private static string[]? SplitExpression(string input)
    {
        var match = Regex.Match(input, @"^(.+?)(<>|!=|≠|>=|<=|≥|≤|＞=|＜=|=|>|<|＞|＜)(.+)$");
        return match.Success ? [match.Groups[1].Value.Trim(), match.Groups[3].Value.Trim(), match.Groups[2].Value.Trim()] : null;
    }

    private static string ParseField(ProjectField field, string folder, string form, string repeatFlag)
    {
        var record = field.IsLog ? "" : "0";
        return $"||StandardValue|{field.VariableOid}|{folder}|{form}|{field.FieldOid}|{record}||||||{repeatFlag}";
    }

    private static string ParseAction(ProjectField field, string folder, string form, string type, string? message = null)
    {
        var record = field.IsLog ? "" : "0";
        return type switch
        {
            "Field" => $"||StandardValue|{field.VariableOid}|{folder}|{form}|{field.FieldOid}|{record}||||||",
            "IsPresent" => $"{folder}|{form}|{field.FieldOid}|{field.VariableOid}|{record}||||||IsPresent||0||",
            "SetDataPointVisible" => $"{folder}|{form}|{field.FieldOid}|{field.VariableOid}|{record}||||||SetDataPointVisible||TRUE,FALSE||",
            "OpenQuery" => $"{folder}|{form}|{field.FieldOid}|{field.VariableOid}|{record}||||||OpenQuery|{message}|Site from System,RequiresResponse,RequiresManualClose||",
            _ => throw new InvalidDataException($"不支持的 Action：{type}")
        };
    }

    private static string ParseFunction(string value) => value.ToLowerInvariant() switch
    {
        "=" => "IsEqualTo|||||||||||||", ">" or "＞" => "IsGreaterThan|||||||||||||",
        "<" or "＜" => "IsLessThan|||||||||||||", ">=" or "＞=" or "≥" => "IsGreaterThanOrEqualTo|||||||||||||",
        "<=" or "＜=" or "≤" => "IsLessThanOrEqualTo|||||||||||||", "!=" or "<>" or "＜＞" or "≠" => "IsNotEqualTo|||||||||||||",
        "and" => "And|||||||||||||", "or" => "Or|||||||||||||", "add" => "Add|||||||||||||",
        "addday" => "AddDay|||||||||||||", "addmin" => "AddMin|||||||||||||", "addhour" => "AddHour|||||||||||||",
        "addmonth" => "AddMonth|||||||||||||", "isempty" => "IsEmpty|||||||||||||",
        "isnotempty" => "IsNotEmpty|||||||||||||", "timespan" => "TimeSpan|||||||||||||",
        _ => throw new InvalidDataException($"无法识别函数：{value}")
    };

    private static string ParseConstant(string value)
    {
        if (value == "00-00") return "|00:00|HH:nn|||||||||||";
        if (int.TryParse(value, out var integer)) return $"|{integer}|{integer.ToString().Length}|||||||||||";
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            var decimals = value.Contains('.') ? value.Split('.')[1].Length : 0;
            return $"|{value}|{value.Length}.{decimals}|||||||||||";
        }
        return value.Length < 7 ? $"|{value}|{value.Length}|||||||||||" : $"警告：该行可能有错|{value}||||||||||||";
    }

    private static List<string> GetFolderRepeatFlags(string[] sections, DataPointFinder finder)
    {
        var flags = sections.Select(section => section.Contains(':')
            ? (finder.IsReusableFolder(section.Split(':')[0]) ? "1" : "0") : "-1").ToList();
        var mixed = flags.Contains("1") && flags.Contains("0");
        for (var i = 0; i < flags.Count; i++) flags[i] = mixed && flags[i] == "0" ? "0" : string.Empty;
        return flags;
    }

    private static void CorrectUniqueFolders(string[] sections, string currentFolderOid, string currentFormOid,
        DataPointFinder finder)
    {
        for (var index = 0; index < sections.Length; index++)
        {
            if (!sections[index].Contains(':')) continue;
            var parts = sections[index].Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 3) continue;

            var returnedFolderOid = parts[0].Equals("ALLVISIT", StringComparison.OrdinalIgnoreCase)
                ? "ALLVISIT"
                : finder.NormalizeFolder(parts[0]);
            if (!returnedFolderOid.Equals(currentFolderOid, StringComparison.OrdinalIgnoreCase) ||
                parts[1].Equals(currentFormOid, StringComparison.OrdinalIgnoreCase) ||
                !finder.TryGetUniqueFolderOid(parts[1], out var uniqueFolderOid)) continue;

            sections[index] = $"{uniqueFolderOid}:{parts[1]}:{parts[2]}";
            LogManager.Write(LogCategory.EditCheck, "query.log",
                $"Folder 自动纠正：{parts[0]}:{parts[1]}:{parts[2]} → {sections[index]}");
        }
    }

    private static string Connect(List<string> steps, List<string> actions) =>
        string.Join(Environment.NewLine, steps) + Environment.NewLine + Environment.NewLine +
        string.Join(Environment.NewLine, actions);
}
