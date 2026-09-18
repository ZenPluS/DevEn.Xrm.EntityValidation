using Newtonsoft.Json.Linq;
using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Checks that at least N of several fields are populated (e.g. "at least one of email/phone/mobile").
    /// Parameters: <c>{"fields": ["emailaddress1", "telephone1"], "minimumRequired": 1}</c>
    /// ("minimumRequired" optional, default 1). <see cref="Model.ValidationRuleDefinition.AttributeLogicalName"/>
    /// is optional for this rule type and only used for tracing/default error messages: since the rule
    /// isn't scoped to a single field, leave it empty or use it as a descriptive label.
    /// </summary>
    internal sealed class AtLeastOneOfRuleEvaluator : IRuleEvaluator
    {
        public string RuleType => "AtLeastOneOf";

        public bool IsValid(Entity effectiveEntity, ValidationRuleDefinition rule, IOrganizationService organizationService)
        {
            var parameters = RuleParameters.Parse(rule);

            if (!(parameters["fields"] is JArray fields) || fields.Count == 0)
            {
                throw new ValidationConfigurationException($"Rule {rule.RuleId} (AtLeastOneOf) does not specify the 'fields' parameter.");
            }

            var minimumRequired = (int?)parameters["minimumRequired"] ?? 1;
            if (minimumRequired < 1)
            {
                throw new ValidationConfigurationException($"Rule {rule.RuleId} (AtLeastOneOf) 'minimumRequired' must be at least 1.");
            }

            var populatedCount = 0;
            foreach (var fieldToken in fields)
            {
                var fieldName = (string)fieldToken;
                var value = AttributeValueConverter.GetRawValue(effectiveEntity, fieldName);
                if (value == null)
                {
                    continue;
                }

                if (!(value is string text) || !string.IsNullOrWhiteSpace(text))
                {
                    populatedCount++;
                }
            }

            return populatedCount >= minimumRequired;
        }
    }
}
