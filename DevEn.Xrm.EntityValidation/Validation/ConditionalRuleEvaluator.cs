using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Applies another rule type only when a condition on a (typically different) field holds; otherwise
    /// the rule is considered satisfied. This is how "required if" ("RequiredIf"), "range if"... are built,
    /// without a dedicated evaluator per combination.
    /// Parameters:
    /// <code>
    /// {
    ///   "when": { "field": "accounttype", "operator": "Equal", "value": "Customer" },
    ///   "then": { "ruleType": "Required", "parameters": {} }
    /// }
    /// </code>
    /// The field validated by "then" is the SAME as this rule's own <c>field</c> (e.g. "new_taxid" above).
    /// "when.operator" only supports "Equal"/"NotEqual" (defaults to "Equal"), compared as text
    /// case-insensitively - covers option sets/strings/booleans, which is what a gate condition needs in
    /// practice.
    /// </summary>
    internal sealed class ConditionalRuleEvaluator : IRuleEvaluator
    {
        private const int MaxNestingDepth = 5;

        [ThreadStatic]
        private static int _nestingDepth;

        private readonly Lazy<RuleEvaluatorRegistry> _registry;

        public ConditionalRuleEvaluator(Lazy<RuleEvaluatorRegistry> registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public string RuleType => "Conditional";

        public bool IsValid(Entity effectiveEntity, ValidationRuleDefinition rule, IOrganizationService organizationService)
        {
            var parameters = RuleParameters.Parse(rule);

            if (!(parameters["when"] is JObject when))
            {
                throw new ValidationConfigurationException($"Rule {rule.RuleId} (Conditional) does not specify a 'when' condition.");
            }

            if (!(parameters["then"] is JObject then))
            {
                throw new ValidationConfigurationException($"Rule {rule.RuleId} (Conditional) does not specify a 'then' rule.");
            }

            if (!EvaluateCondition(effectiveEntity, rule.RuleId, when))
            {
                return true; // Condition not met: the conditional rule simply doesn't apply.
            }

            var innerRuleType = (string)then["ruleType"];
            if (string.IsNullOrWhiteSpace(innerRuleType))
            {
                throw new ValidationConfigurationException($"Rule {rule.RuleId} (Conditional) 'then' does not specify a 'ruleType'.");
            }

            if (!_registry.Value.TryGetEvaluator(innerRuleType, out var innerEvaluator))
            {
                throw new ValidationConfigurationException(
                    $"Rule {rule.RuleId} (Conditional) references an unknown inner rule type: '{innerRuleType}'.");
            }

            var innerRule = new ValidationRuleDefinition(
                rule.RuleId + ":then",
                rule.TargetEntityLogicalName,
                rule.MessageName,
                rule.Stage,
                rule.AttributeLogicalName,
                innerRuleType,
                then["parameters"]?.ToString(Formatting.None),
                rule.ErrorMessage,
                rule.ExecutionOrder);

            if (_nestingDepth >= MaxNestingDepth)
            {
                throw new ValidationConfigurationException(
                    $"Rule {rule.RuleId} (Conditional) exceeds the maximum nesting depth of {MaxNestingDepth}.");
            }

            _nestingDepth++;
            try
            {
                return innerEvaluator.IsValid(effectiveEntity, innerRule, organizationService);
            }
            finally
            {
                _nestingDepth--;
            }
        }

        private static bool EvaluateCondition(Entity effectiveEntity, string ruleId, JObject when)
        {
            var field = (string)when["field"];
            if (string.IsNullOrWhiteSpace(field))
            {
                throw new ValidationConfigurationException($"Rule {ruleId} (Conditional) 'when' does not specify a 'field'.");
            }

            var operatorText = (string)when["operator"] ?? "Equal";
            var isEqualOperator = string.Equals(operatorText, "Equal", StringComparison.OrdinalIgnoreCase);
            if (!isEqualOperator && !string.Equals(operatorText, "NotEqual", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationConfigurationException(
                    $"Rule {ruleId} (Conditional) 'when' only supports 'Equal'/'NotEqual', found: '{operatorText}'.");
            }

            var rawValue = AttributeValueConverter.GetRawValue(effectiveEntity, field);
            var hasText = AttributeValueConverter.TryGetComparableText(rawValue, out var actualText);
            var expectedText = (string)when["value"];

            var isEqual = hasText && string.Equals(actualText, expectedText, StringComparison.OrdinalIgnoreCase);
            return isEqualOperator ? isEqual : !isEqual;
        }
    }
}
