using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;
using DevEn.Xrm.EntityValidation.Validation;

namespace DevEn.Xrm.EntityValidation.RuleBuilder
{
    /// <summary>What came out of a single line of the spreadsheet.</summary>
    internal sealed class RuleOutcome
    {
        public RuleOutcome(int rowNumber, string entityLogicalName)
        {
            RowNumber = rowNumber;
            EntityLogicalName = entityLogicalName;
        }

        public int RowNumber { get; }

        public string EntityLogicalName { get; }

        public string ConfigurationName => string.IsNullOrWhiteSpace(EntityLogicalName)
            ? string.Empty
            : RuleSchema.ConfigurationNamePrefix + EntityLogicalName;

        public JObject Rule { get; set; }

        public List<string> Errors { get; } = new List<string>();

        public List<string> Warnings { get; } = new List<string>();

        public bool IsValid => Errors.Count == 0 && Rule != null;

        public string Json => Rule == null ? string.Empty : Rule.ToString(Formatting.None);

        public string Result => Errors.Count > 0 ? string.Join(" | ", Errors) : "OK";
    }

    internal sealed class EntityConfiguration
    {
        public EntityConfiguration(string entityLogicalName, string json, int ruleCount)
        {
            EntityLogicalName = entityLogicalName;
            Json = json;
            RuleCount = ruleCount;
        }

        public string EntityLogicalName { get; }

        /// <summary>Value of the <c>msdyn_name</c> column of the configuration row.</summary>
        public string ConfigurationName => RuleSchema.ConfigurationNamePrefix + EntityLogicalName;

        /// <summary>Value of the <c>msdyn_value</c> column.</summary>
        public string Json { get; }

        public int RuleCount { get; }

        public string FileName => ConfigurationName.Replace(':', '.') + ".json";
    }

    internal sealed class BuildResult
    {
        public List<RuleOutcome> Rules { get; } = new List<RuleOutcome>();

        public List<EntityConfiguration> Configurations { get; } = new List<EntityConfiguration>();

        /// <summary>Warnings that belong to the file rather than to a line.</summary>
        public List<string> Warnings { get; } = new List<string>();

        public IEnumerable<string> Errors =>
            Rules.SelectMany(rule => rule.Errors.Select(error => $"Row {rule.RowNumber}: {error}"));

        public IEnumerable<string> RowWarnings =>
            Rules.SelectMany(rule => rule.Warnings.Select(warning => $"Row {rule.RowNumber}: {warning}"));

        public bool HasErrors => Rules.Any(rule => rule.Errors.Count > 0);
    }

    /// <summary>
    /// Turns the spreadsheet rows into the JSON of a configuration row, and checks every rule with the very
    /// classes that will run in Dataverse: a line that would be rejected at load time is reported here,
    /// with its row number, instead of reaching the environment.
    /// </summary>
    internal static class ConfigurationBuilder
    {
        public static BuildResult Build(IReadOnlyList<RuleRow> rows)
        {
            var result = new BuildResult();
            var registry = RuleEvaluatorRegistry.CreateDefault();
            var validationContext = new RuleConfigurationContext(null, Guid.Empty, new SilentTracingService());

            foreach (var entityGroup in rows.GroupBy(row => row[RuleSchema.Entity].ToLowerInvariant()))
            {
                var entityLogicalName = entityGroup.Key;
                var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var index = 0;

                foreach (var row in entityGroup)
                {
                    var outcome = new RuleOutcome(row.RowNumber, entityLogicalName);
                    result.Rules.Add(outcome);

                    if (string.IsNullOrWhiteSpace(entityLogicalName))
                    {
                        outcome.Errors.Add($"'{RuleSchema.Entity}' is empty.");
                        continue;
                    }

                    BuildRule(row, outcome, index++, usedIds, registry, validationContext);
                }
            }

            result.Rules.Sort((left, right) => left.RowNumber.CompareTo(right.RowNumber));

            foreach (var entityGroup in result.Rules
                .Where(rule => rule.IsValid)
                .GroupBy(rule => rule.EntityLogicalName)
                .OrderBy(group => group.Key))
            {
                var rules = new JArray(entityGroup.Select(rule => rule.Rule).Cast<object>().ToArray());
                result.Configurations.Add(new EntityConfiguration(entityGroup.Key, rules.ToString(Formatting.Indented), rules.Count));
            }

            return result;
        }

