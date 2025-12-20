using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private TypeChecking.TypeChecker.ITypeReference TypeCheckMemberAccess(
        MemberAccessExpression memberAccessExpression)
    {
        var (ownerExpression, stringToken, typeArgumentsIdentifiers) = memberAccessExpression.MemberAccess;
        ownerExpression.ValueUseful = true;
        var ownerType = TypeCheckExpression(ownerExpression);

        if (stringToken is null)
        {
            return TypeChecking.TypeChecker.UnknownType.Instance;
        }

        switch (ownerType)
        {
            case TypeChecking.TypeChecker.InstantiatedClass classType:
                return TypeCheckClassMemberAccess(classType, memberAccessExpression, stringToken, typeArgumentsIdentifiers,
                    ownerExpression);
            case TypeChecking.TypeChecker.InstantiatedUnion instantiatedUnion:
                return TypeCheckUnionMemberAccess(instantiatedUnion, memberAccessExpression, stringToken, typeArgumentsIdentifiers,
                    ownerExpression);
            default:
                // todo: generic parameter constraints with interfaces?
                _errors.Add(TypeCheckerError.MemberAccessOnGenericExpression(memberAccessExpression));
                return TypeChecking.TypeChecker.UnknownType.Instance;
        }
    }

    private TypeChecking.TypeChecker.ITypeReference TypeCheckClassMemberAccess(
        TypeChecking.TypeChecker.InstantiatedClass classType,
        MemberAccessExpression memberAccessExpression,
        StringToken stringToken,
        IReadOnlyList<ITypeIdentifier>? typeArgumentsIdentifiers,
        IExpression ownerExpression)
    {
        memberAccessExpression.MemberAccess.OwnerType = classType;

        var typeArguments = (typeArgumentsIdentifiers ?? [])
            .Select<ITypeIdentifier, (TypeChecking.TypeChecker.ITypeReference, SourceRange SourceRange)>(x => (GetTypeReference(x), x.SourceRange)).ToArray();

        if (!TryInstantiateClassFunction(
                classType,
                stringToken.StringValue,
                typeArguments,
                memberAccessExpression.SourceRange,
                out var function))
        {
            if (typeArgumentsIdentifiers is not null)
            {
                _errors.Add(TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(memberAccessExpression.SourceRange));
            }

            if (TryGetClassField(classType, stringToken) is not { } field)
            {
                return TypeChecking.TypeChecker.UnknownType.Instance;
            }

            if (field.IsStatic)
            {
                _errors.Add(TypeCheckerError.InstanceMemberAccessOnStaticMember(memberAccessExpression.SourceRange));
            }

            memberAccessExpression.MemberAccess.MemberType = MemberType.Field;

            return field.Type;
        }


        if (function.IsStatic)
        {
            _errors.Add(TypeCheckerError.InstanceMemberAccessOnStaticMember(memberAccessExpression.SourceRange));
        }

        memberAccessExpression.MemberAccess.MemberType = MemberType.Function;
        memberAccessExpression.MemberAccess.InstantiatedFunction = function;

        if (function.IsMutable)
        {
            ExpectAssignableExpression(ownerExpression);
        }

        return new TypeChecking.TypeChecker.FunctionObject(
            parameters: function.Parameters,
            returnType: function.ReturnType);
    }

    private TypeChecking.TypeChecker.ITypeReference TypeCheckUnionMemberAccess(
        TypeChecking.TypeChecker.InstantiatedUnion unionType,
        MemberAccessExpression memberAccessExpression,
        StringToken stringToken,
        IReadOnlyList<ITypeIdentifier>? typeArgumentsIdentifiers,
        IExpression ownerExpression)
    {
        memberAccessExpression.MemberAccess.OwnerType = unionType;

        var typeArguments = (typeArgumentsIdentifiers ?? [])
            .Select<ITypeIdentifier, (TypeChecking.TypeChecker.ITypeReference, SourceRange SourceRange)>(x => (GetTypeReference(x), x.SourceRange)).ToArray();

        if (!TryInstantiateUnionFunction(
                unionType,
                stringToken.StringValue,
                typeArguments,
                memberAccessExpression.SourceRange,
                out var function))
        {
            _errors.Add(TypeCheckerError.UnknownTypeMember(stringToken, unionType.Name));
            return TypeChecking.TypeChecker.UnknownType.Instance;
        }

        if (typeArgumentsIdentifiers is not null)
        {
            _errors.Add(TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(memberAccessExpression.SourceRange));
        }

        if (function.IsStatic)
        {
            _errors.Add(TypeCheckerError.InstanceMemberAccessOnStaticMember(memberAccessExpression.SourceRange));
        }

        if (function.IsMutable)
        {
            ExpectAssignableExpression(ownerExpression);
        }

        memberAccessExpression.MemberAccess.MemberType = MemberType.Function;
        memberAccessExpression.MemberAccess.InstantiatedFunction = function;

        return new TypeChecking.TypeChecker.FunctionObject(
            function.Parameters,
            function.ReturnType);
    }
}
