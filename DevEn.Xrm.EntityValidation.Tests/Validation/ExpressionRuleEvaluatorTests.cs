using System;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Tests.TestHelpers;
using DevEn.Xrm.EntityValidation.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.Validation
{
    [TestClass]
    public class ExpressionRuleEvaluatorTests
    {
        private readonly ExpressionRuleEvaluator _evaluator = new ExpressionRuleEvaluator();

        private bool Evaluate(Entity entity, string conditionJson)
        {
            var rule = TestRuleBuilder.Create(
                "Expression",
                attributeLogicalName: null,
                parametersJson: "{\"condition\":" + conditionJson + "}");

            return _evaluator.IsValid(entity, rule, null);
        }

        [TestMethod]
        public void IsValid_FieldEqualsField_MatchingValues_ReturnsTrue()
        {
            var entity = new Entity("account") { ["fieldA"] = "same", ["fieldB"] = "same" };

            Assert.IsTrue(Evaluate(entity, @"{""field"":""fieldA"",""op"":""=="",""compareToField"":""fieldB""}"));
        }

        [TestMethod]
        public void IsValid_FieldEqualsField_DifferingValues_ReturnsFalse()
        {
            var entity = new Entity("account") { ["fieldA"] = "left", ["fieldB"] = "right" };

            Assert.IsFalse(Evaluate(entity, @"{""field"":""fieldA"",""op"":""=="",""compareToField"":""fieldB""}"));
        }

        [TestMethod]
        public void IsValid_OperatorAliases_BehaveLikeTheSymbols()
        {
            var entity = new Entity("account") { ["tipo"] = "Cliente" };

            Assert.IsTrue(Evaluate(entity, @"{""field"":""tipo"",""op"":""eq"",""value"":""Cliente""}"));
            Assert.IsTrue(Evaluate(entity, @"{""field"":""tipo"",""op"":""="",""value"":""Cliente""}"));
            Assert.IsFalse(Evaluate(entity, @"{""field"":""tipo"",""op"":""<>"",""value"":""Cliente""}"));
        }

        [TestMethod]
        public void IsValid_DateGreaterOrEqualToday_FutureDate_ReturnsTrue()
        {
            var entity = new Entity("account") { ["somedate"] = DateTime.UtcNow.AddDays(1) };

            Assert.IsTrue(Evaluate(entity, @"{""field"":""somedate"",""op"":"">="",""date"":""Today""}"));
        }

        [TestMethod]
        public void IsValid_DateGreaterOrEqualToday_PastDate_ReturnsFalse()
        {
            var entity = new Entity("account") { ["somedate"] = DateTime.UtcNow.AddDays(-1) };

            Assert.IsFalse(Evaluate(entity, @"{""field"":""somedate"",""op"":"">="",""date"":""Today""}"));
        }

        [TestMethod]
        public void IsValid_DateOffsetComparison_WithinThirtyDays_ReturnsTrue()
        {
            var today = DateTime.UtcNow.Date;
            var entity = new Entity("account") { ["dateA"] = today, ["dateB"] = today };

            Assert.IsTrue(Evaluate(entity, @"{""field"":""dateA"",""op"":""<="",""compareTo"":{""field"":""dateB"",""addDays"":30}}"));
        }

        [TestMethod]
        public void IsValid_DateOffsetComparison_BeyondThirtyDays_ReturnsFalse()
        {
            var today = DateTime.UtcNow.Date;
            var entity = new Entity("account") { ["dateA"] = today.AddDays(31), ["dateB"] = today };

            Assert.IsFalse(Evaluate(entity, @"{""field"":""dateA"",""op"":""<="",""compareTo"":{""field"":""dateB"",""addDays"":30}}"));
        }

        [TestMethod]
        public void IsValid_DifferenceInDays_ComparedToANumber()
        {
            var today = DateTime.UtcNow.Date;
            var entity = new Entity("account") { ["dateA"] = today.AddDays(10), ["dateB"] = today };

            Assert.IsFalse(Evaluate(
                entity,
                @"{""left"":{""field"":""dateA"",""differenceInDays"":{""field"":""dateB""}},""op"":""<="",""value"":7}"));
        }

        [TestMethod]
        public void IsValid_AllGroup_BothMatch_ReturnsTrue()
        {
            var entity = new Entity("account") { ["tipo"] = "Cliente", ["paese"] = "IT" };

            Assert.IsTrue(Evaluate(entity, @"{""all"":[
                {""field"":""tipo"",""op"":""=="",""value"":""Cliente""},
                {""field"":""paese"",""op"":""=="",""value"":""IT""}]}"));
        }

        [TestMethod]
        public void IsValid_AllGroup_OneMismatches_ReturnsFalse()
        {
            var entity = new Entity("account") { ["tipo"] = "Cliente", ["paese"] = "FR" };

            Assert.IsFalse(Evaluate(entity, @"{""all"":[
                {""field"":""tipo"",""op"":""=="",""value"":""Cliente""},
                {""field"":""paese"",""op"":""=="",""value"":""IT""}]}"));
        }

        [TestMethod]
        public void IsValid_AnyGroup_OneMatches_ReturnsTrue()
        {
            var entity = new Entity("account") { ["tipo"] = "Partner" };

            Assert.IsTrue(Evaluate(entity, @"{""any"":[
                {""field"":""tipo"",""op"":""=="",""value"":""Cliente""},
                {""field"":""tipo"",""op"":""=="",""value"":""Partner""}]}"));
        }

        [TestMethod]
        public void IsValid_NotGroup_InvertsTheInnerCondition()
        {
            var entity = new Entity("account") { ["tipo"] = "Cliente" };

            Assert.IsFalse(Evaluate(entity, @"{""not"":{""field"":""tipo"",""op"":""=="",""value"":""Cliente""}}"));
        }

        [TestMethod]
        public void IsValid_Arithmetic_WithinCreditLimitMargin_ReturnsTrue()
        {
            var entity = new Entity("account") { ["importo"] = 100m, ["creditlimit"] = 100m };

            Assert.IsTrue(Evaluate(entity, @"{""field"":""importo"",""op"":""<="",""compareTo"":{""field"":""creditlimit"",""multiply"":1.1}}"));
        }

        [TestMethod]
        public void IsValid_Arithmetic_ExceedsCreditLimitMargin_ReturnsFalse()
        {
            var entity = new Entity("account") { ["importo"] = 120m, ["creditlimit"] = 100m };

            Assert.IsFalse(Evaluate(entity, @"{""field"":""importo"",""op"":""<="",""compareTo"":{""field"":""creditlimit"",""multiply"":1.1}}"));
        }

        [TestMethod]
        public void IsValid_TwoOptionsFieldAgainstBooleanLiteral_IsComparable()
        {
            var entity = new Entity("account") { ["donotemail"] = true };

            Assert.IsTrue(Evaluate(entity, @"{""field"":""donotemail"",""op"":""=="",""value"":true}"));
            Assert.IsFalse(Evaluate(entity, @"{""field"":""donotemail"",""op"":""=="",""value"":false}"));
        }

        [TestMethod]
        public void IsValid_OptionSetAgainstNumericLiteral_ComparesTheOptionValue()
        {
            var entity = new Entity("account") { ["statuscode"] = new OptionSetValue(10) };

            Assert.IsTrue(Evaluate(entity, @"{""field"":""statuscode"",""op"":""=="",""value"":10}"));

            // Compared as text, "10" would sort before "9".
            Assert.IsTrue(Evaluate(entity, @"{""field"":""statuscode"",""op"":"">"",""value"":9}"));
        }

        [TestMethod]
        public void IsValid_AbsentField_ReturnsTrue()
        {
            var entity = new Entity("account");

            Assert.IsTrue(Evaluate(entity, @"{""field"":""fieldA"",""op"":""=="",""compareToField"":""fieldB""}"));
        }

        [TestMethod]
        public void IsValid_IncompatibleTypes_ThrowsConfigurationException()
        {
            var entity = new Entity("account") { ["fieldA"] = "text", ["fieldB"] = 123 };

            Assert.ThrowsException<ValidationConfigurationException>(
                () => Evaluate(entity, @"{""field"":""fieldA"",""op"":""=="",""compareToField"":""fieldB""}"));
        }

        [TestMethod]
        public void IsValid_DivisionByZero_ThrowsConfigurationException()
        {
            var entity = new Entity("account") { ["importo"] = 10m, ["creditlimit"] = 0m };

            Assert.ThrowsException<ValidationConfigurationException>(() => Evaluate(
                entity,
                @"{""left"":{""field"":""importo"",""divide"":{""field"":""creditlimit""}},""op"":"">"",""value"":1}"));
        }

        [TestMethod]
        public void IsValid_DayArithmeticOnANumericField_ThrowsConfigurationException()
        {
            var entity = new Entity("account") { ["importo"] = 10m };

            Assert.ThrowsException<ValidationConfigurationException>(() => Evaluate(
                entity,
                @"{""left"":{""field"":""importo"",""addDays"":5},""op"":"">"",""value"":1}"));
        }

        [TestMethod]
        public void IsValid_LegacyExpressionSyntax_PointsAtTheNewFormat()
        {
            var rule = TestRuleBuilder.Create("Expression", parametersJson: @"{""expression"":""fieldA == fieldB""}");

            var exception = Assert.ThrowsException<ValidationConfigurationException>(
                () => _evaluator.IsValid(new Entity("account"), rule, null));

            StringAssert.Contains(exception.Message, "'condition'");
        }

        [TestMethod]
        public void IsValid_MissingCondition_ThrowsConfigurationException()
        {
            var rule = TestRuleBuilder.Create("Expression", parametersJson: "{}");

            Assert.ThrowsException<ValidationConfigurationException>(() => _evaluator.IsValid(new Entity("account"), rule, null));
        }
    }
}
