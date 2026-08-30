using System.Collections.Generic;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Engine;
using DevEn.Xrm.EntityValidation.Model;
using DevEn.Xrm.EntityValidation.Repository;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.Engine
{
    [TestClass]
    public class ValidationEngineTests
    {
        private sealed class StubRepository : IValidationRuleRepository
        {
            private readonly IReadOnlyList<ValidationRuleDefinition> _rules;

            public StubRepository(IReadOnlyList<ValidationRuleDefinition> rules)
            {
                _rules = rules;
            }

            public IReadOnlyList<ValidationRuleDefinition> GetActiveRules(string targetEntityLogicalName, string messageName, PipelineStage stage)
            {
                return _rules;
            }
        }

        [TestMethod]
        public void ValidateAndThrow_NoRules_DoesNotThrow()
        {
            var engine = new ValidationEngine(new StubRepository(new List<ValidationRuleDefinition>()), RuleEvaluatorRegistry.CreateDefault(), new FakeTracingService(), null);

            engine.ValidateAndThrow(new Entity("account"), "account", "Create", PipelineStage.PreOperation);
        }

        [TestMethod]
        public void ValidateAndThrow_AllRulesPass_DoesNotThrow()
        {
            var rules = new List<ValidationRuleDefinition> { TestRuleBuilder.Create("Required", attributeLogicalName: "name") };
            var engine = new ValidationEngine(new StubRepository(rules), RuleEvaluatorRegistry.CreateDefault(), new FakeTracingService(), null);
            var entity = new Entity("account") { ["name"] = "ACME" };

            engine.ValidateAndThrow(entity, "account", "Create", PipelineStage.PreOperation);
        }

        [TestMethod]
        public void ValidateAndThrow_FailingRule_ThrowsWithConfiguredMessage()
        {
            var rules = new List<ValidationRuleDefinition>
            {
                TestRuleBuilder.Create("Required", attributeLogicalName: "name", errorMessage: "The name is required.")
            };
            var engine = new ValidationEngine(new StubRepository(rules), RuleEvaluatorRegistry.CreateDefault(), new FakeTracingService(), null);

            var exception = Assert.ThrowsException<InvalidPluginExecutionException>(() =>
                engine.ValidateAndThrow(new Entity("account"), "account", "Create", PipelineStage.PreOperation));

            Assert.AreEqual("The name is required.", exception.Message);
        }

        [TestMethod]
        public void ValidateAndThrow_MultipleFailingRules_AggregatesAllMessages()
        {
            var rules = new List<ValidationRuleDefinition>
            {
                TestRuleBuilder.Create("Required", attributeLogicalName: "name", errorMessage: "Error 1"),
                TestRuleBuilder.Create("Required", attributeLogicalName: "telephone1", errorMessage: "Error 2")
            };
            var engine = new ValidationEngine(new StubRepository(rules), RuleEvaluatorRegistry.CreateDefault(), new FakeTracingService(), null);

            var exception = Assert.ThrowsException<InvalidPluginExecutionException>(() =>
                engine.ValidateAndThrow(new Entity("account"), "account", "Create", PipelineStage.PreOperation));

            StringAssert.Contains(exception.Message, "Error 1");
            StringAssert.Contains(exception.Message, "Error 2");
        }

        [TestMethod]
        public void ValidateAndThrow_UnknownRuleType_ThrowsValidationConfigurationException()
        {
            var rules = new List<ValidationRuleDefinition> { TestRuleBuilder.Create("DoesNotExist") };
            var engine = new ValidationEngine(new StubRepository(rules), RuleEvaluatorRegistry.CreateDefault(), new FakeTracingService(), null);

            Assert.ThrowsException<ValidationConfigurationException>(() =>
                engine.ValidateAndThrow(new Entity("account"), "account", "Create", PipelineStage.PreOperation));
        }

        [TestMethod]
        public void ValidateAndThrow_MissingErrorMessage_UsesGeneratedDefaultMessage()
        {
            var rules = new List<ValidationRuleDefinition>
            {
                TestRuleBuilder.Create("Required", attributeLogicalName: "name", errorMessage: null)
            };
            var engine = new ValidationEngine(new StubRepository(rules), RuleEvaluatorRegistry.CreateDefault(), new FakeTracingService(), null);

            var exception = Assert.ThrowsException<InvalidPluginExecutionException>(() =>
                engine.ValidateAndThrow(new Entity("account"), "account", "Create", PipelineStage.PreOperation));

            StringAssert.Contains(exception.Message, "name");
        }
    }
}
