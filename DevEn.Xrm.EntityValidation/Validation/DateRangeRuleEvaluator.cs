using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Checks that a date/time value falls within a range. Parameters: <c>{"min": "...", "max": "..."}</c>
    /// (both optional). Each bound accepts an absolute ISO-8601 date/time or a relative token: "Today",
    /// "Now", "Today+30d", "Today-1y", "Today+6m"... (see <see cref="DateTokenParser"/>).
    /// </summary>
    internal sealed class DateRangeRuleEvaluator : IRuleEvaluator
    {
        public string RuleType => "DateRange";

        public bool IsValid(Entity effectiveEntity, ValidationRuleDefinition rule, IOrganizationService organizationService)
        {
            var rawValue = AttributeValueConverter.GetRawValue(effectiveEntity, rule.RequireAttributeLogicalName());
            if (rawValue == null)
            {
                return true;
            }

            if (!AttributeValueConverter.TryGetDateTime(rawValue, out var value))
            {
                throw new ValidationConfigurationException(
                    $"Rule {rule.RuleId} (DateRange) is applied to field '{rule.AttributeLogicalName}', which is not a date/time type.");
            }

            var parameters = RuleParameters.Parse(rule);

            var minToken = (string)parameters["min"];
            if (minToken != null)
            {
                if (!DateTokenParser.TryParse(minToken, out var min))
                {
                    throw new ValidationConfigurationException($"Rule {rule.RuleId} (DateRange) specifies an invalid 'min' value: '{minToken}'.");
                }

                if (value < min)
                {
                    return false;
                }
            }

            var maxToken = (string)parameters["max"];
            if (maxToken != null)
            {
                if (!DateTokenParser.TryParse(maxToken, out var max))
                {
                    throw new ValidationConfigurationException($"Rule {rule.RuleId} (DateRange) specifies an invalid 'max' value: '{maxToken}'.");
                }

                if (value > max)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
