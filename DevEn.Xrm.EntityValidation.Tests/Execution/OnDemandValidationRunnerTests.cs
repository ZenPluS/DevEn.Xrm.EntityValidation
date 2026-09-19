using System;
using System.Collections.Generic;
using System.Linq;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Execution;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using FakeXrmEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DevEn.Xrm.EntityValidation.Tests.Execution
{
    [TestClass]
    public class OnDemandValidationRunnerTests
    {
        private readonly OnDemandValidationRunner _runner = new OnDemandValidationRunner();

        private static Entity CreateConfigurationRow(string targetEntityLogicalName, string message, string stage)
        {
            var rules = new JArray
            {
                new JObject
                {
                    ["id"] = "name-required",
                    ["message"] = message,
                    ["stage"] = stage,
                    ["field"] = "name",
                    ["ruleType"] = "Required",
                    ["errorMessage"] = "The name is required."
                }
            };

            var entity = new Entity("msdyn_configuration", Guid.NewGuid());
            entity["msdyn_name"] = "ValidationRules:" + targetEntityLogicalName;
            entity["msdyn_value"] = rules.ToString(Formatting.None);
            entity["statecode"] = new OptionSetValue(0);
            return entity;
        }

        private static XrmFakedPluginExecutionContext BuildCustomApiContext(XrmFakedContext context, string messageName = "deven_ValidateRecord")
        {
            var pluginContext = context.GetDefaultPluginContext();
            pluginContext.MessageName = messageName;
            pluginContext.Stage = 30; // Custom API main operation.
            pluginContext.OrganizationId = Guid.NewGuid();
            return pluginContext;
        }

        private IPluginExecutionContext Run(XrmFakedContext context, XrmFakedPluginExecutionContext pluginContext)
        {
            _runner.Run(new FakeServiceProvider(pluginContext, context.GetOrganizationService(), new FakeTracingService()));
            return pluginContext;
        }

        [TestMethod]
        public void Run_SavedRecordFailingARule_ReportsTheOutcomeInsteadOfThrowing()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var recordId = Guid.NewGuid();
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity>
            {
                CreateConfigurationRow(entityName, "Update", "PreOperation"),
                new Entity(entityName, recordId) { ["telephone1"] = "12345" }
            });

            var pluginContext = BuildCustomApiContext(context);
            pluginContext.InputParameters["Target"] = new EntityReference(entityName, recordId);

            var result = Run(context, pluginContext);

            Assert.AreEqual(false, result.OutputParameters["IsValid"]);
            StringAssert.Contains((string)result.OutputParameters["Messages"], "The name is required.");
            CollectionAssert.AreEqual(new[] { "name-required" }, (string[])result.OutputParameters["FailedRuleIds"]);
        }

        [TestMethod]
        public void Run_UnsavedValuesFixTheStoredRecord_ReportsValid()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var recordId = Guid.NewGuid();
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity>
            {
                CreateConfigurationRow(entityName, "Update", "PreOperation"),
                new Entity(entityName, recordId) { ["telephone1"] = "12345" }
            });

            var pluginContext = BuildCustomApiContext(context);
            pluginContext.InputParameters["Target"] = new EntityReference(entityName, recordId);

            // What the user typed on the form but hasn't saved yet wins over the stored row.
            pluginContext.InputParameters["Record"] = new Entity(entityName) { ["name"] = "ACME" };

            var result = Run(context, pluginContext);

            Assert.AreEqual(true, result.OutputParameters["IsValid"]);
            Assert.AreEqual(string.Empty, result.OutputParameters["Messages"]);
        }

        [TestMethod]
        public void Run_RecordThatWasNeverSaved_ValidatesOnlyTheValuesProvided()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity> { CreateConfigurationRow(entityName, "Create", "PreOperation") });

            var pluginContext = BuildCustomApiContext(context);
            pluginContext.InputParameters["EntityName"] = entityName;
            pluginContext.InputParameters["Record"] = new Entity(entityName) { ["telephone1"] = "12345" };

            var result = Run(context, pluginContext);

            Assert.AreEqual(false, result.OutputParameters["IsValid"]);
            StringAssert.Contains((string)result.OutputParameters["Messages"], "The name is required.");
        }

        [TestMethod]
        public void Run_EvaluatesRulesOfEveryMessageAndStage()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var context = new XrmFakedContext();

            // A rule written for a message/stage that has nothing to do with this Custom API.
            context.Initialize(new List<Entity> { CreateConfigurationRow(entityName, "Delete", "PostOperation") });

            var pluginContext = BuildCustomApiContext(context);
            pluginContext.InputParameters["EntityName"] = entityName;
            pluginContext.InputParameters["Record"] = new Entity(entityName);

            var result = Run(context, pluginContext);

            Assert.AreEqual(false, result.OutputParameters["IsValid"]);
        }

        [TestMethod]
        public void Run_NoRecordInTheRequest_ExplainsWhatToSend()
        {
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity>());
            var pluginContext = BuildCustomApiContext(context);

            var exception = Assert.ThrowsException<InvalidPluginExecutionException>(() => Run(context, pluginContext));

            StringAssert.Contains(exception.Message, "EntityName");
        }

        [TestMethod]
        public void Run_InvalidRecordId_IsReportedToTheCaller()
        {
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity>());
            var pluginContext = BuildCustomApiContext(context);
            pluginContext.InputParameters["EntityName"] = "account";
            pluginContext.InputParameters["RecordId"] = "not-a-guid";

            Assert.ThrowsException<InvalidPluginExecutionException>(() => Run(context, pluginContext));
        }

        [TestMethod]
        public void Run_RecordDataWithoutReadableMetadata_FailsLoudlyInsteadOfValidatingWrongValues()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity> { CreateConfigurationRow(entityName, "Create", "PreOperation") });

            var pluginContext = BuildCustomApiContext(context);
            pluginContext.InputParameters["EntityName"] = entityName;
            pluginContext.InputParameters["RecordData"] = @"{""name"": ""ACME""}";

            var exception = Assert.ThrowsException<InvalidPluginExecutionException>(() => Run(context, pluginContext));

            StringAssert.Contains(exception.Message, "metadata");
        }

        [TestMethod]
        public void Run_NoConfiguredRules_ReportsValid()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity>());

            var pluginContext = BuildCustomApiContext(context);
            pluginContext.InputParameters["EntityName"] = entityName;
            pluginContext.InputParameters["Record"] = new Entity(entityName);

            var result = Run(context, pluginContext);

            Assert.AreEqual(true, result.OutputParameters["IsValid"]);
            Assert.AreEqual(0, ((string[])result.OutputParameters["FailedRuleIds"]).Length);
        }
    }
}
