using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Validation.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevEn.Xrm.EntityValidation.Tests.Validation.Expressions
{
    [TestClass]
    public class ExpressionParserTests
    {
        private static ExpressionNode Parse(string expression)
        {
            var tokens = ExpressionLexer.Tokenize(expression, "test-rule");
            return ExpressionParser.Parse(tokens, "test-rule");
        }

        private static BinaryNode AssertBinary(ExpressionNode node, BinaryOperator expectedOperator)
        {
            var binary = node as BinaryNode;
            Assert.IsNotNull(binary, $"Expected a BinaryNode but got {node.GetType().Name}.");
            Assert.AreEqual(expectedOperator, binary.Operator);
            return binary;
        }

        private static void AssertField(ExpressionNode node, string expectedFieldName)
        {
            var field = node as FieldReferenceNode;
            Assert.IsNotNull(field, $"Expected a FieldReferenceNode but got {node.GetType().Name}.");
            Assert.AreEqual(expectedFieldName, field.FieldName);
        }

        private static void AssertNumber(ExpressionNode node, decimal expectedValue)
        {
            var number = node as NumberLiteralNode;
            Assert.IsNotNull(number, $"Expected a NumberLiteralNode but got {node.GetType().Name}.");
            Assert.AreEqual(expectedValue, number.Value);
        }

        [TestMethod]
        public void Parse_MultiplicationBindsTighterThanAddition()
        {
            var root = AssertBinary(Parse("1 + 2 * 3"), BinaryOperator.Add);
            AssertNumber(root.Left, 1);
            var right = AssertBinary(root.Right, BinaryOperator.Multiply);
            AssertNumber(right.Left, 2);
            AssertNumber(right.Right, 3);
        }

        [TestMethod]
        public void Parse_ComparisonBindsLooserThanAdditive()
        {
            var root = AssertBinary(Parse("a + 1 == b"), BinaryOperator.Equal);
            var left = AssertBinary(root.Left, BinaryOperator.Add);
            AssertField(left.Left, "a");
            AssertNumber(left.Right, 1);
            AssertField(root.Right, "b");
        }

        [TestMethod]
        public void Parse_AndBindsTighterThanOr()
        {
            var root = AssertBinary(Parse("a || b && c"), BinaryOperator.Or);
            AssertField(root.Left, "a");
            var right = AssertBinary(root.Right, BinaryOperator.And);
            AssertField(right.Left, "b");
            AssertField(right.Right, "c");
        }

        [TestMethod]
        public void Parse_Parentheses_OverridePrecedence()
        {
            var root = AssertBinary(Parse("(1 + 2) * 3"), BinaryOperator.Multiply);
            var left = AssertBinary(root.Left, BinaryOperator.Add);
            AssertNumber(left.Left, 1);
            AssertNumber(left.Right, 2);
            AssertNumber(root.Right, 3);
        }

        [TestMethod]
        public void Parse_UnaryNot_ProducesUnaryNode()
        {
            var node = Parse("!a") as UnaryNode;
            Assert.IsNotNull(node);
            Assert.AreEqual(UnaryOperator.Not, node.Operator);
            AssertField(node.Operand, "a");
        }

        [TestMethod]
        public void Parse_UnaryMinus_ProducesUnaryNode()
        {
            var node = Parse("-a") as UnaryNode;
            Assert.IsNotNull(node);
            Assert.AreEqual(UnaryOperator.Negate, node.Operator);
            AssertField(node.Operand, "a");
        }

        [TestMethod]
        public void Parse_DoubleNegation_NestsUnaryNodes()
        {
            var outer = Parse("!!a") as UnaryNode;
            Assert.IsNotNull(outer);
            Assert.AreEqual(UnaryOperator.Not, outer.Operator);
            var inner = outer.Operand as UnaryNode;
            Assert.IsNotNull(inner);
            Assert.AreEqual(UnaryOperator.Not, inner.Operator);
            AssertField(inner.Operand, "a");
        }

        [TestMethod]
        public void Parse_ChainedComparison_ThrowsConfigurationException()
        {
            Assert.ThrowsException<ValidationConfigurationException>(() => Parse("a < b < c"));
        }

        [TestMethod]
        public void Parse_MissingClosingParen_ThrowsConfigurationException()
        {
            Assert.ThrowsException<ValidationConfigurationException>(() => Parse("(a + b"));
        }

        [TestMethod]
        public void Parse_EmptyExpression_ThrowsConfigurationException()
        {
            Assert.ThrowsException<ValidationConfigurationException>(() => Parse(""));
        }

        [TestMethod]
        public void Parse_TrailingTokens_ThrowsConfigurationException()
        {
            Assert.ThrowsException<ValidationConfigurationException>(() => Parse("a b"));
        }

        [TestMethod]
        public void Parse_ExcessiveParenthesesNesting_ThrowsConfigurationException()
        {
            var expression = new string('(', 25) + "true" + new string(')', 25);

            Assert.ThrowsException<ValidationConfigurationException>(() => Parse(expression));
        }

        [TestMethod]
        public void Parse_ExcessiveUnaryChain_ThrowsConfigurationException()
        {
            var expression = new string('!', 25) + "true";

            Assert.ThrowsException<ValidationConfigurationException>(() => Parse(expression));
        }
    }
}