        private static void BuildRule(
            RuleRow row,
            RuleOutcome outcome,
            int index,
            HashSet<string> usedIds,
            RuleEvaluatorRegistry registry,
            RuleConfigurationContext validationContext)
        {
            var ruleType = row[RuleSchema.RuleType];
            if (!RuleSchema.TryGetUsage(ruleType, out var usage))
            {
                outcome.Errors.Add($"unknown rule type '{ruleType}'. Allowed: {string.Join(", ", RuleSchema.RuleTypes)}.");
                return;
            }

            var message = row[RuleSchema.Message];
            if (!RuleSchema.Messages.Contains(message, StringComparer.OrdinalIgnoreCase))
            {
                outcome.Warnings.Add($"message '{message}' is not one of the usual ones ({string.Join(", ", RuleSchema.Messages)}).");
            }

            if (!Enum.TryParse<PipelineStage>(row[RuleSchema.Stage], true, out var stage))
            {
                outcome.Errors.Add($"invalid stage '{row[RuleSchema.Stage]}'. Allowed: {string.Join(", ", RuleSchema.Stages)}.");
                return;
            }

            foreach (var requiredColumn in usage.Required)
            {
                if (string.IsNullOrWhiteSpace(row[requiredColumn]))
                {
                    outcome.Errors.Add($"rule type '{ruleType}' needs column '{requiredColumn}'.");
                }
            }

            foreach (var column in RuleSchema.Columns)
            {
                if (IsParameterColumn(column.Key) && !usage.Uses(column.Key) && !string.IsNullOrWhiteSpace(row[column.Key]))
                {
                    outcome.Warnings.Add($"'{column.Key}' is ignored by rule type '{ruleType}'.");
                }
            }

            var ruleId = row[RuleSchema.RuleId];
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                ruleId = $"{outcome.EntityLogicalName}-{ruleType.ToLowerInvariant()}-{index + 1}";
            }

            if (!usedIds.Add(ruleId))
            {
                outcome.Errors.Add($"RuleId '{ruleId}' is already used by another rule of '{outcome.EntityLogicalName}'.");
            }

            var parameters = BuildParameters(row, outcome, ruleType);
            if (outcome.Errors.Count > 0)
            {
                return;
            }

            var executionOrder = ReadInt(row, outcome, RuleSchema.ExecutionOrder) ?? 0;
            var isActive = ReadBool(row, outcome, RuleSchema.IsActive);
            var field = row[RuleSchema.Field];
            var errorMessage = row[RuleSchema.ErrorMessage];

            var rule = new JObject { ["id"] = ruleId, ["message"] = message, ["stage"] = stage.ToString() };

            if (!string.IsNullOrWhiteSpace(field))
            {
                rule["field"] = field;
            }

            rule["ruleType"] = ruleType;

            if (parameters.Count > 0)
            {
                rule["parameters"] = parameters;
            }

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                rule["errorMessage"] = errorMessage;
            }

            if (isActive == false)
            {
                rule["isActive"] = false;
            }

            if (executionOrder != 0)
            {
                rule["executionOrder"] = executionOrder;
            }

            VerifyWithTheEngine(outcome, ruleId, message, stage, field, ruleType, parameters, errorMessage, executionOrder, registry, validationContext);

