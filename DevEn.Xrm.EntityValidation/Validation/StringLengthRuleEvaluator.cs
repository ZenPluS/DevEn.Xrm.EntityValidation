using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Checks the length of a text value. Parameters: <c>{"minLength": n, "maxLength": n}</c>
    /// (both optional).
    /// </summary>
    internal sealed class StringLengthRuleEvaluator : IRuleEvaluator
    {
        public string RuleType => "StringLength";

        public bool IsValid(Entity effectiveEntity, ValidationRuleDefinition rule, IOrganizationService organizationService)
        {
            var rawValue = AttributeValueConverter.GetRawValue(effectiveEntity, rule.RequireAttributeLogicalName());
            if (rawValue == null)
            {
                return true;
            }

            if (!(rawValue is string text))
            {
                throw new ValidationConfigurationException(
                    $"Rule {rule.RuleId} (StringLength) is applied to field '{rule.AttributeLogicalName}', which is not a text type.");
            }

            var parameters = RuleParameters.Parse(rule);

            var minLengthToken = parameters["minLength"];
            if (minLengthToken != null && text.Length < (int)minLengthToken)
            {
                return false;
            }

            var maxLengthToken = parameters["maxLength"];
            if (maxLengthToken != null && text.Length > (int)maxLengthToken)
            {
                return false;
            }

            return true;
        }
    }
}
