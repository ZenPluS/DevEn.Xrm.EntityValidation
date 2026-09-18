using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;
using DevEn.Xrm.EntityValidation.Validation.Expressions;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Evaluates a small boolean/arithmetic expression over the record's fields.
    /// Parameters: <c>{"expression": "importo &lt;= creditlimit * 1.1"}</c>.
    /// Generalizes "FieldComparison" and "DateRange" to the compound conditions those rule types
    /// can't express in a single rule: offset comparisons (<c>dateA &lt;= dateB + 30</c>), AND/OR
    /// combinations (<c>tipo == 'Cliente' &amp;&amp; paese == 'IT'</c>), and arithmetic between fields
    /// (<c>importo &lt;= creditlimit * 1.1</c>). See the "Expression" section in README.md for the
    /// full grammar, the null-field convention, and more worked examples.
    /// </summary>
    internal sealed class ExpressionRuleEvaluator : IRuleEvaluator
    {
        private const int MaxExpressionLength = 500;

        public string RuleType => "Expression";

        public bool IsValid(Entity effectiveEntity, ValidationRuleDefinition rule, IOrganizationService organizationService)
        {
            var parameters = RuleParameters.Parse(rule);
            var expression = (string)parameters["expression"];
            if (string.IsNullOrWhiteSpace(expression))
            {
                throw new ValidationConfigurationException($"Rule {rule.RuleId} (Expression) does not specify the 'expression' parameter.");
            }

            if (expression.Length > MaxExpressionLength)
            {
                throw new ValidationConfigurationException($"Rule {rule.RuleId} (Expression) exceeds the maximum allowed length ({MaxExpressionLength} characters).");
            }

            var tokens = ExpressionLexer.Tokenize(expression, rule.RuleId);
            var root = ExpressionParser.Parse(tokens, rule.RuleId);
            return ExpressionEvaluator.EvaluateAsBoolean(root, effectiveEntity, rule.RuleId);
        }
    }
}
