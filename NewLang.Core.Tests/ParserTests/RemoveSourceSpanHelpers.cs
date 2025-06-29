using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NewLang.Core.Tests.ParserTests;

public static class RemoveSourceSpanHelpers
{
    public static IReadOnlyList<ParserError> RemoveSourceSpan(IReadOnlyList<ParserError> errors)
    {
        return [..errors.Select(RemoveSourceSpan)];
    }

    private static ParserError RemoveSourceSpan(ParserError error)
    {
        return error with { ReceivedToken = error.ReceivedToken is null ? null : RemoveSourceSpan(error.ReceivedToken) };
    }

    private static LangFunction RemoveSourceSpan(LangFunction function)
    {
        return new LangFunction(
            RemoveSourceSpan(function.AccessModifier),
            RemoveSourceSpan(function.StaticModifier),
            RemoveSourceSpan(function.Name),
            [..function.TypeArguments.Select(RemoveSourceSpan)!],
            [..function.Parameters.Select(RemoveSourceSpan)],
            RemoveSourceSpan(function.ReturnType),
            RemoveSourceSpan(function.Block)
        );
    }

    private static Block RemoveSourceSpan(Block block)
    {
        return new Block(
            [..block.Expressions.Select(RemoveSourceSpan)!],
            [..block.Functions.Select(RemoveSourceSpan)]);
    }

    private static AccessModifier? RemoveSourceSpan(AccessModifier? accessModifier)
    {
        return accessModifier is null
            ? null
            : new AccessModifier(RemoveSourceSpan(accessModifier.Token));
    }

    [return: NotNullIfNotNull(nameof(typeIdentifier))]
    private static TypeIdentifier? RemoveSourceSpan(TypeIdentifier? typeIdentifier)
    {
        if (typeIdentifier is null)
        {
            return null;
        }

        return new TypeIdentifier(RemoveSourceSpan(typeIdentifier.Identifier),
            [..typeIdentifier.TypeArguments.Select(RemoveSourceSpan)!], SourceRange.Default);
    }

    private static FunctionParameter RemoveSourceSpan(FunctionParameter parameter)
    {
        return new FunctionParameter(
            RemoveSourceSpan(parameter.Type),
            RemoveSourceSpan(parameter.MutabilityModifier),
            RemoveSourceSpan(parameter.Identifier));
    }

    [return: NotNullIfNotNull(nameof(expression))]
    public static IExpression? RemoveSourceSpan(IExpression? expression)
    {
        return expression switch
        {
            null => null,
            ValueAccessorExpression valueAccessorExpression => new ValueAccessorExpression(
                RemoveSourceSpan(valueAccessorExpression.ValueAccessor)),
            UnaryOperatorExpression unaryOperatorExpression => new UnaryOperatorExpression(
                RemoveSourceSpan(unaryOperatorExpression.UnaryOperator)),
            BinaryOperatorExpression binaryOperatorExpression => new BinaryOperatorExpression(
                RemoveSourceSpan(binaryOperatorExpression.BinaryOperator)),
            VariableDeclarationExpression variableDeclarationExpression => new VariableDeclarationExpression(
                RemoveSourceSpan(variableDeclarationExpression.VariableDeclaration), SourceRange.Default),
            IfExpressionExpression ifExpressionExpression => new IfExpressionExpression(
                RemoveSourceSpan(ifExpressionExpression.IfExpression), SourceRange.Default),
            BlockExpression blockExpression => new BlockExpression(RemoveSourceSpan(blockExpression.Block),
                SourceRange.Default),
            MethodCallExpression methodCallExpression => new MethodCallExpression(
                RemoveSourceSpan(methodCallExpression.MethodCall), SourceRange.Default),
            MethodReturnExpression methodReturnExpression => new MethodReturnExpression(
                RemoveSourceSpan(methodReturnExpression.MethodReturn), SourceRange.Default),
            ObjectInitializerExpression objectInitializerExpression => new ObjectInitializerExpression(
                RemoveSourceSpan(objectInitializerExpression.ObjectInitializer), SourceRange.Default),
            MemberAccessExpression memberAccessExpression => new MemberAccessExpression(
                RemoveSourceSpan(memberAccessExpression.MemberAccess)),
            StaticMemberAccessExpression staticMemberAccessExpression => new StaticMemberAccessExpression(
                RemoveSourceSpan(staticMemberAccessExpression.StaticMemberAccess)),
            GenericInstantiationExpression genericInstantiationExpression => new GenericInstantiationExpression(
                RemoveSourceSpan(genericInstantiationExpression.GenericInstantiation), SourceRange.Default),
            UnionStructVariantInitializerExpression unionStructVariantInitializerExpression => new
                UnionStructVariantInitializerExpression(
                    RemoveSourceSpan(unionStructVariantInitializerExpression.UnionInitializer), SourceRange.Default),
            MatchesExpression matchesExpression => RemoveSourceSpan(matchesExpression),
            TupleExpression tupleExpression => RemoveSourceSpan(tupleExpression),
            MatchExpression matchExpression => RemoveSourceSpan(matchExpression),
            _ => throw new NotImplementedException(expression.GetType().ToString())
        };
    }

