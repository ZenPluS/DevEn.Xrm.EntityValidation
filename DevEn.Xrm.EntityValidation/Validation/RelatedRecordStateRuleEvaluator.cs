using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Checks that a lookup field points to a record in a given state (Active by default). Runs a live
    /// Retrieve against Dataverse, in the calling user's security context.
    /// Parameters: <c>{"expectedState": "Active"}</c> ("Active" or "Inactive", optional, default "Active").
    /// If the related record can't be read (deleted, no access...), the rule is NOT satisfied: its state
    /// cannot be confirmed.
    /// </summary>
    internal sealed class RelatedRecordStateRuleEvaluator : IRuleEvaluator
    {
        private const int ActiveStateCode = 0;
        private const int InactiveStateCode = 1;

        public string RuleType => "RelatedRecordState";

        public bool IsValid(Entity effectiveEntity, ValidationRuleDefinition rule, IOrganizationService organizationService)
        {
            if (organizationService == null)
            {
                throw new ValidationConfigurationException(
                    $"Rule {rule.RuleId} (RelatedRecordState) requires Dataverse access, which isn't available in this context.");
            }

            var rawValue = AttributeValueConverter.GetRawValue(effectiveEntity, rule.RequireAttributeLogicalName());
            if (rawValue == null)
            {
                return true;
            }

            if (!(rawValue is EntityReference reference))
            {
                throw new ValidationConfigurationException(
                    $"Rule {rule.RuleId} (RelatedRecordState) is applied to field '{rule.AttributeLogicalName}', which is not a lookup.");
            }

            var parameters = RuleParameters.Parse(rule);
            var expectedStateText = (string)parameters["expectedState"] ?? "Active";
            int expectedStateCode;
            if (string.Equals(expectedStateText, "Active", StringComparison.OrdinalIgnoreCase))
            {
                expectedStateCode = ActiveStateCode;
            }
            else if (string.Equals(expectedStateText, "Inactive", StringComparison.OrdinalIgnoreCase))
            {
                expectedStateCode = InactiveStateCode;
            }
            else
            {
                throw new ValidationConfigurationException(
                    $"Rule {rule.RuleId} (RelatedRecordState) specifies an invalid 'expectedState': '{expectedStateText}'.");
            }

            try
            {
                var related = organizationService.Retrieve(reference.LogicalName, reference.Id, new ColumnSet("statecode"));
                var actualStateCode = related.GetAttributeValue<OptionSetValue>("statecode")?.Value;
                return actualStateCode == expectedStateCode;
            }
            catch (Exception)
            {
                // The related record can't be read (deleted, no access...): its state can't be confirmed.
                return false;
            }
        }
    }
}
