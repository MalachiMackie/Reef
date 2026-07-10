namespace Reef.Core.TypeChecking.PatternAnalysis;

public interface IConstructor
{
    bool IsNonExhaustive()
    {
        return this is NonExhaustiveConstructor;
    }

    uint? AsVariant()
    {
        return this is VariantConstructor { VariantIndex: var variantIndex }
            ? variantIndex
            : null;
    }

    bool? AsBool()
    {
        return this is BooleanConstructor { Value: var boolValue }
            ? boolValue
            : null;
    }

    public static IEnumerable<(TypeChecker.ITypeReference, PrivateUninhabitedField)> CtorSubTypes(
        IConstructor ctor,
        TypeChecker.ITypeReference typeReference)
    {
        switch (ctor)
        {
            case StringLiteralConstructor:
            case BooleanConstructor:
            case HiddenConstructor:
            case MissingConstructor:
            case NeverConstructor:
            case NonExhaustiveConstructor:
            case PrivateUninhabitedConstructor:
            case WildcardConstructor:
                return [];
            case VariantConstructor variantConstructor:
                {
                    var variant = typeReference.ConcreteType() switch
                        {
                            TypeChecker.InstantiatedUnion x => x.GetVariants()[(int)variantConstructor.VariantIndex],
                            TypeChecker.VariantOfType(var x) => x.GetVariants()[(int)variantConstructor.VariantIndex],
                            TypeChecker.SelfTypeReference{Signature: TypeChecker.UnionSignature signature} => signature.Variants[(int)variantConstructor.VariantIndex],
                            _ => throw new InvalidOperationException("Expected union type"),
                        };

                    var subTypes = variant switch
                    {
                        TypeChecker.ClassUnionVariant classUnionVariant => classUnionVariant.Fields.Select(x => x.Type),
                        TypeChecker.TupleUnionVariant tupleUnionVariant => tupleUnionVariant.TupleMembers,
                        TypeChecker.UnitUnionVariant => [],
                        _ => throw new ArgumentOutOfRangeException(nameof(variant))
                    };

                    return subTypes.Select(x => (x, new PrivateUninhabitedField(false)));
                }
            case ClassConstructor:
                {
                    var @class = typeReference as TypeChecker.InstantiatedClass
                        ?? throw new InvalidOperationException("Expected class");

                    return @class.GetFields().Select(x =>
                    {
                        // todo: need to figure out if we can access this field
                        // var isVisible = true;
                        return (x.Type, new PrivateUninhabitedField(false));
                    });
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(ctor));
        }
    }

    public static IConstructorSet CtorsForType(TypeChecker.ITypeReference type)
    {
        while (type is TypeChecker.GenericTypeReference generic)
        {
            type = generic.ResolvedType.NotNull();
        }

        if (type is TypeChecker.GenericPlaceholder)
        {
            return new NoConstructorsConstructorSet();
        }

        var (unionSignature, classSignature) = type switch
        {
            TypeChecker.InstantiatedUnion { Signature: var sig } => (sig, (TypeChecker.ClassSignature?)null),
            TypeChecker.VariantOfType({ Signature: var sig }) => (sig, null),
            TypeChecker.InstantiatedClass { Signature: var sig } => (null, sig),
            TypeChecker.UnspecifiedSizedIntType {ResolvedIntType: TypeChecker.InstantiatedClass{ Signature: var sig } } => (null, sig),
            TypeChecker.SelfTypeReference { Signature: TypeChecker.UnionSignature sig } => (sig, null),
            TypeChecker.SelfTypeReference { Signature: TypeChecker.ClassSignature sig } => (null, sig),
            _ => (null, null)
        };

        if (unionSignature is not null)
        {
            if (unionSignature.Variants.Count == 0)
            {
                return new NoConstructorsConstructorSet();
            }

            var variants = new VariantVisibility[unionSignature.Variants.Count];
            Array.Fill(variants, VariantVisibility.Visible);

            return new VariantsConstructorSet(variants, NonExhaustive: false);
        }

        if (classSignature?.Id == DefId.Boolean)
        {
            return new BooleanConstructorSet();
        }

        if (classSignature?.Id == DefId.Never)
        {
            return new NoConstructorsConstructorSet();
        }

        return new ClassConstructorSet(Empty: false);
    }

