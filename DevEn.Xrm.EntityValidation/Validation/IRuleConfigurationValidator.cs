using System;
using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Optional capability of an <see cref="IRuleEvaluator"/>: check a rule's configuration without a
    /// record to validate. Implementing it means the rule's mistakes surface as soon as the configuration
    /// is loaded - for every message and stage at once - instead of the first time that specific rule
    /// happens to run.
    /// </summary>
    internal interface IRuleConfigurationValidator
    {
        void ValidateConfiguration(ValidationRuleDefinition rule, RuleConfigurationContext context);
    }

    /// <summary>
    /// What a configuration check is allowed to use: an elevated service (metadata/configuration reads),
    /// the organization id (mandatory in every cache key) and tracing.
    /// </summary>
    internal sealed class RuleConfigurationContext
    {
        public RuleConfigurationContext(IOrganizationService organizationService, Guid organizationId, ITracingService tracingService)
        {
            OrganizationService = organizationService;
            OrganizationId = organizationId;
            TracingService = tracingService;
        }

        public IOrganizationService OrganizationService { get; }

        public Guid OrganizationId { get; }

        public ITracingService TracingService { get; }
    }
}
