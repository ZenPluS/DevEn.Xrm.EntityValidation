using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace DevEn.Xrm.EntityValidation.Tests.TestHelpers
{
    /// <summary>
    /// Answers <see cref="RetrieveEntityRequest"/> with a hand-built <see cref="EntityMetadata"/>, or fails
    /// like an environment where metadata can't be read. Everything else is out of scope and throws.
    /// </summary>
    internal sealed class FakeMetadataOrganizationService : IOrganizationService
    {
        private readonly EntityMetadata _entityMetadata;

        private FakeMetadataOrganizationService(EntityMetadata entityMetadata)
        {
            _entityMetadata = entityMetadata;
        }

        public int ExecuteCount { get; private set; }

        public static FakeMetadataOrganizationService WithAttributes(string entityLogicalName, params string[] attributeLogicalNames)
        {
            var metadata = new EntityMetadata();
            SetProperty(metadata, nameof(EntityMetadata.LogicalName), entityLogicalName);

            var attributes = attributeLogicalNames
                .Select(name =>
                {
                    var attribute = new StringAttributeMetadata();
                    SetProperty(attribute, nameof(AttributeMetadata.LogicalName), name);
                    return (AttributeMetadata)attribute;
                })
                .ToArray();

            SetProperty(metadata, nameof(EntityMetadata.Attributes), attributes);
            return new FakeMetadataOrganizationService(metadata);
        }

        public static FakeMetadataOrganizationService Unavailable()
        {
            return new FakeMetadataOrganizationService(null);
        }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            ExecuteCount++;

            if (_entityMetadata == null)
            {
                throw new InvalidOperationException("Metadata is not available in this environment.");
            }

            var response = new RetrieveEntityResponse();
            response.Results["EntityMetadata"] = _entityMetadata;
            return response;
        }

        // The SDK metadata classes seal most setters: tests build them the same way the platform does.
        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName);
            var setter = property?.GetSetMethod(true);
            if (setter != null)
            {
                setter.Invoke(target, new[] { value });
                return;
            }

            var field = FindBackingField(target.GetType(), propertyName);
            if (field == null)
            {
                throw new InvalidOperationException($"Cannot set '{propertyName}' on {target.GetType().Name}.");
            }

            field.SetValue(target, value);
        }

        private static FieldInfo FindBackingField(Type type, string propertyName)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            while (type != null)
            {
                var field = type.GetField($"<{propertyName}>k__BackingField", Flags)
                    ?? type.GetField("_" + char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1), Flags);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        public Guid Create(Entity entity) => throw new NotSupportedException();

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => throw new NotSupportedException();

        public void Update(Entity entity) => throw new NotSupportedException();

        public void Delete(string entityName, Guid id) => throw new NotSupportedException();

        public EntityCollection RetrieveMultiple(QueryBase query) => throw new NotSupportedException();

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
            => throw new NotSupportedException();

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
            => throw new NotSupportedException();
    }
}
