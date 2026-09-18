using System;
using Newtonsoft.Json.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Checks that no OTHER record of the same entity has the same value for this field. Runs a live query
    /// against Dataverse, in the calling user's security context (respects row-level security).
    /// Parameters: <c>{"scopeFields": ["parentaccountid"]}</c> (optional: restrict uniqueness to records
    /// that also share the same value on these additional fields, e.g. "unique per parent account").
    /// </summary>
    internal sealed class UniquenessRuleEvaluator : IRuleEvaluator
    {
        public string RuleType => "Uniqueness";

        public bool IsValid(Entity effectiveEntity, ValidationRuleDefinition rule, IOrganizationService organizationService)
        {
            if (organizationService == null)
            {
                throw new ValidationConfigurationException(
                    $"Rule {rule.RuleId} (Uniqueness) requires Dataverse access, which isn't available in this context.");
            }

            var rawValue = AttributeValueConverter.GetRawValue(effectiveEntity, rule.RequireAttributeLogicalName());
            if (rawValue == null)
            {
                return true;
            }

            var parameters = RuleParameters.Parse(rule);

            var query = new QueryExpression(rule.TargetEntityLogicalName)
            {
                ColumnSet = new ColumnSet(false),
                TopCount = 2,
                NoLock = true
            };
            query.Criteria.AddCondition(rule.AttributeLogicalName, ConditionOperator.Equal, rawValue);

            if (parameters["scopeFields"] is JArray scopeFields)
            {
                foreach (var scopeFieldToken in scopeFields)
                {
                    var scopeField = (string)scopeFieldToken;
                    var scopeValue = AttributeValueConverter.GetRawValue(effectiveEntity, scopeField);
                    if (scopeValue == null)
                    {
                        query.Criteria.AddCondition(scopeField, ConditionOperator.Null);
                    }
                    else
                    {
                        query.Criteria.AddCondition(scopeField, ConditionOperator.Equal, scopeValue);
                    }
                }
            }

            // The record being updated is excluded here rather than in the query: the primary key isn't
            // always "{entitylogicalname}id" (activities all use "activityid"), and a wrong attribute name
            // would make the whole query fail. Two rows are enough to tell "only myself" from "someone else".
            var result = organizationService.RetrieveMultiple(query);
            foreach (var match in result.Entities)
            {
                if (match.Id != effectiveEntity.Id)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