    private static MatchExpression RemoveSourceSpan(MatchExpression matchExpression)
    {
        return new MatchExpression(
            RemoveSourceSpan(matchExpression.Value),
            [..matchExpression.Arms.Select(RemoveSourceSpan)], SourceRange.Default);
    }

    private static MatchArm RemoveSourceSpan(MatchArm matchArm)
    {
        return new MatchArm(
            RemoveSourceSpan(matchArm.Pattern),
            RemoveSourceSpan(matchArm.Expression));
    }

    private static TupleExpression RemoveSourceSpan(TupleExpression tupleExpression)
    {
        return new TupleExpression([..tupleExpression.Values.Select(RemoveSourceSpan)!], SourceRange.Default);
    }

    private static MatchesExpression RemoveSourceSpan(
        MatchesExpression matchesExpression)
    {
        return new MatchesExpression(
            RemoveSourceSpan(matchesExpression.ValueExpression),
            RemoveSourceSpan(matchesExpression.Pattern),
            SourceRange.Default);
    }

    [return: NotNullIfNotNull(nameof(pattern))]
    private static IPattern? RemoveSourceSpan(IPattern? pattern)
    {
        return pattern switch
        {
            DiscardPattern discardPattern => discardPattern,
            VariableDeclarationPattern variablePattern => new VariableDeclarationPattern(
                RemoveSourceSpan(variablePattern.VariableName), SourceRange.Default),
            UnionVariantPattern unionVariantPattern => new UnionVariantPattern(
                RemoveSourceSpan(unionVariantPattern.Type),
                RemoveSourceSpan(unionVariantPattern.VariantName),
                unionVariantPattern.VariableName is null ? null : RemoveSourceSpan(unionVariantPattern.VariableName)
                , SourceRange.Default),
            UnionTupleVariantPattern unionTupleVariantPattern => new UnionTupleVariantPattern(
                RemoveSourceSpan(unionTupleVariantPattern.Type),
                RemoveSourceSpan(unionTupleVariantPattern.VariantName),
                [..unionTupleVariantPattern.TupleParamPatterns.Select(RemoveSourceSpan)!],
                unionTupleVariantPattern.VariableName is null
                    ? null
                    : RemoveSourceSpan(unionTupleVariantPattern.VariableName), SourceRange.Default),
            ClassPattern classPattern => new ClassPattern(RemoveSourceSpan(classPattern.Type),
                [
                    ..classPattern.FieldPatterns.Select(x =>
                        new FieldPattern(RemoveSourceSpan(x.FieldName),
                            RemoveSourceSpan(x.Pattern)))
                ],
                classPattern.RemainingFieldsDiscarded,
                classPattern.VariableName is null ? null : RemoveSourceSpan(classPattern.VariableName),
                SourceRange.Default),
            UnionStructVariantPattern unionStructVariantPattern => new UnionStructVariantPattern(
                RemoveSourceSpan(unionStructVariantPattern.Type),
                RemoveSourceSpan(unionStructVariantPattern.VariantName),
                [
                    ..unionStructVariantPattern.FieldPatterns.Select(x =>
                        new FieldPattern(RemoveSourceSpan(x.FieldName),
                            RemoveSourceSpan(x.Pattern)))
                ],
                unionStructVariantPattern.RemainingFieldsDiscarded,
                unionStructVariantPattern.VariableName is null
                    ? null
                    : RemoveSourceSpan(unionStructVariantPattern.VariableName), SourceRange.Default),
            TypePattern typePattern => new TypePattern(
                RemoveSourceSpan(typePattern.Type),
                RemoveSourceSpan(typePattern.VariableName),
                SourceRange.Default),
            null => null,
            _ => throw new NotImplementedException($"{pattern}")
        };
    }


