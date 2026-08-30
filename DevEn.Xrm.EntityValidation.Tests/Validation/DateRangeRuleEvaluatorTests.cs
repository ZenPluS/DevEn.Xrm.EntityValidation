using System;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class DateRangeRuleEvaluatorTests
    {
        private readonly DateRangeRuleEvaluator _evaluator = new DateRangeRuleEvaluator();

        [TestMethod]
        public void IsValid_NullField_ReturnsTrue()
        {
            var entity = new Entity("contact");
            var rule = TestRuleBuilder.Create("DateRange", parametersJson: "{\"max\":\"Today\"}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_BeforeAbsoluteMin_ReturnsFalse()
        {
            var entity = new Entity("contact") { ["new_field"] = new DateTime(1999, 1, 1) };
            var rule = TestRuleBuilder.Create("DateRange", parametersJson: "{\"min\":\"2000-01-01\"}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_AfterAbsoluteMax_ReturnsFalse()
        {
            var entity = new Entity("contact") { ["new_field"] = new DateTime(2030, 1, 1) };
            var rule = TestRuleBuilder.Create("DateRange", parametersJson: "{\"max\":\"2025-01-01\"}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_RelativeMaxToday_RejectsFutureDate()
        {
            var entity = new Entity("contact") { ["new_field"] = DateTime.UtcNow.AddDays(1) };
            var rule = TestRuleBuilder.Create("DateRange", parametersJson: "{\"max\":\"Today\"}");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_RelativeWindow_AcceptsDateWithinIt()
        {
            var entity = new Entity("contact") { ["new_field"] = DateTime.UtcNow.Date.AddDays(10) };
            var rule = TestRuleBuilder.Create("DateRange", parametersJson: "{\"min\":\"Today\",\"max\":\"Today+30d\"}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_NonDateField_ThrowsConfigurationException()
        {
            var entity = new Entity("contact") { ["new_field"] = "not-a-date" };
            var rule = TestRuleBuilder.Create("DateRange", parametersJson: "{\"max\":\"Today\"}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_InvalidMinToken_ThrowsConfigurationException()
        {
            var entity = new Entity("contact") { ["new_field"] = DateTime.UtcNow };
            var rule = TestRuleBuilder.Create("DateRange", parametersJson: "{\"min\":\"not-a-date\"}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, null));
        }
    }
}
