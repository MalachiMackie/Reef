using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private ITypeReference TypeCheckUnionClassVariantInitializer(UnionClassVariantInitializer initializer)
    {
        var type = GetTypeReference(initializer.UnionType);

        if (type is not InstantiatedUnion instantiatedUnion)
        {
            throw new InvalidOperationException($"{type} is not a union");
        }

        var (variantIndex, variant) =
            instantiatedUnion.GetVariants().Index().FirstOrDefault(x => x.Item.Name == initializer.VariantIdentifier.StringValue);

        if (variant is null)
        {
            AddError(TypeCheckerError.UnknownTypeMember(initializer.VariantIdentifier, initializer.UnionType.Identifier.StringValue));
            return instantiatedUnion;
        }

        if (variant is not ClassUnionVariant classVariant)
        {
            AddError(TypeCheckerError.UnionClassVariantInitializerNotClassVariant(initializer.VariantIdentifier));
            return instantiatedUnion;
        }

        initializer.VariantIndex = (uint)variantIndex;

        if (initializer.FieldInitializers.GroupBy(x => x.FieldName.StringValue)
            .Any(x => x.Count() > 1))
        {
            throw new InvalidOperationException("Field can only be initialized once");
        }

        if (initializer.FieldInitializers.Count != classVariant.Fields.Count)
        {
            throw new InvalidOperationException("Not all fields were initialized");
        }

        var fields = classVariant.Fields.ToDictionary(x => x.Name);

        foreach (var fieldInitializer in initializer.FieldInitializers)
        {
            if (fieldInitializer.Value is not null)
            {
                fieldInitializer.Value.ValueUseful = true;
                TypeCheckExpression(fieldInitializer.Value);
            }

            if (!fields.TryGetValue(fieldInitializer.FieldName.StringValue, out var field))
            {
                AddError(TypeCheckerError.UnknownField(fieldInitializer.FieldName, $"union variant {initializer.UnionType.Identifier.StringValue}::{initializer.VariantIdentifier.StringValue}"));
                continue;
            }

            ExpectExpressionType(field.Type, fieldInitializer.Value);

            fieldInitializer.TypeField = field;
        }

        return type;
    }

    private ITypeReference TypeCheckObjectInitializer(
        ObjectInitializerExpression objectInitializerExpression)
    {
        var objectInitializer = objectInitializerExpression.ObjectInitializer;
        var foundType = GetTypeReference(objectInitializer.Type);

        if (foundType is UnknownType)
        {
            // if we don't know what type this is, type check the field initializers anyway
            foreach (var fieldInitializer in objectInitializer.FieldInitializers.Where(x => x.Value is not null))
            {
                TypeCheckExpression(fieldInitializer.Value!);
            }

            return UnknownType.Instance;
        }

        if (foundType is not InstantiatedClass instantiatedClass)
        {
            throw new InvalidOperationException($"Type {foundType} cannot be initialized");
        }

        var classFields = instantiatedClass.GetFields();

        var initializedFields = new HashSet<string>();
        var instanceFields = classFields.Where(x => !x.IsStatic).ToDictionary(x => x.Name);
        var canAccessPrivateFields = CanAccessPrivateMembers(instantiatedClass.Signature);

        var accessibleInstanceFields = classFields
            .Where(x => !x.IsStatic && x.IsPublic || canAccessPrivateFields)
            .Select(x => x.Name)
            .ToHashSet();

        foreach (var fieldInitializer in objectInitializer.FieldInitializers)
        {
            if (fieldInitializer.Value is not null)
            {
                fieldInitializer.Value.ValueUseful = true;
                TypeCheckExpression(fieldInitializer.Value);
            }

            if (!instanceFields.TryGetValue(fieldInitializer.FieldName.StringValue, out var field))
            {
                AddError(TypeCheckerError.UnknownField(fieldInitializer.FieldName, $"class {objectInitializer.Type.Identifier.StringValue}"));
                continue;
            }

            fieldInitializer.TypeField = field;

            if (!CanAccessPrivateMembers(instantiatedClass.Signature) && !accessibleInstanceFields.Contains(fieldInitializer.FieldName.StringValue))
            {
                AddError(TypeCheckerError.PrivateMemberReferenced(fieldInitializer.FieldName));
            }
            // only set field as initialized if it is public
            else if (!initializedFields.Add(fieldInitializer.FieldName.StringValue))
            {
                AddError(TypeCheckerError.ClassFieldSetMultipleTypesInInitializer(fieldInitializer.FieldName));
            }

            if (field.IsMutable && fieldInitializer.Value is not null)
            {
                ExpectMutableExpression(fieldInitializer.Value);
            }

            ExpectExpressionType(field.Type, fieldInitializer.Value);
        }

        if (!canAccessPrivateFields && instanceFields.Count != accessibleInstanceFields.Count)
        {
            AddError(TypeCheckerError.UninitializableType(instantiatedClass.Signature.Name, objectInitializerExpression.SourceRange));
        }
        else if (initializedFields.Count != accessibleInstanceFields.Count)
        {
            AddError(TypeCheckerError.FieldsLeftUnassignedInClassInitializer(
                objectInitializerExpression,
                accessibleInstanceFields.Where(x => !initializedFields.Contains(x))));
        }

        return foundType;
    }
}
