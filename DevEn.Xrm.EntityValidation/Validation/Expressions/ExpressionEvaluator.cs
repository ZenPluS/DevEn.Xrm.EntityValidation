using System;
using DevEn.Xrm.EntityValidation.Configuration;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Validation.Expressions
{
    /// <summary>
    /// Walks an <see cref="ExpressionNode"/> tree against the effective <see cref="Entity"/>.
    /// Null propagation: an absent field flows as <c>null</c> through arithmetic, and a comparison
    /// where either side is <c>null</c> is vacuously <c>true</c> - the same "absent ⇒ satisfied"
    /// convention every other rule evaluator already applies to a single field, generalized to a
    /// compound expression instead of introducing three-valued logic.
    /// Comparisons delegate to <see cref="ComparisonEvaluator"/> so "Expression" and "FieldComparison"
    /// agree exactly on what "comparable" means.
    /// </summary>
    internal static class ExpressionEvaluator
    {
        public static object Evaluate(ExpressionNode node, Entity entity, string ruleId)
        {
            switch (node)
            {
                case FieldReferenceNode fieldReference:
                    return AttributeValueConverter.GetRawValue(entity, fieldReference.FieldName);
                case NumberLiteralNode number:
                    return number.Value;
                case StringLiteralNode text:
                    return text.Value;
                case DateLiteralNode date:
                    return date.Value;
                case BooleanLiteralNode boolean:
                    return boolean.Value;
                case UnaryNode unary:
                    return EvaluateUnary(unary, entity, ruleId);
                case BinaryNode binary:
                    return EvaluateBinary(binary, entity, ruleId);
                default:
                    throw new ValidationConfigurationException($"Rule {ruleId} (Expression): unsupported expression node '{node.GetType().Name}'.");
            }
        }

        public static bool EvaluateAsBoolean(ExpressionNode node, Entity entity, string ruleId)
        {
            var result = Evaluate(node, entity, ruleId);
            return CoerceToBoolean(result, ruleId);
        }

        private static bool CoerceToBoolean(object value, string ruleId)
        {
            if (value is bool booleanValue)
            {
                return booleanValue;
            }

            if (value == null)
            {
                throw new ValidationConfigurationException($"Rule {ruleId} (Expression): a boolean condition evaluated against an absent field; compare it to something instead of using it directly.");
            }

            throw new ValidationConfigurationException($"Rule {ruleId} (Expression): the expression must evaluate to a boolean condition, not a raw value.");
        }

        private static object EvaluateUnary(UnaryNode node, Entity entity, string ruleId)
        {
            switch (node.Operator)
            {
                case UnaryOperator.Not:
                    return !EvaluateAsBoolean(node.Operand, entity, ruleId);
                case UnaryOperator.Negate:
                    var operand = Evaluate(node.Operand, entity, ruleId);
                    if (operand == null)
                    {
                        return null;
                    }

                    if (AttributeValueConverter.TryGetNumeric(operand, out var numericOperand))
                    {
                        return -numericOperand;
                    }

                    throw new ValidationConfigurationException($"Rule {ruleId} (Expression): unary '-' requires a numeric operand.");
                default:
                    throw new ValidationConfigurationException($"Rule {ruleId} (Expression): unhandled unary operator '{node.Operator}'.");
            }
        }

        private static object EvaluateBinary(BinaryNode node, Entity entity, string ruleId)
        {
            switch (node.Operator)
            {
                case BinaryOperator.And:
                    return EvaluateAsBoolean(node.Left, entity, ruleId) && EvaluateAsBoolean(node.Right, entity, ruleId);
                case BinaryOperator.Or:
                    return EvaluateAsBoolean(node.Left, entity, ruleId) || EvaluateAsBoolean(node.Right, entity, ruleId);
                case BinaryOperator.Equal:
                case BinaryOperator.NotEqual:
                case BinaryOperator.Greater:
                case BinaryOperator.GreaterOrEqual:
                case BinaryOperator.Less:
                case BinaryOperator.LessOrEqual:
                    return EvaluateComparison(node, entity, ruleId);
                case BinaryOperator.Add:
                case BinaryOperator.Subtract:
                case BinaryOperator.Multiply:
                case BinaryOperator.Divide:
                    return EvaluateArithmetic(node, entity, ruleId);
                default:
                    throw new ValidationConfigurationException($"Rule {ruleId} (Expression): unhandled binary operator '{node.Operator}'.");
            }
        }

        private static bool EvaluateComparison(BinaryNode node, Entity entity, string ruleId)
        {
            var left = Evaluate(node.Left, entity, ruleId);
            var right = Evaluate(node.Right, entity, ruleId);
            if (left == null || right == null)
            {
                return true;
            }

            var comparisonOperator = ToComparisonOperator(node.Operator);
            return ComparisonEvaluator.Compare(left, right, comparisonOperator, $"Rule {ruleId} (Expression)");
        }

        private static object EvaluateArithmetic(BinaryNode node, Entity entity, string ruleId)
        {
            var left = Evaluate(node.Left, entity, ruleId);
            var right = Evaluate(node.Right, entity, ruleId);
            if (left == null || right == null)
            {
                return null;
            }

            var leftIsDate = AttributeValueConverter.TryGetDateTime(left, out var leftDate);
            var rightIsDate = AttributeValueConverter.TryGetDateTime(right, out var rightDate);

            if (leftIsDate && rightIsDate)
            {
                if (node.Operator == BinaryOperator.Subtract)
                {
                    return (decimal)(leftDate - rightDate).TotalDays;
                }

                throw new ValidationConfigurationException($"Rule {ruleId} (Expression): two dates can only be combined with '-'.");
            }

            if (leftIsDate && AttributeValueConverter.TryGetNumeric(right, out var rightOffset))
            {
                if (node.Operator == BinaryOperator.Add)
                {
                    return leftDate.AddDays((double)rightOffset);
                }

                if (node.Operator == BinaryOperator.Subtract)
                {
                    return leftDate.AddDays(-(double)rightOffset);
                }

                throw new ValidationConfigurationException($"Rule {ruleId} (Expression): a date can only be combined with a number using '+' or '-'.");
            }

            if (rightIsDate)
            {
                throw new ValidationConfigurationException($"Rule {ruleId} (Expression): a number cannot be combined with a date; put the date on the left (e.g. 'somedate + 30').");
            }

            if (AttributeValueConverter.TryGetNumeric(left, out var leftNumber) && AttributeValueConverter.TryGetNumeric(right, out var rightNumber))
            {
                switch (node.Operator)
                {
                    case BinaryOperator.Add:
                        return leftNumber + rightNumber;
                    case BinaryOperator.Subtract:
                        return leftNumber - rightNumber;
                    case BinaryOperator.Multiply:
                        return leftNumber * rightNumber;
                    case BinaryOperator.Divide:
                        if (rightNumber == 0)
                        {
                            throw new ValidationConfigurationException($"Rule {ruleId} (Expression): division by zero.");
                        }

                        return leftNumber / rightNumber;
                    default:
                        throw new ValidationConfigurationException($"Rule {ruleId} (Expression): unhandled arithmetic operator '{node.Operator}'.");
                }
            }

            throw new ValidationConfigurationException($"Rule {ruleId} (Expression): arithmetic requires numeric or date operands.");
        }

        private static ComparisonOperator ToComparisonOperator(BinaryOperator binaryOperator)
        {
            switch (binaryOperator)
            {
                case BinaryOperator.Equal: return ComparisonOperator.Equal;
                case BinaryOperator.NotEqual: return ComparisonOperator.NotEqual;
                case BinaryOperator.Greater: return ComparisonOperator.GreaterThan;
                case BinaryOperator.GreaterOrEqual: return ComparisonOperator.GreaterThanOrEqual;
                case BinaryOperator.Less: return ComparisonOperator.LessThan;
                case BinaryOperator.LessOrEqual: return ComparisonOperator.LessThanOrEqual;
                default:
                    throw new ValidationConfigurationException($"Unhandled comparison operator '{binaryOperator}'.");
            }
        }
    }
}
