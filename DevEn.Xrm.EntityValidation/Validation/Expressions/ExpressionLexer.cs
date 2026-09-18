using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DevEn.Xrm.EntityValidation.Configuration;

namespace DevEn.Xrm.EntityValidation.Validation.Expressions
{
    /// <summary>
    /// Turns an "Expression" rule's expression string into a flat list of <see cref="ExpressionToken"/>.
    /// Bare words and quoted strings are checked against <see cref="DateTokenParser.TryParseStrict"/> before
    /// falling back to a field reference or a plain string literal, so date tokens (<c>Today</c>,
    /// <c>Today+30d</c>, an absolute ISO-8601 date in quotes...) are recognized with the same vocabulary
    /// "DateRange" uses, without ordinary text being mistaken for a date.
    /// </summary>
    internal static class ExpressionLexer
    {
        public static IReadOnlyList<ExpressionToken> Tokenize(string expression, string ruleId)
        {
            var tokens = new List<ExpressionToken>();
            var length = expression.Length;
            var i = 0;

            while (i < length)
            {
                var c = expression[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                switch (c)
                {
                    case '(':
                        tokens.Add(new ExpressionToken(ExpressionTokenKind.LParen, "("));
                        i++;
                        continue;
                    case ')':
                        tokens.Add(new ExpressionToken(ExpressionTokenKind.RParen, ")"));
                        i++;
                        continue;
                    case '+':
                        tokens.Add(new ExpressionToken(ExpressionTokenKind.Plus, "+"));
                        i++;
                        continue;
                    case '-':
                        tokens.Add(new ExpressionToken(ExpressionTokenKind.Minus, "-"));
                        i++;
                        continue;
                    case '*':
                        tokens.Add(new ExpressionToken(ExpressionTokenKind.Star, "*"));
                        i++;
                        continue;
                    case '/':
                        tokens.Add(new ExpressionToken(ExpressionTokenKind.Slash, "/"));
                        i++;
                        continue;
                    case '=':
                        if (Peek(expression, i + 1) == '=')
                        {
                            tokens.Add(new ExpressionToken(ExpressionTokenKind.Equal, "=="));
                            i += 2;
                            continue;
                        }
                        throw new ValidationConfigurationException($"Rule {ruleId} (Expression): unexpected '=' at position {i}; did you mean '=='?");
                    case '!':
                        if (Peek(expression, i + 1) == '=')
                        {
                            tokens.Add(new ExpressionToken(ExpressionTokenKind.NotEqual, "!="));
                            i += 2;
                            continue;
                        }
                        tokens.Add(new ExpressionToken(ExpressionTokenKind.Not, "!"));
                        i++;
                        continue;
                    case '>':
                        if (Peek(expression, i + 1) == '=')
                        {
                            tokens.Add(new ExpressionToken(ExpressionTokenKind.GreaterOrEqual, ">="));
                            i += 2;
                            continue;
                        }
                        tokens.Add(new ExpressionToken(ExpressionTokenKind.Greater, ">"));
                        i++;
                        continue;
                    case '<':
                        if (Peek(expression, i + 1) == '=')
                        {
                            tokens.Add(new ExpressionToken(ExpressionTokenKind.LessOrEqual, "<="));
                            i += 2;
                            continue;
                        }
                        tokens.Add(new ExpressionToken(ExpressionTokenKind.Less, "<"));
                        i++;
                        continue;
                    case '&':
                        if (Peek(expression, i + 1) == '&')
                        {
                            tokens.Add(new ExpressionToken(ExpressionTokenKind.And, "&&"));
                            i += 2;
                            continue;
                        }
                        throw new ValidationConfigurationException($"Rule {ruleId} (Expression): unexpected '&' at position {i}; did you mean '&&'?");
                    case '|':
                        if (Peek(expression, i + 1) == '|')
                        {
                            tokens.Add(new ExpressionToken(ExpressionTokenKind.Or, "||"));
                            i += 2;
                            continue;
                        }
                        throw new ValidationConfigurationException($"Rule {ruleId} (Expression): unexpected '|' at position {i}; did you mean '||'?");
                    case '\'':
                    case '"':
                        i = ReadString(expression, i, c, ruleId, tokens);
                        continue;
                }

                if (char.IsDigit(c))
                {
                    i = ReadNumber(expression, i, tokens);
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    i = ReadWord(expression, i, tokens);
                    continue;
                }

                throw new ValidationConfigurationException($"Rule {ruleId} (Expression): unexpected character '{c}' at position {i}.");
            }

            tokens.Add(new ExpressionToken(ExpressionTokenKind.End, string.Empty));
            return tokens;
        }

        private static char Peek(string expression, int index) => index < expression.Length ? expression[index] : '\0';

        private static int ReadString(string expression, int start, char quote, string ruleId, List<ExpressionToken> tokens)
        {
            var length = expression.Length;
            var sb = new StringBuilder();
            var i = start + 1;

            while (true)
            {
                if (i >= length)
                {
                    throw new ValidationConfigurationException($"Rule {ruleId} (Expression): unterminated string literal.");
                }

                var c = expression[i];
                if (c == '\\' && i + 1 < length && (expression[i + 1] == quote || expression[i + 1] == '\\'))
                {
                    sb.Append(expression[i + 1]);
                    i += 2;
                    continue;
                }

                if (c == quote)
                {
                    i++;
                    break;
                }

                sb.Append(c);
                i++;
            }

            var text = sb.ToString();
            if (DateTokenParser.TryParseStrict(text, out var dateValue))
            {
                tokens.Add(new ExpressionToken(ExpressionTokenKind.DateLiteral, text, dateValue));
            }
            else
            {
                tokens.Add(new ExpressionToken(ExpressionTokenKind.String, text, text));
            }

            return i;
        }

        private static int ReadNumber(string expression, int start, List<ExpressionToken> tokens)
        {
            var length = expression.Length;
            var i = start;

            while (i < length && char.IsDigit(expression[i]))
            {
                i++;
            }

            if (i < length && expression[i] == '.' && i + 1 < length && char.IsDigit(expression[i + 1]))
            {
                i++;
                while (i < length && char.IsDigit(expression[i]))
                {
                    i++;
                }
            }

            var text = expression.Substring(start, i - start);
            var value = decimal.Parse(text, CultureInfo.InvariantCulture);
            tokens.Add(new ExpressionToken(ExpressionTokenKind.Number, text, value));
            return i;
        }

        private static int ReadWord(string expression, int start, List<ExpressionToken> tokens)
        {
            var length = expression.Length;
            var i = start;

            while (i < length && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_'))
            {
                i++;
            }

            var word = expression.Substring(start, i - start);

            // Greedily try the no-whitespace relative-date-offset form (e.g. "Today+30d") before falling
            // back to the bare word, so "fieldA+30" (no letter suffix) is never mistaken for a date token.
            if (i < length && (expression[i] == '+' || expression[i] == '-'))
            {
                var j = i + 1;
                while (j < length && char.IsDigit(expression[j]))
                {
                    j++;
                }

                if (j > i + 1 && j < length && char.IsLetter(expression[j]) && (j + 1 >= length || !char.IsLetterOrDigit(expression[j + 1])))
                {
                    var candidate = expression.Substring(start, j + 1 - start);
                    if (DateTokenParser.TryParseStrict(candidate, out var offsetDate))
                    {
                        tokens.Add(new ExpressionToken(ExpressionTokenKind.DateLiteral, candidate, offsetDate));
                        return j + 1;
                    }
                }
            }

            if (DateTokenParser.TryParseStrict(word, out var dateValue))
            {
                tokens.Add(new ExpressionToken(ExpressionTokenKind.DateLiteral, word, dateValue));
                return i;
            }

            if (string.Equals(word, "true", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add(new ExpressionToken(ExpressionTokenKind.Boolean, word, true));
                return i;
            }

            if (string.Equals(word, "false", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add(new ExpressionToken(ExpressionTokenKind.Boolean, word, false));
                return i;
            }

            tokens.Add(new ExpressionToken(ExpressionTokenKind.Identifier, word, word));
            return i;
        }
    }
}
