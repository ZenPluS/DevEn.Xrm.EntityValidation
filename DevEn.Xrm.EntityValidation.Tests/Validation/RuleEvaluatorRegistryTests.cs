using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class RuleEvaluatorRegistryTests
    {
        [TestMethod]
        public void CreateDefault_ContainsAllBuiltInRuleTypes()
        {
            var registry = RuleEvaluatorRegistry.CreateDefault();

            foreach (var ruleType in new[]
            {
                "Required", "Regex", "Range", "StringLength", "AllowedValues", "FieldComparison",
                "DateRange", "AtLeastOneOf", "Conditional", "Uniqueness", "RelatedRecordState"
            })
            {
                Assert.IsTrue(registry.TryGetEvaluator(ruleType, out _), $"Missing evaluator for '{ruleType}'.");
            }
        }

        [TestMethod]
        public void TryGetEvaluator_IsCaseInsensitive()
        {
            var registry = RuleEvaluatorRegistry.CreateDefault();

            Assert.IsTrue(registry.TryGetEvaluator("required", out var evaluator));
            Assert.AreEqual("Required", evaluator.RuleType);
        }

        [TestMethod]
        public void TryGetEvaluator_UnknownRuleType_ReturnsFalse()
        {
            var registry = RuleEvaluatorRegistry.CreateDefault();

            Assert.IsFalse(registry.TryGetEvaluator("DoesNotExist", out var evaluator));
            Assert.IsNull(evaluator);
        }

        [TestMethod]
        public void Constructor_DuplicateRuleType_Throws()
        {
            Assert.ThrowsException<ValidationConfigurationException>(() => new RuleEvaluatorRegistry(new IRuleEvaluator[]
            {
                new RequiredRuleEvaluator(),
                new RequiredRuleEvaluator()
            }));
        }
    }
}
