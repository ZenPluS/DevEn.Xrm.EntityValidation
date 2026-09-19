using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using DevEn.Xrm.EntityValidation.Caching;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// What a configuration check or a payload conversion needs to know about an attribute.
    /// </summary>
    internal sealed class AttributeDescriptor
    {
        public AttributeDescriptor(string logicalName, AttributeTypeCode? attributeType, string lookupTarget)
        {
            LogicalName = logicalName;
            AttributeType = attributeType;
            LookupTarget = lookupTarget;
        }

        public string LogicalName { get; }

        public AttributeTypeCode? AttributeType { get; }

        /// <summary>First table a lookup points to; <c>null</c> for every other attribute type.</summary>
        public string LookupTarget { get; }
    }

    /// <summary>
    /// Attribute metadata of an entity, read once and cached, so a configuration check can tell a typo
    /// ("this attribute doesn't exist") from a value that is simply absent, and so a JSON payload can be
    /// turned back into properly typed SDK values.
    /// </summary>
    internal static class EntityAttributeCache
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Returns <c>null</c> - never throws - when the metadata can't be read (no privileges, entity not
        /// present, transient error...): it is up to the caller to decide whether that is fatal.
        /// </summary>
        public static IReadOnlyDictionary<string, AttributeDescriptor> TryGetAttributes(
            IOrganizationService organizationService,
            Guid organizationId,
            ITracingService tracingService,
            string entityLogicalName)
        {
            if (organizationService == null || string.IsNullOrWhiteSpace(entityLogicalName))
            {
                return null;
            }

            var cacheKey = string.Join("|", "attributes", organizationId.ToString("D"), entityLogicalName.ToLowerInvariant());

            try
            {
                return ExpiringCache.GetOrCreate(cacheKey, CacheDuration, () => QueryAttributes(organizationService, entityLogicalName));
            }
            catch (Exception ex)
            {
                tracingService?.Trace(
                    "Attribute metadata for '{0}' is unavailable: {1}", entityLogicalName, ex.Message);
                return null;
            }
        }

        private static IReadOnlyDictionary<string, AttributeDescriptor> QueryAttributes(IOrganizationService organizationService, string entityLogicalName)
        {
            var request = new RetrieveEntityRequest
            {
                LogicalName = entityLogicalName,
                EntityFilters = EntityFilters.Attributes,
                RetrieveAsIfPublished = false
            };

            var response = (RetrieveEntityResponse)organizationService.Execute(request);

            var attributes = new Dictionary<string, AttributeDescriptor>(StringComparer.OrdinalIgnoreCase);
            foreach (var attribute in response.EntityMetadata.Attributes)
            {
                var lookupTarget = (attribute as LookupAttributeMetadata)?.Targets?.FirstOrDefault();
                attributes[attribute.LogicalName] = new AttributeDescriptor(attribute.LogicalName, attribute.AttributeType, lookupTarget);
            }

            return attributes;
        }
    }
}
