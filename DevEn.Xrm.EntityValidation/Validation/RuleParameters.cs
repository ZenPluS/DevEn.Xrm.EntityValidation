using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Safely parses the Parameters (JSON) column of a validation rule.
    /// </summary>
    internal static class RuleParameters
    {
        public static JObject Parse(ValidationRuleDefinition rule)
        {
            if (string.IsNullOrWhiteSpace(rule.ParametersJson))
            {
                return new JObject();
            }

            try
            {
                return JObject.Parse(rule.ParametersJson);
            }
            catch (JsonException ex)
            {
                throw new ValidationConfigurationException(
                    $"The parameters for rule {rule.RuleId} ({rule.RuleType}) are not valid JSON.", ex);
            }
        }
    }
}
