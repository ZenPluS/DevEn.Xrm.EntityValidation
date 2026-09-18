using System;
using System.Linq;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Plugin;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using FakeXrmEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.Plugin
{
    [TestClass]
    public class TargetEntityResolverTests
    {
        private readonly FakeTracingService _tracing = new FakeTracingService();

        private bool TracedPreImageWarning => _tracing.Messages.Any(m => m.Contains("Pre-Image"));

        [TestMethod]
        public void Resolve_Create_ReturnsTargetAsIs()
        {
            var context = new XrmFakedContext();
            var pluginContext = context.GetDefaultPluginContext();
            pluginContext.MessageName = "Create";
            pluginContext.InputParameters["Target"] = new Entity("account") { ["name"] = "ACME" };

            var effective = TargetEntityResolver.Resolve(pluginContext, _tracing);

            Assert.AreEqual("ACME", effective.GetAttributeValue<string>("name"));
        }

        [TestMethod]
        public void Resolve_UpdateWithPreImage_MergesUnchangedAttributes()
        {
            var context = new XrmFakedContext();
            var pluginContext = context.GetDefaultPluginContext();
            pluginContext.MessageName = "Update";
            var accountId = Guid.NewGuid();

            pluginContext.InputParameters["Target"] = new Entity("account", accountId) { ["telephone1"] = "000" };
            pluginContext.PreEntityImages[TargetEntityResolver.PreImageAlias] =
                new Entity("account", accountId) { ["name"] = "ACME", ["telephone1"] = "111" };

            var effective = TargetEntityResolver.Resolve(pluginContext, _tracing);

            Assert.AreEqual("ACME", effective.GetAttributeValue<string>("name"));
            Assert.AreEqual("000", effective.GetAttributeValue<string>("telephone1"));
        }

        [TestMethod]
        public void Resolve_UpdateWithoutPreImage_ReturnsOnlyTarget()
        {
            var context = new XrmFakedContext();
            var pluginContext = context.GetDefaultPluginContext();
            pluginContext.MessageName = "Update";
            pluginContext.InputParameters["Target"] = new Entity("account", Guid.NewGuid()) { ["telephone1"] = "000" };

            var effective = TargetEntityResolver.Resolve(pluginContext, _tracing);

            Assert.IsFalse(effective.Contains("name"));
            Assert.AreEqual("000", effective.GetAttributeValue<string>("telephone1"));
        }

        [TestMethod]
        public void Resolve_Delete_UsesPreImageForFullAttributes()
        {
            var context = new XrmFakedContext();
            var pluginContext = context.GetDefaultPluginContext();
            pluginContext.MessageName = "Delete";
            var accountId = Guid.NewGuid();
            pluginContext.InputParameters["Target"] = new EntityReference("account", accountId);
            pluginContext.PreEntityImages[TargetEntityResolver.PreImageAlias] = new Entity("account", accountId) { ["name"] = "ACME" };

            var effective = TargetEntityResolver.Resolve(pluginContext, _tracing);

            Assert.AreEqual("ACME", effective.GetAttributeValue<string>("name"));
        }

        [TestMethod]
        public void Resolve_SetState_OverlaysStateAndStatusFromInputParameters()
        {
            var context = new XrmFakedContext();
            var pluginContext = context.GetDefaultPluginContext();
            pluginContext.MessageName = "SetStateDynamicEntity";
            var accountId = Guid.NewGuid();
            pluginContext.InputParameters["EntityMoniker"] = new EntityReference("account", accountId);
            pluginContext.InputParameters["State"] = new OptionSetValue(1);
            pluginContext.InputParameters["Status"] = new OptionSetValue(2);
            pluginContext.PreEntityImages[TargetEntityResolver.PreImageAlias] = new Entity("account", accountId) { ["name"] = "ACME" };

            var effective = TargetEntityResolver.Resolve(pluginContext, _tracing);

            Assert.AreEqual("ACME", effective.GetAttributeValue<string>("name"));
            Assert.AreEqual(1, effective.GetAttributeValue<OptionSetValue>("statecode").Value);
            Assert.AreEqual(2, effective.GetAttributeValue<OptionSetValue>("statuscode").Value);
        }

        [TestMethod]
        public void Resolve_Assign_OverlaysOwnerFromAssignee()
        {
            var context = new XrmFakedContext();
            var pluginContext = context.GetDefaultPluginContext();
            pluginContext.MessageName = "Assign";
            var accountId = Guid.NewGuid();
            var newOwnerId = Guid.NewGuid();
            pluginContext.InputParameters["Target"] = new EntityReference("account", accountId);
            pluginContext.InputParameters["Assignee"] = new EntityReference("systemuser", newOwnerId);
            pluginContext.PreEntityImages[TargetEntityResolver.PreImageAlias] = new Entity("account", accountId) { ["name"] = "ACME" };

            var effective = TargetEntityResolver.Resolve(pluginContext, _tracing);

            Assert.AreEqual(newOwnerId, effective.GetAttributeValue<EntityReference>("ownerid").Id);
        }

        [TestMethod]
        public void Resolve_UnsupportedMessage_Throws()
        {
            var context = new XrmFakedContext();
            var pluginContext = context.GetDefaultPluginContext();
            pluginContext.MessageName = "RetrieveMultiple";

            Assert.ThrowsException<ValidationConfigurationException>(() => TargetEntityResolver.Resolve(pluginContext, _tracing));
        }

        [TestMethod]
        public void Resolve_UpdateWithoutPreImage_TracesWarning()
        {
            var context = new XrmFakedContext();
            var pluginContext = context.GetDefaultPluginContext();
            pluginContext.MessageName = "Update";
            pluginContext.InputParameters["Target"] = new Entity("account", Guid.NewGuid()) { ["telephone1"] = "000" };

            TargetEntityResolver.Resolve(pluginContext, _tracing);

            Assert.IsTrue(TracedPreImageWarning);
        }

        [TestMethod]
        public void Resolve_CreateOrWithPreImage_DoesNotTraceWarning()
        {
            var context = new XrmFakedContext();
            var pluginContext = context.GetDefaultPluginContext();
            pluginContext.MessageName = "Create";
            pluginContext.InputParameters["Target"] = new Entity("account") { ["name"] = "ACME" };

            TargetEntityResolver.Resolve(pluginContext, _tracing);

            Assert.IsFalse(TracedPreImageWarning);
        }
    }
}
