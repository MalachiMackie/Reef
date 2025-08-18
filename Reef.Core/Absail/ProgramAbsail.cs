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

        foreach (var union in program.Unions.Select(x => x.Signature.NotNull()))
        {
            var unionTypeReference = new LoweredConcreteTypeReference(
                                            union.Name,
                                            union.Id,
                                            []);

            var dataTypeMethods = union.NotNull()
                .Functions.Select(LowerTypeMethod).ToList();
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
            dataTypes.Add(new DataType(
                    union.NotNull().Id,
                    union.Name,
                    union.TypeParameters.Select(GetGenericPlaceholder).ToArray(),
                    Variants: variants,
                    Methods: dataTypeMethods,
                    StaticFields: []));
        }

        return lowLevelProgram;
    }

    private static DataTypeMethod LowerTypeMethod(FunctionSignature fnSignature)
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

        return new DataTypeMethod(
            fnSignature.Id,
            fnSignature.Name,
            fnSignature.TypeParameters.Select(GetGenericPlaceholder).ToArray(),
            fnSignature.Parameters.Values.Select(y => GetTypeReference(y.Type)).ToArray(),
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
            _ => throw new InvalidOperationException($"Type reference {typeReference.GetType()} is not supported")
        };
    }
}
