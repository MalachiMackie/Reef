using System.Diagnostics;
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
                return TypeCheckClassMemberAccess(classType, classType.Signature, memberAccessExpression, stringToken, typeArgumentsIdentifiers,
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

                    return UInt64();
                }
            case SelfTypeReference { Signature: var signature }:
                {
                    switch (signature)
                    {
                        case UnionSignature unionSignature:
                            {
                                return TypeCheckUnionMemberAccess(ownerType, unionSignature, memberAccessExpression, stringToken, typeArgumentsIdentifiers, ownerExpression);
                            }
                        case ClassSignature classSignature:
                            {
                                return TypeCheckClassMemberAccess(ownerType, classSignature, memberAccessExpression, stringToken, typeArgumentsIdentifiers, ownerExpression);
                            }
                        default:
                            throw new UnreachableException(signature.GetType().ToString());
                    }
                }
            case InstantiatedUnion instantiatedUnion:
                return TypeCheckUnionMemberAccess(instantiatedUnion, instantiatedUnion.Signature, memberAccessExpression, stringToken, typeArgumentsIdentifiers,
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
                        return TypeCheckClassMemberAccess(resolvedIntType, resolvedIntType.Signature, memberAccessExpression, stringToken, typeArgumentsIdentifiers, ownerExpression);
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
        ITypeReference ownerType,
        ClassSignature classSignature,
        MemberAccessExpression memberAccessExpression,
        StringToken stringToken,
        IReadOnlyList<ITypeIdentifier>? typeArgumentsIdentifiers,
        IExpression ownerExpression)
    {
        memberAccessExpression.MemberAccess.OwnerType = ownerType;

        var typeArguments = (typeArgumentsIdentifiers ?? [])
            .Select(x => (GetTypeReference(x), x.SourceRange)).ToArray();

        if (!TryInstantiateClassFunction(
                ownerType,
                classSignature,
                stringToken.StringValue,
                typeArguments,
                memberAccessExpression.SourceRange,
                out var function))
        {
            if (typeArgumentsIdentifiers is not null)
            {
                AddError(TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(memberAccessExpression.SourceRange));
            }

            var fields = (ownerType as InstantiatedClass)?.GetFields() ?? classSignature.Fields;

            if (fields.FirstOrDefault(x => x.Name == stringToken.StringValue) is not { } field)
            {
                AddError(TypeCheckerError.UnknownTypeMember(stringToken, classSignature.Name));
                return UnknownType.Instance;
            }

            if (!field.IsPublic && !CanAccessPrivateMembers(classSignature))
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

        var ownerIsBoxed = IsTypeReferenceBoxed(ownerExpression.ResolvedType.NotNull());

        foreach (var constraint in function.Signature.SelfConstraints)
        {
            switch (constraint)
            {
                case BoxedTypeConstraint:
                    {
                        if (!ownerIsBoxed)
                        {
                            AddError(TypeCheckerError.MethodConstrainedToBoxedInstances(stringToken));
                        }
                        break;
                    }
                case UnboxedTypeConstraint:
                    {
                        if (ownerIsBoxed)
                        {
                            AddError(TypeCheckerError.MethodConstrainedToUnboxedInstances(stringToken));
                        }
                        break;
                    }
                default:
                    throw new UnreachableException(constraint.GetType().ToString());
            }
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
        ITypeReference ownerType,
        UnionSignature unionSignature,
        MemberAccessExpression memberAccessExpression,
        StringToken stringToken,
        IReadOnlyList<ITypeIdentifier>? typeArgumentsIdentifiers,
        IExpression ownerExpression)
    {
        memberAccessExpression.MemberAccess.OwnerType = ownerType;

        var typeArguments = (typeArgumentsIdentifiers ?? [])
            .Select(x => (GetTypeReference(x), x.SourceRange)).ToArray();

        if (!TryInstantiateUnionFunction(
                ownerType,
                unionSignature,
                stringToken.StringValue,
                typeArguments,
                memberAccessExpression.SourceRange,
                out var function))
        {
            AddError(TypeCheckerError.UnknownTypeMember(stringToken, unionSignature.Name));
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

        foreach (var constraint in function.Signature.SelfConstraints)
        {
            switch (constraint)
            {
                case BoxedTypeConstraint:
                    {
                        if (!IsTypeReferenceBoxed(ownerExpression.ResolvedType.NotNull()))
                        {
                            AddError(TypeCheckerError.MethodConstrainedToBoxedInstances(stringToken));
                        }
                        break;
                    }
                case UnboxedTypeConstraint:
                    {
                        if (IsTypeReferenceBoxed(ownerExpression.ResolvedType.NotNull()))
                        {
                            AddError(TypeCheckerError.MethodConstrainedToUnboxedInstances(stringToken));
                        }
                        break;
                    }
                default:
                    throw new UnreachableException(constraint.GetType().ToString());
            }
        }

        memberAccessExpression.MemberAccess.MemberType = MemberType.Function;
        memberAccessExpression.MemberAccess.InstantiatedFunction = function;

        return new FunctionObject(
            function.GetParameters(),
            function.GetReturnType(),
            function.MutableReturn, true);
    }
}
