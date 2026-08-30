using DevEn.Xrm.EntityValidation.Configuration;

namespace DevEn.Xrm.EntityValidation.Model
{
    /// <summary>
    /// Strongly-typed representation of a single validation rule, regardless of which configuration
    /// record it was parsed from.
    /// </summary>
    internal sealed class ValidationRuleDefinition
    {
        public ValidationRuleDefinition(
            string ruleId,
            string targetEntityLogicalName,
            string messageName,
            PipelineStage stage,
            string attributeLogicalName,
            string ruleType,
            string parametersJson,
            string errorMessage,
            int executionOrder)
        {
            if (string.IsNullOrWhiteSpace(targetEntityLogicalName))
            {
                throw new ValidationConfigurationException($"Validation rule {ruleId} does not specify a target entity.");
            }

            if (string.IsNullOrWhiteSpace(messageName))
            {
                throw new ValidationConfigurationException($"Validation rule {ruleId} does not specify a message (Create/Update/...).");
            }

            if (string.IsNullOrWhiteSpace(attributeLogicalName))
            {
                throw new ValidationConfigurationException($"Validation rule {ruleId} does not specify the field to validate.");
            }

            if (string.IsNullOrWhiteSpace(ruleType))
            {
                throw new ValidationConfigurationException($"Validation rule {ruleId} does not specify a rule type.");
            }

            RuleId = ruleId;
            TargetEntityLogicalName = targetEntityLogicalName;
            MessageName = messageName;
            Stage = stage;
            AttributeLogicalName = attributeLogicalName;
            RuleType = ruleType;
            ParametersJson = parametersJson;
            ErrorMessage = errorMessage;
            ExecutionOrder = executionOrder;
        }

        public string RuleId { get; }

        public string TargetEntityLogicalName { get; }

        public string MessageName { get; }

        public PipelineStage Stage { get; }

        public string AttributeLogicalName { get; }

        public string RuleType { get; }

        public string ParametersJson { get; }

        public string ErrorMessage { get; }

        public int ExecutionOrder { get; }
    }
}
