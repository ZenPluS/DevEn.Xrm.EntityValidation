using System;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class FieldComparisonRuleEvaluatorTests
    {
        private readonly FieldComparisonRuleEvaluator _evaluator = new FieldComparisonRuleEvaluator();

        [TestMethod]
        public void IsValid_LeftFieldNull_ReturnsTrue()
        {
            var entity = new Entity("account") { ["new_enddate"] = new DateTime(2026, 1, 1) };
            var rule = TestRuleBuilder.Create(
                "FieldComparison",
                attributeLogicalName: "new_startdate",
                parametersJson: "{\"compareToAttribute\":\"new_enddate\",\"operator\":\"LessThanOrEqual\"}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_RightFieldNull_ReturnsTrue()
        {
            var entity = new Entity("account") { ["new_startdate"] = new DateTime(2026, 1, 1) };
            var rule = TestRuleBuilder.Create(
                "FieldComparison",
                attributeLogicalName: "new_startdate",
                parametersJson: "{\"compareToAttribute\":\"new_enddate\",\"operator\":\"LessThanOrEqual\"}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_DatesInOrder_ReturnsTrue()
        {
            var entity = new Entity("account")
            {
                ["new_startdate"] = new DateTime(2026, 1, 1),
                ["new_enddate"] = new DateTime(2026, 2, 1)
            };
            var rule = TestRuleBuilder.Create(
                "FieldComparison",
                attributeLogicalName: "new_startdate",
                parametersJson: "{\"compareToAttribute\":\"new_enddate\",\"operator\":\"LessThanOrEqual\"}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_DatesOutOfOrder_ReturnsFalse()
        {
            var entity = new Entity("account")
            {
                ["new_startdate"] = new DateTime(2026, 3, 1),
                ["new_enddate"] = new DateTime(2026, 2, 1)
            };
            var rule = TestRuleBuilder.Create(
                "FieldComparison",
                attributeLogicalName: "new_startdate",
                parametersJson: "{\"compareToAttribute\":\"new_enddate\",\"operator\":\"LessThanOrEqual\"}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_NumericComparison_Works()
        {
            var entity = new Entity("account")
            {
                ["new_min"] = 10,
                ["new_max"] = new Money(20m)
            };
            var rule = TestRuleBuilder.Create(
                "FieldComparison",
                attributeLogicalName: "new_min",
                parametersJson: "{\"compareToAttribute\":\"new_max\",\"operator\":\"LessThan\"}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_MissingCompareToAttributeParameter_Throws()
        {
            var entity = new Entity("account") { ["new_startdate"] = DateTime.UtcNow };
            var rule = TestRuleBuilder.Create("FieldComparison", attributeLogicalName: "new_startdate", parametersJson: "{}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_InvalidOperator_Throws()
        {
            var entity = new Entity("account")
            {
                ["new_startdate"] = DateTime.UtcNow,
                ["new_enddate"] = DateTime.UtcNow
            };
            var rule = TestRuleBuilder.Create(
                "FieldComparison",
                attributeLogicalName: "new_startdate",
                parametersJson: "{\"compareToAttribute\":\"new_enddate\",\"operator\":\"Bogus\"}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_IncompatibleTypes_Throws()
        {
            var entity = new Entity("account")
            {
                ["new_startdate"] = DateTime.UtcNow,
                ["new_amount"] = 10
            };
            var rule = TestRuleBuilder.Create(
                "FieldComparison",
                attributeLogicalName: "new_startdate",
                parametersJson: "{\"compareToAttribute\":\"new_amount\",\"operator\":\"LessThan\"}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }
    }
}
