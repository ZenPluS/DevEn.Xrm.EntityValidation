using System;
using System.Collections.Generic;
using DevEn.Xrm.EntityValidation.Configuration;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Extensible registry of available evaluators, indexed by <see cref="IRuleEvaluator.RuleType"/>
    /// (case-insensitive). Adding a new rule type only requires a new <see cref="IRuleEvaluator"/> class
    /// and one line in <see cref="CreateDefault"/>.
    /// </summary>
    internal sealed class RuleEvaluatorRegistry
    {
        private readonly IReadOnlyDictionary<string, IRuleEvaluator> _evaluatorsByRuleType;

        public RuleEvaluatorRegistry(IEnumerable<IRuleEvaluator> evaluators)
        {
            if (evaluators == null)
            {
                throw new ArgumentNullException(nameof(evaluators));
            }

            var map = new Dictionary<string, IRuleEvaluator>(StringComparer.OrdinalIgnoreCase);
            foreach (var evaluator in evaluators)
            {
                if (map.ContainsKey(evaluator.RuleType))
                {
                    throw new ValidationConfigurationException($"Duplicate rule type in the registry: '{evaluator.RuleType}'.");
                }

                map[evaluator.RuleType] = evaluator;
            }

            _evaluatorsByRuleType = map;
        }

        public static RuleEvaluatorRegistry CreateDefault()
        {
            // ConditionalRuleEvaluator delegates to this very registry to resolve its inner rule type, so
            // the registry is captured lazily: by the time IsValid ever runs, CreateDefault has already
            // returned and the local variable below is fully assigned.
            RuleEvaluatorRegistry registry = null;
            var lazyRegistry = new Lazy<RuleEvaluatorRegistry>(() => registry);

            registry = new RuleEvaluatorRegistry(new IRuleEvaluator[]
            {
                new RequiredRuleEvaluator(),
                new RegexRuleEvaluator(),
                new RangeRuleEvaluator(),
                new StringLengthRuleEvaluator(),
                new AllowedValuesRuleEvaluator(),
                new FieldComparisonRuleEvaluator(),
                new DateRangeRuleEvaluator(),
                new AtLeastOneOfRuleEvaluator(),
                new UniquenessRuleEvaluator(),
                new RelatedRecordStateRuleEvaluator(),
                new ConditionalRuleEvaluator(lazyRegistry)
            });

            return registry;
        }

        public bool TryGetEvaluator(string ruleType, out IRuleEvaluator evaluator)
        {
            if (ruleType == null)
            {
                evaluator = null;
                return false;
            }

            return _evaluatorsByRuleType.TryGetValue(ruleType, out evaluator);
        }
    }
}
