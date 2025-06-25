namespace NewLang.Core.Tests.ParserTests;

public static class ParserHelpers
{
    public static ValueAccessorExpression Literal(int value)
    {
        return new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
            Token.IntLiteral(value, SourceSpan.Default)));
    }

    public static ValueAccessorExpression Literal(string value)
    {
        return new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
            Token.StringLiteral(value, SourceSpan.Default)));
    }

    public static BinaryOperatorExpression Multiply(IExpression? left, IExpression? right)
    {
        return new BinaryOperatorExpression(new BinaryOperator(
            BinaryOperatorType.Multiply,
            left,
            right,
            Token.Star(SourceSpan.Default)));
    }

    public static UnaryOperatorExpression FallOut(IExpression? value)
    {
        return new UnaryOperatorExpression(new UnaryOperator(UnaryOperatorType.FallOut, value,
            Token.QuestionMark(SourceSpan.Default)));
    }

    public static UnaryOperatorExpression Not(IExpression? value)
    {
        return new UnaryOperatorExpression(new UnaryOperator(UnaryOperatorType.Not, value,
            Token.QuestionMark(SourceSpan.Default)));
    }

    public static BlockExpression Block(IReadOnlyList<IExpression>? expressions = null)
    {
        return new BlockExpression(new Block(expressions ?? [], []), SourceRange.Default);
    }

    public static VariableDeclarationExpression VariableDeclaration(
        string name,
        IExpression? value = null,
        TypeIdentifier? type = null,
        bool isMutable = false)
    {
        return new VariableDeclarationExpression(new VariableDeclaration(
            Token.Identifier(name, SourceSpan.Default),
            isMutable
                ? new MutabilityModifier(Token.Mut(SourceSpan.Default))
                : null,
            type,
            value), SourceRange.Default);
    }

    public static TypeIdentifier IntType()
    {
        return new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default);
    }

    public static TypeIdentifier StringType()
    {
        return new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default);
    }

    public static ClassField ClassField(
        string name,
        TypeIdentifier type,
        bool isMutable = false,
        bool isStatic = false,
        bool isPublic = false,
        IExpression? value = null)
    {
        return new ClassField(
            isPublic ? new AccessModifier(Token.Pub(SourceSpan.Default)) : null,
            isStatic ? new StaticModifier(Token.Static(SourceSpan.Default)) : null,
            isMutable ? new MutabilityModifier(Token.Mut(SourceSpan.Default)) : null,
            Token.Identifier(name, SourceSpan.Default),
            type,
            value);
    }

    public static ValueAccessorExpression VariableAccessor(string name)
    {
        return new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
            Token.Identifier(name, SourceSpan.Default)));
    }
}