    private static UnionStructVariantInitializer RemoveSourceSpan(
        UnionStructVariantInitializer unionStructVariantInitializer)
    {
        return new UnionStructVariantInitializer(
            RemoveSourceSpan(unionStructVariantInitializer.UnionType),
            RemoveSourceSpan(unionStructVariantInitializer.VariantIdentifier),
            [..unionStructVariantInitializer.FieldInitializers.Select(RemoveSourceSpan)]);
    }

    private static GenericInstantiation RemoveSourceSpan(GenericInstantiation genericInstantiation)
    {
        return new GenericInstantiation(
            RemoveSourceSpan(genericInstantiation.Value),
            [..genericInstantiation.GenericArguments.Select(RemoveSourceSpan)!]);
    }

    private static MemberAccess RemoveSourceSpan(MemberAccess memberAccess)
    {
        return new MemberAccess(RemoveSourceSpan(memberAccess.Owner),
            RemoveSourceSpan(memberAccess.MemberName));
    }

    private static StaticMemberAccess RemoveSourceSpan(StaticMemberAccess staticMemberAccess)
    {
        return new StaticMemberAccess(RemoveSourceSpan(staticMemberAccess.Type),
            RemoveSourceSpan(staticMemberAccess.MemberName));
    }

    private static ObjectInitializer RemoveSourceSpan(ObjectInitializer objectInitializer)
    {
        return new ObjectInitializer(
            RemoveSourceSpan(objectInitializer.Type),
            [..objectInitializer.FieldInitializers.Select(RemoveSourceSpan)]);
    }

    private static FieldInitializer RemoveSourceSpan(FieldInitializer fieldInitializer)
    {
        return new FieldInitializer(
            RemoveSourceSpan(fieldInitializer.FieldName),
            RemoveSourceSpan(fieldInitializer.Value));
    }

    private static MethodCall RemoveSourceSpan(MethodCall methodCall)
    {
        return new MethodCall(
            RemoveSourceSpan(methodCall.Method),
            [..methodCall.ParameterList.Select(RemoveSourceSpan)!]);
    }

    private static IfExpression RemoveSourceSpan(IfExpression ifExpression)
    {
        return new IfExpression(
            RemoveSourceSpan(ifExpression.CheckExpression),
            RemoveSourceSpan(ifExpression.Body),
            [..ifExpression.ElseIfs.Select(RemoveSourceSpan)],
            RemoveSourceSpan(ifExpression.ElseBody));
    }

    private static ElseIf RemoveSourceSpan(ElseIf elseIf)
    {
        return new ElseIf(RemoveSourceSpan(elseIf.CheckExpression), RemoveSourceSpan(elseIf.Body));
    }

    private static VariableDeclaration RemoveSourceSpan(VariableDeclaration variableDeclaration)
    {
        return new VariableDeclaration(
            RemoveSourceSpan(variableDeclaration.VariableNameToken),
            RemoveSourceSpan(variableDeclaration.MutabilityModifier),
            RemoveSourceSpan(variableDeclaration.Type),
            RemoveSourceSpan(variableDeclaration.Value));
    }

    private static MutabilityModifier? RemoveSourceSpan(MutabilityModifier? mutabilityModifier)
    {
        return mutabilityModifier is null
            ? null
            : new MutabilityModifier(RemoveSourceSpan(mutabilityModifier.Modifier));
    }

