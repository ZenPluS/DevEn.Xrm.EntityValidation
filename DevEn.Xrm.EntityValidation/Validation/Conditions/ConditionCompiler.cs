using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Validation.Conditions
{
    /// <summary>
    /// Turns the <c>condition</c> object of an "Expression" rule into a <see cref="ConditionNode"/> tree.
    /// Every error names the exact JSON path it comes from (e.g. <c>condition.all[1].compareTo</c>), since
    /// whoever has to fix it is looking at the JSON, not at the code.
    /// Results are memoized by the rule's parameters JSON: the same condition is compiled once per process
    /// instead of once per validated record.
    /// </summary>
    internal static class ConditionCompiler
    {
        private const int MaxDepth = 20;
        private const int MaxCachedConditions = 500;

        private static readonly ConcurrentDictionary<string, ConditionNode> CompiledByParametersJson =
            new ConcurrentDictionary<string, ConditionNode>(StringComparer.Ordinal);

        private static readonly Dictionary<string, ComparisonOperator> ComparisonOperators =
            new Dictionary<string, ComparisonOperator>(StringComparer.OrdinalIgnoreCase)
            {
                { "==", ComparisonOperator.Equal },
                { "=", ComparisonOperator.Equal },
                { "eq", ComparisonOperator.Equal },
                { "equal", ComparisonOperator.Equal },
                { "equals", ComparisonOperator.Equal },
                { "!=", ComparisonOperator.NotEqual },
                { "<>", ComparisonOperator.NotEqual },
                { "ne", ComparisonOperator.NotEqual },
                { "notEqual", ComparisonOperator.NotEqual },
                { "notEquals", ComparisonOperator.NotEqual },
                { ">", ComparisonOperator.GreaterThan },
                { "gt", ComparisonOperator.GreaterThan },
                { "greaterThan", ComparisonOperator.GreaterThan },
                { ">=", ComparisonOperator.GreaterThanOrEqual },
                { "gte", ComparisonOperator.GreaterThanOrEqual },
                { "greaterThanOrEqual", ComparisonOperator.GreaterThanOrEqual },
                { "<", ComparisonOperator.LessThan },
                { "lt", ComparisonOperator.LessThan },
                { "lessThan", ComparisonOperator.LessThan },
                { "<=", ComparisonOperator.LessThanOrEqual },
                { "lte", ComparisonOperator.LessThanOrEqual },
                { "lessThanOrEqual", ComparisonOperator.LessThanOrEqual }
            };

        private static readonly Dictionary<string, ArithmeticOperator> ArithmeticOperators =
            new Dictionary<string, ArithmeticOperator>(StringComparer.OrdinalIgnoreCase)
            {
                { "add", ArithmeticOperator.Add },
                { "subtract", ArithmeticOperator.Subtract },
                { "multiply", ArithmeticOperator.Multiply },
                { "divide", ArithmeticOperator.Divide },
                { "addDays", ArithmeticOperator.AddDays },
                { "subtractDays", ArithmeticOperator.SubtractDays },
                { "differenceInDays", ArithmeticOperator.DifferenceInDays }
            };

        private static readonly string[] ArithmeticOperatorKeys = ArithmeticOperators.Keys.ToArray();

        public static ConditionNode Compile(ValidationRuleDefinition rule)
        {
            var cacheKey = rule.ParametersJson ?? string.Empty;
            if (CompiledByParametersJson.TryGetValue(cacheKey, out var alreadyCompiled))
            {
                return alreadyCompiled;
            }

            var parameters = RuleParameters.Parse(rule);

            if (Get(parameters, "expression") != null)
            {
                throw new ValidationConfigurationException(
                    $"Rule {rule.RuleId} (Expression): the 'expression' text syntax is no longer supported; describe the condition with the 'condition' object instead (see the README).");
            }

            if (!(Get(parameters, "condition") is JObject conditionObject))
            {
                throw new ValidationConfigurationException(
                    $"Rule {rule.RuleId} (Expression) does not specify the 'condition' object.");
            }

            var compiled = CompileNode(conditionObject, rule.RuleId, "condition", 0);

            if (CompiledByParametersJson.Count >= MaxCachedConditions)
            {
                CompiledByParametersJson.Clear();
            }

            CompiledByParametersJson[cacheKey] = compiled;
            return compiled;
        }

        /// <summary>
        /// Every attribute the compiled condition reads, used to check them against the entity's metadata.
        /// </summary>
        public static IReadOnlyCollection<string> CollectFieldNames(ConditionNode node)
        {
            var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectFieldNames(node, fieldNames);
            return fieldNames;
        }

        private static void CollectFieldNames(ConditionNode node, HashSet<string> fieldNames)
        {
            switch (node)
            {
                case GroupConditionNode group:
                    foreach (var child in group.Children)
                    {
                        CollectFieldNames(child, fieldNames);
                    }

                    break;
                case NegationConditionNode negation:
                    CollectFieldNames(negation.Operand, fieldNames);
                    break;
                case ComparisonConditionNode comparison:
                    CollectFieldNames(comparison.Left, fieldNames);
                    CollectFieldNames(comparison.Right, fieldNames);
                    break;
            }
        }

        private static void CollectFieldNames(ConditionOperand operand, HashSet<string> fieldNames)
        {
            switch (operand)
            {
                case FieldOperand field:
                    fieldNames.Add(field.FieldName);
                    break;
                case ArithmeticOperand arithmetic:
                    CollectFieldNames(arithmetic.Source, fieldNames);
                    CollectFieldNames(arithmetic.Argument, fieldNames);
                    break;
            }
        }

        private static ConditionNode CompileNode(JObject node, string ruleId, string path, int depth)
        {
            if (depth > MaxDepth)
            {
                throw Error(ruleId, path, $"the condition is nested more than {MaxDepth} levels deep.");
            }

            var groupKey = FirstPresent(node, "all", "and", "any", "or");
            var negation = Get(node, "not");

            if (groupKey != null && negation != null)
            {
                throw Error(ruleId, path, "'not' cannot sit in the same object as 'all'/'any'; nest it instead.");
            }

            if (groupKey != null)
            {
                return CompileGroup(node, groupKey, ruleId, path, depth);
            }

            if (negation != null)
            {
                if (!(negation is JObject inner))
                {
                    throw Error(ruleId, $"{path}.not", "must be a condition object.");
                }

                return new NegationConditionNode(CompileNode(inner, ruleId, $"{path}.not", depth + 1));
            }

            return CompileComparison(node, ruleId, path, depth);
        }

        private static ConditionNode CompileGroup(JObject node, string groupKey, string ruleId, string path, int depth)
        {
            var groupPath = $"{path}.{groupKey}";
            if (!(Get(node, groupKey) is JArray children) || children.Count == 0)
            {
                throw Error(ruleId, groupPath, "must be a non-empty array of conditions.");
            }

            var compiledChildren = new List<ConditionNode>(children.Count);
            for (var index = 0; index < children.Count; index++)
            {
                if (!(children[index] is JObject child))
                {
                    throw Error(ruleId, $"{groupPath}[{index}]", "must be a condition object.");
                }

                compiledChildren.Add(CompileNode(child, ruleId, $"{groupPath}[{index}]", depth + 1));
            }

            var requiresAll = string.Equals(groupKey, "all", StringComparison.OrdinalIgnoreCase)
                || string.Equals(groupKey, "and", StringComparison.OrdinalIgnoreCase);

            return new GroupConditionNode(requiresAll, compiledChildren);
        }

        private static ConditionNode CompileComparison(JObject node, string ruleId, string path, int depth)
        {
            var left = CompileLeftSide(node, ruleId, path, depth);

            var operatorText = (string)Get(node, "op") ?? (string)Get(node, "operator");
            if (string.IsNullOrWhiteSpace(operatorText))
            {
                throw Error(ruleId, path, "does not specify 'op' (e.g. \"op\": \"==\").");
            }

            if (!ComparisonOperators.TryGetValue(operatorText.Trim(), out var comparisonOperator))
            {
                throw Error(ruleId, $"{path}.op", $"'{operatorText}' is not a valid operator; use ==, !=, >, >=, < or <= (eq/ne/gt/gte/lt/lte are accepted too).");
            }

            var right = CompileRightSide(node, ruleId, path, depth);
            return new ComparisonConditionNode(left, comparisonOperator, right);
        }

        private static ConditionOperand CompileLeftSide(JObject node, string ruleId, string path, int depth)
        {
            var field = Get(node, "field");
            var left = Get(node, "left");

            if (field != null && left != null)
            {
                throw Error(ruleId, path, "specifies both 'field' and 'left'; keep only one.");
            }

            if (field != null)
            {
                var strayOperation = FirstPresent(node, ArithmeticOperatorKeys);
                if (strayOperation != null)
                {
                    throw Error(ruleId, path, $"'{strayOperation}' cannot sit next to 'field': move it inside a 'left' or 'compareTo' operand object.");
                }

                return CompileFieldOperand(field, ruleId, $"{path}.field");
            }

            if (left is JObject leftObject)
            {
                return CompileOperand(leftObject, ruleId, $"{path}.left", depth + 1);
            }

            if (left != null)
            {
                throw Error(ruleId, $"{path}.left", "must be an operand object (e.g. { \"field\": \"creditlimit\", \"multiply\": 1.1 }).");
            }

            throw Error(ruleId, path, "does not specify what to validate; add \"field\": \"<attribute>\" (or a 'left' operand object).");
        }

        private static ConditionOperand CompileRightSide(JObject node, string ruleId, string path, int depth)
        {
            var presentKey = FirstPresent(node, "value", "date", "compareTo", "compareToField");
            if (presentKey == null)
            {
                throw Error(ruleId, path, "does not specify what to compare to; add 'value', 'date', 'compareToField' or 'compareTo'.");
            }

            var duplicateKey = FirstPresentAfter(node, presentKey, "value", "date", "compareTo", "compareToField");
            if (duplicateKey != null)
            {
                throw Error(ruleId, path, $"specifies more than one right-hand side ('{presentKey}' and '{duplicateKey}'); keep only one.");
            }

            var token = Get(node, presentKey);
            var tokenPath = $"{path}.{presentKey}";

            if (string.Equals(presentKey, "value", StringComparison.OrdinalIgnoreCase))
            {
                return CompileLiteralOperand(token, ruleId, tokenPath);
            }

            if (string.Equals(presentKey, "date", StringComparison.OrdinalIgnoreCase))
            {
                return CompileDateOperand(token, ruleId, tokenPath);
            }

            if (string.Equals(presentKey, "compareToField", StringComparison.OrdinalIgnoreCase))
            {
                return CompileFieldOperand(token, ruleId, tokenPath);
            }

            if (!(token is JObject compareTo))
            {
                throw Error(ruleId, tokenPath, "must be an operand object; use 'compareToField' for a plain attribute name.");
            }

            return CompileOperand(compareTo, ruleId, tokenPath, depth + 1);
        }

        private static ConditionOperand CompileOperand(JObject operand, string ruleId, string path, int depth)
        {
            if (depth > MaxDepth)
            {
                throw Error(ruleId, path, $"the condition is nested more than {MaxDepth} levels deep.");
            }

            var sourceKey = FirstPresent(operand, "field", "value", "date");
            if (sourceKey == null)
            {
                throw Error(ruleId, path, "must specify 'field', 'value' or 'date'.");
            }

            var duplicateSourceKey = FirstPresentAfter(operand, sourceKey, "field", "value", "date");
            if (duplicateSourceKey != null)
            {
                throw Error(ruleId, path, $"specifies both '{sourceKey}' and '{duplicateSourceKey}'; an operand has a single source.");
            }

            var sourceToken = Get(operand, sourceKey);
            var sourcePath = $"{path}.{sourceKey}";
            ConditionOperand source;
            if (string.Equals(sourceKey, "field", StringComparison.OrdinalIgnoreCase))
            {
                source = CompileFieldOperand(sourceToken, ruleId, sourcePath);
            }
            else if (string.Equals(sourceKey, "value", StringComparison.OrdinalIgnoreCase))
            {
                source = CompileLiteralOperand(sourceToken, ruleId, sourcePath);
            }
            else
            {
                source = CompileDateOperand(sourceToken, ruleId, sourcePath);
            }

            string arithmeticKey = null;
            foreach (var candidate in ArithmeticOperators.Keys)
            {
                if (Get(operand, candidate) == null)
                {
                    continue;
                }

                if (arithmeticKey != null)
                {
                    throw Error(ruleId, path, $"applies both '{arithmeticKey}' and '{candidate}'; an operand takes a single operation.");
                }

                arithmeticKey = candidate;
            }

            if (arithmeticKey == null)
            {
                return source;
            }

            var argument = CompileArgument(Get(operand, arithmeticKey), ruleId, $"{path}.{arithmeticKey}", depth);
            return new ArithmeticOperand(source, ArithmeticOperators[arithmeticKey], argument);
        }

        private static ConditionOperand CompileArgument(JToken token, string ruleId, string path, int depth)
        {
            if (token is JObject operandObject)
            {
                return CompileOperand(operandObject, ruleId, path, depth + 1);
            }

            return CompileLiteralOperand(token, ruleId, path);
        }

        private static ConditionOperand CompileFieldOperand(JToken token, string ruleId, string path)
        {
            var fieldName = (token as JValue)?.Value as string;
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                throw Error(ruleId, path, "must be an attribute logical name.");
            }

            return new FieldOperand(fieldName.Trim());
        }

        private static ConditionOperand CompileLiteralOperand(JToken token, string ruleId, string path)
        {
            if (!(token is JValue value))
            {
                throw Error(ruleId, path, "must be a text, number or boolean value.");
            }

            switch (value.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    return new LiteralOperand((decimal)value);
                case JTokenType.Boolean:
                    return new LiteralOperand((bool)value);
                case JTokenType.String:
                    return new LiteralOperand((string)value);
                default:
                    throw Error(ruleId, path, "must be a text, number or boolean value.");
            }
        }

        private static ConditionOperand CompileDateOperand(JToken token, string ruleId, string path)
        {
            var dateText = (token as JValue)?.Value as string;
            if (string.IsNullOrWhiteSpace(dateText))
            {
                throw Error(ruleId, path, "must be a date, e.g. \"2026-01-01\", \"Today\" or \"Today+30d\".");
            }

            dateText = dateText.Trim();

            // Only validated here: the actual value is resolved at evaluation time so that relative tokens
            // follow the clock instead of the moment the condition was compiled.
            if (!DateTokenParser.TryParse(dateText, out _))
            {
                throw Error(ruleId, path, $"'{dateText}' is not a valid date; use an ISO-8601 date or a relative token such as 'Today', 'Now', 'Today+30d', 'Today-1y'.");
            }

            return new DateOperand(dateText);
        }

        private static JToken Get(JObject node, string propertyName)
        {
            return node.GetValue(propertyName, StringComparison.OrdinalIgnoreCase);
        }

        private static string FirstPresent(JObject node, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (Get(node, propertyName) != null)
                {
                    return propertyName;
                }
            }

            return null;
        }

        private static string FirstPresentAfter(JObject node, string alreadyFound, params string[] propertyNames)
        {
            var found = false;
            foreach (var propertyName in propertyNames)
            {
                if (string.Equals(propertyName, alreadyFound, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    continue;
                }

                if (found && Get(node, propertyName) != null)
                {
                    return propertyName;
                }
            }

            return null;
        }

        private static ValidationConfigurationException Error(string ruleId, string path, string message)
        {
            return new ValidationConfigurationException($"Rule {ruleId} (Expression) at '{path}': {message}");
        }
    }
}
