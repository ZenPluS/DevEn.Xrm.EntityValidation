using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using DevEn.Xrm.EntityValidation.Caching;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Attribute logical names of an entity, read once from the metadata and cached, so a configuration
    /// check can tell a typo ("this attribute doesn't exist") from a value that is simply absent from the
    /// current message.
    /// </summary>
    internal static class EntityAttributeCache
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Returns <c>null</c> - never throws - when the metadata can't be read (no privileges, entity not
        /// present, transient error...): an unavailable check must not block the configuration.
        /// </summary>
        public static ISet<string> TryGetAttributeNames(RuleConfigurationContext context, string entityLogicalName)
        {
            if (context?.OrganizationService == null || string.IsNullOrWhiteSpace(entityLogicalName))
            {
                return null;
            }

            var cacheKey = string.Join("|", "attributes", context.OrganizationId.ToString("D"), entityLogicalName.ToLowerInvariant());

            try
            {
                return ExpiringCache.GetOrCreate(cacheKey, CacheDuration, () => QueryAttributeNames(context.OrganizationService, entityLogicalName));
            }
            catch (Exception ex)
            {
                context.TracingService?.Trace(
                    "Attribute metadata for '{0}' is unavailable, skipping the attribute-name check: {1}", entityLogicalName, ex.Message);
                return null;
            }
        }

        private static ISet<string> QueryAttributeNames(IOrganizationService organizationService, string entityLogicalName)
        {
            var request = new RetrieveEntityRequest
            {
                LogicalName = entityLogicalName,
                EntityFilters = EntityFilters.Attributes,
                RetrieveAsIfPublished = false
            };

            var response = (RetrieveEntityResponse)organizationService.Execute(request);

            var attributeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attribute in response.EntityMetadata.Attributes)
            {
                attributeNames.Add(attribute.LogicalName);
            }

            return attributeNames;
        }
    }
}
