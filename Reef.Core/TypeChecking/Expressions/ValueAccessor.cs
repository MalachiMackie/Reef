using System.Diagnostics;
using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{

    private ITypeReference TypeCheckValueAccessor(ValueAccessorExpression valueAccessorExpression,
        bool allowUninstantiatedVariables)
    {
        var type = valueAccessorExpression.ValueAccessor switch
        {
            { AccessType: ValueAccessType.Literal, Token: IntToken { Type: TokenType.IntLiteral } } => new UnspecifiedSizedIntType { Boxed = false },
            { AccessType: ValueAccessType.Literal, Token: StringToken { Type: TokenType.StringLiteral } } => String(),
            { AccessType: ValueAccessType.Literal, Token: StringToken { Type: TokenType.CharLiteral } } => Char(),
            { AccessType: ValueAccessType.Literal, Token.Type: TokenType.True or TokenType.False } => Boolean(),
            // todo: bring union variants into scope
            { AccessType: ValueAccessType.Variable, Token: StringToken { Type: TokenType.Identifier, StringValue: "ok" } } =>
                TypeCheckResultVariantKeyword("Ok", boxed: _typeCheckingScopes.Peek().ExpectedReturnType is InstantiatedUnion union && union.Boxed),
            { AccessType: ValueAccessType.Variable, Token: StringToken { Type: TokenType.Identifier, StringValue: "error" } } =>
                TypeCheckResultVariantKeyword("Error", boxed: _typeCheckingScopes.Peek().ExpectedReturnType is InstantiatedUnion union && union.Boxed),
            { AccessType: ValueAccessType.Variable, Token.Type: TokenType.Todo } => Never(),
            {
                AccessType: ValueAccessType.Variable,
                Token: StringToken { Type: TokenType.Identifier } variableNameToken
            } => TypeCheckVariableAccess(valueAccessorExpression, variableNameToken, allowUninstantiatedVariables),
            _ => throw new UnreachableException($"{valueAccessorExpression}: {valueAccessorExpression.ValueAccessor.AccessType}, {valueAccessorExpression.ValueAccessor.Token.Type}")
        };

        return type;

        ITypeReference TypeCheckResultVariantKeyword(string variantName, bool boxed)
        {
            var signature = GetUnionSignature(DefId.Result);
            var instantiatedUnion = InstantiateUnion(signature, [], boxed ? Token.Boxed(SourceSpan.Default) : Token.Unboxed(SourceSpan.Default), SourceRange.Default);

            var okVariant = GetUnionVariant(instantiatedUnion, variantName)
                            ?? throw new UnreachableException($"{variantName} is a built in variant of Result");

            if (okVariant is not TupleUnionVariant { BoxedCreateFunction: var createFunction, UnboxedCreateFunction: var unboxedCreateFunction })
            {
                throw new UnreachableException($"{variantName} is a tuple variant");
            }

            var tupleVariantFunction = InstantiateFunction(
                instantiatedUnion.Boxed ? createFunction : unboxedCreateFunction,
                instantiatedUnion,
                [],
                SourceRange.Default,
                GenericPlaceholders);
            valueAccessorExpression.FunctionInstantiation = tupleVariantFunction;

            return new FunctionObject(
                tupleVariantFunction.GetParameters(),
                tupleVariantFunction.GetReturnType(),
                tupleVariantFunction.MutableReturn,
                true);
        }
    }

    private ITypeReference TypeCheckVariableAccess(
        ValueAccessorExpression expression,
        StringToken variableName,
        bool allowUninstantiated)
    {
        if (TryGetScopedVariable(variableName, out var valueVariable))
        {
            if (valueVariable is FieldVariable { IsStaticField: false } or ThisVariable && InLocalFunction)
            {
                if (!TopFunctionSignature.NotNull().SelfConstraints.OfType<BoxedTypeConstraint>().Any())
                {
                    AddError(TypeCheckerError.InstanceMemberInClosureInNonBoxedConstrainedMethod(expression.SourceRange));
                }
            }

            expression.ReferencedVariable = valueVariable;

            if (!allowUninstantiated && valueVariable is LocalVariable { Instantiated: false, ContainingFunction: var containingFunction }
                // if we're accessing an outer variable, then we can assume it's been assigned
                && containingFunction == CurrentFunctionSignature)
            {
                AddError(TypeCheckerError.AccessUninitializedVariable(variableName));
            }

            if (expression.ValueAccessor.TypeArguments is not null)
            {
                AddError(TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(expression.SourceRange));
            }

            return valueVariable.Type;
        }

        var typeArguments = (expression.ValueAccessor.TypeArguments ?? [])
            .Select(x => (GetTypeReference(x), x.SourceRange))
            .ToArray();

        if (GetFunctionSignature(
            variableName.StringValue,
            [.. expression.ValueAccessor.ModulePath.Select(x => x.StringValue)],
            expression.ValueAccessor.ModulePathIsGlobal) is { } function)
        {
            var instantiatedFunction = InstantiateFunction(
                function,
                null,
                typeArguments,
                expression.SourceRange,
                GenericPlaceholders);

            expression.FunctionInstantiation = instantiatedFunction;

            return new FunctionObject(
                parameters: instantiatedFunction.GetParameters(),
                returnType: instantiatedFunction.GetReturnType(),
                instantiatedFunction.MutableReturn,
                true);
        }

        if (CurrentTypeSignature is UnionSignature union)
        {
            var unionFunction = union.Functions.FirstOrDefault(x => x.Name == variableName.StringValue);
            if (unionFunction is not null)
            {
                if (!unionFunction.IsStatic && CurrentFunctionSignature is not { IsStatic: false })
                {
                    AddError(TypeCheckerError.AccessInstanceMemberInStaticContext(variableName));
                }

                var instantiatedFunction = InstantiateFunction(
                    unionFunction,
                    ownerType: InstantiateUnion(union, [], null, SourceRange.Default),
                    typeArguments,
                    expression.SourceRange,
                    GenericPlaceholders);

                if (InLocalFunction
                    && !unionFunction.IsStatic
                    && !TopFunctionSignature.NotNull().SelfConstraints.OfType<BoxedTypeConstraint>().Any())
                {
                    AddError(TypeCheckerError.InstanceMemberInClosureInNonBoxedConstrainedMethod(expression.SourceRange));
                }

                foreach (var constraint in instantiatedFunction.Signature.SelfConstraints)
                {
                    switch (constraint)
                    {
                        case BoxedTypeConstraint:
                            {
                                if (!(CurrentFunctionSignature is { } currentSig) || !currentSig.SelfConstraints.OfType<BoxedTypeConstraint>().Any())
                                {
                                    AddError(TypeCheckerError.MethodConstrainedToBoxedInstances(variableName));
                                }
                                break;
                            }
                        case UnboxedTypeConstraint:
                            {
                                if (!(CurrentFunctionSignature is { } currentSig) || !currentSig.SelfConstraints.OfType<UnboxedTypeConstraint>().Any())
                                {
                                    AddError(TypeCheckerError.MethodConstrainedToUnboxedInstances(variableName));
                                }
                                break;
                            }
                        default:
                            throw new UnreachableException(constraint.GetType().ToString());
                    }
                }

                expression.FunctionInstantiation = instantiatedFunction;

                return new FunctionObject(
                    instantiatedFunction.GetParameters(),
                    instantiatedFunction.GetReturnType(),
                    instantiatedFunction.MutableReturn,
                    true);
            }
        }
        else if (CurrentTypeSignature is ClassSignature @class)
        {
            var classFunction = @class.Functions.FirstOrDefault(x => x.Name == variableName.StringValue);
            if (classFunction is not null)
            {
                if (!classFunction.IsStatic && CurrentFunctionSignature is not { IsStatic: false })
                {
                    AddError(TypeCheckerError.AccessInstanceMemberInStaticContext(variableName));
                }

                var instantiatedFunction = InstantiateFunction(
                    classFunction,
                    ownerType: InstantiateClass(@class, [], boxedSpecifier: null, SourceRange.Default),
                    typeArguments,
                    expression.SourceRange,
                    GenericPlaceholders);

                if (InLocalFunction
                    && !classFunction.IsStatic
                    && !TopFunctionSignature.NotNull().SelfConstraints.OfType<BoxedTypeConstraint>().Any())
                {
                    AddError(TypeCheckerError.InstanceMemberInClosureInNonBoxedConstrainedMethod(expression.SourceRange));
                }

                foreach (var constraint in instantiatedFunction.Signature.SelfConstraints)
                {
                    switch (constraint)
                    {
                        case BoxedTypeConstraint:
                            {
                                if (!(CurrentFunctionSignature is { } currentSig) || !currentSig.SelfConstraints.OfType<BoxedTypeConstraint>().Any())
                                {
                                    AddError(TypeCheckerError.MethodConstrainedToBoxedInstances(variableName));
                                }
                                break;
                            }
                        case UnboxedTypeConstraint:
                            {
                                if (!(CurrentFunctionSignature is { } currentSig) || !currentSig.SelfConstraints.OfType<UnboxedTypeConstraint>().Any())
                                {
                                    AddError(TypeCheckerError.MethodConstrainedToUnboxedInstances(variableName));
                                }
                                break;
                            }
                        default:
                            throw new UnreachableException(constraint.GetType().ToString());
                    }
                }

                expression.FunctionInstantiation = instantiatedFunction;

                return new FunctionObject(
                    instantiatedFunction.GetParameters(),
                    instantiatedFunction.GetReturnType(),
                    instantiatedFunction.MutableReturn,
                    true);
            }
        }

        AddError(TypeCheckerError.SymbolNotFound(variableName));
        return UnknownType.Instance;
    }
}
