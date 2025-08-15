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
                _errors.Add(TypeCheckerError.MemberAccessOnGenericExpression(memberAccessExpression));
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
            .Select(x => (GetTypeReference(x), x.SourceRange)).ToArray();
        
        if (!TryInstantiateClassFunction(
                classType,
                stringToken.StringValue,
                typeArguments,
                memberAccessExpression.SourceRange,
                out var function,
                out var functionIndex))
        {
            if (typeArgumentsIdentifiers is not null)
            {
                _errors.Add(TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(memberAccessExpression.SourceRange));
            }

            if (TryGetClassField(classType, stringToken) is not { } field)
            {
                return UnknownType.Instance;
            }

            if (field.IsStatic)
            {
                _errors.Add(TypeCheckerError.InstanceMemberAccessOnStaticMember(memberAccessExpression.SourceRange));
            }

            memberAccessExpression.MemberAccess.MemberType = MemberType.Field;
            memberAccessExpression.MemberAccess.ItemIndex = field.FieldIndex;

            return field.Type;
        }
        
        
        if (function.IsStatic)
        {
            _errors.Add(TypeCheckerError.InstanceMemberAccessOnStaticMember(memberAccessExpression.SourceRange));
        }

        memberAccessExpression.MemberAccess.MemberType = MemberType.Function;
        memberAccessExpression.MemberAccess.ItemIndex = functionIndex;
        memberAccessExpression.MemberAccess.InstantiatedFunction = function;

        if (function.IsMutable)
        {
            ExpectAssignableExpression(ownerExpression);
        }

        return new FunctionObject(
            parameters: function.Parameters,
            returnType: function.ReturnType);
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
            _errors.Add(TypeCheckerError.UnknownTypeMember(stringToken, unionType.Name));
            return UnknownType.Instance;
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
        memberAccessExpression.MemberAccess.ItemIndex = function.FunctionIndex;
        memberAccessExpression.MemberAccess.InstantiatedFunction = function;

        return new FunctionObject(
            function.Parameters,
            function.ReturnType);
    }
}
