using Reef.Core.LoweredExpressions;
using static Reef.Core.TypeChecking.TypeChecker;

namespace Reef.Core.Abseil;

public partial class ProgramAbseil
{
    private readonly Dictionary<
        LoweredMethod,
        (
            FunctionSignature fnSignature,
            List<ILoweredExpression> loweredExpressions,
            IReadOnlyList<Expressions.IExpression> highLevelExpressions,
            LoweredConcreteTypeReference? ownerType,
            bool needsLowering
        )> _methods = [];
    private readonly Dictionary<Guid, DataType> _types = [];
    private readonly LangProgram _program;
    private LoweredConcreteTypeReference? _currentType;
    private (LoweredMethod LoweredMethod, FunctionSignature FunctionSignature)? _currentFunction;

    public static LoweredProgram Lower(LangProgram program)
    {
        return new ProgramAbseil(program).LowerInner();
    }

    private ProgramAbseil(LangProgram program)
    {
        _program = program;
    }

    private LoweredProgram LowerInner()
    {
        foreach (var dataType in _program.Unions.Select(x => LowerUnion(x.Signature.NotNull())))
        {
            _types.Add(dataType.Id, dataType);
        }

        foreach (var dataType in _program.Classes.Select(x => LowerClass(x.Signature.NotNull())))
        {
            _types.Add(dataType.Id, dataType);
        }

        foreach (var fnSignature in _program.Functions
                .Select(x => x.Signature.NotNull())
                .Where(x => x.AccessedOuterVariables.Count == 0))
        {
            var (method, loweredExpressions, expressions) = GenerateLoweredMethod(null, fnSignature, null);
            _methods.Add(method, (fnSignature, loweredExpressions, expressions, null, true));
        }

        var mainSignature = new FunctionSignature(
                Token.Identifier("_Main", SourceSpan.Default),
                [],
                [],
                isStatic: true,
                isMutable: false,
                _program.Expressions,
                functionIndex: null)
        {
            ReturnType = InstantiatedClass.Unit,
            OwnerType = null,
            LocalVariables = _program.TopLevelLocalVariables,
            LocalFunctions = [.. _program.Functions
                .Select(x => x.Signature.NotNull())
                .Where(x => x.AccessedOuterVariables.Count > 0)]
        };
        foreach (var local in _program.TopLevelLocalVariables)
        {
            local.ContainingFunction = mainSignature;
        }

        if (mainSignature.Expressions.Count > 0)
        {
            var (method, loweredExpressions, expressions) = GenerateLoweredMethod(
                    null, mainSignature, null);
            _methods.Add(method, (mainSignature, loweredExpressions, expressions, null, true));
        }

        foreach (var (method, (fnSignature, loweredExpressions, expressions, ownerTypeReference, _)) in _methods.Where(x => x.Value.needsLowering))
        {
            _currentType = ownerTypeReference;
            _currentFunction = (method, fnSignature);

            loweredExpressions.AddRange(expressions.Select(LowerExpression));

            if (expressions.Count == 0 || !expressions[^1].Diverges)
            {
                loweredExpressions.Add(new MethodReturnExpression(
                            new UnitConstantExpression(true)));
            }
        }

        return new LoweredProgram()
        {
            DataTypes = [.._types.Values],
            Methods = [.._methods.Keys]
        };
    }

    private DataType LowerClass(ClassSignature klass)
    {
        var typeParameters = klass.TypeParameters.Select(GetGenericPlaceholder).ToArray();
        var classTypeReference = new LoweredConcreteTypeReference(
                klass.Name,
                klass.Id,
                typeParameters);

        var staticFields = klass.Fields.Where(x => x.IsStatic)
            .Select(x => new StaticDataTypeField(
                        x.Name,
                        GetTypeReference(x.Type),
                        LowerExpression(x.StaticInitializer.NotNull())));

        var fields = klass.Fields.Where(x => !x.IsStatic)
            .Select(x => new DataTypeField(x.Name, GetTypeReference(x.Type)));

        foreach (var method in klass.Functions)
        {
            var (loweredMethod, loweredExpressions, expressions) = GenerateLoweredMethod(klass.Name, method, classTypeReference);
            _methods.Add(loweredMethod, (method, loweredExpressions, expressions, classTypeReference, true));
        }

        return new DataType(
                klass.Id,
                klass.Name,
                typeParameters,
                [new DataTypeVariant("_classVariant", [.. fields])],
                [.. staticFields]);
    }

