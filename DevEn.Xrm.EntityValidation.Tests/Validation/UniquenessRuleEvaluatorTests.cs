using System;
using System.Collections.Generic;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using FakeXrmEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class UniquenessRuleEvaluatorTests
    {
        private readonly UniquenessRuleEvaluator _evaluator = new UniquenessRuleEvaluator();

        [TestMethod]
        public void IsValid_NullField_ReturnsTrue()
        {
            var context = new XrmFakedContext();
            var entity = new Entity("account", Guid.NewGuid());
            var rule = TestRuleBuilder.Create("Uniqueness", attributeLogicalName: "new_code", targetEntityLogicalName: "account");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, context.GetOrganizationService()));
        }

        [TestMethod]
        public void IsValid_NoOtherRecordWithSameValue_ReturnsTrue()
        {
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity> { new Entity("account", Guid.NewGuid()) { ["new_code"] = "OTHER" } });

            var entity = new Entity("account", Guid.NewGuid()) { ["new_code"] = "ACME" };
            var rule = TestRuleBuilder.Create("Uniqueness", attributeLogicalName: "new_code", targetEntityLogicalName: "account");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, context.GetOrganizationService()));
        }

        [TestMethod]
        public void IsValid_AnotherRecordWithSameValue_ReturnsFalse()
        {
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity> { new Entity("account", Guid.NewGuid()) { ["new_code"] = "ACME" } });

            var entity = new Entity("account", Guid.NewGuid()) { ["new_code"] = "ACME" };
            var rule = TestRuleBuilder.Create("Uniqueness", attributeLogicalName: "new_code", targetEntityLogicalName: "account");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, context.GetOrganizationService()));
        }

        [TestMethod]
        public void IsValid_UpdateExcludesItself_ReturnsTrue()
        {
            var existingId = Guid.NewGuid();
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity> { new Entity("account", existingId) { ["new_code"] = "ACME" } });

            var entity = new Entity("account", existingId) { ["new_code"] = "ACME" };
            var rule = TestRuleBuilder.Create("Uniqueness", attributeLogicalName: "new_code", targetEntityLogicalName: "account");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, context.GetOrganizationService()));
        }

        [TestMethod]
        public void IsValid_ScopeFields_OnlyConflictsWithinSameScope()
        {
            var parentA = Guid.NewGuid();
            var parentB = Guid.NewGuid();
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity>
            {
                new Entity("account", Guid.NewGuid())
                {
                    ["new_code"] = "ACME",
                    ["parentaccountid"] = new EntityReference("account", parentA)
                }
            });

            var entity = new Entity("account", Guid.NewGuid())
            {
                ["new_code"] = "ACME",
                ["parentaccountid"] = new EntityReference("account", parentB)
            };
            var rule = TestRuleBuilder.Create(
                "Uniqueness",
                attributeLogicalName: "new_code",
                targetEntityLogicalName: "account",
                parametersJson: "{\"scopeFields\":[\"parentaccountid\"]}");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, context.GetOrganizationService()));
        }
    }
}
