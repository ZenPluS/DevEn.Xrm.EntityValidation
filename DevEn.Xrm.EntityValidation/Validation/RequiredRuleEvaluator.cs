using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Checks that the field is present and not null/empty. Requires no parameters.
    /// </summary>
    internal sealed class RequiredRuleEvaluator : IRuleEvaluator
    {
        public string RuleType => "Required";

        public bool IsValid(Entity effectiveEntity, ValidationRuleDefinition rule, IOrganizationService organizationService)
        {
            var value = AttributeValueConverter.GetRawValue(effectiveEntity, rule.AttributeLogicalName);
            if (value == null)
            {
                return false;
            }

            return !(value is string text) || !string.IsNullOrWhiteSpace(text);
        }
    }
}
