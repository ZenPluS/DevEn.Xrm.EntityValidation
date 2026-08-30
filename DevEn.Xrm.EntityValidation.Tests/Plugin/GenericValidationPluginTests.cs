using System;
using System.Collections.Generic;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using FakeXrmEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PluginType = DevEn.Xrm.EntityValidation.Plugin.GenericValidationPlugin;

namespace DevEn.Xrm.EntityValidation.Tests.Plugin
{
    [TestClass]
    public class GenericValidationPluginTests
    {
        private static Entity CreateConfigurationRow(string targetEntityLogicalName, string message, string stage, string field, string ruleType, string errorMessage)
        {
            var rules = new JArray
            {
                new JObject
                {
                    ["message"] = message,
                    ["stage"] = stage,
                    ["field"] = field,
                    ["ruleType"] = ruleType,
                    ["errorMessage"] = errorMessage
                }
            };

            var entity = new Entity("msdyn_configuration", Guid.NewGuid());
            entity["msdyn_name"] = "ValidationRules:" + targetEntityLogicalName;
            entity["msdyn_value"] = rules.ToString(Formatting.None);
            entity["statecode"] = new OptionSetValue(0);
            return entity;
        }

        private static IServiceProvider BuildServiceProvider(XrmFakedContext context, IPluginExecutionContext pluginExecutionContext)
        {
            return new FakeServiceProvider(pluginExecutionContext, context.GetOrganizationService(), new FakeTracingService());
        }

        [TestMethod]
        public void Execute_RequiredFieldMissing_ThrowsWithConfiguredMessage()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity>
            {
                CreateConfigurationRow(entityName, "Create", "PreOperation", "name", "Required", "The name is required.")
            });

            var pluginContext = context.GetDefaultPluginContext();
            pluginContext.MessageName = "Create";
            pluginContext.Stage = (int)PipelineStage.PreOperation;
            pluginContext.PrimaryEntityName = entityName;
            pluginContext.OrganizationId = Guid.NewGuid();
            pluginContext.InputParameters["Target"] = new Entity(entityName);

            var plugin = new PluginType(null, null);

            var exception = Assert.ThrowsException<InvalidPluginExecutionException>(
                () => plugin.Execute(BuildServiceProvider(context, pluginContext)));

            Assert.AreEqual("The name is required.", exception.Message);
        }

        [TestMethod]
        public void Execute_AllRulesSatisfied_DoesNotThrow()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity>
            {
                CreateConfigurationRow(entityName, "Create", "PreOperation", "name", "Required", "The name is required.")
            });

            var pluginContext = context.GetDefaultPluginContext();
            pluginContext.MessageName = "Create";
            pluginContext.Stage = (int)PipelineStage.PreOperation;
            pluginContext.PrimaryEntityName = entityName;
            pluginContext.OrganizationId = Guid.NewGuid();
            pluginContext.InputParameters["Target"] = new Entity(entityName) { ["name"] = "ACME" };

            var plugin = new PluginType(null, null);

            plugin.Execute(BuildServiceProvider(context, pluginContext));
        }

        [TestMethod]
        public void Execute_NoConfigurationRow_DoesNotThrow()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity>());

            var pluginContext = context.GetDefaultPluginContext();
            pluginContext.MessageName = "Create";
            pluginContext.Stage = (int)PipelineStage.PreOperation;
            pluginContext.PrimaryEntityName = entityName;
            pluginContext.OrganizationId = Guid.NewGuid();
            pluginContext.InputParameters["Target"] = new Entity(entityName);

            var plugin = new PluginType(null, null);

            plugin.Execute(BuildServiceProvider(context, pluginContext));
        }
    }
}
