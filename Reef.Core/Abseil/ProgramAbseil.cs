using Reef.Core.LoweredExpressions;
using static Reef.Core.TypeChecking.TypeChecker;

namespace Reef.Core.Abseil;

public static class ProgramAbseil
{
    public static LoweredProgram Lower(LangProgram program)
    {
        var dataTypes = new List<DataType>();
        var methods = new List<IMethod>();
        var lowLevelProgram = new LoweredProgram()
        {
            DataTypes = dataTypes,
            Methods = methods
        };

        foreach (var (dataType, dataTypeMethods) in program.Unions.Select(x => LowerUnion(x.Signature.NotNull())))
        {
            dataTypes.Add(dataType);
            methods.AddRange(dataTypeMethods);
        }

        foreach (var (dataType, dataTypeMethods) in program.Classes.Select(x => LowerClass(x.Signature.NotNull())))
        {
            dataTypes.Add(dataType);
            methods.AddRange(dataTypeMethods);
        }

        if (CreateMainMethod(program) is {} mainMethod)
        {
            methods.Add(mainMethod);
        }

        return lowLevelProgram;
    }

    private static LoweredMethod? CreateMainMethod(LangProgram program)
    {
        if (program.Expressions.Count == 0)
        {
            return null;
        }

        var locals = program.TopLevelLocalVariables
            .Select(x => new MethodLocal(x.Name.StringValue, GetTypeReference(x.Type)))
            .ToList();

        var expressions = program.Expressions.Select(ExpressionAbseil.LowerExpression).ToList();

        if (!program.Expressions[^1].Diverges)
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

    private static (DataType dataType, IReadOnlyList<IMethod> methods) LowerClass(ClassSignature klass)
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
                        ExpressionAbseil.LowerExpression(x.StaticInitializer.NotNull())));

        var fields = klass.Fields.Where(x => !x.IsStatic)
            .Select(x => new DataTypeField(x.Name, GetTypeReference(x.Type)));

        IReadOnlyList<IMethod> methods = [..klass.Functions.Select(x => LowerTypeMethod(klass.Name, x, classTypeReference))];

        return (new DataType(
                klass.Id,
                klass.Name,
                typeParameters,
                [new DataTypeVariant("_classVariant", [..fields])],
                [..staticFields]), methods);
    }

    private static (DataType, IReadOnlyList<IMethod> methods) LowerUnion(UnionSignature union)
    {
        var typeParameters = union.TypeParameters.Select(GetGenericPlaceholder).ToArray();
        var unionTypeReference = new LoweredConcreteTypeReference(
                                                    union.Name,
                                                    union.Id,
                                                    typeParameters);

        var dataTypeMethods = union.NotNull()
            .Functions.Select(x => LowerTypeMethod(union.Name, x, unionTypeReference))
            .Cast<IMethod>()
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

                        // add the tuple variant as a method
                        dataTypeMethods.Add(new CompilerImplementedMethod(
                                    Guid.NewGuid(),
                                    $"{union.Name}_Create_{u.Name}",
                                    memberTypes,
                                    unionTypeReference,
                                    CompilerImplementationType.UnionTupleVariantInit));
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

    private static LoweredMethod LowerTypeMethod(
            string typeName,
            FunctionSignature fnSignature,
            ILoweredTypeReference ownerTypeReference)
    {
        var locals = fnSignature.LocalVariables
            .Select(x => new MethodLocal(x.Name.StringValue, GetTypeReference(x.Type)))
            .ToList();
        var expressions = fnSignature.Expressions.Select(ExpressionAbseil.LowerExpression)
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

    private static LoweredGenericPlaceholder GetGenericPlaceholder(GenericPlaceholder placeholder)
    {
        return new LoweredGenericPlaceholder(placeholder.OwnerType.Id, placeholder.GenericName);
    }

    private static ILoweredTypeReference GetTypeReference(ITypeReference typeReference)
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
            _ => throw new InvalidOperationException($"Type reference {typeReference.GetType()} is not supported")
        };
    }
}
