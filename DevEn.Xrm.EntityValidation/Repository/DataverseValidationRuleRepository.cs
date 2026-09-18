using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DevEn.Xrm.EntityValidation.Caching;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;
using DevEn.Xrm.EntityValidation.Validation;

namespace DevEn.Xrm.EntityValidation.Repository
{
    /// <summary>
    /// Reads validation rules from the out-of-the-box "Configuration" (<c>msdyn_configuration</c>) table,
    /// with in-memory caching. Deliberately uses an elevated (system) <see cref="IOrganizationService"/>:
    /// reading the plugin's internal configuration must not depend on the security role of the user who
    /// triggered the event.
    ///
    /// One row holds ALL the rules for a single target entity: <c>msdyn_name</c> is
    /// <c>"ValidationRules:{entityLogicalName}"</c> and <c>msdyn_value</c> is a JSON array, e.g.:
    /// <code>
    /// [
    ///   {
    ///     "id": "account-name-required",
    ///     "message": "Create",
    ///     "stage": "PreOperation",
    ///     "field": "name",
    ///     "ruleType": "Required",
    ///     "parameters": {},
    ///     "errorMessage": "The name is required.",
    ///     "isActive": true,
    ///     "executionOrder": 0
    ///   }
    /// ]
    /// </code>
    /// "id", "parameters", "isActive" and "executionOrder" are optional.
    ///
    /// If the row doesn't exist yet, or the table itself can't be queried (e.g. Field Service/Universal
    /// Resource Scheduling isn't installed in this environment), validation is silently skipped for that
    /// entity: this is treated as "no rules configured", not as an error, so the plugin never blocks
    /// Create/Update on entities that simply haven't been configured yet. A row that DOES exist but
    /// contains malformed JSON still fails loudly, since that is an actual configuration mistake to fix.
    /// </summary>
    internal sealed class DataverseValidationRuleRepository : IValidationRuleRepository
    {
        private const string RuleEntityLogicalName = "msdyn_configuration";
        private const string NameAttribute = "msdyn_name";
        private const string ValueAttribute = "msdyn_value";
        private const string StateCodeAttribute = "statecode";
        private const string RuleNamePrefix = "ValidationRules:";
        private const int ActiveStateCode = 0;

        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        private readonly IOrganizationService _organizationService;
        private readonly ITracingService _tracingService;
        private readonly Guid _organizationId;
        private readonly IRuleConfigurationValidator _configurationValidator;

        public DataverseValidationRuleRepository(
            IOrganizationService organizationService,
            ITracingService tracingService,
            Guid organizationId,
            IRuleConfigurationValidator configurationValidator = null)
        {
            _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
            _tracingService = tracingService ?? throw new ArgumentNullException(nameof(tracingService));
            _organizationId = organizationId;
            _configurationValidator = configurationValidator;
        }

