using System;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Checks that the text value matches a regex pattern. Parameters: <c>{"pattern": "..."}</c>.
    /// </summary>
    internal sealed class RegexRuleEvaluator : IRuleEvaluator
    {
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

        public string RuleType => "Regex";

        public bool IsValid(Entity effectiveEntity, ValidationRuleDefinition rule, IOrganizationService organizationService)
        {
            var rawValue = AttributeValueConverter.GetRawValue(effectiveEntity, rule.RequireAttributeLogicalName());
            if (!AttributeValueConverter.TryGetComparableText(rawValue, out var text))
            {
                return true;
            }

            var parameters = RuleParameters.Parse(rule);
            var pattern = (string)parameters["pattern"];
            if (string.IsNullOrEmpty(pattern))
            {
                throw new ValidationConfigurationException($"Rule {rule.RuleId} (Regex) does not specify the 'pattern' parameter.");
            }

            try
            {
                // Defensive timeout: a malformed/catastrophic pattern must never be able to hang the plugin.
                return Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant, MatchTimeout);
            }
            catch (ArgumentException ex)
            {
                throw new ValidationConfigurationException($"Invalid regex pattern in rule {rule.RuleId}: '{pattern}'.", ex);
            }
            catch (RegexMatchTimeoutException ex)
            {
                throw new ValidationConfigurationException($"The regex pattern for rule {rule.RuleId} took too long to evaluate.", ex);
            }
        }
    }
}
