using Reef.Core.LoweredExpressions;
using static Reef.Core.TypeChecking.TypeChecker;

namespace Reef.Core.Abseil;

public partial class ProgramAbseil
{
    private readonly List<LoweredMethod> _methods = [];
    private readonly List<DataType> _types = [];
    private readonly LangProgram _program;

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
        var lowLevelProgram = new LoweredProgram()
        {
            DataTypes = _types,
            Methods = _methods
        };

        foreach (var (dataType, dataTypeMethods) in _program.Unions.Select(x => LowerUnion(x.Signature.NotNull())))
        {
            _types.Add(dataType);
            _methods.AddRange(dataTypeMethods);
        }

        foreach (var (dataType, dataTypeMethods) in _program.Classes.Select(x => LowerClass(x.Signature.NotNull())))
        {
            _types.Add(dataType);
            _methods.AddRange(dataTypeMethods);
        }

        if (CreateMainMethod() is { } mainMethod)
        {
            _methods.Add(mainMethod);
        }

        return lowLevelProgram;
    }

    private LoweredMethod? CreateMainMethod()
    {
        if (_program.Expressions.Count == 0)
        {
            return null;
        }

        var locals = _program.TopLevelLocalVariables
            .Select(x => new MethodLocal(x.Name.StringValue, GetTypeReference(x.Type)))
            .ToList();

        var expressions = _program.Expressions.Select(LowerExpression).ToList();

        if (!_program.Expressions[^1].Diverges)
        {
            expressions.Add(new MethodReturnExpression(
                        new UnitConstantExpression(true)));
        }

        return new LoweredMethod(
                Guid.NewGuid(),
                "_Main",
                TypeParameters: [],
                Parameters: [],
                ReturnType: GetTypeReference(InstantiatedClass.Unit),
                expressions,
                locals);
    }

    private (DataType dataType, IReadOnlyList<LoweredMethod> methods) LowerClass(ClassSignature klass)
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

        IReadOnlyList<LoweredMethod> methods = [.. klass.Functions.Select(x => LowerTypeMethod(klass.Name, x, classTypeReference))];

        return (new DataType(
                klass.Id,
                klass.Name,
                typeParameters,
                [new DataTypeVariant("_classVariant", [.. fields])],
                [.. staticFields]), methods);
    }

    private (DataType, IReadOnlyList<LoweredMethod> methods) LowerUnion(UnionSignature union)
    {
        var typeParameters = union.TypeParameters.Select(GetGenericPlaceholder).ToArray();
        var unionTypeReference = new LoweredConcreteTypeReference(
                                                    union.Name,
                                                    union.Id,
                                                    typeParameters);

        var dataTypeMethods = union.NotNull()
            .Functions.Select(x => LowerTypeMethod(union.Name, x, unionTypeReference))
            .ToList();
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

                        // add the tuple variant as a method
                        dataTypeMethods.Add(new LoweredMethod(
                                    Guid.NewGuid(),
                                    $"{union.Name}_Create_{u.Name}",
                                    [],
                                    memberTypes,
                                    unionTypeReference,
                                    [
                                        new MethodReturnExpression(
                                            new CreateObjectExpression(
                                                unionTypeReference,
                                                variant.Name,
                                                true,
                                                createMethodFieldInitializations
                                                ))
                                    ],
                                    []));
                        break;
                    }
                default:
                    throw new InvalidOperationException($"Invalid union variant {variant}");
            }
            variants.Add(new DataTypeVariant(
                        variant.Name,
                        fields));
        }
        return (new DataType(
                union.NotNull().Id,
                union.Name,
                typeParameters,
                Variants: variants,
                StaticFields: []), dataTypeMethods);
    }

    private LoweredMethod LowerTypeMethod(
            string typeName,
            FunctionSignature fnSignature,
            ILoweredTypeReference ownerTypeReference)
    {
        var locals = fnSignature.LocalVariables
            .Select(x => new MethodLocal(x.Name.StringValue, GetTypeReference(x.Type)))
            .ToList();
        var expressions = fnSignature.Expressions.Select(LowerExpression)
            .ToList();

        // if we passed type checking, and either there are no expressions or the last 
        // expression does not diverge (throw or return), then we need to add an explicit
        // return unit
        if (expressions.Count == 0 || !fnSignature.Expressions[^1].Diverges)
        {
            expressions.Add(new MethodReturnExpression(
                new UnitConstantExpression(ValueUseful: true)));
        }

        var parameters = fnSignature.Parameters.Values.Select(y => GetTypeReference(y.Type));

        if (!fnSignature.IsStatic)
        {
            parameters = parameters.Prepend(ownerTypeReference);
        }

        return new LoweredMethod(
            fnSignature.Id,
            $"{typeName}__{fnSignature.Name}",
            fnSignature.TypeParameters.Select(GetGenericPlaceholder).ToArray(),
            parameters.ToArray(),
            GetTypeReference(fnSignature.ReturnType),
            expressions,
            locals);
    }

    private LoweredGenericPlaceholder GetGenericPlaceholder(GenericPlaceholder placeholder)
    {
        return new LoweredGenericPlaceholder(placeholder.OwnerType.Id, placeholder.GenericName);
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
            GenericPlaceholder g => new LoweredGenericPlaceholder(
                    g.OwnerType.Id,
                    g.GenericName),
            UnknownInferredType i => GetTypeReference(i.ResolvedType.NotNull()),
            _ => throw new InvalidOperationException($"Type reference {typeReference.GetType()} is not supported")
        };
    }
}
