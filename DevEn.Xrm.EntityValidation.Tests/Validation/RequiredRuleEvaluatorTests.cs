using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class RequiredRuleEvaluatorTests
    {
        private readonly RequiredRuleEvaluator _evaluator = new RequiredRuleEvaluator();

        [TestMethod]
        public void IsValid_AttributeMissing_ReturnsFalse()
        {
            var entity = new Entity("account");
            var rule = TestRuleBuilder.Create("Required");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_AttributeNull_ReturnsFalse()
        {
            var entity = new Entity("account") { ["new_field"] = null };
            var rule = TestRuleBuilder.Create("Required");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_EmptyString_ReturnsFalse()
        {
            var entity = new Entity("account") { ["new_field"] = "   " };
            var rule = TestRuleBuilder.Create("Required");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_NonEmptyValue_ReturnsTrue()
        {
            var entity = new Entity("account") { ["new_field"] = "ACME" };
            var rule = TestRuleBuilder.Create("Required");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_RuleWithoutField_ThrowsConfigurationException()
        {
            var entity = new Entity("account");
            var rule = TestRuleBuilder.Create("Required", attributeLogicalName: null);

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }
    }
}