            if (outcome.Errors.Count == 0)
            {
                outcome.Rule = rule;
            }
        }

        private static void VerifyWithTheEngine(
            RuleOutcome outcome,
            string ruleId,
            string message,
            PipelineStage stage,
            string field,
            string ruleType,
            JObject parameters,
            string errorMessage,
            int executionOrder,
            RuleEvaluatorRegistry registry,
            RuleConfigurationContext validationContext)
        {
            try
            {
                var definition = new ValidationRuleDefinition(
                    ruleId,
                    outcome.EntityLogicalName,
                    message,
                    stage,
                    field,
                    ruleType,
                    parameters.Count > 0 ? parameters.ToString(Formatting.None) : null,
                    errorMessage,
                    executionOrder);

                registry.ValidateConfiguration(definition, validationContext);
            }
            catch (ValidationConfigurationException ex)
            {
                outcome.Errors.Add(ex.Message);
            }
        }

        private static JObject BuildParameters(RuleRow row, RuleOutcome outcome, string ruleType)
        {
            var parameters = new JObject();

            switch (ruleType.ToLowerInvariant())
            {
                case "required":
                    break;
                case "regex":
                    parameters["pattern"] = row[RuleSchema.Pattern];
                    break;
                case "range":
                    AddNumber(parameters, "min", row, outcome, RuleSchema.Min);
                    AddNumber(parameters, "max", row, outcome, RuleSchema.Max);
                    WarnIfBothMissing(parameters, outcome, ruleType, RuleSchema.Min, RuleSchema.Max);
                    break;
                case "daterange":
                    AddText(parameters, "min", row, RuleSchema.Min);
                    AddText(parameters, "max", row, RuleSchema.Max);
                    WarnIfBothMissing(parameters, outcome, ruleType, RuleSchema.Min, RuleSchema.Max);
                    break;
                case "stringlength":
                    AddInteger(parameters, "minLength", row, outcome, RuleSchema.MinLength);
                    AddInteger(parameters, "maxLength", row, outcome, RuleSchema.MaxLength);
                    WarnIfBothMissing(parameters, outcome, ruleType, RuleSchema.MinLength, RuleSchema.MaxLength);
                    break;
                case "allowedvalues":
                    parameters["values"] = ToArray(row[RuleSchema.Values]);
                    if (ReadBool(row, outcome, RuleSchema.CaseSensitive) == true)
                    {
                        parameters["caseSensitive"] = true;
                    }

                    break;
                case "fieldcomparison":
                    parameters["compareToAttribute"] = row[RuleSchema.CompareToAttribute];
                    parameters["operator"] = row[RuleSchema.Operator];
                    break;
                case "expression":
                    var condition = ParseJson(row, outcome, RuleSchema.Condition);
                    if (condition != null)
                    {
                        parameters["condition"] = condition;
                    }

                    break;
                case "atleastoneof":
                    parameters["fields"] = ToArray(row[RuleSchema.Fields]);
                    AddInteger(parameters, "minimumRequired", row, outcome, RuleSchema.MinimumRequired);
                    break;
                case "conditional":
                    var when = new JObject { ["field"] = row[RuleSchema.WhenField] };
                    var whenOperator = row[RuleSchema.WhenOperator];
                    if (!string.IsNullOrWhiteSpace(whenOperator))
                    {
                        when["operator"] = whenOperator;
                    }

                    when["value"] = row[RuleSchema.WhenValue];
                    parameters["when"] = when;

                    var then = new JObject { ["ruleType"] = row[RuleSchema.ThenRuleType] };
                    var thenParameters = ParseJson(row, outcome, RuleSchema.ThenParameters);
                    if (thenParameters != null)
                    {
                        then["parameters"] = thenParameters;
                    }

                    parameters["then"] = then;
                    break;
                case "uniqueness":
                    var scopeFields = ToArray(row[RuleSchema.ScopeFields]);
                    if (scopeFields.Count > 0)
                    {
                        parameters["scopeFields"] = scopeFields;
                    }

                    break;
                case "relatedrecordstate":
                    AddText(parameters, "expectedState", row, RuleSchema.ExpectedState);
                    break;
            }

            return parameters;
        }

        private static void WarnIfBothMissing(JObject parameters, RuleOutcome outcome, string ruleType, string first, string second)
        {
            if (parameters.Count == 0)
            {
                outcome.Warnings.Add($"rule type '{ruleType}' without '{first}' nor '{second}' never fails.");
            }
        }

        private static void AddText(JObject parameters, string name, RuleRow row, string columnKey)
        {
            var value = row[columnKey];
            if (!string.IsNullOrWhiteSpace(value))
            {
                parameters[name] = value;
            }
        }

        private static void AddNumber(JObject parameters, string name, RuleRow row, RuleOutcome outcome, string columnKey)
        {
            var text = row[columnKey];
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (!decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                outcome.Errors.Add($"'{columnKey}' must be a number, found '{text}'.");
                return;
            }

            parameters[name] = value;
        }

        private static void AddInteger(JObject parameters, string name, RuleRow row, RuleOutcome outcome, string columnKey)
        {
            var value = ReadInt(row, outcome, columnKey);
            if (value.HasValue)
            {
                parameters[name] = value.Value;
            }
        }

        private static int? ReadInt(RuleRow row, RuleOutcome outcome, string columnKey)
        {
            var text = row[columnKey];
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (!int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                outcome.Errors.Add($"'{columnKey}' must be a whole number, found '{text}'.");
                return null;
            }

            return value;
        }

        private static bool? ReadBool(RuleRow row, RuleOutcome outcome, string columnKey)
        {
            var text = row[columnKey];
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (bool.TryParse(text, out var value))
            {
                return value;
            }

            if (text == "1" || text == "0")
            {
                return text == "1";
            }

            outcome.Errors.Add($"'{columnKey}' must be TRUE or FALSE, found '{text}'.");
            return null;
        }

        private static JObject ParseJson(RuleRow row, RuleOutcome outcome, string columnKey)
        {
            var text = row[columnKey];
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                return JObject.Parse(text);
            }
            catch (JsonException ex)
            {
                outcome.Errors.Add($"'{columnKey}' is not a valid JSON object: {ex.Message}");
                return null;
            }
        }

        private static JArray ToArray(string text)
        {
            var array = new JArray();
            if (string.IsNullOrWhiteSpace(text))
            {
                return array;
            }

            foreach (var value in text.Split(RuleSchema.ListSeparator).Select(part => part.Trim()).Where(part => part.Length > 0))
            {
                array.Add(value);
            }

            return array;
        }

        private static bool IsParameterColumn(string columnKey)
        {
            return columnKey != RuleSchema.Entity
                && columnKey != RuleSchema.RuleId
                && columnKey != RuleSchema.Message
                && columnKey != RuleSchema.Stage
                && columnKey != RuleSchema.RuleType
                && columnKey != RuleSchema.Field
                && columnKey != RuleSchema.ErrorMessage
                && columnKey != RuleSchema.IsActive
                && columnKey != RuleSchema.ExecutionOrder
                && columnKey != RuleSchema.Notes;
        }

        private sealed class SilentTracingService : ITracingService
        {
            public void Trace(string format, params object[] args)
            {
            }
        }
    }
}
