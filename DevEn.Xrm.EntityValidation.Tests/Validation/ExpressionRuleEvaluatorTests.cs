using System;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class ExpressionRuleEvaluatorTests
    {
        private readonly ExpressionRuleEvaluator _evaluator = new ExpressionRuleEvaluator();

        [TestMethod]
        public void IsValid_FieldEqualsField_MatchingValues_ReturnsTrue()
        {
            var entity = new Entity("account") { ["fieldA"] = "same", ["fieldB"] = "same" };
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{\"expression\":\"fieldA == fieldB\"}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_FieldEqualsField_DifferingValues_ReturnsFalse()
        {
            var entity = new Entity("account") { ["fieldA"] = "left", ["fieldB"] = "right" };
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{\"expression\":\"fieldA == fieldB\"}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_DateGreaterOrEqualToday_FutureDate_ReturnsTrue()
        {
            var entity = new Entity("account") { ["somedate"] = DateTime.UtcNow.AddDays(1) };
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{\"expression\":\"somedate >= Today\"}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_DateGreaterOrEqualToday_PastDate_ReturnsFalse()
        {
            var entity = new Entity("account") { ["somedate"] = DateTime.UtcNow.AddDays(-1) };
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{\"expression\":\"somedate >= Today\"}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_DateOffsetComparison_WithinThirtyDays_ReturnsTrue()
        {
            var today = DateTime.UtcNow.Date;
            var entity = new Entity("account") { ["dateA"] = today, ["dateB"] = today };
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{\"expression\":\"dateA <= dateB + 30\"}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_DateOffsetComparison_BeyondThirtyDays_ReturnsFalse()
        {
            var today = DateTime.UtcNow.Date;
            var entity = new Entity("account") { ["dateA"] = today.AddDays(31), ["dateB"] = today };
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{\"expression\":\"dateA <= dateB + 30\"}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_CombinedAndCondition_BothMatch_ReturnsTrue()
        {
            var entity = new Entity("account") { ["tipo"] = "Cliente", ["paese"] = "IT" };
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{\"expression\":\"tipo == 'Cliente' && paese == 'IT'\"}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_CombinedAndCondition_OneMismatches_ReturnsFalse()
        {
            var entity = new Entity("account") { ["tipo"] = "Cliente", ["paese"] = "FR" };
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{\"expression\":\"tipo == 'Cliente' && paese == 'IT'\"}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_Arithmetic_WithinCreditLimitMargin_ReturnsTrue()
        {
            var entity = new Entity("account") { ["importo"] = 100m, ["creditlimit"] = 100m };
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{\"expression\":\"importo <= creditlimit * 1.1\"}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_Arithmetic_ExceedsCreditLimitMargin_ReturnsFalse()
        {
            var entity = new Entity("account") { ["importo"] = 120m, ["creditlimit"] = 100m };
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{\"expression\":\"importo <= creditlimit * 1.1\"}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_AbsentField_ReturnsTrue()
        {
            var entity = new Entity("account");
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{\"expression\":\"fieldA == fieldB\"}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_IncompatibleTypes_ThrowsConfigurationException()
        {
            var entity = new Entity("account") { ["fieldA"] = "text", ["fieldB"] = 123 };
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{\"expression\":\"fieldA == fieldB\"}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_DivisionByZero_ThrowsConfigurationException()
        {
            var entity = new Entity("account") { ["importo"] = 10m, ["creditlimit"] = 0m };
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{\"expression\":\"importo / creditlimit > 1\"}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_ExpressionTooLong_ThrowsConfigurationException()
        {
            var entity = new Entity("account");
            var longExpression = new string('a', 600);
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{\"expression\":\"" + longExpression + "\"}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_NonBooleanRootResult_ThrowsConfigurationException()
        {
            var entity = new Entity("account") { ["importo"] = 5m };
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{\"expression\":\"importo\"}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_MissingExpressionParameter_ThrowsConfigurationException()
        {
            var entity = new Entity("account");
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }
    }
}
