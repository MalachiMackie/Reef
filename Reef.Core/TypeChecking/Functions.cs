using System.Diagnostics;
using System.Text;
using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private FunctionSignature TypeCheckFunctionSignature(DefId defId, LangFunction fn, ITypeSignature? ownerType)
    {
        var parameters = new OrderedDictionary<string, FunctionSignatureParameter>();

        var name = fn.Name.StringValue;
        var typeParameters = new List<GenericPlaceholder>(fn.TypeParameters.Count);
        var fnSignature = new FunctionSignature(
            defId,
            fn.Name,
            typeParameters,
            parameters,
            fn.StaticModifier is not null,
            fn.MutabilityModifier is not null,
            fn.Block.Expressions)
        {
            ReturnType = null!,
            OwnerType = ownerType
        };

        if (CurrentTypeSignature is null && fnSignature.IsMutable)
        {
            _errors.Add(TypeCheckerError.GlobalFunctionMarkedAsMutable(fn.Name));
        }

        if (CurrentFunctionSignature is { IsMutable: false } && fnSignature.IsMutable)
        {
            _errors.Add(TypeCheckerError.MutableFunctionWithinNonMutableFunction(new SourceRange(fn.Name.SourceSpan, fn.Name.SourceSpan)));
        }

        fn.Signature = fnSignature;

        if (fnSignature is { IsStatic: true, IsMutable: true })
        {
            var mutModifierSourceSpan = fn.MutabilityModifier!.Modifier.SourceSpan;
            _errors.Add(TypeCheckerError.StaticFunctionMarkedAsMutable(name, new SourceRange(mutModifierSourceSpan, mutModifierSourceSpan)));
        }

        var foundTypeParameters = new HashSet<string>();
        var genericPlaceholdersDictionary = GenericPlaceholders.ToDictionary(x => x.GenericName);
        foreach (var typeParameter in fn.TypeParameters)
        {
            if (!foundTypeParameters.Add(typeParameter.StringValue))
            {
                _errors.Add(TypeCheckerError.DuplicateTypeParameter(typeParameter));
            }

            if (genericPlaceholdersDictionary.ContainsKey(typeParameter.StringValue))
            {
                _errors.Add(TypeCheckerError.ConflictingTypeParameter(typeParameter));
            }

            if (_types.ContainsKey(typeParameter.StringValue))
            {
                _errors.Add(TypeCheckerError.TypeParameterConflictsWithType(typeParameter));
            }
            typeParameters.Add(new GenericPlaceholder
            {
                GenericName = typeParameter.StringValue,
                OwnerType = fnSignature
            });
        }

        using var _ = PushScope(genericPlaceholders: fnSignature.TypeParameters, currentFunctionSignature: fnSignature);

        fnSignature.ReturnType = fn.ReturnType is null
            ? InstantiatedClass.Unit
            : GetTypeReference(fn.ReturnType);

        foreach (var (index, parameter) in fn.Parameters.Index())
        {
            var paramName = parameter.Identifier;
            var type = parameter.Type is null ? UnknownType.Instance : GetTypeReference(parameter.Type);

            if (!parameters.TryAdd(paramName.StringValue, new FunctionSignatureParameter(fnSignature, paramName, type, parameter.MutabilityModifier is not null, (uint)index)))
            {
                _errors.Add(TypeCheckerError.DuplicateFunctionParameter(parameter.Identifier, fn.Name));
            }
        }

        fnSignature.LocalFunctions.AddRange(fn.Block.Functions.Select(x => TypeCheckFunctionSignature(
            new DefId(defId.ModuleId, defId.FullName + $"__{x.Name}"),
            x,
            ownerType: null)));

        // todo: function overloading
        return fnSignature;
    }

    private void TypeCheckFunctionBody(FunctionSignature fnSignature)
    {
        using var _ = PushScope(null, fnSignature, fnSignature.ReturnType,
            genericPlaceholders: fnSignature.TypeParameters, fnSignature.Id);
        foreach (var parameter in fnSignature.Parameters.Values)
        {
            AddScopedVariable(
                parameter.Name.StringValue,
                parameter);
        }

        if (fnSignature is { IsStatic: false, OwnerType: not null })
        {
            AddScopedVariable(
                    "this",
                    new ThisVariable(fnSignature.OwnerType switch {
                            ClassSignature c => InstantiateClass(c, boxedSpecifier: null),
                            UnionSignature u => InstantiateUnion(u, boxingSpecifier: null),
                            _ => throw new UnreachableException()
                        }));
        }

        foreach (var fn in fnSignature.LocalFunctions)
        {
            ScopedFunctions[fn.Name] = fn;
        }

        var expressionsDiverge = false;
        foreach (var expression in fnSignature.Expressions)
        {
            TypeCheckExpression(expression);
            expressionsDiverge |= expression.Diverges;
        }

        if (!expressionsDiverge && !Equals(fnSignature.ReturnType, InstantiatedClass.Unit))
        {
            // todo: figure out source range
            _errors.Add(TypeCheckerError.MismatchedTypes(
                new SourceRange(fnSignature.NameToken.SourceSpan, fnSignature.NameToken.SourceSpan),
                fnSignature.ReturnType,
                InstantiatedClass.Unit));
        }

        foreach (var localFn in fnSignature.LocalFunctions)
        {
            TypeCheckFunctionBody(localFn);
            fnSignature.AccessedOuterVariables.AddRange(
                localFn.AccessedOuterVariables.Where(accessedOuterVariable =>
                    !fnSignature.AccessedOuterVariables.Contains(accessedOuterVariable)
                    && accessedOuterVariable switch
                    {
                        // need to add the field and this as a captured variable if we're not the top level function in the type
                        FieldVariable or ThisVariable => fnSignature.OwnerType is null, 
                        FunctionSignatureParameter functionParameterVariable => functionParameterVariable.ContainingFunction !=
                                                                       fnSignature,
                        LocalVariable localVariable => localVariable.ContainingFunction != fnSignature,
                        _ => throw new ArgumentOutOfRangeException(nameof(accessedOuterVariable))
                    }));
        }
    }

    private InstantiatedFunction GetUnionTupleVariantFunction(TupleUnionVariant tupleVariant,
        InstantiatedUnion instantiatedUnion)
    {
        return InstantiateFunction(tupleVariant.CreateFunction, instantiatedUnion, typeArguments: [], SourceRange.Default, inScopeTypeParameters: []);
    }

    public interface IFunction
    {
        IReadOnlyList<FunctionParameter> Parameters { get; }
        ITypeReference ReturnType { get; }
    }

    public class FunctionObject(
        IReadOnlyList<FunctionParameter> parameters,
        ITypeReference returnType) : IFunction, ITypeReference
    {
        public IReadOnlyList<FunctionParameter> Parameters { get; } = parameters;
        public ITypeReference ReturnType { get; } = returnType;

        public override string ToString()
        {
            var sb = new StringBuilder("Fn(");
            sb.AppendJoin(", ", Parameters.Select(x => x.Mutable ? $"mut {x.Type}" : x.Type.ToString()));
            sb.Append(')');

            return sb.ToString();
        }
    }

    public class InstantiatedFunction : IFunction
    {
        public InstantiatedFunction(
            ITypeReference? ownerType,
            FunctionSignature signature,
            IReadOnlyCollection<GenericPlaceholder> inScopeTypeParameters)
        {
            OwnerType = ownerType;
            Signature = signature;

            var instantiatedTypeArguments = signature.TypeParameters.Select(x => x.Instantiate()).ToArray();

            TypeArguments = instantiatedTypeArguments;
            var parametersList = new List<FunctionParameter>();
            Parameters = parametersList;

            var ownerTypeArguments = ownerType switch
            {
                null => [],
                InstantiatedClass ownerClass => ownerClass.TypeArguments,
                InstantiatedUnion ownerUnion => ownerUnion.TypeArguments,
                _ => throw new InvalidOperationException($"Unexpected owner type {ownerType.GetType()}")
            };

            for (var i = 0; i < signature.Parameters.Count; i++)
            {
                var parameter = signature.Parameters.GetAt(i);
                var functionParameter = new FunctionParameter(parameter.Value.Type switch
                {
                    GenericPlaceholder placeholder => (ITypeReference?)instantiatedTypeArguments.FirstOrDefault(x => x.GenericName == placeholder.GenericName)
                        ?? inScopeTypeParameters.FirstOrDefault(x => x.GenericName == placeholder.GenericName)
                        ?? (ITypeReference)ownerTypeArguments.First(x => x.GenericName == placeholder.GenericName),
                    _ => parameter.Value.Type
                }, parameter.Value.Mutable);
                parametersList.Add(functionParameter);
            }
            ReturnType = signature.ReturnType switch
            {
                GenericPlaceholder placeholder => (ITypeReference?)instantiatedTypeArguments.FirstOrDefault(x => x.GenericName == placeholder.GenericName)
                        ?? inScopeTypeParameters.FirstOrDefault(x => x.GenericName == placeholder.GenericName)
                        ?? (ITypeReference)ownerTypeArguments.First(x => x.GenericName == placeholder.GenericName),
                _ => signature.ReturnType
            };
        }

        private FunctionSignature Signature { get; }

        public bool IsStatic => Signature.IsStatic;
        public bool IsMutable => Signature.IsMutable;
        public IReadOnlyList<IVariable> AccessedOuterVariables => Signature.AccessedOuterVariables;
        public ITypeReference? OwnerType { get; }
        public ITypeSignature? OwnerSignature => Signature.OwnerType;
        public IReadOnlyList<GenericTypeReference> TypeArguments { get; }
        public ITypeReference ReturnType { get; }
        public DefId FunctionId => Signature.Id;
        public IReadOnlyList<FunctionParameter> Parameters { get; }
        public string Name => Signature.Name;
        public DefId? ClosureTypeId => Signature.ClosureTypeId;
        public List<(DefId fieldTypeId, List<(IVariable fieldVariable, uint fieldIndex)> referencedVariables)> ClosureTypeFields =>
            Signature.ClosureTypeFields;
    }

    private InstantiatedFunction InstantiateFunction(FunctionSignature signature,
        ITypeReference? ownerType,
        IReadOnlyList<(ITypeReference, SourceRange)> typeArguments,
        SourceRange typeArgumentsSourceRange,
        IReadOnlyCollection<GenericPlaceholder> inScopeTypeParameters)
    {
        var instantiatedFunction = new InstantiatedFunction(ownerType, signature, inScopeTypeParameters);
        if (typeArguments.Count > 0)
        {
            if (typeArguments.Count != instantiatedFunction.TypeArguments.Count)
            {
                _errors.Add(TypeCheckerError.IncorrectNumberOfTypeArguments(
                    typeArgumentsSourceRange,
                    typeArguments.Count,
                    instantiatedFunction.TypeArguments.Count));
            }

            for (var i = 0; i < Math.Min(instantiatedFunction.TypeArguments.Count, typeArguments.Count); i++)
            {
                var (typeArgument, sourceRange) = typeArguments[i];
                var typeParameter = instantiatedFunction.TypeArguments[i];
                ExpectType(typeArgument, typeParameter, sourceRange);
            }
        }

        return instantiatedFunction;
    }

    public record FunctionSignature(
        DefId Id,
        StringToken NameToken,
        IReadOnlyList<GenericPlaceholder> TypeParameters,
        OrderedDictionary<string, FunctionSignatureParameter> Parameters,
        bool IsStatic,
        bool IsMutable,
        IReadOnlyList<IExpression> Expressions) : ITypeSignature
    {
        public DefId? LocalsTypeId { get; set; }
        public DefId? ClosureTypeId { get; set; }
        public List<(DefId fieldTypeId, List<(IVariable fieldVariable, uint fieldIndex)> referencedVariables)> ClosureTypeFields { get; set; } = [];

        // mutable due to setting up signatures and generic stuff
        public required ITypeReference ReturnType { get; set; }
        public required ITypeSignature? OwnerType { get; init; }
        public string Name => NameToken.StringValue;
        public List<FunctionSignature> LocalFunctions { get; init; } = [];
        public List<LocalVariable> LocalVariables { get; init; } = [];
        public List<IVariable> AccessedOuterVariables { get; } = [];
        
        public static FunctionSignature Printf { get; }

        static FunctionSignature()
        {
            var parameters = new OrderedDictionary<string, FunctionSignatureParameter>();
            var signature = new FunctionSignature(
                DefId.Printf,
                Token.Identifier("printf", SourceSpan.Default),
                [],
                parameters,
                IsStatic: true,
                IsMutable: false,
                Expressions: [])
            {
                OwnerType = null,
                ReturnType = InstantiatedClass.Unit
            };

            parameters["format"] = new FunctionSignatureParameter(
                signature,
                Token.Identifier("format", SourceSpan.Default),
                InstantiatedClass.String,
                false,
                0);

            Printf = signature;
        }
    }

    public record FunctionParameter(ITypeReference Type, bool Mutable);

    public record FunctionSignatureParameter(
        FunctionSignature ContainingFunction,
        StringToken Name,
        ITypeReference Type,
        bool Mutable,
        uint ParameterIndex) : IVariable
    {
        public bool ReferencedInClosure { get; set; }
    }

}
