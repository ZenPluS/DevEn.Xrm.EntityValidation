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

        /// <summary>
        /// Returns the field the rule is about, failing with a clear configuration error when it wasn't
        /// specified. Rule types that really do work on a single field call this instead of reading
        /// <see cref="AttributeLogicalName"/> directly, because others (AtLeastOneOf, Expression) carry
        /// their fields in the parameters and legitimately leave it empty.
        /// </summary>
        public string RequireAttributeLogicalName()
        {
            if (string.IsNullOrWhiteSpace(AttributeLogicalName))
            {
                throw new ValidationConfigurationException($"Rule {RuleId} ({RuleType}) does not specify the field to validate.");
            }

            return AttributeLogicalName;
        }
    }
}
