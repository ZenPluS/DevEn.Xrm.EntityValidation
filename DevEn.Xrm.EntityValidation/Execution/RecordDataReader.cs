using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DevEn.Xrm.EntityValidation.Validation;

namespace DevEn.Xrm.EntityValidation.Execution
{
    /// <summary>
    /// Turns the record data a client sends (a flat <c>{"attributelogicalname": value}</c> JSON object,
    /// exactly what a form script gets out of <c>getAttribute(...).getValue()</c>) into properly typed SDK
    /// values, using the entity's attribute metadata. Without that conversion an option set would arrive as
    /// a plain number and a money field as a plain decimal, and the rules would compare the wrong things.
    ///
    /// Lookups accept every shape the client may have at hand: a bare id, <c>{"id": "...", "entityType":
    /// "account"}</c>, or the single-element array the form API returns.
    /// </summary>
    internal static class RecordDataReader
    {
        public static void Overlay(
            Entity effectiveEntity,
            string recordDataJson,
            IReadOnlyDictionary<string, AttributeDescriptor> attributes)
        {
            if (string.IsNullOrWhiteSpace(recordDataJson))
            {
                return;
            }

            if (attributes == null)
            {
                throw new InvalidPluginExecutionException(
                    $"The attribute metadata of '{effectiveEntity.LogicalName}' cannot be read, so the record data cannot be interpreted.");
            }

            JObject payload;
            try
            {
                payload = JObject.Parse(recordDataJson);
            }
            catch (JsonException ex)
            {
                throw new InvalidPluginExecutionException($"The record data is not a valid JSON object: {ex.Message}", ex);
            }

            foreach (var property in payload.Properties())
            {
                if (!attributes.TryGetValue(property.Name, out var descriptor))
                {
                    // Dropping it silently would validate a record that doesn't match what the user sees.
                    throw new InvalidPluginExecutionException(
                        $"The record data references '{property.Name}', which is not an attribute of '{effectiveEntity.LogicalName}'.");
                }

                effectiveEntity[descriptor.LogicalName] = Convert(property.Value, descriptor);
            }
        }

        private static object Convert(JToken token, AttributeDescriptor descriptor)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            {
                return null;
            }

            try
            {
                switch (descriptor.AttributeType)
                {
                    case AttributeTypeCode.String:
                    case AttributeTypeCode.Memo:
                        return (string)token;
                    case AttributeTypeCode.Integer:
                        return (int)token;
                    case AttributeTypeCode.BigInt:
                        return (long)token;
                    case AttributeTypeCode.Decimal:
                        return (decimal)token;
                    case AttributeTypeCode.Double:
                        return (double)token;
                    case AttributeTypeCode.Money:
                        return new Money((decimal)token);
                    case AttributeTypeCode.Boolean:
                        return (bool)token;
                    case AttributeTypeCode.DateTime:
                        return ToDateTime(token);
                    case AttributeTypeCode.Picklist:
                    case AttributeTypeCode.State:
                    case AttributeTypeCode.Status:
                        return new OptionSetValue((int)token);
                    case AttributeTypeCode.Uniqueidentifier:
                        return (Guid)token;
                    case AttributeTypeCode.Lookup:
                    case AttributeTypeCode.Customer:
                    case AttributeTypeCode.Owner:
                        return ToEntityReference(token, descriptor);
                    default:
                        throw new InvalidPluginExecutionException(
                            $"Attribute '{descriptor.LogicalName}' is of a type ({descriptor.AttributeType}) that cannot be sent as record data.");
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    $"The value sent for '{descriptor.LogicalName}' is not valid for a {descriptor.AttributeType} attribute: {ex.Message}", ex);
            }
        }

        private static DateTime ToDateTime(JToken token)
        {
            if (token.Type == JTokenType.Date)
            {
                return ((DateTime)token).ToUniversalTime();
            }

            return DateTime.Parse(
                (string)token,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        private static EntityReference ToEntityReference(JToken token, AttributeDescriptor descriptor)
        {
            if (token is JArray array)
            {
                if (array.Count == 0)
                {
                    return null;
                }

                return ToEntityReference(array[0], descriptor);
            }

            if (token is JObject lookup)
            {
                var id = ReadLookupId(lookup);
                var targetEntity = (string)(lookup["entityType"] ?? lookup["logicalName"] ?? lookup["entityName"]) ?? descriptor.LookupTarget;
                return BuildReference(targetEntity, id, descriptor);
            }

            return BuildReference(descriptor.LookupTarget, (Guid)token, descriptor);
        }

        private static Guid ReadLookupId(JObject lookup)
        {
            var id = lookup["id"] ?? lookup["Id"];
            if (id == null)
            {
                throw new InvalidPluginExecutionException("A lookup value must carry an 'id'.");
            }

            return (Guid)id;
        }

        private static EntityReference BuildReference(string targetEntity, Guid id, AttributeDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(targetEntity))
            {
                throw new InvalidPluginExecutionException(
                    $"The table of the lookup '{descriptor.LogicalName}' cannot be determined: send it as {{ \"id\": \"...\", \"entityType\": \"...\" }}.");
            }

            return new EntityReference(targetEntity, id);
        }
    }
}
