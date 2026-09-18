using System;
using DevEn.Xrm.EntityValidation.Configuration;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Validation.Conditions
{
    /// <summary>
    /// Walks a compiled <see cref="ConditionNode"/> against the effective <see cref="Entity"/>.
    /// A comparison where either side is absent/null is vacuously satisfied - the same "absent ⇒ satisfied"
    /// convention every other rule type applies to a single field. Comparisons delegate to
    /// <see cref="ComparisonEvaluator"/>, so "Expression" and "FieldComparison" agree exactly on what
    /// "comparable" means.
    /// </summary>
    internal static class ConditionEvaluator
    {
        public static bool Evaluate(ConditionNode node, Entity entity, string errorContext)
        {
            switch (node)
            {
                case GroupConditionNode group:
                    foreach (var child in group.Children)
                    {
                        var childResult = Evaluate(child, entity, errorContext);
                        if (group.RequiresAll && !childResult)
                        {
                            return false;
                        }

                        if (!group.RequiresAll && childResult)
                        {
                            return true;
                        }
                    }

                    return group.RequiresAll;
                case NegationConditionNode negation:
                    return !Evaluate(negation.Operand, entity, errorContext);
                case ComparisonConditionNode comparison:
                    return EvaluateComparison(comparison, entity, errorContext);
                default:
                    throw new ValidationConfigurationException($"{errorContext}: unsupported condition '{node.GetType().Name}'.");
            }
        }

        private static bool EvaluateComparison(ComparisonConditionNode node, Entity entity, string errorContext)
        {
            var left = Resolve(node.Left, entity, errorContext);
            var right = Resolve(node.Right, entity, errorContext);
            if (left == null || right == null)
            {
                return true;
            }

            return ComparisonEvaluator.Compare(left, right, node.Operator, errorContext);
        }

        private static object Resolve(ConditionOperand operand, Entity entity, string errorContext)
        {
            switch (operand)
            {
                case FieldOperand field:
                    return AttributeValueConverter.GetRawValue(entity, field.FieldName);
                case LiteralOperand literal:
                    return literal.Value;
                case DateOperand date:
                    if (!DateTokenParser.TryParse(date.Token, out var dateValue))
                    {
                        throw new ValidationConfigurationException($"{errorContext}: '{date.Token}' is not a valid date.");
                    }

                    return dateValue;
                case ArithmeticOperand arithmetic:
                    return ResolveArithmetic(arithmetic, entity, errorContext);
                default:
                    throw new ValidationConfigurationException($"{errorContext}: unsupported operand '{operand.GetType().Name}'.");
            }
        }

        private static object ResolveArithmetic(ArithmeticOperand operand, Entity entity, string errorContext)
        {
            var source = Resolve(operand.Source, entity, errorContext);
            var argument = Resolve(operand.Argument, entity, errorContext);
            if (source == null || argument == null)
            {
                return null;
            }

            switch (operand.Operator)
            {
                case ArithmeticOperator.AddDays:
                case ArithmeticOperator.SubtractDays:
                    return ShiftDate(operand.Operator, source, argument, errorContext);
                case ArithmeticOperator.DifferenceInDays:
                    if (!AttributeValueConverter.TryGetDateTime(source, out var fromDate)
                        || !AttributeValueConverter.TryGetDateTime(argument, out var toDate))
                    {
                        throw new ValidationConfigurationException($"{errorContext}: 'differenceInDays' requires two dates.");
                    }

                    return (decimal)(fromDate - toDate).TotalDays;
                default:
                    return Calculate(operand.Operator, source, argument, errorContext);
            }
        }

        private static object ShiftDate(ArithmeticOperator arithmeticOperator, object source, object argument, string errorContext)
        {
            if (!AttributeValueConverter.TryGetDateTime(source, out var date))
            {
                throw new ValidationConfigurationException($"{errorContext}: 'addDays'/'subtractDays' apply to a date, not to '{source.GetType().Name}'.");
            }

            if (!AttributeValueConverter.TryGetNumeric(argument, out var days))
            {
                throw new ValidationConfigurationException($"{errorContext}: 'addDays'/'subtractDays' take a number of days.");
            }

            return arithmeticOperator == ArithmeticOperator.AddDays
                ? date.AddDays((double)days)
                : date.AddDays(-(double)days);
        }

        private static object Calculate(ArithmeticOperator arithmeticOperator, object source, object argument, string errorContext)
        {
            if (!AttributeValueConverter.TryGetNumeric(source, out var left) || !AttributeValueConverter.TryGetNumeric(argument, out var right))
            {
                throw new ValidationConfigurationException(
                    $"{errorContext}: '{ToJsonKey(arithmeticOperator)}' requires numeric operands (use 'addDays'/'subtractDays' with dates).");
            }

            switch (arithmeticOperator)
            {
                case ArithmeticOperator.Add:
                    return left + right;
                case ArithmeticOperator.Subtract:
                    return left - right;
                case ArithmeticOperator.Multiply:
                    return left * right;
                case ArithmeticOperator.Divide:
                    if (right == 0)
                    {
                        throw new ValidationConfigurationException($"{errorContext}: division by zero.");
                    }

                    return left / right;
                default:
                    throw new ValidationConfigurationException($"{errorContext}: unhandled operation '{arithmeticOperator}'.");
            }
        }

        private static string ToJsonKey(ArithmeticOperator arithmeticOperator)
        {
            var name = arithmeticOperator.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
