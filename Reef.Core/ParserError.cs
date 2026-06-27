using System.Diagnostics;
using System.Text;
using Reef.Core.Expressions;

namespace Reef.Core;

public record ParserError
{
    private ParserError(ParserErrorType type, SourceRange? sourceRange, string message)
    {
        Type = type;
        SourceRange = sourceRange;
        Message = message;
    }

    public ParserErrorType Type { get; }
    public SourceRange? SourceRange { get; }
    public string Message { get; }

    public static ParserError ExpectedConstraint(Token? receivedToken)
    {
        return new ParserError(
            ParserErrorType.ExpectedConstraint,
            receivedToken?.SourceSpan.ToRange(),
            $"Expected type constraint, but found {receivedToken?.ToString() ?? "EOF"}");
    }

    public static ParserError ExpectedToken(Token? receivedToken, params IReadOnlyList<TokenType> expectedTokens)
    {
        if (expectedTokens.Count == 0)
        {
            throw new InvalidOperationException("Expected at least one token type");
        }

        return new ParserError(
            ParserErrorType.ExpectedToken,
            receivedToken?.SourceSpan.ToRange(),
            $"Expcted one of token types: [{string.Join(", ", expectedTokens.Order())}], but found {receivedToken?.ToString() ?? "EOF"}");
    }

    public static ParserError ExpectedTokenOrExpression(Token? receivedToken, params IReadOnlyList<TokenType> expectedTokens)
    {
        if (expectedTokens.Count == 0)
        {
            throw new InvalidOperationException("Expected at least one token type");
        }

        return new ParserError(
            ParserErrorType.ExpectedTokenOrExpression,
            receivedToken?.SourceSpan.ToRange(),
            $"Expcted either expression or one of token types: [{string.Join(", ", expectedTokens.Order())}], but found {receivedToken?.ToString() ?? "EOF"}");
    }

    public static ParserError ExpectedType(Token? receivedToken)
    {
        return new ParserError(
            ParserErrorType.ExpectedType,
            receivedToken?.SourceSpan.ToRange(),
            $"Expcted type identifier, but found {receivedToken?.ToString() ?? "EOF"}");
    }

    public static ParserError ExpectedTypeName(Token? receivedToken)
    {
        return new ParserError(
            ParserErrorType.ExpectedTypeName,
            receivedToken?.SourceSpan.ToRange(),
            $"Expcted type name, but found {receivedToken?.ToString() ?? "EOF"}");
    }

    public static ParserError ExpectedTypeOrToken(Token? receivedToken, params IReadOnlyList<TokenType> expectedTokens)
    {
        Debug.Assert(expectedTokens.Count > 0);

        return new ParserError(
            ParserErrorType.ExpectedTypeOrToken,
            receivedToken?.SourceSpan.ToRange(),
            $"Expcted type identifier or one of tokens: [{string.Join(",", expectedTokens.Order())}], but found {receivedToken?.ToString() ?? "EOF"}");
    }

    public static ParserError ExpectedExpression(Token? receivedToken)
    {
        return new ParserError(
            ParserErrorType.ExpectedExpression,
            receivedToken?.SourceSpan.ToRange(),
            $"Expcted expression, but found {receivedToken?.ToString() ?? "EOF"}");
    }

    public static ParserError UnexpectedModifier(Token receivedToken, params IReadOnlyList<TokenType> allowedModifiers)
    {
        var error = new StringBuilder("Unexpected modifier");
        if (allowedModifiers.Count > 0)
        {
            error.Append($", allowed modifiers: [{string.Join(", ", allowedModifiers.Order())}]");
        }
        return new ParserError(
            ParserErrorType.UnexpectedModifier,
            receivedToken.SourceSpan.ToRange(),
            error.ToString());
    }

    public static ParserError DuplicateModifier(Token receivedToken)
    {
        return new ParserError(
            ParserErrorType.DuplicateModifier,
            receivedToken.SourceSpan.ToRange(),
            $"Duplicate modifier \"{receivedToken}\"");
    }

    public static ParserError ExpectedPattern(Token? receivedToken)
    {
        return new ParserError(
            ParserErrorType.ExpectedPattern,
            receivedToken?.SourceSpan.ToRange(),
            $"Expcted pattern, but found {receivedToken?.ToString() ?? "EOF"}");
    }
    public static ParserError ExpectedPatternOrToken(Token? receivedToken, params IReadOnlyList<TokenType> tokens)
    {
        Debug.Assert(tokens.Count > 0);

        return new ParserError(
            ParserErrorType.ExpectedPatternOrToken,
            receivedToken?.SourceSpan.ToRange(),
            $"Expcted pattern or one of tokens: [{string.Join(", ", tokens.Order())}], but found {receivedToken?.ToString() ?? "EOF"}");
    }

    public static ParserError UnexpectedAttribute(LangAttribute attribute)
    {
        return new ParserError(
            ParserErrorType.UnexpectedAttribute,
            attribute.SourceRange,
            $"Unexpected attribute: \"{attribute}\"");
    }

    public static ParserError EmptyCharLiteral(StringToken token)
    {
        return new ParserError(
            ParserErrorType.EmptyCharLiteral,
            new SourceRange(token.SourceSpan, token.SourceSpan),
            "Char literal cannot be empty");
    }

    public static ParserError VariantOfNonNamedTypeIdentifier(SourceRange sourceRange)
    {
        return new ParserError(
            ParserErrorType.VariantOfWithBoxingSpecifier,
            sourceRange,
            "variantOf must be for a named type identifier"
        );
    }

    public static ParserError VariantOfWithBoxingSpecifier(SourceRange sourceRange)
    {
        return new ParserError(
            ParserErrorType.VariantOfWithBoxingSpecifier,
            sourceRange,
            "variantOf type cannot have a boxing specifier"
        );
    }

    public static ParserError CharLiteralTooLong(StringToken token)
    {
        return new ParserError(
            ParserErrorType.CharLiteralTooLong,
            new SourceRange(token.SourceSpan, token.SourceSpan),
            "Char literal can only contain a single character or a single escape sequence");
    }

    public static ParserError ForLoopIncorrectNumberOfExpressions(int foundExpressionCount, SourceRange sourceRange)
    {
        return new ParserError(
            ParserErrorType.ForLoopIncorrectNumberOfExpressions,
            sourceRange,
            $"For loop expects 3 expressions, but found {foundExpressionCount}");
        throw new NotImplementedException();
    }
}

public enum ParserErrorType
{
    ExpectedToken,
    ExpectedExpression,
    ExpectedType,
    ExpectedPattern,
    ExpectedPatternOrToken,
    ExpectedTypeOrToken,
    ExpectedTokenOrExpression,

    DuplicateModifier,
    UnexpectedModifier,

    ExpectedTypeName,
    ExpectedConstraint,
    UnexpectedAttribute,
    EmptyCharLiteral,
    CharLiteralTooLong,
    ForLoopIncorrectNumberOfExpressions,
    VariantOfWithBoxingSpecifier,
}
