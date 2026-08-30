using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class RegexRuleEvaluatorTests
    {
        private readonly RegexRuleEvaluator _evaluator = new RegexRuleEvaluator();

        [TestMethod]
        public void IsValid_NullField_ReturnsTrue()
        {
            var entity = new Entity("account");
            var rule = TestRuleBuilder.Create("Regex", parametersJson: "{\"pattern\":\"^[0-9]{5}$\"}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_MatchingPattern_ReturnsTrue()
        {
            var entity = new Entity("account") { ["new_field"] = "12345" };
            var rule = TestRuleBuilder.Create("Regex", parametersJson: "{\"pattern\":\"^[0-9]{5}$\"}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_NonMatchingPattern_ReturnsFalse()
        {
            var entity = new Entity("account") { ["new_field"] = "abcde" };
            var rule = TestRuleBuilder.Create("Regex", parametersJson: "{\"pattern\":\"^[0-9]{5}$\"}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_MissingPatternParameter_ThrowsConfigurationException()
        {
            var entity = new Entity("account") { ["new_field"] = "abcde" };
            var rule = TestRuleBuilder.Create("Regex", parametersJson: "{}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_InvalidPattern_ThrowsConfigurationException()
        {
            var entity = new Entity("account") { ["new_field"] = "abcde" };
            var rule = TestRuleBuilder.Create("Regex", parametersJson: "{\"pattern\":\"[\"}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }
    }
}
