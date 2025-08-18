using Reef.Core.LoweredExpressions;
using static Reef.Core.TypeChecking.TypeChecker;

namespace Reef.Core.Absail;

public static class ProgramAbsail
{
    public static LoweredProgram Lower(LangProgram program)
    {
        var dataTypes = new List<DataType>();
        var methods = new List<LoweredGlobalMethod>();
        var lowLevelProgram = new LoweredProgram()
        {
            DataTypes = dataTypes,
            Methods = methods
        };

        dataTypes.AddRange(program.Unions.Select(x => LowerUnion(x.Signature.NotNull())));
        dataTypes.AddRange(program.Classes.Select(x => LowerClass(x.Signature.NotNull())));

        return lowLevelProgram;
    }

    private static DataType LowerClass(ClassSignature klass)
    {
        var typeParameters = klass.TypeParameters.Select(GetGenericPlaceholder).ToArray();
        var classTypeReference = new LoweredConcreteTypeReference(
                klass.Name,
                klass.Id,
                typeParameters);

        return new DataType(
                klass.Id,
                klass.Name,
                typeParameters,
                [],
                [],
                []);
    }

    private static DataType LowerUnion(UnionSignature union)
    {
        var typeParameters = union.TypeParameters.Select(GetGenericPlaceholder).ToArray();
        var unionTypeReference = new LoweredConcreteTypeReference(
                                                    union.Name,
                                                    union.Id,
                                                    typeParameters);

        var dataTypeMethods = union.NotNull()
            .Functions.Select(x => LowerTypeMethod(x, unionTypeReference)).ToList();
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
                        dataTypeMethods.Add(new DataTypeMethod(
                                    Guid.NewGuid(),
                                    $"{union.Name}_Create_{u.Name}",
                                    [],
                                    memberTypes,
                                    unionTypeReference,
                                    Expressions: [],
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
        return new DataType(
                union.NotNull().Id,
                union.Name,
                typeParameters,
                Variants: variants,
                Methods: dataTypeMethods,
                StaticFields: []);
    }

    private static DataTypeMethod LowerTypeMethod(
            FunctionSignature fnSignature,
            ILoweredTypeReference ownerTypeReference)
    {
        var expressions = fnSignature.Expressions.SelectMany(ExpressionAbsail.LowerExpression)
            .ToList();

        // if we passed type checking, and either there are no expressions or the last 
        // expression does not diverge (throw or return), then we need to add an explicit
        // return unit
        if (expressions.Count == 0 || !expressions[^1].Diverges)
        {
            expressions.Add(new MethodReturnExpression(
                new UnitConstantExpression(ValueUseful: true)));
        }

        var parameters = fnSignature.Parameters.Values.Select(y => GetTypeReference(y.Type));

        if (!fnSignature.IsStatic)
        {
            parameters = parameters.Prepend(ownerTypeReference);
        }

        return new DataTypeMethod(
            fnSignature.Id,
            fnSignature.Name,
            fnSignature.TypeParameters.Select(GetGenericPlaceholder).ToArray(),
            parameters.ToArray(),
            GetTypeReference(fnSignature.ReturnType),
            expressions);
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