    uint Arity(TypeChecker.ITypeReference typeReference)
    {
        switch (this)
        {
            case ClassConstructor:
                {
                    var concreteType = typeReference.ConcreteType();
                    if (concreteType is TypeChecker.InstantiatedClass { Signature: var signature })
                    {
                        return (uint)signature.Fields.Count;
                    }

                    throw new InvalidOperationException("Expected type to be class");
                }
            case VariantConstructor { VariantIndex: var variantIndex }:
                {
                    var concreteType = typeReference.ConcreteType();
                    if (concreteType is TypeChecker.InstantiatedUnion { Signature: var unionSignature })
                    {
                        var variant = unionSignature.Variants[(int)variantIndex];
                        return variant switch
                        {
                            TypeChecker.ClassUnionVariant classUnionVariant => (uint)classUnionVariant.Fields.Count,
                            TypeChecker.TupleUnionVariant tupleUnionVariant => (uint)tupleUnionVariant.TupleMembers.Count,
                            TypeChecker.UnitUnionVariant => 0,
                            _ => throw new ArgumentOutOfRangeException(nameof(variant))
                        };
                    }

                    if (concreteType is TypeChecker.VariantOfType({ Signature: var variantOfUnionSignature }))
                    {
                        // all variants of variantOf have an arity of 0
                        return 0;
                    }

                    throw new InvalidOperationException("Expected type to be union");

                }
            case BooleanConstructor or StringLiteralConstructor or NeverConstructor
                or NonExhaustiveConstructor or HiddenConstructor
                or MissingConstructor or PrivateUninhabitedConstructor or WildcardConstructor:
                {
                    return 0;
                }
            default:
                throw new InvalidOperationException($"Unexpected constructor type: {GetType()}");
        }
    }

    bool IsCoveredBy(IConstructor other)
    {
        return (this, other) switch
        {
            (WildcardConstructor, _) => throw new InvalidOperationException(
                "Constructor splitting should not have return wildcard"),
            // wildcards cover anything
            // privateUninhabited skips everything
            (_, WildcardConstructor) or (PrivateUninhabitedConstructor, _) => true,
            // only a wildcard can match these special constructors
            (MissingConstructor or NonExhaustiveConstructor or HiddenConstructor, _) => false,
            (ClassConstructor, ClassConstructor) => true,
            (VariantConstructor { VariantIndex: var thisVariantIndex }, VariantConstructor
            {
                VariantIndex: var otherVariantIndex
            }) => thisVariantIndex == otherVariantIndex,
            (BooleanConstructor { Value: var thisB }, BooleanConstructor { Value: var otherB }) => thisB == otherB,
            (StringLiteralConstructor { Value: var thisStr }, StringLiteralConstructor { Value: var otherStr }) =>
                thisStr == otherStr,
            _ => throw new InvalidOperationException($"Trying to compare incompatible constructors {this} and {other}")
        };
    }
}

public record ClassConstructor : IConstructor;
public record VariantConstructor(uint VariantIndex) : IConstructor;
public record BooleanConstructor(bool Value) : IConstructor;
public record StringLiteralConstructor(string Value) : IConstructor;
public record WildcardConstructor : IConstructor;
public record NeverConstructor : IConstructor;

public record NonExhaustiveConstructor : IConstructor;
public record HiddenConstructor : IConstructor;
public record MissingConstructor : IConstructor;
public record PrivateUninhabitedConstructor : IConstructor;


public enum VariantVisibility
{
    Visible,
    Hidden,
    Empty
}

public record PrivateUninhabitedField(bool Value);
