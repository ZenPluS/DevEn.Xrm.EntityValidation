using System;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Tests.TestHelpers
{
    /// <summary>
    /// Builds valid <see cref="ValidationRuleDefinition"/> instances for tests, with convenience values for
    /// fields not relevant to a given test.
    /// </summary>
    internal static class TestRuleBuilder
    {
        public static ValidationRuleDefinition Create(
            string ruleType,
            string attributeLogicalName = "new_field",
            string parametersJson = null,
            string errorMessage = "Validation error.",
            int executionOrder = 0,
            string targetEntityLogicalName = "account",
            string messageName = "Create",
            PipelineStage stage = PipelineStage.PreOperation)
        {
            return new ValidationRuleDefinition(
                Guid.NewGuid().ToString(),
                targetEntityLogicalName,
                messageName,
                stage,
                attributeLogicalName,
                ruleType,
                parametersJson,
                errorMessage,
                executionOrder);
        }
    }
}
