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
            fn.Block.Expressions,
            false,
            fn.ReturnMutabilityModifier?.Type == TokenType.Mut)
        {
            ReturnType = null!,
            OwnerType = ownerType
        };

        if (CurrentTypeSignature is null && fnSignature.IsMutable)
        {
            AddError(TypeCheckerError.GlobalFunctionMarkedAsMutable(fn.Name));
        }

        if (CurrentFunctionSignature is { IsMutable: false } && fnSignature.IsMutable)
        {
            AddError(TypeCheckerError.MutableFunctionWithinNonMutableFunction(new SourceRange(fn.Name.SourceSpan, fn.Name.SourceSpan)));
        }

        fn.Signature = fnSignature;

        if (fnSignature is { IsStatic: true, IsMutable: true })
        {
            var mutModifierSourceSpan = fn.MutabilityModifier!.Modifier.SourceSpan;
            AddError(TypeCheckerError.StaticFunctionMarkedAsMutable(name, new SourceRange(mutModifierSourceSpan, mutModifierSourceSpan)));
        }

        var foundTypeParameters = new HashSet<string>();
        var genericPlaceholdersDictionary = GenericPlaceholders.ToDictionary(x => x.GenericName);
        foreach (var typeParameter in fn.TypeParameters)
        {
            if (!foundTypeParameters.Add(typeParameter.StringValue))
            {
                AddError(TypeCheckerError.DuplicateTypeParameter(typeParameter));
            }

            if (genericPlaceholdersDictionary.ContainsKey(typeParameter.StringValue))
            {
                AddError(TypeCheckerError.ConflictingTypeParameter(typeParameter));
            }

            if (_types.ContainsKey(typeParameter.StringValue))
            {
                AddError(TypeCheckerError.TypeParameterConflictsWithType(typeParameter));
            }
            typeParameters.Add(new GenericPlaceholder
            {
                GenericName = typeParameter.StringValue,
                OwnerType = fnSignature,
                Constraints = []
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
                AddError(TypeCheckerError.DuplicateFunctionParameter(parameter.Identifier, fn.Name));
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
        if (fnSignature.Extern)
        {
            return;
        }
        
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
            AddError(TypeCheckerError.MismatchedTypes(
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
        bool MutableReturn { get; }
    }

    public class FunctionObject(
        IReadOnlyList<FunctionParameter> parameters,
        ITypeReference returnType,
        bool isMutableReturn) : IFunction, ITypeReference
    {
        public IReadOnlyList<FunctionParameter> Parameters { get; } = parameters;
        public ITypeReference ReturnType { get; } = returnType;
        public bool MutableReturn { get; } = isMutableReturn; 

        public override string ToString()
        {
            var sb = new StringBuilder("Fn(");
            sb.AppendJoin(", ", Parameters.Select(x => x.Mutable ? $"mut {x.Type}" : x.Type.ToString()));
            sb.Append(')');

            return sb.ToString();
        }
    }

    public interface IInstantiatedGeneric
    {
        IReadOnlyList<GenericTypeReference> TypeArguments { get; } 
    }

    public class InstantiatedFunction : IFunction, IInstantiatedGeneric
    {
        public InstantiatedFunction(
            ITypeReference? ownerType,
            FunctionSignature signature,
            IReadOnlyCollection<GenericPlaceholder> inScopeTypeParameters)
        {
            OwnerType = ownerType;
            Signature = signature;

            TypeArguments = signature.TypeParameters.Select(x => x.Instantiate(instantiatedFrom: this)).ToArray();
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
                    GenericPlaceholder placeholder => TypeArguments.FirstOrDefault(x => x.GenericName == placeholder.GenericName)
                        ?? inScopeTypeParameters.FirstOrDefault(x => x.GenericName == placeholder.GenericName)
                        ?? (ITypeReference)ownerTypeArguments.First(x => x.GenericName == placeholder.GenericName),
                    _ => parameter.Value.Type
                }, parameter.Value.Mutable);
                parametersList.Add(functionParameter);
            }
            ReturnType = signature.ReturnType switch
            {
                GenericPlaceholder placeholder => TypeArguments.FirstOrDefault(x => x.GenericName == placeholder.GenericName)
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
        public bool MutableReturn => Signature.IsMutableReturn;
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
                AddError(TypeCheckerError.IncorrectNumberOfTypeArguments(
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
        IReadOnlyList<IExpression> Expressions,
        bool Extern,
        bool IsMutableReturn) : ITypeSignature
    {
        public DefId? LocalsTypeId { get; set; }
        public DefId? ClosureTypeId { get; set; }
        public List<(DefId fieldTypeId, List<(IVariable fieldVariable, uint fieldIndex)> referencedVariables)> ClosureTypeFields { get; } = [];

        // mutable due to setting up signatures and generic stuff
        public required ITypeReference ReturnType { get; set; }
        public required ITypeSignature? OwnerType { get; init; }
        public string Name => NameToken.StringValue;
        public List<FunctionSignature> LocalFunctions { get; init; } = [];
        public List<LocalVariable> LocalVariables { get; init; } = [];
        public List<IVariable> AccessedOuterVariables { get; } = [];

        public static FunctionSignature PrintString { get; }
        public static FunctionSignature PrintI8 { get; } = CreatePrintInt(InstantiatedClass.Int8, DefId.PrintI8);
        public static FunctionSignature PrintI16 { get; } = CreatePrintInt(InstantiatedClass.Int16, DefId.PrintI16);
        public static FunctionSignature PrintI32 { get; } = CreatePrintInt(InstantiatedClass.Int32, DefId.PrintI32);
        public static FunctionSignature PrintI64 { get; } = CreatePrintInt(InstantiatedClass.Int64, DefId.PrintI64);
        public static FunctionSignature PrintU8 { get; } = CreatePrintInt(InstantiatedClass.UInt8, DefId.PrintU8);
        public static FunctionSignature PrintU16 { get; } = CreatePrintInt(InstantiatedClass.UInt16, DefId.PrintU16);
        public static FunctionSignature PrintU32 { get; } = CreatePrintInt(InstantiatedClass.UInt32, DefId.PrintU32);
        public static FunctionSignature PrintU64 { get; } = CreatePrintInt(InstantiatedClass.UInt64, DefId.PrintU64);
        
        public static FunctionSignature Allocate { get; }
        public static FunctionSignature Box { get; }
        public static FunctionSignature Unbox { get; }

        private static FunctionSignature CreatePrintInt(InstantiatedClass type, DefId id)
        {
            var parameters = new OrderedDictionary<string, FunctionSignatureParameter>();
            var signature = new FunctionSignature(
                id,
                Token.Identifier($"print_{type.Signature.Name}", SourceSpan.Default),
                [],
                parameters,
                IsStatic: true,
                IsMutable: false,
                Expressions: [],
                Extern: true,
                true)
            {
                OwnerType = null,
                ReturnType = InstantiatedClass.Unit
            };

            parameters["num"] = new FunctionSignatureParameter(
                signature,
                Token.Identifier("num", SourceSpan.Default),
                type,
                false,
                0);

            return signature;
        }

        static FunctionSignature()
        {
            var allocateParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
            Allocate = new FunctionSignature(
                DefId.Allocate,
                Token.Identifier("allocate", SourceSpan.Default),
                [],
                allocateParameters,
                IsStatic: true,
                IsMutable: false,
                [],
                Extern: true,
                IsMutableReturn: true)
            {
                OwnerType = null,
                ReturnType = InstantiatedClass.RawPointer
            };
            
            allocateParameters["byteSize"] = new FunctionSignatureParameter(
                Allocate,
                Token.Identifier("byteSize", SourceSpan.Default),
                InstantiatedClass.UInt64,
                Mutable: false,
                ParameterIndex: 0);

            var printStringParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
            PrintString = new FunctionSignature(
                DefId.PrintString,
                Token.Identifier("print_string", SourceSpan.Default),
                [],
                printStringParameters,
                IsStatic: true,
                IsMutable: false,
                Expressions: [],
                Extern: true,
                IsMutableReturn: true)
            {
                OwnerType = null,
                ReturnType = InstantiatedClass.Unit
            };

            printStringParameters["str"] = new FunctionSignatureParameter(
                PrintString,
                Token.Identifier("str", SourceSpan.Default),
                InstantiatedClass.String,
                false,
                0);

            var boxParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
            var boxTypeParameters = new List<GenericPlaceholder>();
            Box = new FunctionSignature(
                DefId.Box,
                Token.Identifier("box", SourceSpan.Default),
                boxTypeParameters,
                boxParameters,
                IsStatic: true,
                IsMutable: false,
                Expressions: [],
                Extern: true,
                IsMutableReturn: true)
            {
                OwnerType = null,
                ReturnType = null!
            };
            
            /*
             * pub fn box<TParam, TResult>(param: TParam): TResult
             *  where TParam: unboxed TResult,
             *        TResult: boxed TParam
             * {}
             */
            
            var boxTParamConstraints = new List<ITypeConstraint>();
            boxTypeParameters.Add(new GenericPlaceholder{GenericName = "TParam", OwnerType = Box, Constraints = boxTParamConstraints});
            boxTypeParameters.Add(new GenericPlaceholder{GenericName = "TReturn", OwnerType = Box, Constraints = [new BoxedTypeConstraint(boxTypeParameters[0])]});
            boxTParamConstraints.Add(new UnboxedTypeConstraint(boxTypeParameters[1]));
            Box.ReturnType = boxTypeParameters[1];
            boxParameters["value"] = new FunctionSignatureParameter(
                Box,
                Token.Identifier("value", SourceSpan.Default),
                boxTypeParameters[0],
                false,
                0);
            
            var unboxParameters = new OrderedDictionary<string, FunctionSignatureParameter>();
            var unboxTypeParameters = new List<GenericPlaceholder>();
            Unbox = new FunctionSignature(
                DefId.Unbox,
                Token.Identifier("unbox", SourceSpan.Default),
                unboxTypeParameters,
                unboxParameters,
                IsStatic: true,
                IsMutable: false,
                Expressions: [],
                Extern: true,
                IsMutableReturn: true)
            {
                OwnerType = null,
                ReturnType = null!
            };
            
            /*
             * pub fn unbox<TParam, TResult>(param: TParam): TResult
             *  where TParam: boxed TResult,
             *        TResult: unboxed TParam
             * {}
             */
            
            var unboxTParamConstraints = new List<ITypeConstraint>();
            unboxTypeParameters.Add(new GenericPlaceholder{GenericName = "TParam", OwnerType = Unbox, Constraints = unboxTParamConstraints});
            unboxTypeParameters.Add(new GenericPlaceholder{GenericName = "TReturn", OwnerType = Unbox, Constraints = [new UnboxedTypeConstraint(unboxTypeParameters[0])]});
            unboxTParamConstraints.Add(new BoxedTypeConstraint(unboxTypeParameters[1]));
            Unbox.ReturnType = unboxTypeParameters[1];
            unboxParameters["value"] = new FunctionSignatureParameter(
                Unbox,
                Token.Identifier("value", SourceSpan.Default),
                unboxTypeParameters[0],
                false,
                0);
        }
    }

    public interface ITypeConstraint;
    
    public record BoxedTypeConstraint(ITypeReference BoxedOfType) : ITypeConstraint;
    public record UnboxedTypeConstraint(ITypeReference BoxedOfType) : ITypeConstraint;

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
