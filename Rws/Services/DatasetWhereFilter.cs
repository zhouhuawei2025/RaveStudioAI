using RaveStudioAI.Rws.Models;

namespace RaveStudioAI.Rws.Services
{
    public static class DatasetWhereFilter
    {
        public static Func<RaveDatasetRow, bool> Parse(
            string expression,
            IReadOnlyCollection<string> availableFields)
        {
            var tokens = Tokenize(expression);
            var parser = new Parser(tokens, availableFields);
            return parser.Parse();
        }

        private static List<Token> Tokenize(string expression)
        {
            var tokens = new List<Token>();
            var index = 0;

            while (index < expression.Length)
            {
                var current = expression[index];
                if (char.IsWhiteSpace(current))
                {
                    index += 1;
                    continue;
                }

                if (current is '（' or '）')
                {
                    throw new InvalidOperationException("筛选表达式语法错误：请使用英文括号 ()，不要使用中文括号（）。");
                }

                if (current == '(')
                {
                    tokens.Add(new Token(TokenType.OpenParen, "("));
                    index += 1;
                    continue;
                }

                if (current == ')')
                {
                    tokens.Add(new Token(TokenType.CloseParen, ")"));
                    index += 1;
                    continue;
                }

                if (current == ',')
                {
                    tokens.Add(new Token(TokenType.Comma, ","));
                    index += 1;
                    continue;
                }

                if (current == '=')
                {
                    tokens.Add(new Token(TokenType.Equals, "="));
                    index += 1;
                    continue;
                }

                if (current == '!' && index + 1 < expression.Length && expression[index + 1] == '=')
                {
                    tokens.Add(new Token(TokenType.NotEquals, "!="));
                    index += 2;
                    continue;
                }

                if (current == '!')
                {
                    throw new InvalidOperationException("筛选表达式语法错误：不支持单独的 !，请使用 !=。");
                }

                if (current is '\'' or '"')
                {
                    var quote = current;
                    var start = index + 1;
                    index += 1;
                    while (index < expression.Length && expression[index] != quote)
                    {
                        index += 1;
                    }

                    if (index >= expression.Length)
                    {
                        throw new InvalidOperationException("筛选表达式语法错误：字符串缺少结束引号。");
                    }

                    tokens.Add(new Token(TokenType.Value, expression[start..index]));
                    index += 1;
                    continue;
                }

                var wordStart = index;
                while (index < expression.Length &&
                       !char.IsWhiteSpace(expression[index]) &&
                       expression[index] != '(' &&
                       expression[index] != ')' &&
                       expression[index] != ',' &&
                       expression[index] != '=')
                {
                    if (expression[index] == '!' &&
                        index + 1 < expression.Length &&
                        expression[index + 1] == '=')
                    {
                        break;
                    }

                    index += 1;
                }

                var word = expression[wordStart..index];
                tokens.Add(ToWordToken(word));
            }

            tokens.Add(new Token(TokenType.End, string.Empty));
            return tokens;
        }

        private static Token ToWordToken(string word)
        {
            return word.ToLowerInvariant() switch
            {
                "and" => new Token(TokenType.And, word),
                "or" => new Token(TokenType.Or, word),
                "contains" => new Token(TokenType.Contains, word),
                "in" => new Token(TokenType.In, word),
                "is" => new Token(TokenType.Is, word),
                "not" => new Token(TokenType.Not, word),
                "null" => new Token(TokenType.Null, word),
                _ => new Token(TokenType.Identifier, word)
            };
        }

        private static string? GetValue(RaveDatasetRow row, string fieldName)
        {
            if (row.Values.TryGetValue(fieldName, out var itemValue))
            {
                return itemValue;
            }

            return fieldName.ToLowerInvariant() switch
            {
                "studyoid" => row.StudyOID,
                "subject" => row.SubjectKey,
                "subjectkey" => row.SubjectKey,
                "siteoid" => row.SiteOID,
                "folderoid" => row.StudyEventOID,
                "studyeventoid" => row.StudyEventOID,
                "folderrepeatkey" => row.StudyEventRepeatKey,
                "studyeventrepeatkey" => row.StudyEventRepeatKey,
                "formoid" => row.FormOID,
                "formrepeatkey" => row.FormRepeatKey,
                "itemgroupoid" => row.ItemGroupOID,
                "recordposition" => row.ItemGroupRepeatKey,
                "itemgrouprepeatkey" => row.ItemGroupRepeatKey,
                _ => null
            };
        }