    private DataType LowerUnion(UnionSignature union)
    {
        var typeParameters = union.TypeParameters.Select(GetGenericPlaceholder).ToArray();
        var unionTypeReference = new LoweredConcreteTypeReference(
                                                    union.Name,
                                                    union.Id,
                                                    typeParameters);

        foreach (var function in union.Functions)
        {
            var (loweredMethod, loweredExpressions, expressions) = GenerateLoweredMethod(union.Name, function, unionTypeReference);

            _methods.Add(loweredMethod, (function, loweredExpressions, expressions, unionTypeReference, true));
        }

        var variants = new List<DataTypeVariant>(union.Variants.Count);
        foreach (var variant in union.Variants)
        {
            var variantIdentifierField = new DataTypeField(
                    "_variantIdentifier",
                    GetTypeReference(InstantiatedClass.Int));
            var fields = new List<DataTypeField>() { variantIdentifierField };
            switch (variant)
            {
                case TypeChecking.TypeChecker.UnitUnionVariant u:
                    break;
                case TypeChecking.TypeChecker.ClassUnionVariant u:
                    {
                        fields.AddRange(u.Fields.Select(x => new DataTypeField(
                                        x.Name,
                                        GetTypeReference(x.Type))));
                        break;
                    }
                case TypeChecking.TypeChecker.TupleUnionVariant u:
                    {
                        var memberTypes = u.TupleMembers.NotNull().Select(GetTypeReference).ToArray();
                        fields.AddRange(memberTypes.Select((x, i) => new DataTypeField(
                                        $"Item{i}",
                                        x)));

                        var createMethodFieldInitializations = fields.Skip(1).Index().ToDictionary(x => x.Item.Name, x => (ILoweredExpression)new LoadArgumentExpression((uint)x.Index, true, x.Item.Type));
                        createMethodFieldInitializations["_variantIdentifier"] = new IntConstantExpression(true, variants.Count);

                        List<ILoweredExpression> expressions = [
                                        new MethodReturnExpression(
                                            new CreateObjectExpression(
                                                unionTypeReference,
                                                variant.Name,
                                                true,
                                                createMethodFieldInitializations
                                                ))
                                    ];

                        var method = new LoweredMethod(
                                    Guid.NewGuid(),
                                    $"{union.Name}_Create_{u.Name}",
                                    [],
                                    memberTypes,
                                    unionTypeReference,
                                    expressions,
                                    []);

                        // add the tuple variant as a method
                        _methods.Add(
                                method,
                                // pass null as the signature because it's never used as the current function
                                (null!, expressions, [], unionTypeReference, false));
                        break;
                    }
                default:
                    throw new InvalidOperationException($"Invalid union variant {variant}");
            }
            variants.Add(new DataTypeVariant(
                        variant.Name,
                        fields));
        }

        return new DataType(
                union.NotNull().Id,
                union.Name,
                typeParameters,
                Variants: variants,
                StaticFields: []);
    }

