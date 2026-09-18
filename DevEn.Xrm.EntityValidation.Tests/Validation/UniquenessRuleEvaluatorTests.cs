using System;
using System.Collections.Generic;
using System.Linq;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using FakeXrmEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class UniquenessRuleEvaluatorTests
    {
        private readonly UniquenessRuleEvaluator _evaluator = new UniquenessRuleEvaluator();

        /// <summary>
        /// Returns a fixed result and keeps the query, so a test can assert on how it was built.
        /// </summary>
        private sealed class CapturingOrganizationService : IOrganizationService
        {
            private readonly EntityCollection _result;

            public CapturingOrganizationService(params Entity[] matches)
            {
                _result = new EntityCollection(matches.ToList());
            }

            public QueryExpression LastQuery { get; private set; }

            public EntityCollection RetrieveMultiple(QueryBase query)
            {
                LastQuery = (QueryExpression)query;
                return _result;
            }

            public Guid Create(Entity entity) => throw new NotSupportedException();

            public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => throw new NotSupportedException();

            public void Update(Entity entity) => throw new NotSupportedException();

            public void Delete(string entityName, Guid id) => throw new NotSupportedException();

            public OrganizationResponse Execute(OrganizationRequest request) => throw new NotSupportedException();

            public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
                => throw new NotSupportedException();

            public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
                => throw new NotSupportedException();
        }

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

        [TestMethod]
        public void IsValid_OnlyTheRecordItselfMatches_ReturnsTrueWithoutGuessingThePrimaryKey()
        {
            var recordId = Guid.NewGuid();
            var service = new CapturingOrganizationService(new Entity("phonecall", recordId));
            var entity = new Entity("phonecall", recordId) { ["subject"] = "ACME" };
            var rule = TestRuleBuilder.Create("Uniqueness", attributeLogicalName: "subject", targetEntityLogicalName: "phonecall");

            Assert.IsTrue(_evaluator.IsValid(entity, rule, service));

            // Activities have no "phonecallid" attribute: filtering on it would make the query fail.
            Assert.AreEqual(1, service.LastQuery.Criteria.Conditions.Count);
            Assert.AreEqual("subject", service.LastQuery.Criteria.Conditions[0].AttributeName);
        }

        [TestMethod]
        public void IsValid_AnotherRecordAmongTheMatches_ReturnsFalse()
        {
            var recordId = Guid.NewGuid();
            var service = new CapturingOrganizationService(
                new Entity("phonecall", recordId),
                new Entity("phonecall", Guid.NewGuid()));
            var entity = new Entity("phonecall", recordId) { ["subject"] = "ACME" };
            var rule = TestRuleBuilder.Create("Uniqueness", attributeLogicalName: "subject", targetEntityLogicalName: "phonecall");

            Assert.IsFalse(_evaluator.IsValid(entity, rule, service));
        }
    }
}
