using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Engine;
using DevEn.Xrm.EntityValidation.Repository;
using DevEn.Xrm.EntityValidation.Validation;

namespace DevEn.Xrm.EntityValidation.Execution
{
    /// <summary>
    /// Validates a record on demand - typically from a Custom API behind a "Validate" button - instead of
    /// while an operation goes through the pipeline. Two things change compared to
    /// <see cref="ValidationChainRunner"/>: every active rule of the entity is evaluated, whatever message
    /// or stage it was configured for, and the outcome is returned as output parameters rather than thrown,
    /// so the caller can display the problems without the platform reporting an error.
    ///
    /// The record to validate is rebuilt the same way the pipeline rebuilds it from a Pre-Image: the stored
    /// row is the baseline and the values sent by the caller are laid on top. That is what makes the button
    /// work on a record that was never saved, or one with unsaved changes on the form.
    ///
    /// Input parameters (all optional individually, but the record must be identifiable):
    /// <list type="bullet">
    /// <item><c>Target</c>: <see cref="EntityReference"/> or <see cref="Entity"/> - what a table-bound
    /// Custom API passes automatically.</item>
    /// <item><c>EntityName</c> (string) + <c>RecordId</c> (Guid) - for an unbound Custom API. Leave
    /// <c>RecordId</c> empty for a record that doesn't exist yet.</item>
    /// <item><c>Record</c> (<see cref="Entity"/>) and/or <c>RecordData</c> (string, a
    /// <c>{"attributelogicalname": value}</c> JSON object): the current values, including unsaved ones.</item>
    /// </list>
    /// Output parameters: <c>IsValid</c> (bool), <c>Messages</c> (string, one message per line) and
    /// <c>FailedRuleIds</c> (string array).
    /// </summary>
    public sealed class OnDemandValidationRunner
    {
        public const string TargetParameterName = "Target";
        public const string EntityNameParameterName = "EntityName";
        public const string RecordIdParameterName = "RecordId";
        public const string RecordParameterName = "Record";
        public const string RecordDataParameterName = "RecordData";
        public const string IsValidParameterName = "IsValid";
        public const string MessagesParameterName = "Messages";
        public const string FailedRuleIdsParameterName = "FailedRuleIds";

        private readonly RuleEvaluatorRegistry _registry;

        public OnDemandValidationRunner()
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
                var effectiveEntity = BuildEffectiveRecord(localContext);

                var repository = new DataverseValidationRuleRepository(localContext.SystemOrganizationService, tracingService, context.OrganizationId, _registry);
                var engine = new ValidationEngine(repository, _registry, tracingService, localContext.UserOrganizationService);

                var outcome = engine.ValidateAll(effectiveEntity, effectiveEntity.LogicalName);

                context.OutputParameters[IsValidParameterName] = outcome.IsValid;
                context.OutputParameters[MessagesParameterName] = string.Join(Environment.NewLine, outcome.Messages);
                context.OutputParameters[FailedRuleIdsParameterName] = outcome.FailedRuleIds.ToArray();
            }
            catch (InvalidPluginExecutionException)
            {
                throw; // Already a ready-to-show message: the caller sent something we can't work with.
            }
            catch (ValidationConfigurationException ex)
            {
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

        private Entity BuildEffectiveRecord(LocalPluginContext localContext)
        {
            var context = localContext.PluginExecutionContext;
            var inputs = context.InputParameters;

            ResolveRecordIdentity(inputs, out var entityLogicalName, out var recordId);

            var effectiveEntity = new Entity(entityLogicalName, recordId);
            OverlayStoredValues(localContext, effectiveEntity);

            if (inputs.Contains(RecordParameterName) && inputs[RecordParameterName] is Entity providedRecord)
            {
                foreach (var attribute in providedRecord.Attributes)
                {
                    effectiveEntity[attribute.Key] = attribute.Value;
                }
            }

            var recordData = inputs.Contains(RecordDataParameterName) ? inputs[RecordDataParameterName] as string : null;
            if (!string.IsNullOrWhiteSpace(recordData))
            {
                var attributes = EntityAttributeCache.TryGetAttributes(
                    localContext.SystemOrganizationService,
                    context.OrganizationId,
                    localContext.TracingService,
                    entityLogicalName);

                RecordDataReader.Overlay(effectiveEntity, recordData, attributes);
            }

            return effectiveEntity;
        }

        private static void ResolveRecordIdentity(ParameterCollection inputs, out string entityLogicalName, out Guid recordId)
        {
            if (inputs.Contains(TargetParameterName))
            {
                if (inputs[TargetParameterName] is EntityReference reference)
                {
                    entityLogicalName = reference.LogicalName;
                    recordId = reference.Id;
                    return;
                }

                if (inputs[TargetParameterName] is Entity target)
                {
                    entityLogicalName = target.LogicalName;
                    recordId = target.Id;
                    return;
                }
            }

            entityLogicalName = inputs.Contains(EntityNameParameterName) ? inputs[EntityNameParameterName] as string : null;
            if (string.IsNullOrWhiteSpace(entityLogicalName))
            {
                throw new InvalidPluginExecutionException(
                    $"The request must carry the record to validate: either '{TargetParameterName}', or '{EntityNameParameterName}' (+ '{RecordIdParameterName}' when the record already exists).");
            }

            recordId = ReadRecordId(inputs);
        }

        private static Guid ReadRecordId(ParameterCollection inputs)
        {
            if (!inputs.Contains(RecordIdParameterName))
            {
                return Guid.Empty;
            }

            switch (inputs[RecordIdParameterName])
            {
                case Guid id:
                    return id;
                case string text when !string.IsNullOrWhiteSpace(text):
                    if (!Guid.TryParse(text, out var parsed))
                    {
                        throw new InvalidPluginExecutionException($"'{RecordIdParameterName}' is not a valid record id: '{text}'.");
                    }

                    return parsed;
                default:
                    return Guid.Empty;
            }
        }

        private static void OverlayStoredValues(LocalPluginContext localContext, Entity effectiveEntity)
        {
            if (effectiveEntity.Id == Guid.Empty)
            {
                return; // The record doesn't exist yet: only what the caller sends can be validated.
            }

            Entity stored;
            try
            {
                // The calling user's service on purpose: nobody should validate against values they can't read.
                stored = localContext.UserOrganizationService.Retrieve(effectiveEntity.LogicalName, effectiveEntity.Id, new ColumnSet(true));
            }
            catch (Exception ex)
            {
                localContext.TracingService.Trace(
                    "The stored record {0}:{1} could not be read, validating only the values provided by the caller: {2}",
                    effectiveEntity.LogicalName,
                    effectiveEntity.Id,
                    ex.Message);
                return;
            }

            foreach (var attribute in stored.Attributes)
            {
                effectiveEntity[attribute.Key] = attribute.Value;
            }
        }
    }
}
