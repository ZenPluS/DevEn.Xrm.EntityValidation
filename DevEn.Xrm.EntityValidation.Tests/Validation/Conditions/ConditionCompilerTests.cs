using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation.Conditions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevEn.Xrm.EntityValidation.Tests.Validation.Conditions
{
    [TestClass]
    public class ConditionCompilerTests
    {
        private static ValidationConfigurationException CompileError(string parametersJson)
        {
            var rule = TestRuleBuilder.Create("Expression", attributeLogicalName: null, parametersJson: parametersJson);

            return Assert.ThrowsException<ValidationConfigurationException>(() => ConditionCompiler.Compile(rule));
        }

        [TestMethod]
        public void Compile_UnknownOperator_NamesTheJsonPathAndTheValidOperators()
        {
            var exception = CompileError(@"{""condition"":{""field"":""tipo"",""op"":""equalz"",""value"":""X""}}");

            StringAssert.Contains(exception.Message, "'condition.op'");
            StringAssert.Contains(exception.Message, "equalz");
        }

        [TestMethod]
        public void Compile_ErrorInsideAGroup_NamesTheIndexedJsonPath()
        {
            var exception = CompileError(@"{""condition"":{""all"":[
                {""field"":""tipo"",""op"":""=="",""value"":""X""},
                {""field"":""paese"",""op"":""==""}]}}");

            StringAssert.Contains(exception.Message, "condition.all[1]");
        }

        [TestMethod]
        public void Compile_MissingLeftSide_ExplainsWhatToAdd()
        {
            var exception = CompileError(@"{""condition"":{""op"":""=="",""value"":""X""}}");

            StringAssert.Contains(exception.Message, "field");
        }

        [TestMethod]
        public void Compile_TwoRightHandSides_IsRejected()
        {
            var exception = CompileError(@"{""condition"":{""field"":""tipo"",""op"":""=="",""value"":""X"",""compareToField"":""altro""}}");

            StringAssert.Contains(exception.Message, "more than one right-hand side");
        }

        [TestMethod]
        public void Compile_EmptyGroup_IsRejected()
        {
            var exception = CompileError(@"{""condition"":{""all"":[]}}");

            StringAssert.Contains(exception.Message, "condition.all");
        }

        [TestMethod]
        public void Compile_InvalidDateToken_ExplainsTheAcceptedFormats()
        {
            var exception = CompileError(@"{""condition"":{""field"":""somedate"",""op"":"">="",""date"":""domani""}}");

            StringAssert.Contains(exception.Message, "condition.date");
            StringAssert.Contains(exception.Message, "Today+30d");
        }

        [TestMethod]
        public void Compile_ArithmeticNextToField_IsRejectedAsAmbiguous()
        {
            var exception = CompileError(@"{""condition"":{""field"":""importo"",""multiply"":2,""op"":"">"",""value"":10}}");

            StringAssert.Contains(exception.Message, "multiply");
        }

        [TestMethod]
        public void Compile_KeysAreCaseInsensitive()
        {
            var rule = TestRuleBuilder.Create(
                "Expression",
                attributeLogicalName: null,
                parametersJson: @"{""Condition"":{""Field"":""tipo"",""Op"":""=="",""Value"":""X""}}");

            Assert.IsNotNull(ConditionCompiler.Compile(rule));
        }

        [TestMethod]
        public void CollectFieldNames_ReturnsEveryAttributeTheConditionReads()
        {
            var rule = TestRuleBuilder.Create(
                "Expression",
                attributeLogicalName: null,
                parametersJson: @"{""condition"":{""all"":[
                    {""field"":""tipo"",""op"":""=="",""value"":""Cliente""},
                    {""field"":""importo"",""op"":""<="",""compareTo"":{""field"":""creditlimit"",""multiply"":1.1}}]}}");

            var fieldNames = ConditionCompiler.CollectFieldNames(ConditionCompiler.Compile(rule));

            CollectionAssert.AreEquivalent(
                new[] { "tipo", "importo", "creditlimit" },
                System.Linq.Enumerable.ToArray(fieldNames));
        }
    }
}
