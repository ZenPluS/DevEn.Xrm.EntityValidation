using System.Linq;
using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;
using DevEn.Xrm.EntityValidation.Validation.Conditions;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Evaluates a condition built from fields, literals, dates and simple arithmetic - the compound checks
    /// that "FieldComparison" and "DateRange" can't express in a single rule.
    /// Parameters: <c>{"condition": {...}}</c>, e.g.
    /// <code>
    /// { "condition": { "all": [
    ///     { "field": "tipo", "op": "==", "value": "Cliente" },
    ///     { "field": "importo", "op": "&lt;=", "compareTo": { "field": "creditlimit", "multiply": 1.1 } } ] } }
    /// </code>
    /// See the "Expression" section in README.md for the full schema and more examples.
    /// </summary>
    internal sealed class ExpressionRuleEvaluator : IRuleEvaluator, IRuleConfigurationValidator
    {
        public string RuleType => "Expression";

        public bool IsValid(Entity effectiveEntity, ValidationRuleDefinition rule, IOrganizationService organizationService)
        {
            var condition = ConditionCompiler.Compile(rule);
            return ConditionEvaluator.Evaluate(condition, effectiveEntity, ErrorContext(rule));
        }

        public void ValidateConfiguration(ValidationRuleDefinition rule, RuleConfigurationContext context)
        {
            var condition = ConditionCompiler.Compile(rule);

            var knownAttributes = EntityAttributeCache.TryGetAttributes(
                context.OrganizationService,
                context.OrganizationId,
                context.TracingService,
                rule.TargetEntityLogicalName);

            if (knownAttributes == null)
            {
                return;
            }

            var unknownAttributes = ConditionCompiler.CollectFieldNames(condition)
                .Where(fieldName => !knownAttributes.ContainsKey(fieldName))
                .OrderBy(fieldName => fieldName)
                .ToList();

            if (unknownAttributes.Count == 0)
            {
                return;
            }

            // Without this check a mistyped attribute would just read as "absent" and the rule would pass
            // silently forever.
            throw new ValidationConfigurationException(
                $"{ErrorContext(rule)}: '{rule.TargetEntityLogicalName}' has no attribute named {string.Join(", ", unknownAttributes.Select(name => $"'{name}'"))}.");
        }

        private static string ErrorContext(ValidationRuleDefinition rule)
        {
            return $"Rule {rule.RuleId} (Expression)";
        }
    }
}
