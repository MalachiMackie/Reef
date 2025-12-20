using System.Diagnostics;
using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{

    private TypeChecking.TypeChecker.ITypeReference TypeCheckValueAccessor(ValueAccessorExpression valueAccessorExpression,
        bool allowUninstantiatedVariables)
    {
        var type = valueAccessorExpression.ValueAccessor switch
        {
            { AccessType: ValueAccessType.Literal, Token: IntToken { Type: TokenType.IntLiteral } } => new TypeChecking.TypeChecker.UnspecifiedSizedIntType(),
            { AccessType: ValueAccessType.Literal, Token: StringToken { Type: TokenType.StringLiteral } } =>
                TypeChecking.TypeChecker.InstantiatedClass.String,
            { AccessType: ValueAccessType.Literal, Token.Type: TokenType.True or TokenType.False } => TypeChecking.TypeChecker.InstantiatedClass
                .Boolean,
            // todo: bring union variants into scope
            { AccessType: ValueAccessType.Variable, Token: StringToken { Type: TokenType.Identifier, StringValue: "ok" } } => TypeCheckResultVariantKeyword("Ok"),
            { AccessType: ValueAccessType.Variable, Token: StringToken { Type: TokenType.Identifier, StringValue: "error" } } =>
                TypeCheckResultVariantKeyword("Error"),
            { AccessType: ValueAccessType.Variable, Token.Type: TokenType.Todo } => TypeChecking.TypeChecker.InstantiatedClass.Never,
            {
                AccessType: ValueAccessType.Variable,
                Token: StringToken { Type: TokenType.Identifier } variableNameToken
            } => TypeCheckVariableAccess(valueAccessorExpression, variableNameToken, allowUninstantiatedVariables),
            _ => throw new UnreachableException($"{valueAccessorExpression}")
        };

        return type;

        TypeChecking.TypeChecker.ITypeReference TypeCheckResultVariantKeyword(string variantName)
        {
            var instantiatedUnion = InstantiateResult(valueAccessorExpression.SourceRange);

            var okVariant = Enumerable.FirstOrDefault<TypeChecking.TypeChecker.IUnionVariant>(instantiatedUnion.Variants, x => x.Name == variantName)
                            ?? throw new UnreachableException($"{variantName} is a built in variant of Result");

            if (okVariant is not TypeChecking.TypeChecker.TupleUnionVariant {CreateFunction: var tupleVariantFunctionSignature} tupleVariant)
            {
                throw new UnreachableException($"{variantName} is a tuple variant");
            }

            var tupleVariantFunction = InstantiateFunction(tupleVariantFunctionSignature, instantiatedUnion, [], SourceRange.Default, GenericPlaceholders);
            valueAccessorExpression.FunctionInstantiation = tupleVariantFunction;

            return new TypeChecking.TypeChecker.FunctionObject(
                tupleVariantFunction.Parameters,
                tupleVariantFunction.ReturnType);
        }
    }

    private TypeChecking.TypeChecker.ITypeReference TypeCheckVariableAccess(
        ValueAccessorExpression expression,
        StringToken variableName,
        bool allowUninstantiated)
    {
        var typeArguments = (expression.ValueAccessor.TypeArguments ?? [])
            .Select<ITypeIdentifier, (TypeChecking.TypeChecker.ITypeReference, SourceRange SourceRange)>(x => (GetTypeReference(x), x.SourceRange))
            .ToArray();

        if (ScopedFunctions.TryGetValue(variableName.StringValue, out var function))
        {
            var instantiatedFunction = InstantiateFunction(
                function,
                null,
                typeArguments,
                expression.SourceRange,
                GenericPlaceholders);

            expression.FunctionInstantiation = instantiatedFunction;

            return new TypeChecking.TypeChecker.FunctionObject(
                parameters: instantiatedFunction.Parameters,
                returnType: instantiatedFunction.ReturnType);
        }

        if (CurrentTypeSignature is TypeChecking.TypeChecker.UnionSignature union)
        {
            var unionFunction = union.Functions.FirstOrDefault(x => x.Name == variableName.StringValue);
            if (unionFunction is not null)
            {
                if (!unionFunction.IsStatic && CurrentFunctionSignature is not { IsStatic: false })
                {
                    _errors.Add(TypeCheckerError.AccessInstanceMemberInStaticContext(variableName));
                }

                var instantiatedFunction = InstantiateFunction(
                    unionFunction,
                    ownerType: InstantiateUnion(union, [], SourceRange.Default),
                    typeArguments,
                    expression.SourceRange,
                    GenericPlaceholders);

                expression.FunctionInstantiation = instantiatedFunction;

                return new TypeChecking.TypeChecker.FunctionObject(
                    instantiatedFunction.Parameters,
                    instantiatedFunction.ReturnType);
            }
        }
        else if (CurrentTypeSignature is TypeChecking.TypeChecker.ClassSignature @class)
        {
            var classFunction = @class.Functions.FirstOrDefault(x => x.Name == variableName.StringValue);
            if (classFunction is not null)
            {
                if (!classFunction.IsStatic && CurrentFunctionSignature is not { IsStatic: false })
                {
                    _errors.Add(TypeCheckerError.AccessInstanceMemberInStaticContext(variableName));
                }

                var instantiatedFunction = InstantiateFunction(
                    classFunction,
                    ownerType: InstantiateClass(@class, [], SourceRange.Default),
                    typeArguments,
                    expression.SourceRange,
                    GenericPlaceholders);

                expression.FunctionInstantiation = instantiatedFunction;

                return new TypeChecking.TypeChecker.FunctionObject(
                    instantiatedFunction.Parameters,
                    instantiatedFunction.ReturnType);
            }
        }

        if (expression.ValueAccessor.TypeArguments is not null)
        {
            _errors.Add(TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(expression.SourceRange));
        }

        if (!TryGetScopedVariable(variableName, out var valueVariable))
        {
            _errors.Add(TypeCheckerError.SymbolNotFound(variableName));
            return TypeChecking.TypeChecker.UnknownType.Instance;
        }

        expression.ReferencedVariable = valueVariable;

        if (!allowUninstantiated && valueVariable is TypeChecking.TypeChecker.LocalVariable { Instantiated: false, ContainingFunction: var containingFunction }
            // if we're accessing an outer variable, then we can assume it's been assigned                     
            && containingFunction == CurrentFunctionSignature)
        {
            _errors.Add(TypeCheckerError.AccessUninitializedVariable(variableName));
        }

        return valueVariable.Type;
    }
}

