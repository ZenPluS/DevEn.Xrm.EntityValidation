using System;
using System.Collections.Generic;
using System.Linq;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Repository;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using FakeXrmEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DevEn.Xrm.EntityValidation.Tests.Repository
{
    [TestClass]
    public class DataverseValidationRuleRepositoryTests
    {
        /// <summary>
        /// Makes the first N <c>RetrieveMultiple</c> calls fail, the way a transient Dataverse error
        /// (throttling, timeout...) would; everything else goes to the real fake service.
        /// </summary>
        private sealed class TransientlyFailingOrganizationService : IOrganizationService
        {
            private readonly IOrganizationService _inner;
            private int _failuresLeft;

            public TransientlyFailingOrganizationService(IOrganizationService inner, int failures)
            {
                _inner = inner;
                _failuresLeft = failures;
            }

            public EntityCollection RetrieveMultiple(QueryBase query)
            {
                if (_failuresLeft <= 0)
                {
                    return _inner.RetrieveMultiple(query);
                }

                _failuresLeft--;
                throw new InvalidOperationException("Transient failure.");
            }

            public Guid Create(Entity entity) => _inner.Create(entity);

            public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => _inner.Retrieve(entityName, id, columnSet);

            public void Update(Entity entity) => _inner.Update(entity);

            public void Delete(string entityName, Guid id) => _inner.Delete(entityName, id);

            public OrganizationResponse Execute(OrganizationRequest request) => _inner.Execute(request);

            public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
                => _inner.Associate(entityName, entityId, relationship, relatedEntities);

            public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
                => _inner.Disassociate(entityName, entityId, relationship, relatedEntities);
        }
        private static Entity CreateConfigurationRow(string targetEntityLogicalName, JArray rules)
        {
            var entity = CreateConfigurationRowForInsert(targetEntityLogicalName, rules);
            entity["statecode"] = new OptionSetValue(0);
            return entity;
        }

        private static Entity CreateConfigurationRowForInsert(string targetEntityLogicalName, JArray rules)
        {
            // Dataverse (and FakeXrmEasy) rejects statecode on Create: new records are Active by default and
            // statecode can only be changed afterwards via Update/SetState.
            var entity = new Entity("msdyn_configuration", Guid.NewGuid());
            entity["msdyn_name"] = "ValidationRules:" + targetEntityLogicalName;
            entity["msdyn_value"] = rules.ToString(Formatting.None);
            return entity;
        }

        private static JObject CreateRule(
            string message,
            string stage,
            string field,
            string ruleType = "Required",
            bool isActive = true,
            int executionOrder = 0,
            string errorMessage = "Error")
        {
            return new JObject
            {
                ["message"] = message,
                ["stage"] = stage,
                ["field"] = field,
                ["ruleType"] = ruleType,
                ["isActive"] = isActive,
                ["executionOrder"] = executionOrder,
                ["errorMessage"] = errorMessage
            };
        }

        [TestMethod]
        public void GetActiveRules_FiltersByMessageStageAndActiveFlag_AndOrdersByExecutionOrder()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var rules = new JArray
            {
                CreateRule("Create", "PreOperation", "name", executionOrder: 2),
                CreateRule("Create", "PreOperation", "telephone1", executionOrder: 1),
                CreateRule("Update", "PreOperation", "name"),
                CreateRule("Create", "PostOperation", "name"),
                CreateRule("Create", "PreOperation", "fax", isActive: false)
            };

            var context = new XrmFakedContext();
            context.Initialize(new List<Entity> { CreateConfigurationRow(entityName, rules) });

            var repository = new DataverseValidationRuleRepository(context.GetOrganizationService(), new FakeTracingService(), Guid.NewGuid());

            var activeRules = repository.GetActiveRules(entityName, "Create", PipelineStage.PreOperation);

            Assert.AreEqual(2, activeRules.Count);
            Assert.AreEqual("telephone1", activeRules[0].AttributeLogicalName);
            Assert.AreEqual("name", activeRules[1].AttributeLogicalName);
        }

        [TestMethod]
        public void GetActiveRules_DifferentEntity_IsNotAffectedByAnotherEntitysRow()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity>
            {
                CreateConfigurationRow("otherentity", new JArray { CreateRule("Create", "PreOperation", "name") })
            });

            var repository = new DataverseValidationRuleRepository(context.GetOrganizationService(), new FakeTracingService(), Guid.NewGuid());

            var rules = repository.GetActiveRules(entityName, "Create", PipelineStage.PreOperation);

            Assert.AreEqual(0, rules.Count);
        }

        [TestMethod]
        public void GetActiveRules_NoConfigurationRow_ReturnsEmptyInsteadOfThrowing()
        {
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity>());

            var repository = new DataverseValidationRuleRepository(context.GetOrganizationService(), new FakeTracingService(), Guid.NewGuid());

            var rules = repository.GetActiveRules("account", "Create", PipelineStage.PreOperation);

            Assert.AreEqual(0, rules.Count);
        }

        [TestMethod]
        public void GetActiveRules_MalformedJson_ThrowsValidationConfigurationException()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var entity = new Entity("msdyn_configuration", Guid.NewGuid())
            {
                ["msdyn_name"] = "ValidationRules:" + entityName,
                ["msdyn_value"] = "{not-an-array",
                ["statecode"] = new OptionSetValue(0)
            };

            var context = new XrmFakedContext();
            context.Initialize(new List<Entity> { entity });

            var repository = new DataverseValidationRuleRepository(context.GetOrganizationService(), new FakeTracingService(), Guid.NewGuid());

            Assert.ThrowsException<ValidationConfigurationException>(
                () => repository.GetActiveRules(entityName, "Create", PipelineStage.PreOperation));
        }

        [TestMethod]
        public void GetActiveRules_CachesPerOrganization_DoesNotLeakBetweenOrganizations()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity>
            {
                CreateConfigurationRow(entityName, new JArray { CreateRule("Create", "PreOperation", "name") })
            });
            var organizationService = context.GetOrganizationService();

            var repositoryOrgA = new DataverseValidationRuleRepository(organizationService, new FakeTracingService(), Guid.NewGuid());
            var repositoryOrgB = new DataverseValidationRuleRepository(organizationService, new FakeTracingService(), Guid.NewGuid());

            // Populate organization A's cache with a single rule.
            Assert.AreEqual(1, repositoryOrgA.GetActiveRules(entityName, "Create", PipelineStage.PreOperation).Count);

            // Add a second row for the same entity (same msdyn_name) after A's cache has already been populated:
            // the repository merges rules from every row sharing that name.
            organizationService.Create(CreateConfigurationRowForInsert(entityName, new JArray { CreateRule("Create", "PreOperation", "telephone1") }));

            // Organization B has no cache entry yet: it must immediately see both rules.
            Assert.AreEqual(2, repositoryOrgB.GetActiveRules(entityName, "Create", PipelineStage.PreOperation).Count);

            // Organization A must instead keep seeing the already-cached result (a single rule).
            Assert.AreEqual(1, repositoryOrgA.GetActiveRules(entityName, "Create", PipelineStage.PreOperation).Count);
        }

        [TestMethod]
        public void GetActiveRules_QueryFails_SkipsValidationWithoutCachingTheFailure()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var context = new XrmFakedContext();
            context.Initialize(new List<Entity>
            {
                CreateConfigurationRow(entityName, new JArray { CreateRule("Create", "PreOperation", "name") })
            });

            var organizationService = new TransientlyFailingOrganizationService(context.GetOrganizationService(), 1);
            var repository = new DataverseValidationRuleRepository(organizationService, new FakeTracingService(), Guid.NewGuid());

            Assert.AreEqual(
                0,
                repository.GetActiveRules(entityName, "Create", PipelineStage.PreOperation).Count,
                "a failed configuration read must not block the business operation");

            Assert.AreEqual(
                1,
                repository.GetActiveRules(entityName, "Create", PipelineStage.PreOperation).Count,
                "the failure must not be cached, otherwise a transient error would disable validation for the whole cache window");
        }

        [TestMethod]
        public void GetActiveRules_SameExecutionOrder_KeepsTheConfiguredOrder()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var expectedOrder = new[] { "fieldA", "fieldB", "fieldC", "fieldD", "fieldE", "fieldF" };
            var rules = new JArray();
            foreach (var field in expectedOrder)
            {
                rules.Add(CreateRule("Create", "PreOperation", field));
            }

            var context = new XrmFakedContext();
            context.Initialize(new List<Entity> { CreateConfigurationRow(entityName, rules) });

            var repository = new DataverseValidationRuleRepository(context.GetOrganizationService(), new FakeTracingService(), Guid.NewGuid());

            var activeRules = repository.GetActiveRules(entityName, "Create", PipelineStage.PreOperation);

            CollectionAssert.AreEqual(expectedOrder, activeRules.Select(rule => rule.AttributeLogicalName).ToArray());
        }

        [TestMethod]
        public void GetActiveRules_PropertyOfTheWrongType_IsReportedAsAConfigurationError()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var rule = CreateRule("Create", "PreOperation", "name");
            rule["executionOrder"] = "first";

            var context = new XrmFakedContext();
            context.Initialize(new List<Entity> { CreateConfigurationRow(entityName, new JArray { rule }) });

            var repository = new DataverseValidationRuleRepository(context.GetOrganizationService(), new FakeTracingService(), Guid.NewGuid());

            Assert.ThrowsException<ValidationConfigurationException>(
                () => repository.GetActiveRules(entityName, "Create", PipelineStage.PreOperation));
        }

        [TestMethod]
        public void GetActiveRules_BrokenRuleForAnotherMessage_IsStillReportedAtLoadTime()
        {
            var entityName = "vldtest_" + Guid.NewGuid().ToString("N");
            var brokenRule = CreateRule("Update", "PostOperation", null, ruleType: "Expression");
            brokenRule["parameters"] = JObject.Parse(@"{""condition"":{""field"":""tipo"",""op"":""equalz"",""value"":""X""}}");

            var rules = new JArray { CreateRule("Create", "PreOperation", "name"), brokenRule };

            var context = new XrmFakedContext();
            context.Initialize(new List<Entity> { CreateConfigurationRow(entityName, rules) });

            var repository = new DataverseValidationRuleRepository(
                context.GetOrganizationService(),
                new FakeTracingService(),
                Guid.NewGuid(),
                RuleEvaluatorRegistry.CreateDefault());

            // The broken rule targets Update/PostOperation: without load-time validation this call would
            // happily return the Create rule and the mistake would stay hidden.
            var exception = Assert.ThrowsException<ValidationConfigurationException>(
                () => repository.GetActiveRules(entityName, "Create", PipelineStage.PreOperation));

            StringAssert.Contains(exception.Message, "equalz");
        }
    }
}

