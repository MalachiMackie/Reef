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
            if (genericTypeReference.ResolvedType is null)
            {
                throw new NotImplementedException();
            }

            ownerType = genericTypeReference.ResolvedType;
        }
        
        switch (ownerType)
        {
            case InstantiatedClass classType:
                return TypeCheckClassMemberAccess(classType, memberAccessExpression, stringToken, typeArgumentsIdentifiers,
                    ownerExpression);
            case InstantiatedUnion instantiatedUnion:
                return TypeCheckUnionMemberAccess(instantiatedUnion, memberAccessExpression, stringToken, typeArgumentsIdentifiers,
                    ownerExpression);
            default:
                // todo: generic parameter constraints with interfaces?
                AddError(TypeCheckerError.MemberAccessOnGenericExpression(memberAccessExpression));
                return UnknownType.Instance;
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
            .Select<ITypeIdentifier, (ITypeReference, SourceRange SourceRange)>(x => (GetTypeReference(x), x.SourceRange)).ToArray();

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

            if (TryGetClassField(classType, stringToken) is not { } field)
            {
                return UnknownType.Instance;
            }

            if (field.IsStatic)
            {
                AddError(TypeCheckerError.InstanceMemberAccessOnStaticMember(memberAccessExpression.SourceRange));
            }

            memberAccessExpression.MemberAccess.MemberType = MemberType.Field;

            return field.Type;
        }


        if (function.IsStatic)
        {
            AddError(TypeCheckerError.InstanceMemberAccessOnStaticMember(memberAccessExpression.SourceRange));
        }

        memberAccessExpression.MemberAccess.MemberType = MemberType.Function;
        memberAccessExpression.MemberAccess.InstantiatedFunction = function;

        if (function.IsMutable)
        {
            ExpectAssignableExpression(ownerExpression);
        }

        return new FunctionObject(
            parameters: function.Parameters,
            returnType: function.ReturnType,
            function.MutableReturn);
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
            .Select<ITypeIdentifier, (ITypeReference, SourceRange SourceRange)>(x => (GetTypeReference(x), x.SourceRange)).ToArray();

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
            AddError(TypeCheckerError.InstanceMemberAccessOnStaticMember(memberAccessExpression.SourceRange));
        }

        if (function.IsMutable)
        {
            ExpectAssignableExpression(ownerExpression);
        }

        memberAccessExpression.MemberAccess.MemberType = MemberType.Function;
        memberAccessExpression.MemberAccess.InstantiatedFunction = function;

        return new FunctionObject(
            function.Parameters,
            function.ReturnType,
            function.MutableReturn);
    }
}
