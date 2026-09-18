using System;
using System.Collections.Generic;
using DevEn.Xrm.EntityValidation.Configuration;

namespace DevEn.Xrm.EntityValidation.Validation.Expressions
{
    /// <summary>
    /// Recursive-descent parser turning the token list from <see cref="ExpressionLexer"/> into an
    /// <see cref="ExpressionNode"/> tree. Precedence, low to high:
    /// <c>or -&gt; and -&gt; not -&gt; comparison (non-chaining) -&gt; additive -&gt; multiplicative -&gt; unary -&gt; primary</c>.
    /// Mirrors <see cref="ConditionalRuleEvaluator"/>'s nesting-depth guard, applied here to
    /// parenthesized groups and unary-operator chains (the recursions that actually add call-stack depth).
    /// </summary>
    internal sealed class ExpressionParser
    {
        private const int MaxDepth = 20;

        private readonly IReadOnlyList<ExpressionToken> _tokens;
        private readonly string _ruleId;
        private int _position;
        private int _depth;

        private ExpressionParser(IReadOnlyList<ExpressionToken> tokens, string ruleId)
        {
            _tokens = tokens;
            _ruleId = ruleId;
        }

        public static ExpressionNode Parse(IReadOnlyList<ExpressionToken> tokens, string ruleId)
        {
            var parser = new ExpressionParser(tokens, ruleId);
            var node = parser.ParseOr();
            parser.Expect(ExpressionTokenKind.End, "end of expression");
            return node;
        }

        private ExpressionToken Current => _tokens[_position];

        private void Advance()
        {
            if (Current.Kind != ExpressionTokenKind.End)
            {
                _position++;
            }
        }

        private void Expect(ExpressionTokenKind kind, string description)
        {
            if (Current.Kind != kind)
            {
                throw new ValidationConfigurationException($"Rule {_ruleId} (Expression): expected {description} but found '{Current.Text}'.");
            }

            Advance();
        }

        private void EnterNesting()
        {
            _depth++;
            if (_depth > MaxDepth)
            {
                throw new ValidationConfigurationException($"Rule {_ruleId} (Expression): expression nesting exceeds the maximum allowed depth ({MaxDepth}).");
            }
        }

        private void ExitNesting()
        {
            _depth--;
        }

        private ExpressionNode ParseOr()
        {
            var left = ParseAnd();
            while (Current.Kind == ExpressionTokenKind.Or)
            {
                Advance();
                var right = ParseAnd();
                left = new BinaryNode(BinaryOperator.Or, left, right);
            }

            return left;
        }

        private ExpressionNode ParseAnd()
        {
            var left = ParseNot();
            while (Current.Kind == ExpressionTokenKind.And)
            {
                Advance();
                var right = ParseNot();
                left = new BinaryNode(BinaryOperator.And, left, right);
            }

            return left;
        }

        private ExpressionNode ParseNot()
        {
            if (Current.Kind == ExpressionTokenKind.Not)
            {
                Advance();
                EnterNesting();
                try
                {
                    var operand = ParseNot();
                    return new UnaryNode(UnaryOperator.Not, operand);
                }
                finally
                {
                    ExitNesting();
                }
            }

            return ParseComparison();
        }

        private ExpressionNode ParseComparison()
        {
            var left = ParseAdditive();

            var operatorKind = Current.Kind;
            BinaryOperator? comparisonOperator;
            switch (operatorKind)
            {
                case ExpressionTokenKind.Equal: comparisonOperator = BinaryOperator.Equal; break;
                case ExpressionTokenKind.NotEqual: comparisonOperator = BinaryOperator.NotEqual; break;
                case ExpressionTokenKind.Greater: comparisonOperator = BinaryOperator.Greater; break;
                case ExpressionTokenKind.GreaterOrEqual: comparisonOperator = BinaryOperator.GreaterOrEqual; break;
                case ExpressionTokenKind.Less: comparisonOperator = BinaryOperator.Less; break;
                case ExpressionTokenKind.LessOrEqual: comparisonOperator = BinaryOperator.LessOrEqual; break;
                default: comparisonOperator = null; break;
            }

            if (comparisonOperator == null)
            {
                return left;
            }

            Advance();
            var right = ParseAdditive();

            if (IsComparisonOperator(Current.Kind))
            {
                throw new ValidationConfigurationException($"Rule {_ruleId} (Expression): comparisons cannot be chained (e.g. 'a < b < c' is not valid).");
            }

            return new BinaryNode(comparisonOperator.Value, left, right);
        }

        private static bool IsComparisonOperator(ExpressionTokenKind kind)
        {
            switch (kind)
            {
                case ExpressionTokenKind.Equal:
                case ExpressionTokenKind.NotEqual:
                case ExpressionTokenKind.Greater:
                case ExpressionTokenKind.GreaterOrEqual:
                case ExpressionTokenKind.Less:
                case ExpressionTokenKind.LessOrEqual:
                    return true;
                default:
                    return false;
            }
        }

        private ExpressionNode ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (Current.Kind == ExpressionTokenKind.Plus || Current.Kind == ExpressionTokenKind.Minus)
            {
                var binaryOperator = Current.Kind == ExpressionTokenKind.Plus ? BinaryOperator.Add : BinaryOperator.Subtract;
                Advance();
                var right = ParseMultiplicative();
                left = new BinaryNode(binaryOperator, left, right);
            }

            return left;
        }

        private ExpressionNode ParseMultiplicative()
        {
            var left = ParseUnary();
            while (Current.Kind == ExpressionTokenKind.Star || Current.Kind == ExpressionTokenKind.Slash)
            {
                var binaryOperator = Current.Kind == ExpressionTokenKind.Star ? BinaryOperator.Multiply : BinaryOperator.Divide;
                Advance();
                var right = ParseUnary();
                left = new BinaryNode(binaryOperator, left, right);
            }

            return left;
        }

        private ExpressionNode ParseUnary()
        {
            if (Current.Kind == ExpressionTokenKind.Minus)
            {
                Advance();
                EnterNesting();
                try
                {
                    var operand = ParseUnary();
                    return new UnaryNode(UnaryOperator.Negate, operand);
                }
                finally
                {
                    ExitNesting();
                }
            }

            return ParsePrimary();
        }

        private ExpressionNode ParsePrimary()
        {
            var token = Current;
            switch (token.Kind)
            {
                case ExpressionTokenKind.Number:
                    Advance();
                    return new NumberLiteralNode((decimal)token.Value);
                case ExpressionTokenKind.String:
                    Advance();
                    return new StringLiteralNode((string)token.Value);
                case ExpressionTokenKind.DateLiteral:
                    Advance();
                    return new DateLiteralNode((DateTime)token.Value);
                case ExpressionTokenKind.Boolean:
                    Advance();
                    return new BooleanLiteralNode((bool)token.Value);
                case ExpressionTokenKind.Identifier:
                    Advance();
                    return new FieldReferenceNode(token.Text);
                case ExpressionTokenKind.LParen:
                    Advance();
                    EnterNesting();
                    try
                    {
                        var inner = ParseOr();
                        Expect(ExpressionTokenKind.RParen, "')'");
                        return inner;
                    }
                    finally
                    {
                        ExitNesting();
                    }
                default:
                    throw new ValidationConfigurationException($"Rule {_ruleId} (Expression): unexpected token '{token.Text}'.");
            }
        }
    }
}