    private static BinaryOperator RemoveSourceSpan(BinaryOperator binaryOperator)
    {
        return binaryOperator with
        {
            Left = RemoveSourceSpan(binaryOperator.Left),
            Right = RemoveSourceSpan(binaryOperator.Right),
            OperatorToken = RemoveSourceSpan(binaryOperator.OperatorToken)
        };
    }

    private static UnaryOperator RemoveSourceSpan(UnaryOperator unaryOperator)
    {
        return unaryOperator with
        {
            OperatorToken = RemoveSourceSpan(unaryOperator.OperatorToken),
            Operand = RemoveSourceSpan(unaryOperator.Operand)
        };
    }

    private static ValueAccessor RemoveSourceSpan(ValueAccessor valueAccessor)
    {
        return valueAccessor with { Token = RemoveSourceSpan(valueAccessor.Token) };
    }

    private static Token RemoveSourceSpan(Token token)
    {
        return token with { SourceSpan = SourceSpan.Default };
    }

    [return: NotNullIfNotNull(nameof(token))]
    private static StringToken? RemoveSourceSpan(StringToken? token)
    {
        return token is null ? null : token with { SourceSpan = SourceSpan.Default };
    }

    public static LangProgram RemoveSourceSpan(LangProgram program)
    {
        return new LangProgram(
            [..program.Expressions.Select(RemoveSourceSpan)!],
            [..program.Functions.Select(RemoveSourceSpan)],
            [..program.Classes.Select(RemoveSourceSpan)],
            [..program.Unions.Select(RemoveSourceSpan)]);
    }

    private static ProgramUnion RemoveSourceSpan(ProgramUnion union)
    {
        return new ProgramUnion(
            RemoveSourceSpan(union.AccessModifier),
            RemoveSourceSpan(union.Name),
            [..union.GenericArguments.Select(RemoveSourceSpan)!],
            [..union.Functions.Select(RemoveSourceSpan)],
            [..union.Variants.Select(RemoveSourceSpan)]
        );
    }

    private static IProgramUnionVariant RemoveSourceSpan(IProgramUnionVariant variant)
    {
        return variant switch
        {
            UnitStructUnionVariant unitStructVariant => new UnitStructUnionVariant(
                RemoveSourceSpan(unitStructVariant.Name)),
            TupleUnionVariant tupleUnionVariant => new TupleUnionVariant(
                RemoveSourceSpan(tupleUnionVariant.Name),
                [..tupleUnionVariant.TupleMembers.Select(RemoveSourceSpan)!]),
            StructUnionVariant structUnionVariant => new StructUnionVariant
            {
                Name = RemoveSourceSpan(structUnionVariant.Name),
                Fields = [..structUnionVariant.Fields.Select(RemoveSourceSpan)]
            },
            _ => throw new UnreachableException()
        };
    }

    private static ProgramClass RemoveSourceSpan(ProgramClass @class)
    {
        return new ProgramClass(
            RemoveSourceSpan(@class.AccessModifier),
            RemoveSourceSpan(@class.Name),
            [..@class.TypeArguments.Select(RemoveSourceSpan)!],
            [..@class.Functions.Select(RemoveSourceSpan)],
            [..@class.Fields.Select(RemoveSourceSpan)]);
    }

    private static ClassField RemoveSourceSpan(ClassField field)
    {
        return new ClassField(
            RemoveSourceSpan(field.AccessModifier),
            RemoveSourceSpan(field.StaticModifier),
            RemoveSourceSpan(field.MutabilityModifier),
            RemoveSourceSpan(field.Name),
            RemoveSourceSpan(field.Type),
            RemoveSourceSpan(field.InitializerValue));
    }

    private static StaticModifier? RemoveSourceSpan(StaticModifier? staticModifier)
    {
        return staticModifier is null
            ? null
            : new StaticModifier(RemoveSourceSpan(staticModifier.Token));
    }

    private static MethodReturn RemoveSourceSpan(MethodReturn methodReturn)
    {
        return new MethodReturn(RemoveSourceSpan(methodReturn.Expression));
    }
}