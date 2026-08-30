using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevEn.Xrm.EntityValidation.Tests.Model
{
    [TestClass]
    public class ValidationRuleDefinitionTests
    {
        [TestMethod]
        public void Constructor_MissingTargetEntity_Throws()
        {
            Assert.ThrowsException<ValidationConfigurationException>(() =>
                new ValidationRuleDefinition("rule-1", " ", "Create", PipelineStage.PreOperation, "field", "Required", null, "msg", 0));
        }

        [TestMethod]
        public void Constructor_MissingMessageName_Throws()
        {
            Assert.ThrowsException<ValidationConfigurationException>(() =>
                new ValidationRuleDefinition("rule-1", "account", " ", PipelineStage.PreOperation, "field", "Required", null, "msg", 0));
        }

        [TestMethod]
        public void Constructor_MissingAttribute_Throws()
        {
            Assert.ThrowsException<ValidationConfigurationException>(() =>
                new ValidationRuleDefinition("rule-1", "account", "Create", PipelineStage.PreOperation, " ", "Required", null, "msg", 0));
        }

        [TestMethod]
        public void Constructor_MissingRuleType_Throws()
        {
            Assert.ThrowsException<ValidationConfigurationException>(() =>
                new ValidationRuleDefinition("rule-1", "account", "Create", PipelineStage.PreOperation, "field", " ", null, "msg", 0));
        }

        [TestMethod]
        public void Constructor_ValidValues_PopulatesProperties()
        {
            var rule = new ValidationRuleDefinition("rule-1", "account", "Create", PipelineStage.PreOperation, "field", "Required", "{}", "msg", 5);

            Assert.AreEqual("rule-1", rule.RuleId);
            Assert.AreEqual("account", rule.TargetEntityLogicalName);
            Assert.AreEqual("Create", rule.MessageName);
            Assert.AreEqual(PipelineStage.PreOperation, rule.Stage);
            Assert.AreEqual("field", rule.AttributeLogicalName);
            Assert.AreEqual("Required", rule.RuleType);
            Assert.AreEqual("{}", rule.ParametersJson);
            Assert.AreEqual("msg", rule.ErrorMessage);
            Assert.AreEqual(5, rule.ExecutionOrder);
        }
    }
}
