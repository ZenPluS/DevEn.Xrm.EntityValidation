using System;
using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Engine;
using DevEn.Xrm.EntityValidation.Repository;
using DevEn.Xrm.EntityValidation.Validation;

namespace DevEn.Xrm.EntityValidation.Plugin
{
    /// <summary>
    /// Generic, reusable plugin: registrable on any entity/message/stage (Create, Update, SetState,
    /// SetStateDynamicEntity, Delete, Assign...). Applies the validation rules configured in D365 for that
    /// specific combination, with no entity-specific code.
    ///
    /// The same class must be registered once per entity/message/stage combination to validate (via the
    /// Plugin Registration Tool): "one or more plugins" in the D365 domain sense translates to "one class,
    /// many registrations".
    ///
    /// Rules are read from the out-of-the-box "Configuration" (msdyn_configuration) table: see
    /// <see cref="Repository.DataverseValidationRuleRepository"/> for the exact row/column layout.
    /// </summary>
    public sealed class GenericValidationPlugin : IPlugin
    {
        private readonly RuleEvaluatorRegistry _registry;

        public GenericValidationPlugin(string unsecureConfiguration, string secureConfiguration)
        {
            // Neither parameter is currently used (no configuration/secrets required), but both are kept
            // to match the standard constructor signature recognized by the Plugin Registration Tool.
            _registry = RuleEvaluatorRegistry.CreateDefault();
        }

        public void Execute(IServiceProvider serviceProvider)
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
                var repository = new DataverseValidationRuleRepository(localContext.SystemOrganizationService, tracingService, context.OrganizationId);
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
                tracingService.Trace("Validation configuration error: {0}", ex);
                throw new InvalidPluginExecutionException(
                    "The validation configuration for this record is not valid. Contact your system administrator.", ex);
            }
            catch (Exception ex)
            {
                tracingService.Trace("Unexpected error in the validation plugin: {0}", ex);
                throw new InvalidPluginExecutionException(
                    "An unexpected error occurred during validation. Contact your system administrator.", ex);
            }
        }
    }
}
