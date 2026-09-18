using System;
using System.Linq;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Validation.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevEn.Xrm.EntityValidation.Tests.Validation.Expressions
{
    [TestClass]
    public class ExpressionLexerTests
    {
        private static ExpressionTokenKind[] Kinds(string expression)
        {
            return ExpressionLexer.Tokenize(expression, "test-rule").Select(t => t.Kind).ToArray();
        }

        [TestMethod]
        public void Tokenize_BlankExpression_ReturnsOnlyEndToken()
        {
            CollectionAssert.AreEqual(new[] { ExpressionTokenKind.End }, Kinds("   "));
        }

        [TestMethod]
        public void Tokenize_Number_ProducesNumberToken()
        {
            var tokens = ExpressionLexer.Tokenize("12.5", "test-rule");

            Assert.AreEqual(ExpressionTokenKind.Number, tokens[0].Kind);
            Assert.AreEqual(12.5m, tokens[0].Value);
        }

        [TestMethod]
        public void Tokenize_SingleQuotedString_ProducesStringToken()
        {
            var tokens = ExpressionLexer.Tokenize("'Cliente'", "test-rule");

            Assert.AreEqual(ExpressionTokenKind.String, tokens[0].Kind);
            Assert.AreEqual("Cliente", tokens[0].Value);
        }

        [TestMethod]
        public void Tokenize_DoubleQuotedString_ProducesStringToken()
        {
            var tokens = ExpressionLexer.Tokenize("\"Cliente\"", "test-rule");

            Assert.AreEqual(ExpressionTokenKind.String, tokens[0].Kind);
            Assert.AreEqual("Cliente", tokens[0].Value);
        }

        [TestMethod]
        public void Tokenize_QuotedAbsoluteDate_ProducesDateLiteralTokenInstead()
        {
            var tokens = ExpressionLexer.Tokenize("'2026-01-01'", "test-rule");

            Assert.AreEqual(ExpressionTokenKind.DateLiteral, tokens[0].Kind);
            Assert.AreEqual(new DateTime(2026, 1, 1), tokens[0].Value);
        }

        [TestMethod]
        public void Tokenize_BareToday_ProducesDateLiteralToken()
        {
            var tokens = ExpressionLexer.Tokenize("Today", "test-rule");

            Assert.AreEqual(ExpressionTokenKind.DateLiteral, tokens[0].Kind);
        }

        [TestMethod]
        public void Tokenize_RelativeDateOffsetWithNoWhitespace_ProducesSingleDateLiteralToken()
        {
            var tokens = ExpressionLexer.Tokenize("Today+30d", "test-rule");

            Assert.AreEqual(ExpressionTokenKind.DateLiteral, tokens[0].Kind);
            Assert.AreEqual(ExpressionTokenKind.End, tokens[1].Kind);
        }

        [TestMethod]
        public void Tokenize_FieldNamePlusNumber_DoesNotMistakeItForADateOffset()
        {
            CollectionAssert.AreEqual(
                new[] { ExpressionTokenKind.Identifier, ExpressionTokenKind.Plus, ExpressionTokenKind.Number, ExpressionTokenKind.End },
                Kinds("fieldA+30"));
        }

        [TestMethod]
        public void Tokenize_BooleanLiterals_AreCaseInsensitive()
        {
            var tokens = ExpressionLexer.Tokenize("TRUE false", "test-rule");

            Assert.AreEqual(ExpressionTokenKind.Boolean, tokens[0].Kind);
            Assert.AreEqual(true, tokens[0].Value);
            Assert.AreEqual(ExpressionTokenKind.Boolean, tokens[1].Kind);
            Assert.AreEqual(false, tokens[1].Value);
        }

        [TestMethod]
        public void Tokenize_PlainIdentifier_ProducesIdentifierToken()
        {
            var tokens = ExpressionLexer.Tokenize("creditlimit", "test-rule");

            Assert.AreEqual(ExpressionTokenKind.Identifier, tokens[0].Kind);
            Assert.AreEqual("creditlimit", tokens[0].Value);
        }

        [TestMethod]
        public void Tokenize_AllOperatorsAndPunctuation_ProduceExpectedKinds()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    ExpressionTokenKind.LParen, ExpressionTokenKind.RParen, ExpressionTokenKind.Plus,
                    ExpressionTokenKind.Minus, ExpressionTokenKind.Star, ExpressionTokenKind.Slash,
                    ExpressionTokenKind.Equal, ExpressionTokenKind.NotEqual, ExpressionTokenKind.Greater,
                    ExpressionTokenKind.GreaterOrEqual, ExpressionTokenKind.Less, ExpressionTokenKind.LessOrEqual,
                    ExpressionTokenKind.And, ExpressionTokenKind.Or, ExpressionTokenKind.Not, ExpressionTokenKind.End
                },
                Kinds("( ) + - * / == != > >= < <= && || !"));
        }

        [TestMethod]
        public void Tokenize_SingleEquals_ThrowsConfigurationException()
        {
            Assert.ThrowsException<ValidationConfigurationException>(() => ExpressionLexer.Tokenize("a = b", "test-rule"));
        }

        [TestMethod]
        public void Tokenize_SingleAmpersand_ThrowsConfigurationException()
        {
            Assert.ThrowsException<ValidationConfigurationException>(() => ExpressionLexer.Tokenize("a & b", "test-rule"));
        }

        [TestMethod]
        public void Tokenize_SinglePipe_ThrowsConfigurationException()
        {
            Assert.ThrowsException<ValidationConfigurationException>(() => ExpressionLexer.Tokenize("a | b", "test-rule"));
        }

        [TestMethod]
        public void Tokenize_UnterminatedString_ThrowsConfigurationException()
        {
            Assert.ThrowsException<ValidationConfigurationException>(() => ExpressionLexer.Tokenize("'unterminated", "test-rule"));
        }

        [TestMethod]
        public void Tokenize_InvalidCharacter_ThrowsConfigurationException()
        {
            Assert.ThrowsException<ValidationConfigurationException>(() => ExpressionLexer.Tokenize("a @ b", "test-rule"));
        }
    }
}
