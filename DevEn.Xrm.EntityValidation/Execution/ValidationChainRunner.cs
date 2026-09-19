using System;
using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Engine;
using DevEn.Xrm.EntityValidation.Repository;
using DevEn.Xrm.EntityValidation.Validation;

namespace DevEn.Xrm.EntityValidation.Execution
{
    /// <summary>
    /// Runs the whole validation chain for one plugin execution: takes the services the platform provides,
    /// loads the rules configured for the current entity, rebuilds the record to validate and evaluates
    /// every rule matching the current message/stage, turning failures into a message the end user can act on.
    ///
    /// This code ships no <c>IPlugin</c> of its own: the host assembly declares its own plugin class -
    /// registered once per entity/message/stage combination - and forwards to this runner:
    /// <code>
    /// public sealed class AccountValidationPlugin : IPlugin
    /// {
    ///     private static readonly ValidationChainRunner Runner = new ValidationChainRunner();
    ///
    ///     public void Execute(IServiceProvider serviceProvider)
    ///     {
    ///         Runner.Run(serviceProvider);
    ///     }
    /// }
    /// </code>
    /// The runner keeps no per-execution state, so a single instance can serve every execution and thread
    /// of a step; building it once avoids rebuilding the evaluator registry on each call.
    /// </summary>
    public sealed class ValidationChainRunner
    {
        private readonly RuleEvaluatorRegistry _registry;

        public ValidationChainRunner()
        {
            _registry = RuleEvaluatorRegistry.CreateDefault();
        }

        public void Run(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var localContext = new LocalPluginContext(serviceProvider);
            var context = localContext.PluginExecutionContext;
            var tracingService = localContext.TracingService;

            try
            {
                var repository = new DataverseValidationRuleRepository(localContext.SystemOrganizationService, tracingService, context.OrganizationId, _registry);
                var engine = new ValidationEngine(repository, _registry, tracingService, localContext.UserOrganizationService);

                var effectiveEntity = TargetEntityResolver.Resolve(context, tracingService);
                var stage = (PipelineStage)context.Stage;

                engine.ValidateAndThrow(effectiveEntity, context.PrimaryEntityName, context.MessageName, stage);
            }
            catch (InvalidPluginExecutionException)
            {
                throw; // Already a ready-to-show end-user message: no further wrapping needed.
            }
            catch (ValidationConfigurationException ex)
            {
                // The details (rule ids, field logical names, regex patterns...) only go to the trace log:
                // the end user who triggered the operation has no use for them and shouldn't see them.
                tracingService.Trace("Validation configuration error: {0}", ex);
                throw new InvalidPluginExecutionException(
                    "The validation configuration for this record is not valid. Contact your system administrator.", ex);
            }
            catch (Exception ex)
            {
                tracingService.Trace("Unexpected error during validation: {0}", ex);
                throw new InvalidPluginExecutionException(
                    "An unexpected error occurred during validation. Contact your system administrator.", ex);
            }
        }
    }
}
