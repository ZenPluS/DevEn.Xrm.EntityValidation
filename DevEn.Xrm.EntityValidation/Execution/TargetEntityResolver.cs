using System;
using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Configuration;

namespace DevEn.Xrm.EntityValidation.Execution
{
    /// <summary>
    /// Rebuilds the "effective" entity to validate from the plugin execution context, handling the
    /// different shapes a message can take:
    /// <list type="bullet">
    /// <item>Create/Update: <c>InputParameters["Target"]</c> is an <see cref="Entity"/> (for Update it only
    /// contains the changed attributes: it's merged with the Pre-Image, if registered, to get the full
    /// values).</item>
    /// <item>Delete/Assign: <c>InputParameters["Target"]</c> is an <see cref="EntityReference"/>: the full
    /// values are only available from the Pre-Image, if registered.</item>
    /// <item>SetState/SetStateDynamicEntity: <c>InputParameters["EntityMoniker"]</c> is an
    /// <see cref="EntityReference"/>, with the new state/status in "State"/"Status" (not in "Target").</item>
    /// </list>
    /// Whenever the Pre-Image is missing, validation only runs against the attributes actually present in
    /// the message: register a Pre-Image named "PreImage" on the step to also validate fields not changed
    /// by the current operation.
    /// </summary>
    internal static class TargetEntityResolver
    {
        public const string PreImageAlias = "PreImage";

        public static Entity Resolve(IPluginExecutionContext context, ITracingService tracingService)
        {
            WarnIfPreImageMissing(context, tracingService);

            var effective = ResolveBase(context);
            OverlayWellKnownParameters(effective, context);
            return effective;
        }

        private static void WarnIfPreImageMissing(IPluginExecutionContext context, ITracingService tracingService)
        {
            if (GetPreImage(context) != null || string.Equals(context.MessageName, "Create", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            tracingService.Trace(
                "No '{0}' Pre-Image is registered on this step for message '{1}': attributes not carried by the request cannot be validated and their rules will pass silently.",
                PreImageAlias,
                context.MessageName);
        }

        private static Entity ResolveBase(IPluginExecutionContext context)
        {
            if (context.InputParameters.Contains("Target"))
            {
                if (context.InputParameters["Target"] is Entity targetEntity)
                {
                    return MergeWithPreImage(targetEntity, context);
                }

                if (context.InputParameters["Target"] is EntityReference targetReference)
                {
                    return BuildFromReferenceOrPreImage(targetReference, context);
                }
            }

            if (context.InputParameters.Contains("EntityMoniker") && context.InputParameters["EntityMoniker"] is EntityReference moniker)
            {
                return BuildFromReferenceOrPreImage(moniker, context);
            }

            throw new ValidationConfigurationException(
                $"Message '{context.MessageName}' is not supported: unable to determine the entity to validate (no 'Target' or 'EntityMoniker' in InputParameters).");
        }

        private static void OverlayWellKnownParameters(Entity effective, IPluginExecutionContext context)
        {
            if (context.InputParameters.Contains("State") && context.InputParameters["State"] is OptionSetValue state)
            {
                effective["statecode"] = state;
            }

            if (context.InputParameters.Contains("Status") && context.InputParameters["Status"] is OptionSetValue status)
            {
                effective["statuscode"] = status;
            }

            if (context.InputParameters.Contains("Assignee") && context.InputParameters["Assignee"] is EntityReference assignee)
            {
                effective["ownerid"] = assignee;
            }
        }

        private static Entity MergeWithPreImage(Entity target, IPluginExecutionContext context)
        {
            var preImage = GetPreImage(context);
            if (preImage == null)
            {
                return target;
            }

            var merged = new Entity(preImage.LogicalName, preImage.Id);
            foreach (var attribute in preImage.Attributes)
            {
                merged[attribute.Key] = attribute.Value;
            }

            foreach (var attribute in target.Attributes)
            {
                merged[attribute.Key] = attribute.Value;
            }

            return merged;
        }

        private static Entity BuildFromReferenceOrPreImage(EntityReference reference, IPluginExecutionContext context)
        {
            var preImage = GetPreImage(context);
            if (preImage == null)
            {
                return new Entity(reference.LogicalName, reference.Id);
            }

            var copy = new Entity(preImage.LogicalName, preImage.Id);
            foreach (var attribute in preImage.Attributes)
            {
                copy[attribute.Key] = attribute.Value;
            }

            return copy;
        }

        private static Entity GetPreImage(IPluginExecutionContext context)
        {
            if (context.PreEntityImages.Contains(PreImageAlias))
            {
                return context.PreEntityImages[PreImageAlias];
            }

            return null;
        }
    }
}
