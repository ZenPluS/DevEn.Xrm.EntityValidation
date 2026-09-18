using System;

namespace DevEn.Xrm.EntityValidation.Validation.Expressions
{
    internal enum UnaryOperator
    {
        Negate,
        Not
    }

    internal enum BinaryOperator
    {
        Or,
        And,
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual,
        Add,
        Subtract,
        Multiply,
        Divide
    }

    /// <summary>Base type for the "Expression" rule type's AST, produced by <see cref="ExpressionParser"/>.</summary>
    internal abstract class ExpressionNode
    {
    }

    internal sealed class FieldReferenceNode : ExpressionNode
    {
        public FieldReferenceNode(string fieldName)
        {
            FieldName = fieldName;
        }

        public string FieldName { get; }
    }

    internal sealed class NumberLiteralNode : ExpressionNode
    {
        public NumberLiteralNode(decimal value)
        {
            Value = value;
        }

        public decimal Value { get; }
    }

    internal sealed class StringLiteralNode : ExpressionNode
    {
        public StringLiteralNode(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }

    internal sealed class DateLiteralNode : ExpressionNode
    {
        public DateLiteralNode(DateTime value)
        {
            Value = value;
        }

        public DateTime Value { get; }
    }

    internal sealed class BooleanLiteralNode : ExpressionNode
    {
        public BooleanLiteralNode(bool value)
        {
            Value = value;
        }

        public bool Value { get; }
    }

    internal sealed class UnaryNode : ExpressionNode
    {
        public UnaryNode(UnaryOperator unaryOperator, ExpressionNode operand)
        {
            Operator = unaryOperator;
            Operand = operand;
        }

        public UnaryOperator Operator { get; }

        public ExpressionNode Operand { get; }
    }

    internal sealed class BinaryNode : ExpressionNode
    {
        public BinaryNode(BinaryOperator binaryOperator, ExpressionNode left, ExpressionNode right)
        {
            Operator = binaryOperator;
            Left = left;
            Right = right;
        }

        public BinaryOperator Operator { get; }

        public ExpressionNode Left { get; }

        public ExpressionNode Right { get; }
    }
}
