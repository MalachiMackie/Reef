using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private ITypeReference TypeCheckMemberAccess(
        MemberAccessExpression memberAccessExpression)
    {
        var (ownerExpression, stringToken, typeArgumentsIdentifiers) = memberAccessExpression.MemberAccess;
        ownerExpression.ValueUseful = true;
        var ownerType = TypeCheckExpression(ownerExpression);

        if (stringToken is null)
        {
            return UnknownType.Instance;
        }

        while (ownerType is GenericTypeReference genericTypeReference)
        {
            ownerType = genericTypeReference.ResolvedType ?? new UnknownInferredType();
        }

        switch (ownerType)
        {
            case InstantiatedClass classType:
                return TypeCheckClassMemberAccess(classType, memberAccessExpression, stringToken, typeArgumentsIdentifiers,
                    ownerExpression);
            case ArrayType:
                {
                    memberAccessExpression.MemberAccess.MemberType = MemberType.Field;
                    memberAccessExpression.MemberAccess.OwnerType = ownerType;
                    if (memberAccessExpression.MemberAccess.MemberName is not (null or { StringValue: "length" }))
                    {
                        AddError(TypeCheckerError.UnknownTypeMember(memberAccessExpression.MemberAccess.MemberName, "array"));
                        return UnknownType.Instance;
                    }

                    return InstantiatedClass.UInt64;
                }
            case InstantiatedUnion instantiatedUnion:
                return TypeCheckUnionMemberAccess(instantiatedUnion, memberAccessExpression, stringToken, typeArgumentsIdentifiers,
                    ownerExpression);
            case GenericTypeReference or GenericPlaceholder:
                // todo: generic parameter constraints with interfaces?
                AddError(TypeCheckerError.MemberAccessOnGenericExpression(memberAccessExpression));
                return UnknownType.Instance;
            case UnknownType:
                return UnknownType.Instance;
            case UnknownInferredType:
                return UnknownType.Instance;
            case UnspecifiedSizedIntType { ResolvedIntType: var resolvedIntType }:
                {
                    if (resolvedIntType is not null)
                    {
                        return TypeCheckClassMemberAccess(resolvedIntType, memberAccessExpression, stringToken, typeArgumentsIdentifiers, ownerExpression);
                    }

                    // todo: this may actually have members, need to figure that out
                    if (memberAccessExpression.MemberAccess.MemberName is not null)
                    {
                        AddError(TypeCheckerError.UnknownTypeMember(memberAccessExpression.MemberAccess.MemberName, "unspecified sized int"));
                    }
                    return UnknownType.Instance;
                }
            default:
                throw new InvalidOperationException(ownerType.GetType().ToString());
        }
    }

    private ITypeReference TypeCheckClassMemberAccess(
        InstantiatedClass classType,
        MemberAccessExpression memberAccessExpression,
        StringToken stringToken,
        IReadOnlyList<ITypeIdentifier>? typeArgumentsIdentifiers,
        IExpression ownerExpression)
    {
        memberAccessExpression.MemberAccess.OwnerType = classType;

        var typeArguments = (typeArgumentsIdentifiers ?? [])
            .Select(x => (GetTypeReference(x), x.SourceRange)).ToArray();

        if (!TryInstantiateClassFunction(
                classType,
                stringToken.StringValue,
                typeArguments,
                memberAccessExpression.SourceRange,
                out var function))
        {
            if (typeArgumentsIdentifiers is not null)
            {
                AddError(TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(memberAccessExpression.SourceRange));
            }

            if (classType.GetFields().FirstOrDefault(x => x.Name == stringToken.StringValue) is not { } field)
            {
                AddError(TypeCheckerError.UnknownTypeMember(stringToken, classType.Signature.Name));
                return UnknownType.Instance;
            }

            if (!field.IsPublic && !CanAccessPrivateMembers(classType.Signature))
            {
                AddError(TypeCheckerError.PrivateMemberReferenced(stringToken));
            }

            if (field.IsStatic)
            {
                AddError(TypeCheckerError.InstanceMemberAccessOnStaticMember(memberAccessExpression.SourceRange, memberAccessExpression.MemberAccess.MemberName?.StringValue ?? ""));
            }

            memberAccessExpression.MemberAccess.MemberType = MemberType.Field;

            return field.Type;
        }


        if (function.IsStatic)
        {
            AddError(TypeCheckerError.InstanceMemberAccessOnStaticMember(memberAccessExpression.SourceRange, memberAccessExpression.MemberAccess.MemberName?.StringValue ?? ""));
        }

        memberAccessExpression.MemberAccess.MemberType = MemberType.Function;
        memberAccessExpression.MemberAccess.InstantiatedFunction = function;

        if (function.IsMutable)
        {
            ExpectMutableExpression(ownerExpression);
        }

        return new FunctionObject(
            parameters: function.GetParameters(),
            returnType: function.GetReturnType(),
            function.MutableReturn, true);
    }

    private ITypeReference TypeCheckUnionMemberAccess(
        InstantiatedUnion unionType,
        MemberAccessExpression memberAccessExpression,
        StringToken stringToken,
        IReadOnlyList<ITypeIdentifier>? typeArgumentsIdentifiers,
        IExpression ownerExpression)
    {
        memberAccessExpression.MemberAccess.OwnerType = unionType;

        var typeArguments = (typeArgumentsIdentifiers ?? [])
            .Select(x => (GetTypeReference(x), x.SourceRange)).ToArray();

        if (!TryInstantiateUnionFunction(
                unionType,
                stringToken.StringValue,
                typeArguments,
                memberAccessExpression.SourceRange,
                out var function))
        {
            AddError(TypeCheckerError.UnknownTypeMember(stringToken, unionType.Name));
            return UnknownType.Instance;
        }

        if (typeArgumentsIdentifiers is not null)
        {
            AddError(TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(memberAccessExpression.SourceRange));
        }

        if (function.IsStatic)
        {
            AddError(TypeCheckerError.InstanceMemberAccessOnStaticMember(memberAccessExpression.SourceRange, memberAccessExpression.MemberAccess.MemberName?.StringValue ?? ""));
        }

        if (function.IsMutable)
        {
            ExpectMutableExpression(ownerExpression);
        }

        memberAccessExpression.MemberAccess.MemberType = MemberType.Function;
        memberAccessExpression.MemberAccess.InstantiatedFunction = function;

        return new FunctionObject(
            function.GetParameters(),
            function.GetReturnType(),
            function.MutableReturn, true);
    }
}
