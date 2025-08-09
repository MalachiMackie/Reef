namespace Reef.Core.Tests;

public static class ExpressionHelpers
{
    public static ValueAccessorExpression Literal(int value)
    {
        return new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
            Token.IntLiteral(value, SourceSpan.Default), null));
    }

    public static StringToken Identifier(string identifier) => Token.Identifier(identifier, SourceSpan.Default);

    public static ValueAccessorExpression Literal(string value)
    {
        return new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
            Token.StringLiteral(value, SourceSpan.Default), null));
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
            Token.Bang(SourceSpan.Default)));
    }

    public static BlockExpression Block(IReadOnlyList<IExpression>? expressions = null)
    {
        return new BlockExpression(new Block(expressions ?? [], []), SourceRange.Default);
    }

    public static VariableDeclarationExpression VariableDeclaration(
        string name,
        IExpression? value = null,
        ITypeIdentifier? type = null,
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

    public static NamedTypeIdentifier IntType()
    {
        return new NamedTypeIdentifier(Identifier("int"), [], SourceRange.Default);
    }

    public static NamedTypeIdentifier StringType()
    {
        return new NamedTypeIdentifier(Identifier("string"), [], SourceRange.Default);
    }
    
    public static NamedTypeIdentifier BooleanType()
    {
        return new NamedTypeIdentifier(Identifier("bool"), [], SourceRange.Default);
    }

    public static ClassField ClassField(
        string name,
        ITypeIdentifier? type = null,
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

    public static ValueAccessorExpression VariableAccessor(string name, IReadOnlyList<ITypeIdentifier>? typeArguments = null)
    {
        return new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
            Token.Identifier(name, SourceSpan.Default), typeArguments));
    }

    public static ProgramClass Class(string name, bool isPublic = false, IReadOnlyList<ClassField>? fields = null, IReadOnlyList<string>? typeParameters = null)
    {
        return new ProgramClass(
            isPublic ? new AccessModifier(Token.Pub(SourceSpan.Default)) : null,
            Token.Identifier(name, SourceSpan.Default),
            [..typeParameters?.Select(x => Token.Identifier(x, SourceSpan.Default)) ?? []],
            [],
            fields ?? []);
    }
    
    public static ProgramUnion Union(string name, bool isPublic = false, IReadOnlyList<IProgramUnionVariant>? variants = null, IReadOnlyList<string>? typeParameters = null)
    {
        return new ProgramUnion(
            isPublic ? new AccessModifier(Token.Pub(SourceSpan.Default)) : null,
            Token.Identifier(name, SourceSpan.Default),
            [..typeParameters?.Select(x => Token.Identifier(x, SourceSpan.Default)) ?? []],
            [],
            variants ?? []);
    }

    public static TypeChecker.FunctionObject FunctionObject(
        IEnumerable<(bool isMut, TypeChecker.ITypeReference parameterType)>? parameters = null,
        TypeChecker.ITypeReference? returnType = null)
    {
        return new TypeChecker.FunctionObject(
            parameters: parameters?.Select(x => new TypeChecker.FunctionParameter(x.parameterType, x.isMut)).ToArray() ?? [],
            returnType: returnType ?? TypeChecker.InstantiatedClass.Unit);
    }
    
    public static LangFunction Function(
        string name,
        bool isPublic = false,
        bool isStatic = false,
        bool isMutable = false,
        IReadOnlyList<FunctionParameter>? parameters = null,
        IReadOnlyList<string>? typeParameters = null,
        ITypeIdentifier? returnType = null)
    {
        return new LangFunction(isPublic
                ? new AccessModifier(Token.Pub(SourceSpan.Default))
                : null,
            isStatic
                ? new StaticModifier(Token.Static(SourceSpan.Default))
                : null,
            isMutable
                ? new MutabilityModifier(Token.Mut(SourceSpan.Default))
                : null,
            Token.Identifier(name, SourceSpan.Default),
            typeParameters?.Select(x => Token.Identifier(x, SourceSpan.Default)).ToArray() ?? [],
            parameters ?? [],
            returnType,
            new Block([], []));
    }

    public static FunctionParameter FunctionParameter(string name, ITypeIdentifier? type = null, bool isMutable = false)
    {
        return new FunctionParameter(
            type,
            isMutable ? new MutabilityModifier(Token.Mut(SourceSpan.Default)) : null,
            Token.Identifier(name, SourceSpan.Default));
    }

    public static MemberAccessExpression MemberAccess(IExpression owner, string? memberName, IReadOnlyList<ITypeIdentifier>? typeArguments = null)
    {
        return new MemberAccessExpression(new MemberAccess(owner, memberName is null ? null : Token.Identifier(memberName, SourceSpan.Default), typeArguments));
    }

    public static MethodCallExpression MethodCall(IExpression method, params IReadOnlyList<IExpression> arguments)
    {
        return new MethodCallExpression(new MethodCall(method, arguments), SourceRange.Default);
    }
    
    public static StaticMemberAccessExpression StaticMemberAccess(NamedTypeIdentifier type, string? memberName, IReadOnlyList<ITypeIdentifier>? typeArguments = null)
    {
        return new StaticMemberAccessExpression(new StaticMemberAccess(type, memberName is null ? null : Token.Identifier(memberName, SourceSpan.Default), typeArguments));
    }

    public static TupleExpression Tuple(IExpression first, params IEnumerable<IExpression> values)
    {
        return new TupleExpression([first, ..values], SourceRange.Default);
    }

    public static MatchesExpression Matches(IExpression value, IPattern? pattern = null)
    {
        return new MatchesExpression(value, pattern, SourceRange.Default);
    }

    public static MatchExpression Match(IExpression value, IReadOnlyList<MatchArm>? arms = null)
    {
        return new MatchExpression(value, arms ?? [],  SourceRange.Default);
    }

    public static ObjectInitializerExpression ObjectInitializer(NamedTypeIdentifier type, IReadOnlyList<FieldInitializer>? fieldInitializers = null)
    {
        return new ObjectInitializerExpression(new ObjectInitializer(type, fieldInitializers ?? []), SourceRange.Default);
    }

    public static UnionClassVariantInitializerExpression UnionClassVariantInitializer(NamedTypeIdentifier type,
        string variantName, IReadOnlyList<FieldInitializer>? fieldInitializers = null)
    {
        return new UnionClassVariantInitializerExpression(
            new UnionClassVariantInitializer(
                type,
                Token.Identifier(variantName, SourceSpan.Default),
                fieldInitializers ?? []
            ),
            SourceRange.Default);
    }

    public static FieldInitializer FieldInitializer(string fieldName, IExpression? value = null)
    {
        return new FieldInitializer(Token.Identifier(fieldName, SourceSpan.Default), value);
    }

    public static MatchArm MatchArm(IPattern pattern, IExpression? expression = null)
    {
        return new MatchArm(pattern, expression);
    }

    public static DiscardPattern DiscardPattern() => new (SourceRange.Default);

    public static UnionVariantPattern UnionVariantPattern(ITypeIdentifier unionType, string? variantName = null, string? variableName = null)
    {
        return new UnionVariantPattern(
            unionType,
            variantName is null ? null : Token.Identifier(variantName, SourceSpan.Default),
            variableName is null ? null : Token.Identifier(variableName, SourceSpan.Default),
            SourceRange.Default);
    }

    public static UnionTupleVariantPattern UnionTupleVariantPattern(ITypeIdentifier unionType, string variantName, IReadOnlyList<IPattern>? patterns = null, string? variableName = null)
    {
        return new UnionTupleVariantPattern(unionType,
            Token.Identifier(variantName, SourceSpan.Default),
            patterns ?? [],
            variableName is null ? null : Token.Identifier(variableName, SourceSpan.Default),
            SourceRange.Default);
    }
    
    public static UnionClassVariantPattern UnionClassVariantPattern(
        ITypeIdentifier unionType,
        string variantName,
        IReadOnlyList<(string, IPattern?)>? patterns = null,
        string? variableName = null,
        bool fieldsDiscarded = false)
    {
        return new UnionClassVariantPattern(
            unionType,
            Token.Identifier(variantName, SourceSpan.Default),
            patterns?.Select(x => new FieldPattern(Token.Identifier(x.Item1, SourceSpan.Default), x.Item2)).ToArray() ?? [],
            fieldsDiscarded,
            variableName is null ? null : Token.Identifier(variableName, SourceSpan.Default),
            SourceRange.Default);
    }

    public static ClassPattern ClassPattern(
        ITypeIdentifier type,
        IReadOnlyList<(string, IPattern?)>? patterns = null,
        string? variableName = null,
        bool fieldsDiscarded = false)
    {
        return new ClassPattern(
            type,
            patterns?.Select(x =>
                new FieldPattern(Token.Identifier(x.Item1, SourceSpan.Default), x.Item2)).ToArray() ?? [],
            fieldsDiscarded,
            variableName is null ? null : Token.Identifier(variableName, SourceSpan.Default),
            SourceRange.Default);
    }

    public static NamedTypeIdentifier NamedTypeIdentifier(string typeName, IReadOnlyList<ITypeIdentifier>? typeArguments = null)
    {
        return new NamedTypeIdentifier(Token.Identifier(typeName, SourceSpan.Default), typeArguments ?? [], SourceRange.Default);
    }

    public static TupleTypeIdentifier TupleTypeIdentifier(IReadOnlyList<ITypeIdentifier> members)
    {
        return new TupleTypeIdentifier(members, SourceRange.Default);
    }

    public static UnitTypeIdentifier UnitTypeIdentifier() => new(SourceRange.Default);

    public static FnTypeIdentifier FnTypeIdentifier(IReadOnlyList<FnTypeIdentifierParameter>? parameters = null,
        ITypeIdentifier? returnType = null)
    {
        return new FnTypeIdentifier(parameters ?? [], returnType, SourceRange.Default);
    }

    public static FnTypeIdentifierParameter FnTypeIdentifierParameter(ITypeIdentifier parameterType, bool isMut = false)
    {
        return new FnTypeIdentifierParameter(parameterType, isMut);
    }
    
    public static TypePattern TypePattern(ITypeIdentifier type, string? variableName = null)
    {
        return new TypePattern(
            type,
            variableName is null ? null : Token.Identifier(variableName, SourceSpan.Default),
            SourceRange.Default);
    }

    public static VariableDeclarationPattern VariableDeclarationPattern(string variableName)
    {
        return new VariableDeclarationPattern(
            Token.Identifier(variableName, SourceSpan.Default), SourceRange.Default);
    }

    public static IfExpressionExpression IfExpression(
        IExpression checkExpression,
        IExpression? body,
        IExpression? elseBody = null,
        IReadOnlyList<ElseIf>? elseIfs = null)
    {
        return new IfExpressionExpression(new IfExpression(checkExpression, body, elseIfs ?? [], elseBody), SourceRange.Default);
    }

    public static ElseIf ElseIf(IExpression checkExpression, IExpression? elseBody = null)
    {
        return new ElseIf(checkExpression, elseBody);
    }
}