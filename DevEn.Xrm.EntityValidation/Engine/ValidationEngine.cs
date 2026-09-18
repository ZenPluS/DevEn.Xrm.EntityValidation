using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Repository;
using DevEn.Xrm.EntityValidation.Validation;

namespace DevEn.Xrm.EntityValidation.Engine
{
    /// <summary>
    /// Retrieves the rules configured for the current entity/message/stage, evaluates all of them (to
    /// show the user the complete list of errors in a single pass) and, if at least one fails, throws a
    /// single <see cref="InvalidPluginExecutionException"/> containing all the configured messages.
    /// Misconfigured rules are collected the same way and reported together as a single
    /// <see cref="ValidationConfigurationException"/>, which takes precedence over validation messages.
    /// </summary>
    internal sealed class ValidationEngine
    {
        private readonly IValidationRuleRepository _repository;
        private readonly RuleEvaluatorRegistry _registry;
        private readonly ITracingService _tracingService;
        private readonly IOrganizationService _organizationService;

        public ValidationEngine(
            IValidationRuleRepository repository,
            RuleEvaluatorRegistry registry,
            ITracingService tracingService,
            IOrganizationService organizationService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _tracingService = tracingService ?? throw new ArgumentNullException(nameof(tracingService));

            // Deliberately not null-checked: most evaluators don't need Dataverse access at all, and the
            // few that do (Uniqueness, RelatedRecordState) already raise their own clear
            // ValidationConfigurationException if this is null when they actually try to use it.
            _organizationService = organizationService;
        }

        public void ValidateAndThrow(Entity effectiveEntity, string entityLogicalName, string messageName, PipelineStage stage)
        {
            if (effectiveEntity == null)
            {
                throw new ArgumentNullException(nameof(effectiveEntity));
            }

            var rules = _repository.GetActiveRules(entityLogicalName, messageName, stage);
            if (rules.Count == 0)
            {
                return;
            }

            var errorMessages = new List<string>();
            var configurationErrors = new List<string>();
            foreach (var rule in rules)
            {
                bool isValid;
                try
                {
                    if (!_registry.TryGetEvaluator(rule.RuleType, out var evaluator))
                    {
                        throw new ValidationConfigurationException($"Unknown validation rule type: '{rule.RuleType}' (rule {rule.RuleId}).");
                    }

                    isValid = evaluator.IsValid(effectiveEntity, rule, _organizationService);
                }
                catch (ValidationConfigurationException ex)
                {
                    _tracingService.Trace("Validation rule misconfigured: {0} (type={1}): {2}", rule.RuleId, rule.RuleType, ex.Message);
                    configurationErrors.Add(ex.Message);
                    continue;
                }

                if (isValid)
                {
                    continue;
                }

                _tracingService.Trace(
                    "Validation rule failed: {0} (type={1}, field={2})",
                    rule.RuleId,
                    rule.RuleType,
                    rule.AttributeLogicalName);

                var message = string.IsNullOrWhiteSpace(rule.ErrorMessage)
                    ? $"Field '{rule.AttributeLogicalName}' failed validation rule '{rule.RuleType}'."
                    : rule.ErrorMessage;

                errorMessages.Add(message);
            }

            if (configurationErrors.Count > 0)
            {
                throw new ValidationConfigurationException(string.Join(Environment.NewLine, configurationErrors.Distinct()));
            }

            if (errorMessages.Count == 0)
            {
                return;
            }

            throw new InvalidPluginExecutionException(string.Join(Environment.NewLine, errorMessages.Distinct()));
        }
    }
}
