using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Checks that a numeric value falls within a range. Parameters: <c>{"min": n, "max": n}</c>
    /// (both optional).
    /// </summary>
    internal sealed class RangeRuleEvaluator : IRuleEvaluator
    {
        public string RuleType => "Range";

        public bool IsValid(Entity effectiveEntity, ValidationRuleDefinition rule, IOrganizationService organizationService)
        {
            var rawValue = AttributeValueConverter.GetRawValue(effectiveEntity, rule.AttributeLogicalName);
            if (rawValue == null)
            {
                return true;
            }

            if (!AttributeValueConverter.TryGetNumeric(rawValue, out var numericValue))
            {
                throw new ValidationConfigurationException(
                    $"Rule {rule.RuleId} (Range) is applied to field '{rule.AttributeLogicalName}', which is not a numeric type.");
            }

            var parameters = RuleParameters.Parse(rule);

            var minToken = parameters["min"];
            if (minToken != null && numericValue < (decimal)minToken)
            {
                return false;
            }

            var maxToken = parameters["max"];
            if (maxToken != null && numericValue > (decimal)maxToken)
            {
                return false;
            }

            return true;
        }
    }
}
