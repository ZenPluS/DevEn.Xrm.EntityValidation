using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class RangeRuleEvaluatorTests
    {
        private readonly RangeRuleEvaluator _evaluator = new RangeRuleEvaluator();

        [TestMethod]
        public void IsValid_NullField_ReturnsTrue()
        {
            var entity = new Entity("account");
            var rule = TestRuleBuilder.Create("Range", parametersJson: "{\"min\":0,\"max\":100}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_WithinRange_ReturnsTrue()
        {
            var entity = new Entity("account") { ["new_field"] = 50 };
            var rule = TestRuleBuilder.Create("Range", parametersJson: "{\"min\":0,\"max\":100}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_BelowMin_ReturnsFalse()
        {
            var entity = new Entity("account") { ["new_field"] = -1 };
            var rule = TestRuleBuilder.Create("Range", parametersJson: "{\"min\":0,\"max\":100}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_AboveMax_ReturnsFalse()
        {
            var entity = new Entity("account") { ["new_field"] = new Money(150m) };
            var rule = TestRuleBuilder.Create("Range", parametersJson: "{\"min\":0,\"max\":100}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_NonNumericField_ThrowsConfigurationException()
        {
            var entity = new Entity("account") { ["new_field"] = "not-a-number" };
            var rule = TestRuleBuilder.Create("Range", parametersJson: "{\"min\":0,\"max\":100}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }
    }
}
