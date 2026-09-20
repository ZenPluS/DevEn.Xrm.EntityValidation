using System;
using System.Collections.Generic;
using System.Linq;

namespace DevEn.Xrm.EntityValidation.RuleBuilder
{
    /// <summary>
    /// The one place that knows which columns exist, which ones each rule type uses and which of those are
    /// mandatory. The template generator, the conditional formatting that greys out inapplicable cells and
    /// the JSON builder all read from here, so the spreadsheet and the generated configuration can never
    /// drift apart.
    /// </summary>
    internal static class RuleSchema
    {
        public const string ConfigurationNamePrefix = "ValidationRules:";

        /// <summary>Separator for the columns holding a list of values.</summary>
        public const char ListSeparator = ';';

        public static readonly string[] Messages =
        {
            "Create", "Update", "Delete", "Assign", "SetState", "SetStateDynamicEntity"
        };

        public static readonly string[] Stages =
        {
            "PreValidation", "PreOperation", "PostOperation"
        };

        public static readonly string[] RuleTypes =
        {
            "Required", "Regex", "Range", "StringLength", "AllowedValues", "FieldComparison",
            "DateRange", "Expression", "AtLeastOneOf", "Conditional", "Uniqueness", "RelatedRecordState"
        };

        public static readonly string[] Operators =
        {
            "Equal", "NotEqual", "GreaterThan", "GreaterThanOrEqual", "LessThan", "LessThanOrEqual"
        };

        public static readonly string[] WhenOperators = { "Equal", "NotEqual" };

        public static readonly string[] Booleans = { "TRUE", "FALSE" };

        public static readonly string[] States = { "Active", "Inactive" };

        // Column keys, also used as the dictionary keys of a row.
        public const string Entity = "Entity";
        public const string RuleId = "RuleId";
        public const string Message = "Message";
        public const string Stage = "Stage";
        public const string RuleType = "RuleType";
        public const string Field = "Field";
        public const string ErrorMessage = "ErrorMessage";
        public const string IsActive = "IsActive";
        public const string ExecutionOrder = "ExecutionOrder";
        public const string Pattern = "Pattern";
        public const string Min = "Min";
        public const string Max = "Max";
        public const string MinLength = "MinLength";
        public const string MaxLength = "MaxLength";
        public const string Values = "Values";
        public const string CaseSensitive = "CaseSensitive";
        public const string CompareToAttribute = "CompareToAttribute";
        public const string Operator = "Operator";
        public const string Fields = "Fields";
        public const string MinimumRequired = "MinimumRequired";
        public const string WhenField = "WhenField";
        public const string WhenOperator = "WhenOperator";
        public const string WhenValue = "WhenValue";
        public const string ThenRuleType = "ThenRuleType";
        public const string ThenParameters = "ThenParameters";
        public const string ScopeFields = "ScopeFields";
        public const string ExpectedState = "ExpectedState";
        public const string Condition = "Condition";
        public const string Notes = "Notes";

        public static readonly IReadOnlyList<RuleColumn> Columns = new[]
        {
            new RuleColumn(Entity, "Entity", 22, null, "Logical name of the table the rule belongs to. Drives the configuration row name."),
            new RuleColumn(RuleId, "RuleId", 26, null, "Free identifier, shown in the trace log and in FailedRuleIds. Left empty, the generator builds one."),
            new RuleColumn(Message, "Message", 20, nameof(Messages), "Dataverse message the rule applies to."),
            new RuleColumn(Stage, "Stage", 15, nameof(Stages), "Pipeline stage of the step the rule applies to."),
            new RuleColumn(RuleType, "RuleType", 18, nameof(RuleTypes), "Kind of check. It decides which of the following columns you must fill in."),
            new RuleColumn(Field, "Field", 24, null, "Logical name of the attribute being validated."),
            new RuleColumn(ErrorMessage, "ErrorMessage", 45, null, "Message shown to the user. Left empty, a generic one is generated."),
            new RuleColumn(IsActive, "IsActive", 10, nameof(Booleans), "FALSE disables the single rule without deleting it. Empty means TRUE."),
            new RuleColumn(ExecutionOrder, "ExecutionOrder", 14, null, "Whole number. Rules sharing a value keep the order of the sheet."),
            new RuleColumn(Pattern, "Pattern", 38, null, ".NET regular expression."),
            new RuleColumn(Min, "Min", 16, null, "Range: number. DateRange: ISO date or relative token (Today, Today+30d)."),
            new RuleColumn(Max, "Max", 16, null, "Range: number. DateRange: ISO date or relative token (Today, Today+30d)."),
            new RuleColumn(MinLength, "MinLength", 11, null, "Minimum number of characters."),
            new RuleColumn(MaxLength, "MaxLength", 11, null, "Maximum number of characters."),
            new RuleColumn(Values, "Values", 30, null, "Allowed values, separated by ';'."),
            new RuleColumn(CaseSensitive, "CaseSensitive", 13, nameof(Booleans), "TRUE to compare the allowed values case-sensitively. Empty means FALSE."),
            new RuleColumn(CompareToAttribute, "CompareToAttribute", 24, null, "Logical name of the attribute to compare against."),
            new RuleColumn(Operator, "Operator", 20, nameof(Operators), "Comparison operator."),
            new RuleColumn(Fields, "Fields", 34, null, "Attributes to count, separated by ';'."),
            new RuleColumn(MinimumRequired, "MinimumRequired", 15, null, "How many of those attributes must be filled in. Empty means 1."),
            new RuleColumn(WhenField, "WhenField", 22, null, "Attribute the condition is tested on."),
            new RuleColumn(WhenOperator, "WhenOperator", 14, nameof(WhenOperators), "Comparison of the condition. Empty means Equal."),
            new RuleColumn(WhenValue, "WhenValue", 20, null, "Value the condition compares to (text, case-insensitive)."),
            new RuleColumn(ThenRuleType, "ThenRuleType", 18, nameof(RuleTypes), "Rule applied when the condition holds."),
            new RuleColumn(ThenParameters, "ThenParameters", 40, null, "Parameters of that rule, as a JSON object. Empty means {}."),
            new RuleColumn(ScopeFields, "ScopeFields", 30, null, "Attributes narrowing the uniqueness check, separated by ';'."),
            new RuleColumn(ExpectedState, "ExpectedState", 14, nameof(States), "State the related record must be in. Empty means Active."),
            new RuleColumn(Condition, "Condition", 60, null, "Condition object of the Expression rule, as JSON."),
            new RuleColumn(Notes, "Notes", 50, null, "Free notes. Never ends up in the configuration.")
        };

