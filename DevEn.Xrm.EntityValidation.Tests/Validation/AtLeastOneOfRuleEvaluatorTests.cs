using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class AtLeastOneOfRuleEvaluatorTests
    {
        private readonly AtLeastOneOfRuleEvaluator _evaluator = new AtLeastOneOfRuleEvaluator();

        [TestMethod]
        public void IsValid_NoneFilled_ReturnsFalse()
        {
            var entity = new Entity("contact");
            var rule = TestRuleBuilder.Create("AtLeastOneOf", parametersJson: "{\"fields\":[\"emailaddress1\",\"telephone1\"]}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_OneFilled_ReturnsTrue()
        {
            var entity = new Entity("contact") { ["telephone1"] = "12345" };
            var rule = TestRuleBuilder.Create("AtLeastOneOf", parametersJson: "{\"fields\":[\"emailaddress1\",\"telephone1\"]}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_BlankStringDoesNotCount()
        {
            var entity = new Entity("contact") { ["telephone1"] = "   " };
            var rule = TestRuleBuilder.Create("AtLeastOneOf", parametersJson: "{\"fields\":[\"emailaddress1\",\"telephone1\"]}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_MinimumRequiredTwo_OnlyOneFilled_ReturnsFalse()
        {
            var entity = new Entity("contact") { ["telephone1"] = "12345" };
            var rule = TestRuleBuilder.Create(
                "AtLeastOneOf",
                parametersJson: "{\"fields\":[\"emailaddress1\",\"telephone1\",\"mobilephone\"],\"minimumRequired\":2}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_MinimumRequiredTwo_TwoFilled_ReturnsTrue()
        {
            var entity = new Entity("contact") { ["telephone1"] = "12345", ["mobilephone"] = "999" };
            var rule = TestRuleBuilder.Create(
                "AtLeastOneOf",
                parametersJson: "{\"fields\":[\"emailaddress1\",\"telephone1\",\"mobilephone\"],\"minimumRequired\":2}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_MissingFieldsParameter_ThrowsConfigurationException()
        {
            var entity = new Entity("contact");
            var rule = TestRuleBuilder.Create("AtLeastOneOf", parametersJson: "{}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }
    }
}
