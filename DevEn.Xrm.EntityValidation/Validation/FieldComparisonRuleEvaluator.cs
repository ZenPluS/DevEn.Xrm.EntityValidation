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
            var leftRaw = AttributeValueConverter.GetRawValue(effectiveEntity, rule.RequireAttributeLogicalName());
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

            return ComparisonEvaluator.Compare(
                leftRaw,
                rightRaw,
                comparisonOperator,
                $"Rule {rule.RuleId} (FieldComparison) between '{rule.AttributeLogicalName}' and '{compareToAttribute}'");
        }
    }
}
