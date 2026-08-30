using System;
using System.Collections.Generic;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using FakeXrmEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class RelatedRecordStateRuleEvaluatorTests
    {
        private readonly RelatedRecordStateRuleEvaluator _evaluator = new RelatedRecordStateRuleEvaluator();

        [TestMethod]
        public void IsValid_NullLookup_ReturnsTrue()
        {
            var context = new XrmFakedContext();
            var entity = new Entity("opportunity");
            var rule = TestRuleBuilder.Create("RelatedRecordState", attributeLogicalName: "parentaccountid");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, context.GetOrganizationService()));
        }

        [TestMethod]
        public void IsValid_RelatedRecordActive_ReturnsTrue()
        {
            var accountId = Guid.NewGuid();
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity> { new Entity("account", accountId) { ["statecode"] = new OptionSetValue(0) } });

            var entity = new Entity("opportunity") { ["parentaccountid"] = new EntityReference("account", accountId) };
            var rule = TestRuleBuilder.Create("RelatedRecordState", attributeLogicalName: "parentaccountid");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, context.GetOrganizationService()));
        }

        [TestMethod]
        public void IsValid_RelatedRecordInactive_ReturnsFalse()
        {
            var accountId = Guid.NewGuid();
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity> { new Entity("account", accountId) { ["statecode"] = new OptionSetValue(1) } });

            var entity = new Entity("opportunity") { ["parentaccountid"] = new EntityReference("account", accountId) };
            var rule = TestRuleBuilder.Create("RelatedRecordState", attributeLogicalName: "parentaccountid");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, context.GetOrganizationService()));
        }

        [TestMethod]
        public void IsValid_ExpectedInactive_MatchesInactiveRelatedRecord()
        {
            var accountId = Guid.NewGuid();
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity> { new Entity("account", accountId) { ["statecode"] = new OptionSetValue(1) } });

            var entity = new Entity("opportunity") { ["parentaccountid"] = new EntityReference("account", accountId) };
            var rule = TestRuleBuilder.Create(
                "RelatedRecordState",
                attributeLogicalName: "parentaccountid",
                parametersJson: "{\"expectedState\":\"Inactive\"}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, context.GetOrganizationService()));
        }

        [TestMethod]
        public void IsValid_RelatedRecordDoesNotExist_ReturnsFalse()
        {
            var context = new XrmFakedContext();
            var entity = new Entity("opportunity") { ["parentaccountid"] = new EntityReference("account", Guid.NewGuid()) };
            var rule = TestRuleBuilder.Create("RelatedRecordState", attributeLogicalName: "parentaccountid");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, context.GetOrganizationService()));
        }

        [TestMethod]
        public void IsValid_NonLookupField_ThrowsConfigurationException()
        {
            var context = new XrmFakedContext();
            var entity = new Entity("opportunity") { ["parentaccountid"] = "not-a-reference" };
            var rule = TestRuleBuilder.Create("RelatedRecordState", attributeLogicalName: "parentaccountid");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, context.GetOrganizationService()));
        }

        [TestMethod]
        public void IsValid_InvalidExpectedState_ThrowsConfigurationException()
        {
            var accountId = Guid.NewGuid();
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity> { new Entity("account", accountId) { ["statecode"] = new OptionSetValue(0) } });

            var entity = new Entity("opportunity") { ["parentaccountid"] = new EntityReference("account", accountId) };
            var rule = TestRuleBuilder.Create(
                "RelatedRecordState",
                attributeLogicalName: "parentaccountid",
                parametersJson: "{\"expectedState\":\"Bogus\"}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(entity, rule, context.GetOrganizationService()));
        }
    }
}
