using DevEn.Xrm.EntityValidation.Configuration;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Shared numeric/date/strict-text comparison logic, used by both <see cref="FieldComparisonRuleEvaluator"/>
    /// and the "Expression" rule type, so both agree on exactly what "comparable" means instead of drifting
    /// apart over time.
    /// </summary>
    internal static class ComparisonEvaluator
    {
        /// <summary>
        /// Compares two already-resolved values (numeric, <see cref="System.DateTime"/>, or strict text -
        /// see <see cref="AttributeValueConverter.TryGetComparableTextStrict"/>) and applies
        /// <paramref name="comparisonOperator"/> to the result. Throws <see cref="ValidationConfigurationException"/>
        /// if the two values aren't of a mutually comparable type.
        /// </summary>
        public static bool Compare(object leftRaw, object rightRaw, ComparisonOperator comparisonOperator, string errorContext)
        {
            int comparisonResult;
            if (AttributeValueConverter.TryGetNumeric(leftRaw, out var leftNumeric) && AttributeValueConverter.TryGetNumeric(rightRaw, out var rightNumeric))
            {
                comparisonResult = leftNumeric.CompareTo(rightNumeric);
            }
            else if (AttributeValueConverter.TryGetDateTime(leftRaw, out var leftDate) && AttributeValueConverter.TryGetDateTime(rightRaw, out var rightDate))
            {
                comparisonResult = leftDate.CompareTo(rightDate);
            }
            else if (leftRaw is bool leftBoolean && rightRaw is bool rightBoolean)
            {
                comparisonResult = leftBoolean.CompareTo(rightBoolean);
            }
            else if (AttributeValueConverter.TryGetComparableTextStrict(leftRaw, out var leftText) && AttributeValueConverter.TryGetComparableTextStrict(rightRaw, out var rightText))
            {
                comparisonResult = string.CompareOrdinal(leftText, rightText);
            }
            else
            {
                throw new ValidationConfigurationException($"{errorContext} compares incompatible data types.");
            }

            switch (comparisonOperator)
            {
                case ComparisonOperator.Equal:
                    return comparisonResult == 0;
                case ComparisonOperator.NotEqual:
                    return comparisonResult != 0;
                case ComparisonOperator.GreaterThan:
                    return comparisonResult > 0;
                case ComparisonOperator.GreaterThanOrEqual:
                    return comparisonResult >= 0;
                case ComparisonOperator.LessThan:
                    return comparisonResult < 0;
                case ComparisonOperator.LessThanOrEqual:
                    return comparisonResult <= 0;
                default:
                    throw new ValidationConfigurationException($"Unhandled comparison operator: '{comparisonOperator}'.");
            }
        }
    }
}
