namespace DevEn.Xrm.EntityValidation.Validation.Expressions
{
    internal enum ExpressionTokenKind
    {
        Number,
        String,
        DateLiteral,
        Boolean,
        Identifier,
        LParen,
        RParen,
        Plus,
        Minus,
        Star,
        Slash,
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual,
        And,
        Or,
        Not,
        End
    }

    /// <summary>
    /// A single lexical token produced by <see cref="ExpressionLexer"/>. <see cref="Value"/> carries the
    /// already-typed literal value for literal tokens (decimal/string/DateTime/bool), or the field name text
    /// for an <see cref="ExpressionTokenKind.Identifier"/>.
    /// </summary>
    internal sealed class ExpressionToken
    {
        public ExpressionToken(ExpressionTokenKind kind, string text, object value = null)
        {
            Kind = kind;
            Text = text;
            Value = value;
        }

        public ExpressionTokenKind Kind { get; }

        public string Text { get; }

        public object Value { get; }
    }
}
