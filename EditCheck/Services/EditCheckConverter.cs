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
        if (string.IsNullOrWhiteSpace(query.NormalizedLogicText))
            throw new InvalidDataException("NormalizedLogicText 为空，请先完成 OpenQuery 前置校验。");
        var prompt = $"{folderOid}##{query.FormOid}##{query.NormalizedLogicText}";
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
        var folderOid = Regex.IsMatch(blind.FolderOid, @"^\s*all[\s_-]*visits?\s*$", RegexOptions.IgnoreCase)
            ? string.Empty : finder.NormalizeFolder(blind.FolderOid.Trim());
        var targets = blind.FieldOid.Split(['、', '/', ',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var logicSource = string.IsNullOrWhiteSpace(blind.NormalizedLogicText)
            ? blind.LogicText
            : blind.NormalizedLogicText;
        var logic = logicSource.Split([",", "，", "set", "Set", "SET"], StringSplitOptions.None)[0];
        var tokens = ParseRuleText(logic);
        if (tokens.Count == 0) throw new InvalidDataException("LogicText 为空。");
        var presentField = FindNearestPresentField(blind.FormOid, targets[0], tokens);

        var postfix = new List<string>();
        string[]? previousExpression = null;
        foreach (var token in tokens)
        {
            if (token is "and" or "or") { postfix.Add(token); continue; }
            var parts = SplitExpression(token);
            if (parts is null && previousExpression is not null &&
                Regex.IsMatch(token, @"^[-+]?\d+(?:\.\d+)?(?:d|h|min|mon)?$", RegexOptions.IgnoreCase))
                parts = [previousExpression[0], token, previousExpression[2]];
            if (parts is null) throw new InvalidDataException($"无法解析表达式：{token}");
            postfix.AddRange(parts);
            previousExpression = parts;
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

    private static string? FindNearestPresentField(string formOid, string firstTarget, IReadOnlyList<string> tokens)
    {
        var fields = CurrentProject.Instance.Fields
            .Where(x => x.FormOid.Equals(formOid, StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.FieldOid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        if (!fields.TryGetValue(firstTarget, out var target) || target.Ordinal is null)
            return LastConditionField(tokens, fields);

        return tokens
            .Select((token, index) => (Parts: token is "and" or "or" ? null : SplitExpression(token), Index: index))
            .Where(x => x.Parts is not null && fields.TryGetValue(x.Parts[0], out var field) && field.Ordinal is not null)
            .Select(x => (FieldOid: x.Parts![0], Distance: Math.Abs(fields[x.Parts[0]].Ordinal!.Value - target.Ordinal.Value), x.Index))
            .OrderBy(x => x.Distance)
            .ThenByDescending(x => x.Index)
            .Select(x => x.FieldOid)
            .FirstOrDefault() ?? LastConditionField(tokens, fields);
    }

    private static string? LastConditionField(IReadOnlyList<string> tokens, IReadOnlyDictionary<string, ProjectField> fields) =>
        tokens.Select(x => x is "and" or "or" ? null : SplitExpression(x)?[0])
            .LastOrDefault(x => x is not null && fields.ContainsKey(x));

    private static List<string> ParseRuleText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];
        var output = new List<string>();
        var operators = new Stack<string>();
        var matches = Regex.Matches(input, @"\(|\)|\b(?:and|or)\b", RegexOptions.IgnoreCase);
        var position = 0;

        foreach (Match match in matches)
        {
            var condition = input[position..match.Index].Trim();
            if (!string.IsNullOrWhiteSpace(condition)) output.Add(condition);

            var token = match.Value.ToLowerInvariant();
            if (token == "(") operators.Push(token);
            else if (token == ")")
            {
                while (operators.Count > 0 && operators.Peek() != "(") output.Add(operators.Pop());
                if (operators.Count == 0) throw new InvalidDataException("LogicText 括号不匹配。");
                operators.Pop();
            }
            else
            {
                // 没有括号时保持旧版从左到右的处理方式；括号用于明确补全后的 or 逻辑。
                while (operators.Count > 0 && operators.Peek() != "(") output.Add(operators.Pop());
                operators.Push(token);
            }
            position = match.Index + match.Length;
        }

        var lastCondition = input[position..].Trim();
        if (!string.IsNullOrWhiteSpace(lastCondition)) output.Add(lastCondition);
        while (operators.Count > 0)
        {
            if (operators.Peek() == "(") throw new InvalidDataException("LogicText 括号不匹配。");
            output.Add(operators.Pop());
        }
        return output;
    }

    private static string[]? SplitExpression(string input)
    {
       if(input == "NOW is present" || input == "NOW is Present")
                return ["NOW", "IsPresent"];

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
        "=" => "IsEqualTo|||||||||||||", 
        ">" or "＞" => "IsGreaterThan|||||||||||||",
        "<" or "＜" => "IsLessThan|||||||||||||", 
        ">=" or "＞=" or "≥" => "IsGreaterThanOrEqualTo|||||||||||||",
        "<=" or "＜=" or "≤" => "IsLessThanOrEqualTo|||||||||||||", 
        "!=" or "<>" or "＜＞" or "≠" => "IsNotEqualTo|||||||||||||",
        "and" => "And|||||||||||||", 
        "or" => "Or|||||||||||||", 
        "add" => "Add|||||||||||||",
        "addday" => "AddDay|||||||||||||", 
        "addmin" => "AddMin|||||||||||||", 
        "addhour" => "AddHour|||||||||||||",
        "addmonth" => "AddMonth|||||||||||||", 
        "isempty" => "IsEmpty|||||||||||||",
        "isnotempty" => "IsNotEmpty|||||||||||||", 
        "timespan" => "TimeSpan|||||||||||||",
        "ispresent" => "IsPresent|||||||||||||",
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
