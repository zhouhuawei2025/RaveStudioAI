using RaveStudioAI.Common;
using RaveStudioAI.EditCheck.Models;
using System.Text.RegularExpressions;

namespace RaveStudioAI.EditCheck.Services;

public static class BlindValidator
{
    private static readonly Regex LogicalWord = new(@"\b(and|or)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LogicalSeparator = new(@"\s+(and|or)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Comparison = new(
        @"^(.+?)(<>|!=|≠|>=|<=|≥|≤|＞=|＜=|=|>|<|＞|＜)(.+)$",
        RegexOptions.Compiled);
    private static readonly Regex AnyComparison = new(@"<>|!=|≠|>=|<=|≥|≤|＞=|＜=|=|>|<|＞|＜", RegexOptions.Compiled);
    private static readonly char[] TargetSeparators = ['、', '/', ',', '，', ' ', '\t', '\r', '\n'];

    public static bool ValidateAndNormalize(IReadOnlyList<BlindRow> rows)
    {
        foreach (var row in rows)
        {
            row.HasError = false;
            row.ValidationMessage = string.Empty;
            row.NormalizedLogicText = string.Empty;
            row.HasSuggestion = false;
        }

        ValidateDuplicateBlindOids(rows);
        var parsed = new Dictionary<BlindRow, ParsedExpression>();
        foreach (var row in rows)
        {
            var expression = ValidateOriginalRow(row);
            if (expression is not null) parsed[row] = expression;
        }

        var rules = BuildRuleIndex(rows, parsed);

        // 每一行独立生成建议。某行校验失败不再阻断其他可解析行的建议补全。
        foreach (var row in rows.Where(parsed.ContainsKey))
        {
            try
            {
                var scope = Scope(row);
                var original = parsed[row].Text;
                row.NormalizedLogicText = ExpandExpression(
                    parsed[row], scope, rules, new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Text;
                row.HasSuggestion = !row.NormalizedLogicText.Equals(original, StringComparison.OrdinalIgnoreCase);
                if (row.HasSuggestion) AddWarning(row, "已补充上游条件，请人工确认");
            }
            catch (InvalidOperationException ex)
            {
                AddError(row, ex.Message);
            }
        }

        return rows.All(x => !x.HasError);
    }

    private static ParsedExpression? ValidateOriginalRow(BlindRow row)
    {
        if (string.IsNullOrWhiteSpace(row.BlindOid)) AddError(row, "BlindOID 不能为空");
        if (string.IsNullOrWhiteSpace(row.FolderOid)) AddError(row, "FolderOID 不能为空");
        if (string.IsNullOrWhiteSpace(row.FormOid)) AddError(row, "FormOID 不能为空");
        if (string.IsNullOrWhiteSpace(row.FieldOid)) AddError(row, "目标 FieldOID 不能为空");
        if (string.IsNullOrWhiteSpace(row.LogicText)) AddError(row, "LogicText 不能为空");

        var folder = NormalizeFolder(row.FolderOid);
        if (!string.IsNullOrWhiteSpace(folder) && folder != "ALLVISIT" && !FolderExists(folder))
            AddError(row, $"Folder {row.FolderOid.Trim()} 不存在");

        var form = row.FormOid.Trim();
        if (!string.IsNullOrWhiteSpace(form) && !FormExists(form))
            AddError(row, $"Form {form} 不存在");

        var targets = TargetFields(row.FieldOid);
        if (!string.IsNullOrWhiteSpace(row.FieldOid) && targets.Count == 0)
            AddError(row, "目标 FieldOID 不能为空");
        if (FormExists(form))
        {
            foreach (var target in targets.Where(x => !FieldExists(form, x)))
                AddError(row, $"目标字段 {target} 不位于 Form {form}");
        }

        if (string.IsNullOrWhiteSpace(row.LogicText)) return null;
        try
        {
            var expression = ParseExpression(ExtractCondition(row.LogicText));
            if (FormExists(form))
            {
                foreach (var condition in expression.Conditions)
                {
                    if (condition.LeftField.Equals("NOW", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!FieldExists(form, condition.LeftField))
                        AddError(row, $"LogicText 字段 {condition.LeftField} 不位于 Form {form}");

                    if (FieldExistsAnywhere(condition.RightValue) && !FieldExists(form, condition.RightValue))
                        AddError(row, $"LogicText 字段 {condition.RightValue} 不位于 Form {form}");
                }
            }
            return expression;
        }
        catch (InvalidOperationException ex)
        {
            AddError(row, ex.Message);
            return null;
        }
    }

    private static Dictionary<string, List<Rule>> BuildRuleIndex(
        IReadOnlyList<BlindRow> rows,
        IReadOnlyDictionary<BlindRow, ParsedExpression> parsed)
    {
        var candidates = new Dictionary<string, List<Rule>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Where(x => !x.HasError && parsed.ContainsKey(x)))
        {
            var scope = Scope(row);
            foreach (var target in TargetFields(row.FieldOid))
            {
                var key = TargetKey(scope, target);
                if (!candidates.TryGetValue(key, out var values)) candidates[key] = values = [];
                values.Add(new Rule(row, target, parsed[row]));
            }
        }
        return candidates;
    }

    private static ExpandedExpression ExpandExpression(
        ParsedExpression expression,
        string scope,
        IReadOnlyDictionary<string, List<Rule>> rules,
        HashSet<string> visiting)
    {
        if (expression.Connectors.Contains("or", StringComparer.OrdinalIgnoreCase))
        {
            var dependencies = expression.Conditions
                .Select(x => TargetKey(scope, x.LeftField))
                .Where(rules.ContainsKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (dependencies.Count == 0)
                return new ExpandedExpression(expression.Text, expression.Conditions.Select(x => x.Text).ToList());
            if (dependencies.Count != 1 || expression.Conditions.Any(x => !TargetKey(scope, x.LeftField).Equals(dependencies[0], StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("包含 or 的表达式引用了不同显示依赖，无法安全自动补全");

            var dependency = SingleDependency(dependencies[0], rules);
            if (!visiting.Add(dependencies[0]))
                throw new InvalidOperationException($"检测到循环显示依赖：{string.Join(" → ", visiting.Select(KeyField))} → {dependency.TargetField}");
            var upstream = ExpandExpression(dependency.Expression, scope, rules, visiting);
            visiting.Remove(dependencies[0]);

            return new ExpandedExpression(
                $"({expression.Text}) and {upstream.Text}",
                expression.Conditions.Select(x => x.Text).Concat(upstream.ConditionTexts).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }

        var blocks = new List<string>();
        var includedConditions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < expression.Conditions.Count; index++)
        {
            var condition = expression.Conditions[index];
            var blockParts = new List<string>();
            var dependencyKey = TargetKey(scope, condition.LeftField);
            if (rules.ContainsKey(dependencyKey))
            {
                var dependency = SingleDependency(dependencyKey, rules);
                if (!visiting.Add(dependencyKey))
                    throw new InvalidOperationException($"检测到循环显示依赖：{string.Join(" → ", visiting.Select(KeyField))} → {condition.LeftField}");

                var upstream = ExpandExpression(dependency.Expression, scope, rules, visiting);
                visiting.Remove(dependencyKey);
                var missingUpstream = upstream.ConditionTexts.Where(includedConditions.Add).ToList();
                if (missingUpstream.Count == upstream.ConditionTexts.Count)
                    blockParts.Add(upstream.Text);
                else
                {
                    // 只在部分条件已由用户显式写出时降为去重后的 and 条件，避免重复生成。
                    blockParts.AddRange(missingUpstream);
                }
            }

            if (includedConditions.Add(condition.Text)) blockParts.Add(condition.Text);
            if (blockParts.Count > 0) blocks.Add(string.Join(" and ", blockParts));
            if (index < expression.Connectors.Count && blocks.Count > 0) blocks.Add(expression.Connectors[index]);
        }

        if (blocks.Count > 0 && IsConnector(blocks[^1])) blocks.RemoveAt(blocks.Count - 1);
        return new ExpandedExpression(string.Join(" ", blocks), includedConditions.ToList());
    }

    private static Rule SingleDependency(string key, IReadOnlyDictionary<string, List<Rule>> rules)
    {
        var matches = rules[key];
        if (matches.Count == 1) return matches[0];
        throw new InvalidOperationException($"同一访视和表单中的字段 {KeyField(key)} 存在多条激活规则，无法确定应补入哪一条");
    }

    private static ParsedExpression ParseExpression(string text)
    {
        text = (text ?? string.Empty).Replace('（', '(').Replace('）', ')');
        text = Regex.Replace(text, @"\(\s*(?:the\s+)?same\s+visit\s*\)", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("LogicText 条件为空");
        if (text.Contains('(') || text.Contains(')') || text.Contains('（') || text.Contains('）'))
            throw new InvalidOperationException("SetDataPointVisible 暂不支持括号表达式");

        foreach (Match match in LogicalWord.Matches(text))
        {
            var beforeOk = match.Index > 0 && char.IsWhiteSpace(text[match.Index - 1]);
            var afterIndex = match.Index + match.Length;
            var afterOk = afterIndex < text.Length && char.IsWhiteSpace(text[afterIndex]);
            if (!beforeOk || !afterOk)
                throw new InvalidOperationException("and/or 前后必须有空格");
        }

        var conditions = LogicalSeparator.Split(text)
            .Where((_, index) => index % 2 == 0)
            .Select(x => x.Trim()).ToList();
        var connectors = LogicalSeparator.Matches(text)
            .Select(x => x.Groups[1].Value.ToLowerInvariant()).ToList();
        if (conditions.Count == 0 || conditions.Any(string.IsNullOrWhiteSpace) || connectors.Count != conditions.Count - 1)
            throw new InvalidOperationException("and/or 两侧必须是完整条件");

        var parsedConditions = new List<Condition>();
        for (var index = 0; index < conditions.Count; index++)
        {
            var conditionText = conditions[index];
            if (Regex.IsMatch(conditionText, @"^NOW\s+is\s+present$", RegexOptions.IgnoreCase))
            {
                parsedConditions.Add(new Condition("NOW", "is present", string.Empty, "NOW is present"));
                continue;
            }

            var match = Comparison.Match(conditionText);
            if (!match.Success)
            {
                if (index > 0 && connectors[index - 1] == "or" &&
                    Regex.IsMatch(conditionText, @"^[-+]?\d+(?:\.\d+)?(?:d|h|min|mon)?$", RegexOptions.IgnoreCase))
                {
                    var previous = parsedConditions[^1];
                    parsedConditions.Add(new Condition(previous.LeftField, conditionText, previous.Operator, conditionText));
                    continue;
                }
                throw new InvalidOperationException($"表达式不完整或缺少比较操作符：{conditionText}");
            }
            var left = match.Groups[1].Value.Trim();
            var right = match.Groups[3].Value.Trim();
            var op = match.Groups[2].Value;
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right) || AnyComparison.IsMatch(right))
                throw new InvalidOperationException($"表达式不完整：{conditionText}");
            if (!IsSimpleField(left))
                throw new InvalidOperationException($"无法识别 LogicText 字段：{left}");
            parsedConditions.Add(new Condition(left, right, op, $"{left} {op} {right}"));
        }
        return new ParsedExpression(parsedConditions, connectors);
    }

    // 与既有转换器保持相同规则：只取逗号或 set 之前的条件文本。
    private static string ExtractCondition(string logicText) =>
        logicText.Split([",", "，", "set", "Set", "SET"], StringSplitOptions.None)[0].Trim();

    private static void ValidateDuplicateBlindOids(IReadOnlyList<BlindRow> rows)
    {
        foreach (var group in rows.Where(x => !string.IsNullOrWhiteSpace(x.BlindOid))
                     .GroupBy(x => x.BlindOid.Trim(), StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
            foreach (var row in group) AddError(row, $"BlindOID {group.Key} 重复");
    }

    private static string Scope(BlindRow row) => $"{NormalizeFolder(row.FolderOid)}\u001F{row.FormOid.Trim()}";
    private static string TargetKey(string scope, string field) => $"{scope}\u001F{field.Trim()}";
    private static string KeyField(string key) => key.Split('\u001F').LastOrDefault() ?? key;
    private static bool IsConnector(string value) => value is "and" or "or";
    private static string NormalizeFolder(string value) => IsAllVisit(value) ? "ALLVISIT" : value.Trim();
    private static bool IsAllVisit(string value)
    {
        var normalized = Regex.Replace(value ?? string.Empty, @"[\s_-]+", string.Empty);
        return normalized.Equals("ALLVISIT", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("ALLVISITS", StringComparison.OrdinalIgnoreCase);
    }
    private static List<string> TargetFields(string text) => text.Split(TargetSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private static bool IsSimpleField(string value) => Regex.IsMatch(value, @"^[A-Za-z][A-Za-z0-9_-]*$");
    private static bool FolderExists(string oid) => CurrentProject.Instance.Folders.Any(x => x.Oid.Equals(oid, StringComparison.OrdinalIgnoreCase));
    private static bool FormExists(string oid) => CurrentProject.Instance.Forms.Any(x => x.Oid.Equals(oid, StringComparison.OrdinalIgnoreCase));
    private static bool FieldExists(string form, string field) => CurrentProject.Instance.Fields.Any(x =>
        x.FormOid.Equals(form, StringComparison.OrdinalIgnoreCase) && x.FieldOid.Equals(field, StringComparison.OrdinalIgnoreCase));
    private static bool FieldExistsAnywhere(string field) => CurrentProject.Instance.Fields.Any(x => x.FieldOid.Equals(field, StringComparison.OrdinalIgnoreCase));

    private static void AddError(BlindRow row, string message)
    {
        var messages = row.ValidationMessage.Split('；', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (!messages.Contains(message, StringComparer.OrdinalIgnoreCase)) messages.Add(message);
        row.ValidationMessage = string.Join("；", messages);
        row.HasError = true;
    }

    private static void AddWarning(BlindRow row, string message)
    {
        var messages = row.ValidationMessage.Split('；', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (!messages.Contains(message, StringComparer.OrdinalIgnoreCase)) messages.Add(message);
        row.ValidationMessage = string.Join("；", messages);
    }

    private sealed record Condition(string LeftField, string RightValue, string Operator, string Text);
    private sealed record ParsedExpression(IReadOnlyList<Condition> Conditions, IReadOnlyList<string> Connectors)
    {
        public string Text
        {
            get
            {
                var values = new List<string>();
                for (var index = 0; index < Conditions.Count; index++)
                {
                    values.Add(Conditions[index].Text);
                    if (index < Connectors.Count) values.Add(Connectors[index]);
                }
                return string.Join(" ", values);
            }
        }
    }
    private sealed record Rule(BlindRow Row, string TargetField, ParsedExpression Expression);
    private sealed record ExpandedExpression(string Text, IReadOnlyList<string> ConditionTexts);
}
