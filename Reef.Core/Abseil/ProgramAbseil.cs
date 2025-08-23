using Reef.Core.LoweredExpressions;
using static Reef.Core.TypeChecking.TypeChecker;

namespace Reef.Core.Abseil;

public partial class ProgramAbseil
{
    private readonly Dictionary<
        LoweredMethod,
        (
            List<ILoweredExpression> loweredExpressions,
            IReadOnlyList<Expressions.IExpression> highLevelExpressions,
            LoweredConcreteTypeReference? ownerType
        )> _methods = [];
    private readonly List<DataType> _types = [];
    private readonly LangProgram _program;
    private LoweredConcreteTypeReference? _currentType;
    private LoweredMethod? _currentFunction;

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
            _types.Add(dataType);
        }

        foreach (var dataType in _program.Classes.Select(x => LowerClass(x.Signature.NotNull())))
        {
            _types.Add(dataType);
        }

        foreach (var (method, loweredExpressions, expressions) in _program.Functions.Select(x => GenerateLoweredMethod(null, x.Signature.NotNull(), null)))
        {
            _methods.Add(method, (loweredExpressions, expressions, null));
        }

        if (CreateMainMethod() is { } mainMethod)
        {
            _methods.Add(mainMethod.Item1, (mainMethod.Item2, mainMethod.Item3, null));
        }

        foreach (var (method, (loweredExpressions, expressions, ownerTypeReference)) in _methods.Where(x => x.Value.loweredExpressions.Count == 0))
        {
            _currentType = ownerTypeReference;
            _currentFunction = method;

            loweredExpressions.AddRange(expressions.Select(LowerExpression));

            if (expressions.Count == 0 || !expressions[^1].Diverges)
            {
                loweredExpressions.Add(new MethodReturnExpression(
                            new UnitConstantExpression(true)));
            }
        }

        return new LoweredProgram()
        {
            DataTypes = _types,
            Methods = [.._methods.Keys]
        };
    }

    private (LoweredMethod, List<ILoweredExpression>, IReadOnlyList<Expressions.IExpression>)? CreateMainMethod()
    {
        if (_program.Expressions.Count == 0)
        {
            return null;
        }

        var locals = _program.TopLevelLocalVariables
            .Select(x => new MethodLocal(x.Name.StringValue, GetTypeReference(x.Type)))
            .ToList();

        var expressions = new List<ILoweredExpression>();

        return (new LoweredMethod(
                Guid.NewGuid(),
                "_Main",
                TypeParameters: [],
                Parameters: [],
                ReturnType: GetTypeReference(InstantiatedClass.Unit),
                expressions,
                locals), expressions, _program.Expressions);
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
            _methods.Add(loweredMethod, (loweredExpressions, expressions, classTypeReference));
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

            _methods.Add(loweredMethod, (loweredExpressions, expressions, unionTypeReference));
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
                                        $"_tupleMember_{i}",
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

                        // add the tuple variant as a method
                        _methods.Add(new LoweredMethod(
                                    Guid.NewGuid(),
                                    $"{union.Name}_Create_{u.Name}",
                                    [],
                                    memberTypes,
                                    unionTypeReference,
                                    expressions,
                                    []), (expressions, [], unionTypeReference));
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
            string? typeName,
            FunctionSignature fnSignature,
            LoweredConcreteTypeReference? ownerTypeReference)
    {
        foreach (var (localMethod, localLoweredExpressions, localExpressions) in fnSignature.LocalFunctions.Select(x => GenerateLoweredMethod(null, x, null)))
        {
            _methods.Add(localMethod, (localLoweredExpressions, localExpressions, ownerTypeReference));
        }

        var locals = fnSignature.LocalVariables
            .Select(x => new MethodLocal(x.Name.StringValue, GetTypeReference(x.Type)))
            .ToList();
        var expressions = new List<ILoweredExpression>();
        var parameters = fnSignature.Parameters.Values.Select(y => GetTypeReference(y.Type));

        if (!fnSignature.IsStatic && ownerTypeReference is not null)
        {
            parameters = parameters.Prepend(ownerTypeReference);
        }
        var name = typeName is null
            ? fnSignature.Name
            : $"{typeName}__{fnSignature.Name}";

        return (new LoweredMethod(
            fnSignature.Id,
            name,
            fnSignature.TypeParameters.Select(GetGenericPlaceholder).ToArray(),
            parameters.ToArray(),
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
                    c.TypeArguments.Select(x => GetTypeReference(x.ResolvedType.NotNull())).ToArray()),
            InstantiatedUnion u => new LoweredConcreteTypeReference(
                    u.Signature.Name,
                    u.Signature.Id,
                    u.TypeArguments.Select(x => GetTypeReference(x.ResolvedType.NotNull())).ToArray()),
            GenericTypeReference g => GetTypeReference(g.ResolvedType.NotNull()),
            GenericPlaceholder g => new LoweredGenericPlaceholder(
                    g.OwnerType.Id,
                    g.GenericName),
            UnknownInferredType i => GetTypeReference(i.ResolvedType.NotNull()),
            _ => throw new InvalidOperationException($"Type reference {typeReference.GetType()} is not supported")
        };
    }

    private bool EqualTypeReferences(ILoweredTypeReference a, ILoweredTypeReference b)
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

    private bool EqualFunctionReferences(LoweredFunctionReference a, LoweredFunctionReference b)
    {
        return a.DefinitionId == b.DefinitionId
            && a.TypeArguments.Zip(b.TypeArguments)
                .All(x => EqualTypeReferences(x.First, x.Second));
    }
}