        public IReadOnlyList<ValidationRuleDefinition> GetActiveRules(string targetEntityLogicalName, string messageName, PipelineStage stage)
        {
            if (string.IsNullOrWhiteSpace(targetEntityLogicalName))
            {
                throw new ArgumentException("The target entity is required.", nameof(targetEntityLogicalName));
            }

            if (string.IsNullOrWhiteSpace(messageName))
            {
                throw new ArgumentException("The message is required.", nameof(messageName));
            }

            var allRulesForEntity = GetAllRulesForEntity(targetEntityLogicalName);

            // OrderBy (unlike List.Sort) is stable, so rules sharing the same executionOrder - the default
            // 0 for every rule - keep the order they were written in, and so do the messages shown.
            return allRulesForEntity
                .Where(rule => rule.Stage == stage && string.Equals(rule.MessageName, messageName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(rule => rule.ExecutionOrder)
                .ToList();
        }

        private IReadOnlyList<ValidationRuleDefinition> GetAllRulesForEntity(string targetEntityLogicalName)
        {
            // The organization id is part of the key because the cache is static: a sandbox worker
            // process can serve multiple organizations, and it's essential not to mix their rules together.
            var cacheKey = string.Join("|", "rules", _organizationId.ToString("D"), targetEntityLogicalName.ToLowerInvariant());

            try
            {
                return ExpiringCache.GetOrCreate(cacheKey, CacheDuration, () => QueryRulesForEntity(targetEntityLogicalName));
            }
            catch (ConfigurationUnavailableException)
            {
                return Array.Empty<ValidationRuleDefinition>();
            }
        }

        private IReadOnlyList<ValidationRuleDefinition> QueryRulesForEntity(string targetEntityLogicalName)
        {
            var expectedName = RuleNamePrefix + targetEntityLogicalName;

            EntityCollection result;
            try
            {
                var query = new QueryExpression(RuleEntityLogicalName)
                {
                    ColumnSet = new ColumnSet(ValueAttribute),
                    NoLock = true
                };
                query.Criteria.AddCondition(NameAttribute, ConditionOperator.Equal, expectedName);
                query.Criteria.AddCondition(StateCodeAttribute, ConditionOperator.Equal, ActiveStateCode);

                result = _organizationService.RetrieveMultiple(query);
            }
            catch (Exception ex)
            {
                // No configuration available for this entity (missing row, table not present in this
                // environment, no privileges...): skip validation rather than blocking every operation.
                _tracingService.Trace(
                    "Validation configuration for '{0}' is unavailable, skipping validation: {1}", targetEntityLogicalName, ex.Message);
                throw new ConfigurationUnavailableException();
            }

            var rules = new List<ValidationRuleDefinition>();
            foreach (var entity in result.Entities)
            {
                var json = entity.GetAttributeValue<string>(ValueAttribute);
                rules.AddRange(ParseRules(json, targetEntityLogicalName));
            }

            ValidateRuleConfiguration(rules);
            return rules;
        }

        /// <summary>
        /// Checks every rule of the entity - not just the ones matching the current message/stage - while
        /// the configuration is being loaded, so a mistake surfaces at the first save of any record instead
        /// of the first time that particular rule happens to run. All the problems are reported together.
        /// </summary>
        private void ValidateRuleConfiguration(IReadOnlyList<ValidationRuleDefinition> rules)
        {
            if (_configurationValidator == null)
            {
                return;
            }

            var context = new RuleConfigurationContext(_organizationService, _organizationId, _tracingService);
            var errors = new List<string>();
            foreach (var rule in rules)
            {
                try
                {
                    _configurationValidator.ValidateConfiguration(rule, context);
                }
                catch (ValidationConfigurationException ex)
                {
                    errors.Add(ex.Message);
                }
            }

            if (errors.Count > 0)
            {
                throw new ValidationConfigurationException(string.Join(Environment.NewLine, errors.Distinct()));
            }
        }

        private static List<ValidationRuleDefinition> ParseRules(string json, string targetEntityLogicalName)
        {
            var rules = new List<ValidationRuleDefinition>();
            if (string.IsNullOrWhiteSpace(json))
            {
                return rules;
            }

            JArray array;
            try
            {
                array = JArray.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new ValidationConfigurationException(
                    $"The '{ValueAttribute}' configuration for entity '{targetEntityLogicalName}' is not a valid JSON array.", ex);
            }

            for (var index = 0; index < array.Count; index++)
            {
                if (!(array[index] is JObject item))
                {
                    throw new ValidationConfigurationException(
                        $"Rule #{index} for entity '{targetEntityLogicalName}' is not a JSON object.");
                }

                try
                {
                    if (!((bool?)item["isActive"] ?? true))
                    {
                        continue;
                    }

                    var ruleId = (string)item["id"] ?? $"{targetEntityLogicalName}#{index}";

                    rules.Add(new ValidationRuleDefinition(
                        ruleId,
                        targetEntityLogicalName,
                        (string)item["message"],
                        ParseStage((string)item["stage"], ruleId),
                        (string)item["field"],
                        (string)item["ruleType"],
                        item["parameters"]?.ToString(Formatting.None),
                        (string)item["errorMessage"],
                        (int?)item["executionOrder"] ?? 0));
                }
                catch (ValidationConfigurationException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new ValidationConfigurationException(
                        $"Rule #{index} for entity '{targetEntityLogicalName}' has a property of the wrong type: {ex.Message}", ex);
                }
            }

            return rules;
        }

        private static PipelineStage ParseStage(string stageText, string ruleId)
        {
            if (Enum.TryParse(stageText, true, out PipelineStage stage))
            {
                return stage;
            }

            throw new ValidationConfigurationException($"Rule '{ruleId}' specifies an invalid stage: '{stageText}'.");
        }

        /// <summary>
        /// Reports "the configuration couldn't be read" without letting <see cref="ValidationRuleCache"/>
        /// store the resulting empty list: the cache evicts faulted entries, so a transient failure only
        /// skips validation for the current execution instead of for the whole cache window. A row that
        /// genuinely doesn't exist returns an empty list normally, and that IS cached.
        /// </summary>
        private sealed class ConfigurationUnavailableException : Exception
        {
        }
    }
}
