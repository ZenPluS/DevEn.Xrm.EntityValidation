using System.Collections.Generic;

namespace DevEn.Xrm.EntityValidation.Validation.Conditions
{
    /// <summary>
    /// Compiled form of an "Expression" rule's <c>condition</c> object: a small tree of comparisons
    /// combined with all/any/not. Built once by <see cref="ConditionCompiler"/> and walked by
    /// <see cref="ConditionEvaluator"/>.
    /// </summary>
    internal abstract class ConditionNode
    {
    }

    internal sealed class ComparisonConditionNode : ConditionNode
    {
        public ComparisonConditionNode(ConditionOperand left, ComparisonOperator comparisonOperator, ConditionOperand right)
        {
            Left = left;
            Operator = comparisonOperator;
            Right = right;
        }

        public ConditionOperand Left { get; }

        public ComparisonOperator Operator { get; }

        public ConditionOperand Right { get; }
    }

    internal sealed class GroupConditionNode : ConditionNode
    {
        public GroupConditionNode(bool requiresAll, IReadOnlyList<ConditionNode> children)
        {
            RequiresAll = requiresAll;
            Children = children;
        }

        public bool RequiresAll { get; }

        public IReadOnlyList<ConditionNode> Children { get; }
    }

    internal sealed class NegationConditionNode : ConditionNode
    {
        public NegationConditionNode(ConditionNode operand)
        {
            Operand = operand;
        }

        public ConditionNode Operand { get; }
    }

    internal abstract class ConditionOperand
    {
    }

    internal sealed class FieldOperand : ConditionOperand
    {
        public FieldOperand(string fieldName)
        {
            FieldName = fieldName;
        }

        public string FieldName { get; }
    }

    internal sealed class LiteralOperand : ConditionOperand
    {
        public LiteralOperand(object value)
        {
            Value = value;
        }

        public object Value { get; }
    }

    /// <summary>
    /// A date token kept in its textual form on purpose: "Today" must follow the clock at evaluation time,
    /// not freeze at the moment the condition was compiled and cached.
    /// </summary>
    internal sealed class DateOperand : ConditionOperand
    {
        public DateOperand(string token)
        {
            Token = token;
        }

        public string Token { get; }
    }

    internal enum ArithmeticOperator
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        AddDays,
        SubtractDays,
        DifferenceInDays
    }

    internal sealed class ArithmeticOperand : ConditionOperand
    {
        public ArithmeticOperand(ConditionOperand source, ArithmeticOperator arithmeticOperator, ConditionOperand argument)
        {
            Source = source;
            Operator = arithmeticOperator;
            Argument = argument;
        }

        public ConditionOperand Source { get; }

        public ArithmeticOperator Operator { get; }

        public ConditionOperand Argument { get; }
    }
}
