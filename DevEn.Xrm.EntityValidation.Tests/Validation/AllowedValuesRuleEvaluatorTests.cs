using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class AllowedValuesRuleEvaluatorTests
    {
        private readonly AllowedValuesRuleEvaluator _evaluator = new AllowedValuesRuleEvaluator();

        [TestMethod]
        public void IsValid_NullField_ReturnsTrue()
        {
            var entity = new Entity("account");
            var rule = TestRuleBuilder.Create("AllowedValues", parametersJson: "{\"values\":[\"A\",\"B\"]}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_ValueInList_ReturnsTrue()
        {
            var entity = new Entity("account") { ["new_field"] = "B" };
            var rule = TestRuleBuilder.Create("AllowedValues", parametersJson: "{\"values\":[\"A\",\"B\"]}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_ValueNotInList_ReturnsFalse()
        {
            var entity = new Entity("account") { ["new_field"] = "C" };
            var rule = TestRuleBuilder.Create("AllowedValues", parametersJson: "{\"values\":[\"A\",\"B\"]}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_DefaultIsCaseInsensitive()
        {
            var entity = new Entity("account") { ["new_field"] = "a" };
            var rule = TestRuleBuilder.Create("AllowedValues", parametersJson: "{\"values\":[\"A\",\"B\"]}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_CaseSensitive_RejectsDifferentCase()
        {
            var entity = new Entity("account") { ["new_field"] = "a" };
            var rule = TestRuleBuilder.Create("AllowedValues", parametersJson: "{\"values\":[\"A\",\"B\"],\"caseSensitive\":true}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_OptionSetValue_ComparesNumericValue()
        {
            var entity = new Entity("account") { ["new_field"] = new OptionSetValue(1) };
            var rule = TestRuleBuilder.Create("AllowedValues", parametersJson: "{\"values\":[\"1\",\"2\"]}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_MissingValuesParameter_ThrowsConfigurationException()
        {
            var entity = new Entity("account") { ["new_field"] = "A" };
            var rule = TestRuleBuilder.Create("AllowedValues", parametersJson: "{}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }
    }
}
