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
            { AccessType: ValueAccessType.Literal, Token: IntToken { Type: TokenType.IntLiteral } } => InstantiatedClass
                .Int,
            { AccessType: ValueAccessType.Literal, Token: StringToken { Type: TokenType.StringLiteral } } =>
                InstantiatedClass.String,
            { AccessType: ValueAccessType.Literal, Token.Type: TokenType.True or TokenType.False } => InstantiatedClass
                .Boolean,
            // todo: bring union variants into scope
            { AccessType: ValueAccessType.Variable, Token: StringToken { Type: TokenType.Identifier, StringValue: "ok" } } => TypeCheckResultVariantKeyword("Ok"),
            { AccessType: ValueAccessType.Variable, Token: StringToken { Type: TokenType.Identifier, StringValue: "error" } } =>
                TypeCheckResultVariantKeyword("Error"),
            { AccessType: ValueAccessType.Variable, Token.Type: TokenType.Todo } => InstantiatedClass.Never,
            {
                AccessType: ValueAccessType.Variable,
                Token: StringToken { Type: TokenType.Identifier } variableNameToken
            } => TypeCheckVariableAccess(valueAccessorExpression, variableNameToken, allowUninstantiatedVariables),
            _ => throw new UnreachableException($"{valueAccessorExpression}")
        };

        return type;

        ITypeReference TypeCheckResultVariantKeyword(string variantName)
        {
            var instantiatedUnion = InstantiateResult(valueAccessorExpression.SourceRange);

            var okVariant = instantiatedUnion.Variants.FirstOrDefault(x => x.Name == variantName)
                            ?? throw new UnreachableException($"{variantName} is a built in variant of Result");

            if (okVariant is not TupleUnionVariant tupleVariant)
            {
                throw new UnreachableException($"{variantName} is a tuple variant");
            }

            var tupleVariantFunction = GetUnionTupleVariantFunction(tupleVariant, instantiatedUnion);

            valueAccessorExpression.FunctionInstantiation = tupleVariantFunction;

            return new FunctionObject(
                tupleVariantFunction.Parameters,
                tupleVariantFunction.ReturnType);
        }
    }

    private ITypeReference TypeCheckVariableAccess(
        ValueAccessorExpression expression,
        StringToken variableName,
        bool allowUninstantiated)
    {
        var typeArguments = (expression.ValueAccessor.TypeArguments ?? [])
            .Select(x => (GetTypeReference(x), x.SourceRange))
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

            return new FunctionObject(
                parameters: instantiatedFunction.Parameters,
                returnType: instantiatedFunction.ReturnType);
        }

        if (CurrentTypeSignature is UnionSignature union)
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

                return new FunctionObject(
                    instantiatedFunction.Parameters,
                    instantiatedFunction.ReturnType);
            }
        }
        else if (CurrentTypeSignature is ClassSignature @class)
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

                return new FunctionObject(
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
            return UnknownType.Instance;
        }

        expression.ReferencedVariable = valueVariable;

        if (!allowUninstantiated && valueVariable is LocalVariable { Instantiated: false, ContainingFunction: var containingFunction }
            // if we're accessing an outer variable, then we can assume it's been assigned                     
            && containingFunction == CurrentFunctionSignature)
        {
            _errors.Add(TypeCheckerError.AccessUninitializedVariable(variableName));
        }

        return valueVariable.Type;
    }
}

