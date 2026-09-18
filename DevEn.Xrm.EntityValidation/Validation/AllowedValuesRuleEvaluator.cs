using System;
using Newtonsoft.Json.Linq;
using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Checks that the value is contained in an allowed list.
    /// Parameters: <c>{"values": ["A","B"], "caseSensitive": false}</c> ("caseSensitive" optional, default false).
    /// </summary>
    internal sealed class AllowedValuesRuleEvaluator : IRuleEvaluator
    {
        public string RuleType => "AllowedValues";

        public bool IsValid(Entity effectiveEntity, ValidationRuleDefinition rule, IOrganizationService organizationService)
        {
            var rawValue = AttributeValueConverter.GetRawValue(effectiveEntity, rule.RequireAttributeLogicalName());
            if (rawValue == null)
            {
                return true;
            }

            if (!AttributeValueConverter.TryGetComparableText(rawValue, out var text))
            {
                throw new ValidationConfigurationException(
                    $"Rule {rule.RuleId} (AllowedValues) is applied to a value of field '{rule.AttributeLogicalName}' that cannot be compared as text.");
            }

            var parameters = RuleParameters.Parse(rule);
            if (!(parameters["values"] is JArray allowedValues) || allowedValues.Count == 0)
            {
                throw new ValidationConfigurationException($"Rule {rule.RuleId} (AllowedValues) does not specify the 'values' parameter.");
            }

            var caseSensitive = (bool?)parameters["caseSensitive"] ?? false;
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            foreach (var allowedValue in allowedValues)
            {
                if (string.Equals((string)allowedValue, text, comparison))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