    // instead of lowering the methods expressions right here, we return the list of expressions 
    // to add to later so that all the type and function references are available to be used
    private (LoweredMethod, List<ILoweredExpression>, IReadOnlyList<Expressions.IExpression>) GenerateLoweredMethod(
            string? ownerName,
            FunctionSignature fnSignature,
            LoweredConcreteTypeReference? ownerTypeReference)
    {
        var name = ownerName is null
            ? fnSignature.Name
            : $"{ownerName}__{fnSignature.Name}";

        var localsAccessedInClosure = fnSignature.LocalVariables.Where(x => x.ReferencedInClosure).ToArray();
        DataType? localsType = null;
        if (localsAccessedInClosure.Length > 0)
        {
            localsType = new DataType(
                Guid.NewGuid(),
                $"{name}__Locals",
                [],
                [
                    new DataTypeVariant(
                        "_classVariant",
                        [..localsAccessedInClosure.Select(
                            x => new DataTypeField(
                                x.Name.StringValue,
                                GetTypeReference(x.Type)))])
                ],
                []);
            _types.Add(localsType.Id, localsType);

            fnSignature.LocalsTypeId = localsType.Id;
        }

        DataType? closureType = null;
        if (fnSignature.AccessedOuterVariables.Count > 0)
        {
            var fields = new Dictionary<Guid, DataTypeField>();

            foreach (var variable in fnSignature.AccessedOuterVariables)
            {
                switch (variable)
                {
                    case LocalVariable localVariable:
                        {
                            var containingFunction = localVariable.ContainingFunction.NotNull();
                            var localTypeId = containingFunction
                                .LocalsTypeId.NotNull(expectedReason: "the containing function containing the referenced local should have already been lowered");
                            var localType = _types[localTypeId];
                            var localTypeReference = new LoweredConcreteTypeReference(
                                localType.Name,
                                localTypeId,
                                []);

                            fields.TryAdd(
                                localTypeId,
                                new DataTypeField(localType.Name, localTypeReference)); 
                            break;
                        }
                    case FunctionSignatureParameter parameterVariable:
                            throw new NotImplementedException();
                    case FieldVariable fieldVariable:
                            throw new NotImplementedException();
                    case ThisVariable thisVariable:
                            throw new NotImplementedException();
                }
            }

            closureType = new DataType(
                Guid.NewGuid(),
                $"{name}__Closure",
                [],
                [
                    new DataTypeVariant(
                        "_classVariant",
                        [..fields.Values])
                ],
                []);

            fnSignature.ClosureTypeId = closureType.Id;

            _types.Add(closureType.Id, closureType);
        }

        foreach (var localSignature in fnSignature.LocalFunctions)
        {
            var (localMethod, localLoweredExpressions, localExpressions) = 
                GenerateLoweredMethod(ownerName: name, localSignature, null);

            _methods.Add(localMethod, (localSignature, localLoweredExpressions, localExpressions, ownerTypeReference, true));
        }

        var locals = new List<MethodLocal>(
                fnSignature.LocalVariables.Count
                - localsAccessedInClosure.Length
                + (localsAccessedInClosure.Length > 0 ? 1 : 0));
        var expressions = new List<ILoweredExpression>(fnSignature.Expressions.Count);

        if (localsType is not null)
        {
            var localsTypeReference = new LoweredConcreteTypeReference(
                            localsType.Name,
                            localsType.Id,
                            []);

            locals.Add(new MethodLocal(
                        "__locals",
                        localsTypeReference));

            expressions.Add(
                new VariableDeclarationAndAssignmentExpression(
                    "__locals",
                    new CreateObjectExpression(
                        localsTypeReference,
                        "_classVariant",
                        true,
                        []),
                    false));

        }

        locals.AddRange(fnSignature.LocalVariables.Where(x => !x.ReferencedInClosure)
                .Select(x => new MethodLocal(x.Name.StringValue, GetTypeReference(x.Type))));

        var parameters = fnSignature.Parameters.Values.Select(y => GetTypeReference(y.Type));

        if (!fnSignature.IsStatic && ownerTypeReference is not null)
        {
            parameters = parameters.Prepend(ownerTypeReference);
        }
        else if (closureType is not null)
        {
            parameters = parameters.Prepend(new LoweredConcreteTypeReference(
                closureType.Name,
                closureType.Id,
                []));
        }

        return (new LoweredMethod(
            fnSignature.Id,
            name,
            [.. fnSignature.TypeParameters.Select(GetGenericPlaceholder)],
            [.. parameters],
            GetTypeReference(fnSignature.ReturnType),
            expressions,
            locals), expressions, fnSignature.Expressions);
    }

    private LoweredGenericPlaceholder GetGenericPlaceholder(GenericPlaceholder placeholder)
    {
        return new LoweredGenericPlaceholder(placeholder.OwnerType.Id, placeholder.GenericName);
    }

    private LoweredFunctionReference GetFunctionReference(Guid functionId, IReadOnlyList<ILoweredTypeReference> typeArguments)
    {
        var loweredMethod = _methods.Keys.First(x => x.Id == functionId);
        return new(
                loweredMethod.Name,
                functionId,
                typeArguments);
    }

    private ILoweredTypeReference GetTypeReference(ITypeReference typeReference)
    {
        return typeReference switch
        {
            InstantiatedClass c => new LoweredConcreteTypeReference(
                    c.Signature.Name,
                    c.Signature.Id,
                    [.. c.TypeArguments.Select(x => GetTypeReference(x.ResolvedType.NotNull()))]),
            InstantiatedUnion u => new LoweredConcreteTypeReference(
                    u.Signature.Name,
                    u.Signature.Id,
                    [.. u.TypeArguments.Select(x => GetTypeReference(x.ResolvedType.NotNull()))]),
            GenericTypeReference g => GetTypeReference(g.ResolvedType.NotNull()),
            GenericPlaceholder g => new LoweredGenericPlaceholder(
                    g.OwnerType.Id,
                    g.GenericName),
            UnknownInferredType i => GetTypeReference(i.ResolvedType.NotNull()),
            _ => throw new InvalidOperationException($"Type reference {typeReference.GetType()} is not supported")
        };
    }

    private static bool EqualTypeReferences(ILoweredTypeReference a, ILoweredTypeReference b)
    {
        return (a, b) switch
        {
            (LoweredConcreteTypeReference concreteA, LoweredConcreteTypeReference concreteB)
                when concreteA.DefinitionId == concreteB.DefinitionId
                && concreteA.TypeArguments.Zip(concreteB.TypeArguments)
                    .All(x => EqualTypeReferences(x.First, x.Second)) => true,
            (LoweredGenericPlaceholder genericA, LoweredGenericPlaceholder genericB)
                when genericA.OwnerDefinitionId == genericB.OwnerDefinitionId => true,
            _ => false
        };
    }
}
