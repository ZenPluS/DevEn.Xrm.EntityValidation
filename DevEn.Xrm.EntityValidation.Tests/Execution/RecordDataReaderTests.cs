using System;
using System.Collections.Generic;
using DevEn.Xrm.EntityValidation.Execution;
using DevEn.Xrm.EntityValidation.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;

namespace DevEn.Xrm.EntityValidation.Tests.Execution
{
    [TestClass]
    public class RecordDataReaderTests
    {
        private static IReadOnlyDictionary<string, AttributeDescriptor> Attributes()
        {
            return new Dictionary<string, AttributeDescriptor>(StringComparer.OrdinalIgnoreCase)
            {
                { "name", new AttributeDescriptor("name", AttributeTypeCode.String, null) },
                { "numberofemployees", new AttributeDescriptor("numberofemployees", AttributeTypeCode.Integer, null) },
                { "revenue", new AttributeDescriptor("revenue", AttributeTypeCode.Money, null) },
                { "donotemail", new AttributeDescriptor("donotemail", AttributeTypeCode.Boolean, null) },
                { "statuscode", new AttributeDescriptor("statuscode", AttributeTypeCode.Status, null) },
                { "lastonholdtime", new AttributeDescriptor("lastonholdtime", AttributeTypeCode.DateTime, null) },
                { "parentaccountid", new AttributeDescriptor("parentaccountid", AttributeTypeCode.Lookup, "account") },
                { "entityimage", new AttributeDescriptor("entityimage", AttributeTypeCode.Virtual, null) }
            };
        }

        private static Entity Overlay(string json)
        {
            var entity = new Entity("account", Guid.NewGuid());
            RecordDataReader.Overlay(entity, json, Attributes());
            return entity;
        }

        [TestMethod]
        public void Overlay_SimpleTypes_AreConvertedToTheirSdkType()
        {
            var entity = Overlay(@"{
                ""name"": ""ACME"",
                ""numberofemployees"": 12,
                ""revenue"": 1500.50,
                ""donotemail"": true,
                ""statuscode"": 2
            }");

            Assert.AreEqual("ACME", entity.GetAttributeValue<string>("name"));
            Assert.AreEqual(12, entity.GetAttributeValue<int>("numberofemployees"));
            Assert.AreEqual(1500.50m, entity.GetAttributeValue<Money>("revenue").Value);
            Assert.AreEqual(true, entity.GetAttributeValue<bool>("donotemail"));
            Assert.AreEqual(2, entity.GetAttributeValue<OptionSetValue>("statuscode").Value);
        }

        [TestMethod]
        public void Overlay_DateTime_IsReadAsUtc()
        {
            var entity = Overlay(@"{""lastonholdtime"": ""2026-03-01T10:30:00Z""}");

            var value = entity.GetAttributeValue<DateTime>("lastonholdtime");
            Assert.AreEqual(new DateTime(2026, 3, 1, 10, 30, 0, DateTimeKind.Utc), value.ToUniversalTime());
        }

        [TestMethod]
        public void Overlay_LookupAsBareId_UsesTheTableFromMetadata()
        {
            var parentId = Guid.NewGuid();

            var entity = Overlay(@"{""parentaccountid"": """ + parentId + @"""}");

            var reference = entity.GetAttributeValue<EntityReference>("parentaccountid");
            Assert.AreEqual("account", reference.LogicalName);
            Assert.AreEqual(parentId, reference.Id);
        }

        [TestMethod]
        public void Overlay_LookupAsFormApiArray_IsAccepted()
        {
            var parentId = Guid.NewGuid();

            var entity = Overlay(@"{""parentaccountid"": [{""id"": """ + parentId + @""", ""name"": ""Parent"", ""entityType"": ""account""}]}");

            var reference = entity.GetAttributeValue<EntityReference>("parentaccountid");
            Assert.AreEqual("account", reference.LogicalName);
            Assert.AreEqual(parentId, reference.Id);
        }

        [TestMethod]
        public void Overlay_NullValue_ClearsTheAttribute()
        {
            var entity = new Entity("account") { ["name"] = "ACME" };

            RecordDataReader.Overlay(entity, @"{""name"": null}", Attributes());

            Assert.IsTrue(entity.Contains("name"));
            Assert.IsNull(entity["name"]);
        }

        [TestMethod]
        public void Overlay_UnknownAttribute_Throws()
        {
            var exception = Assert.ThrowsException<InvalidPluginExecutionException>(() => Overlay(@"{""naem"": ""ACME""}"));

            StringAssert.Contains(exception.Message, "naem");
        }

        [TestMethod]
        public void Overlay_ValueOfTheWrongType_Throws()
        {
            var exception = Assert.ThrowsException<InvalidPluginExecutionException>(() => Overlay(@"{""numberofemployees"": ""many""}"));

            StringAssert.Contains(exception.Message, "numberofemployees");
        }

        [TestMethod]
        public void Overlay_UnsupportedAttributeType_Throws()
        {
            Assert.ThrowsException<InvalidPluginExecutionException>(() => Overlay(@"{""entityimage"": ""abc""}"));
        }

        [TestMethod]
        public void Overlay_MalformedJson_Throws()
        {
            Assert.ThrowsException<InvalidPluginExecutionException>(() => Overlay(@"{""name"": "));
        }

        [TestMethod]
        public void Overlay_WithoutMetadata_Throws()
        {
            var entity = new Entity("account");

            var exception = Assert.ThrowsException<InvalidPluginExecutionException>(
                () => RecordDataReader.Overlay(entity, @"{""name"": ""ACME""}", null));

            StringAssert.Contains(exception.Message, "metadata");
        }

        [TestMethod]
        public void Overlay_EmptyPayload_LeavesTheRecordUntouched()
        {
            var entity = new Entity("account") { ["name"] = "ACME" };

            RecordDataReader.Overlay(entity, null, null);

            Assert.AreEqual("ACME", entity.GetAttributeValue<string>("name"));
        }
    }
}
