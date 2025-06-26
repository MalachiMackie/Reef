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
        return new TypeIdentifier(Token.Identifier("int", SourceSpan.Default), [], SourceRange.Default);
    }

    public static TypeIdentifier StringType()
    {
        return new TypeIdentifier(Token.Identifier("string", SourceSpan.Default), [], SourceRange.Default);
    }

    public static ClassField ClassField(
        string name,
        TypeIdentifier? type = null,
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

    public static ProgramClass Class(string name, bool isPublic = false, IReadOnlyList<ClassField>? fields = null, IReadOnlyList<string>? genericParameters = null)
    {
        return new ProgramClass(
            isPublic ? new AccessModifier(Token.Pub(SourceSpan.Default)) : null,
            Token.Identifier(name, SourceSpan.Default),
            [..genericParameters?.Select(x => Token.Identifier(x, SourceSpan.Default)) ?? []],
            [],
            fields ?? []);
    }
    
    public static ProgramUnion Union(string name, bool isPublic = false, IReadOnlyList<IProgramUnionVariant>? variants = null, IReadOnlyList<string>? genericParameters = null)
    {
        return new ProgramUnion(
            isPublic ? new AccessModifier(Token.Pub(SourceSpan.Default)) : null,
            Token.Identifier(name, SourceSpan.Default),
            [..genericParameters?.Select(x => Token.Identifier(x, SourceSpan.Default)) ?? []],
            [],
            variants ?? []);
    }

    public static LangFunction Function(string name, bool isPublic = false, bool isStatic = false)
    {
        return new LangFunction(isPublic ? new AccessModifier(Token.Pub(SourceSpan.Default)) : null,
            isStatic ? new StaticModifier(Token.Static(SourceSpan.Default)) : null,
            Token.Identifier(name, SourceSpan.Default),
            [],
            [],
            null,
            new Block([], []));
    }

    public static MemberAccessExpression MemberAccess(IExpression owner, string? memberName)
    {
        return new MemberAccessExpression(new MemberAccess(owner, memberName is null ? null : Token.Identifier(memberName, SourceSpan.Default)));
    }
    
    public static StaticMemberAccessExpression StaticMemberAccess(TypeIdentifier type, string? memberName)
    {
        return new StaticMemberAccessExpression(new StaticMemberAccess(type, memberName is null ? null : Token.Identifier(memberName, SourceSpan.Default)));
    }
}