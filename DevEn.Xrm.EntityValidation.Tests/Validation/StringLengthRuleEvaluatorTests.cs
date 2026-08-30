using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class StringLengthRuleEvaluatorTests
    {
        private readonly StringLengthRuleEvaluator _evaluator = new StringLengthRuleEvaluator();

        [TestMethod]
        public void IsValid_NullField_ReturnsTrue()
        {
            var entity = new Entity("account");
            var rule = TestRuleBuilder.Create("StringLength", parametersJson: "{\"minLength\":3,\"maxLength\":5}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_TooShort_ReturnsFalse()
        {
            var entity = new Entity("account") { ["new_field"] = "ab" };
            var rule = TestRuleBuilder.Create("StringLength", parametersJson: "{\"minLength\":3,\"maxLength\":5}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_TooLong_ReturnsFalse()
        {
            var entity = new Entity("account") { ["new_field"] = "abcdef" };
            var rule = TestRuleBuilder.Create("StringLength", parametersJson: "{\"minLength\":3,\"maxLength\":5}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_WithinBounds_ReturnsTrue()
        {
            var entity = new Entity("account") { ["new_field"] = "abcd" };
            var rule = TestRuleBuilder.Create("StringLength", parametersJson: "{\"minLength\":3,\"maxLength\":5}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_NonStringField_ThrowsConfigurationException()
        {
            var entity = new Entity("account") { ["new_field"] = 123 };
            var rule = TestRuleBuilder.Create("StringLength", parametersJson: "{\"minLength\":3,\"maxLength\":5}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }
    }
}