        private sealed class Parser
        {
            private readonly IReadOnlyList<Token> _tokens;
            private readonly HashSet<string> _availableFields;
            private int _position;

            public Parser(IReadOnlyList<Token> tokens, IReadOnlyCollection<string> availableFields)
            {
                _tokens = tokens;
                _availableFields = new HashSet<string>(availableFields, StringComparer.OrdinalIgnoreCase);
            }

            public Func<RaveDatasetRow, bool> Parse()
            {
                var left = ParseCondition();

                if (Match(TokenType.End))
                {
                    return left;
                }

                var logical = Current;
                if (!Match(TokenType.And) && !Match(TokenType.Or))
                {
                    throw new InvalidOperationException("筛选表达式语法错误：条件之间只能使用 and 或 or。");
                }

                var right = ParseCondition();
                if (!Match(TokenType.End))
                {
                    throw new InvalidOperationException("筛选表达式语法错误：目前最多支持一个 and/or。");
                }

                return logical.Type == TokenType.And
                    ? row => left(row) && right(row)
                    : row => left(row) || right(row);
            }

            private Func<RaveDatasetRow, bool> ParseCondition()
            {
                var field = Consume(TokenType.Identifier, "筛选表达式语法错误：条件必须以字段名开头。").Text;
                ValidateField(field);

                if (Match(TokenType.Equals))
                {
                    var value = ConsumeValue();
                    return row => string.Equals(GetValue(row, field), value, StringComparison.OrdinalIgnoreCase);
                }

                if (Match(TokenType.NotEquals))
                {
                    var value = ConsumeValue();
                    return row => !string.Equals(GetValue(row, field), value, StringComparison.OrdinalIgnoreCase);
                }

                if (Match(TokenType.Contains))
                {
                    var value = ConsumeValue();
                    return row => GetValue(row, field)?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
                }

                if (Match(TokenType.In))
                {
                    Consume(TokenType.OpenParen, "筛选表达式语法错误：in 后面需要使用括号，例如 AESEV in ('1','2')。");
                    var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ConsumeValue() };

                    while (Match(TokenType.Comma))
                    {
                        values.Add(ConsumeValue());
                    }

                    Consume(TokenType.CloseParen, "筛选表达式语法错误：in 的值列表缺少右括号。");
                    return row =>
                    {
                        var value = GetValue(row, field);
                        return value is not null && values.Contains(value);
                    };
                }

                if (Match(TokenType.Is))
                {
                    if (Match(TokenType.Null))
                    {
                        return row => string.IsNullOrEmpty(GetValue(row, field));
                    }

                    Consume(TokenType.Not, "筛选表达式语法错误：is 后面只能跟 null 或 not null。");
                    Consume(TokenType.Null, "筛选表达式语法错误：is not 后面只能跟 null。");
                    return row => !string.IsNullOrEmpty(GetValue(row, field));
                }

                throw new InvalidOperationException("筛选表达式语法错误：字段名后面只能使用 =、!=、contains、in、is null 或 is not null。");
            }

            private string ConsumeValue()
            {
                if (Current.Type is TokenType.Value or TokenType.Identifier)
                {
                    var value = Current.Text;
                    _position += 1;
                    return value;
                }

                throw new InvalidOperationException("筛选表达式语法错误：缺少筛选值。");
            }

            private void ValidateField(string field)
            {
                if (!_availableFields.Contains(field))
                {
                    throw new InvalidOperationException($"字段 {field} 不存在，请检查字段名。");
                }
            }

            private bool Match(TokenType type)
            {
                if (Current.Type != type)
                {
                    return false;
                }

                _position += 1;
                return true;
            }

            private Token Consume(TokenType type, string errorMessage)
            {
                if (Current.Type != type)
                {
                    throw new InvalidOperationException(errorMessage);
                }

                var token = Current;
                _position += 1;
                return token;
            }

            private Token Current => _tokens[_position];
        }

        private enum TokenType
        {
            Identifier,
            Value,
            Equals,
            NotEquals,
            Contains,
            In,
            Is,
            Not,
            Null,
            And,
            Or,
            OpenParen,
            CloseParen,
            Comma,
            End
        }

        private sealed record Token(TokenType Type, string Text);
    }
}