        /// <summary>Columns every rule must fill in, whatever its type.</summary>
        public static readonly string[] AlwaysRequired = { Entity, Message, Stage, RuleType };

        private static readonly IReadOnlyDictionary<string, RuleTypeUsage> Usages = new Dictionary<string, RuleTypeUsage>(StringComparer.OrdinalIgnoreCase)
        {
            { "Required", new RuleTypeUsage("The attribute must be filled in.", new[] { Field }, new string[0]) },
            { "Regex", new RuleTypeUsage("The text must match a regular expression.", new[] { Field, Pattern }, new string[0]) },
            { "Range", new RuleTypeUsage("A number must stay within bounds.", new[] { Field }, new[] { Min, Max }) },
            { "StringLength", new RuleTypeUsage("The text length must stay within bounds.", new[] { Field }, new[] { MinLength, MaxLength }) },
            { "AllowedValues", new RuleTypeUsage("The value must be one of a list.", new[] { Field, Values }, new[] { CaseSensitive }) },
            { "FieldComparison", new RuleTypeUsage("Compare the attribute with another one of the same record.", new[] { Field, CompareToAttribute, Operator }, new string[0]) },
            { "DateRange", new RuleTypeUsage("A date must stay within bounds, absolute or relative.", new[] { Field }, new[] { Min, Max }) },
            { "Expression", new RuleTypeUsage("Compound condition: comparisons, all/any/not, arithmetic.", new[] { Condition }, new[] { Field }) },
            { "AtLeastOneOf", new RuleTypeUsage("At least N of several attributes must be filled in.", new[] { Fields }, new[] { Field, MinimumRequired }) },
            { "Conditional", new RuleTypeUsage("Apply another rule only when a condition holds.", new[] { WhenField, ThenRuleType }, new[] { Field, WhenOperator, WhenValue, ThenParameters }) },
            { "Uniqueness", new RuleTypeUsage("No other record may share this value.", new[] { Field }, new[] { ScopeFields }) },
            { "RelatedRecordState", new RuleTypeUsage("A lookup must point to a record in a given state.", new[] { Field }, new[] { ExpectedState }) }
        };

        public static bool TryGetUsage(string ruleType, out RuleTypeUsage usage)
        {
            usage = null;
            return !string.IsNullOrWhiteSpace(ruleType) && Usages.TryGetValue(ruleType.Trim(), out usage);
        }

        public static RuleTypeUsage GetUsage(string ruleType)
        {
            TryGetUsage(ruleType, out var usage);
            return usage;
        }

        /// <summary>Rule types that use a given column, for the spreadsheet's enable/disable formatting.</summary>
        public static IReadOnlyList<string> RuleTypesUsing(string columnKey)
        {
            return RuleTypes.Where(ruleType => Usages[ruleType].Uses(columnKey)).ToList();
        }

        public static IReadOnlyList<string> RuleTypesRequiring(string columnKey)
        {
            return RuleTypes.Where(ruleType => Usages[ruleType].Required.Contains(columnKey, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        public static string[] ListValues(string listName)
        {
            switch (listName)
            {
                case nameof(Messages): return Messages;
                case nameof(Stages): return Stages;
                case nameof(RuleTypes): return RuleTypes;
                case nameof(Operators): return Operators;
                case nameof(WhenOperators): return WhenOperators;
                case nameof(Booleans): return Booleans;
                case nameof(States): return States;
                default: throw new ArgumentOutOfRangeException(nameof(listName), listName, "Unknown list.");
            }
        }
    }

    internal sealed class RuleColumn
    {
        public RuleColumn(string key, string header, int width, string listName, string help)
        {
            Key = key;
            Header = header;
            Width = width;
            ListName = listName;
            Help = help;
        }

        public string Key { get; }

        public string Header { get; }

        public int Width { get; }

        /// <summary>Name of the list backing the drop-down, <c>null</c> for a free-text column.</summary>
        public string ListName { get; }

        public string Help { get; }
    }

    internal sealed class RuleTypeUsage
    {
        public RuleTypeUsage(string purpose, string[] required, string[] optional)
        {
            Purpose = purpose;
            Required = required;
            Optional = optional;
        }

        public string Purpose { get; }

        public string[] Required { get; }

        public string[] Optional { get; }

        public bool Uses(string columnKey)
        {
            return Required.Contains(columnKey, StringComparer.OrdinalIgnoreCase)
                || Optional.Contains(columnKey, StringComparer.OrdinalIgnoreCase);
        }
    }
}
