using System;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class ExpressionRuleConfigurationTests
    {
        private readonly ExpressionRuleEvaluator _evaluator = new ExpressionRuleEvaluator();

        private const string Condition =
            @"{""condition"":{""all"":[
                {""field"":""tipo"",""op"":""=="",""value"":""Cliente""},
                {""field"":""paese"",""op"":""=="",""value"":""IT""}]}}";

        private static string NewEntityName()
        {
            return "vldtest_" + Guid.NewGuid().ToString("N");
        }

        private void Validate(string entityName, FakeMetadataOrganizationService service, string parametersJson)
        {
            var rule = TestRuleBuilder.Create(
                "Expression",
                targetEntityLogicalName: entityName,
                attributeLogicalName: null,
                parametersJson: parametersJson);

            _evaluator.ValidateConfiguration(rule, new RuleConfigurationContext(service, Guid.NewGuid(), new FakeTracingService()));
        }

        [TestMethod]
        public void ValidateConfiguration_UnknownAttribute_ThrowsAndNamesIt()
        {
            var entityName = NewEntityName();
            var service = FakeMetadataOrganizationService.WithAttributes(entityName, "tipo", "paese");

            var exception = Assert.ThrowsException<ValidationConfigurationException>(
                () => Validate(entityName, service, Condition.Replace("paese", "paesse")));

            StringAssert.Contains(exception.Message, "paesse");
        }

        [TestMethod]
        public void ValidateConfiguration_KnownAttributes_DoesNotThrow()
        {
            var entityName = NewEntityName();
            var service = FakeMetadataOrganizationService.WithAttributes(entityName, "tipo", "paese");

            Validate(entityName, service, Condition);
        }

        [TestMethod]
        public void ValidateConfiguration_MetadataUnavailable_SkipsTheCheckInsteadOfBlocking()
        {
            var entityName = NewEntityName();
            var service = FakeMetadataOrganizationService.Unavailable();

            Validate(entityName, service, Condition.Replace("paese", "paesse"));
        }

        [TestMethod]
        public void ValidateConfiguration_ReadsTheMetadataOnlyOncePerEntity()
        {
            var entityName = NewEntityName();
            var service = FakeMetadataOrganizationService.WithAttributes(entityName, "tipo", "paese");
            var context = new RuleConfigurationContext(service, Guid.NewGuid(), new FakeTracingService());
            var rule = TestRuleBuilder.Create(
                "Expression",
                targetEntityLogicalName: entityName,
                attributeLogicalName: null,
                parametersJson: Condition);

            _evaluator.ValidateConfiguration(rule, context);
            _evaluator.ValidateConfiguration(rule, context);

            Assert.AreEqual(1, service.ExecuteCount);
        }

        [TestMethod]
        public void ValidateConfiguration_BrokenCondition_ThrowsWithoutNeedingARecord()
        {
            var entityName = NewEntityName();
            var service = FakeMetadataOrganizationService.WithAttributes(entityName, "tipo");

            Assert.ThrowsException<ValidationConfigurationException>(
                () => Validate(entityName, service, @"{""condition"":{""field"":""tipo"",""op"":""==""}}"));
        }
    }
}
