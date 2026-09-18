using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class ConditionalRuleEvaluatorTests
    {
        private static IRuleEvaluator CreateEvaluator()
        {
            var found = RuleEvaluatorRegistry.CreateDefault().TryGetEvaluator("Conditional", out var evaluator);
            Assert.IsTrue(found);
            return evaluator;
        }

        [TestMethod]
        public void IsValid_ConditionNotMet_SkipsInnerRule()
        {
            var entity = new Entity("account") { ["accounttype"] = "Partner" };
            var rule = TestRuleBuilder.Create(
                "Conditional",
                attributeLogicalName: "new_taxid",
                parametersJson: "{\"when\":{\"field\":\"accounttype\",\"operator\":\"Equal\",\"value\":\"Customer\"},\"then\":{\"ruleType\":\"Required\"}}");

            Assert.IsTrue(CreateEvaluator().IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_ConditionMet_InnerFieldMissing_ReturnsFalse()
        {
            var entity = new Entity("account") { ["accounttype"] = "Customer" };
            var rule = TestRuleBuilder.Create(
                "Conditional",
                attributeLogicalName: "new_taxid",
                parametersJson: "{\"when\":{\"field\":\"accounttype\",\"operator\":\"Equal\",\"value\":\"Customer\"},\"then\":{\"ruleType\":\"Required\"}}");

            Assert.IsFalse(CreateEvaluator().IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_ConditionMet_InnerFieldPresent_ReturnsTrue()
        {
            var entity = new Entity("account") { ["accounttype"] = "Customer", ["new_taxid"] = "IT12345678901" };
            var rule = TestRuleBuilder.Create(
                "Conditional",
                attributeLogicalName: "new_taxid",
                parametersJson: "{\"when\":{\"field\":\"accounttype\",\"operator\":\"Equal\",\"value\":\"Customer\"},\"then\":{\"ruleType\":\"Required\"}}");

            Assert.IsTrue(CreateEvaluator().IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_NotEqualOperator_InvertsCondition()
        {
            var entity = new Entity("account") { ["accounttype"] = "Partner" };
            var rule = TestRuleBuilder.Create(
                "Conditional",
                attributeLogicalName: "new_taxid",
                parametersJson: "{\"when\":{\"field\":\"accounttype\",\"operator\":\"NotEqual\",\"value\":\"Customer\"},\"then\":{\"ruleType\":\"Required\"}}");

            Assert.IsFalse(CreateEvaluator().IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_InnerRuleParameters_PassThrough()
        {
            var entity = new Entity("account") { ["accounttype"] = "Customer", ["new_score"] = 5 };
            var rule = TestRuleBuilder.Create(
                "Conditional",
                attributeLogicalName: "new_score",
                parametersJson: "{\"when\":{\"field\":\"accounttype\",\"operator\":\"Equal\",\"value\":\"Customer\"},"
                    + "\"then\":{\"ruleType\":\"Range\",\"parameters\":{\"min\":0,\"max\":3}}}");

            Assert.IsFalse(CreateEvaluator().IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_InnerRuleTypeExpression_EvaluatesInnerExpression()
        {
            var entity = new Entity("account") { ["accounttype"] = "Customer", ["fieldA"] = "X", ["fieldB"] = "Y" };
            var rule = TestRuleBuilder.Create(
                "Conditional",
                attributeLogicalName: "fieldA",
                parametersJson: "{\"when\":{\"field\":\"accounttype\",\"operator\":\"Equal\",\"value\":\"Customer\"},"
                    + "\"then\":{\"ruleType\":\"Expression\",\"parameters\":{\"condition\":"
                    + "{\"field\":\"fieldA\",\"op\":\"==\",\"compareToField\":\"fieldB\"}}}}");

            Assert.IsFalse(CreateEvaluator().IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_MissingWhen_ThrowsConfigurationException()
        {
            var entity = new Entity("account");
            var rule = TestRuleBuilder.Create(
                "Conditional",
                attributeLogicalName: "new_taxid",
                parametersJson: "{\"then\":{\"ruleType\":\"Required\"}}");

            Assert.ThrowsException<ValidationConfigurationException>(() => CreateEvaluator().IsValid(entity, rule, null));
        }

        [TestMethod]
        public void IsValid_UnknownInnerRuleType_ThrowsConfigurationException()
        {
            var entity = new Entity("account") { ["accounttype"] = "Customer" };
            var rule = TestRuleBuilder.Create(
                "Conditional",
                attributeLogicalName: "new_taxid",
                parametersJson: "{\"when\":{\"field\":\"accounttype\",\"operator\":\"Equal\",\"value\":\"Customer\"},\"then\":{\"ruleType\":\"DoesNotExist\"}}");

            Assert.ThrowsException<ValidationConfigurationException>(() => CreateEvaluator().IsValid(entity, rule, null));
        }
    }
}
