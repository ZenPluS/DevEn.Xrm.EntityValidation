using System;
using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Compares the field's value against another field on the same entity.
    /// Parameters: <c>{"compareToAttribute": "...", "operator": "GreaterThanOrEqual"}</c>.
    /// Supports numeric, date/time, or text pairs; if either field is absent/null the rule is considered
    /// satisfied (use a separate "Required" rule to also enforce presence).
    /// </summary>
    internal sealed class FieldComparisonRuleEvaluator : IRuleEvaluator
    {
        public string RuleType => "FieldComparison";

        public bool IsValid(Entity effectiveEntity, ValidationRuleDefinition rule, IOrganizationService organizationService)
        {
            var leftRaw = AttributeValueConverter.GetRawValue(effectiveEntity, rule.AttributeLogicalName);
            if (leftRaw == null)
            {
                return true;
            }

            var parameters = RuleParameters.Parse(rule);
            var compareToAttribute = (string)parameters["compareToAttribute"];
            if (string.IsNullOrWhiteSpace(compareToAttribute))
            {
                throw new ValidationConfigurationException($"Rule {rule.RuleId} (FieldComparison) does not specify the 'compareToAttribute' parameter.");
            }

            var rightRaw = AttributeValueConverter.GetRawValue(effectiveEntity, compareToAttribute);
            if (rightRaw == null)
            {
                return true;
            }

            var operatorText = (string)parameters["operator"];
            if (!Enum.TryParse(operatorText, true, out ComparisonOperator comparisonOperator))
            {
                throw new ValidationConfigurationException($"Rule {rule.RuleId} (FieldComparison) specifies an invalid operator: '{operatorText}'.");
            }

            int comparisonResult;
            if (AttributeValueConverter.TryGetNumeric(leftRaw, out var leftNumeric) && AttributeValueConverter.TryGetNumeric(rightRaw, out var rightNumeric))
            {
                comparisonResult = leftNumeric.CompareTo(rightNumeric);
            }
            else if (AttributeValueConverter.TryGetDateTime(leftRaw, out var leftDate) && AttributeValueConverter.TryGetDateTime(rightRaw, out var rightDate))
            {
                comparisonResult = leftDate.CompareTo(rightDate);
            }
            else if (AttributeValueConverter.TryGetComparableTextStrict(leftRaw, out var leftText) && AttributeValueConverter.TryGetComparableTextStrict(rightRaw, out var rightText))
            {
                comparisonResult = string.CompareOrdinal(leftText, rightText);
            }
            else
            {
                throw new ValidationConfigurationException(
                    $"Rule {rule.RuleId} (FieldComparison) compares incompatible data types between '{rule.AttributeLogicalName}' and '{compareToAttribute}'.");
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
