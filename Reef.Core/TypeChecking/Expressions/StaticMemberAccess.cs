using System.Diagnostics;
using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private ITypeReference TypeCheckStaticMemberAccess(
        StaticMemberAccessExpression staticMemberAccessExpression)
    {
        var staticMemberAccess = staticMemberAccessExpression.StaticMemberAccess;
        var type = GetTypeReference(staticMemberAccess.Type);

        staticMemberAccessExpression.OwnerType = type;

        var memberName = staticMemberAccess.MemberName?.StringValue;
        if (memberName is null)
        {
            return UnknownType.Instance;
        }

        var typeArguments = (staticMemberAccess.TypeArguments ?? [])
            .Select<ITypeIdentifier, (ITypeReference, SourceRange SourceRange)>(x => (GetTypeReference(x), x.SourceRange))
            .ToArray();

        switch (type)
        {
            case InstantiatedClass { Fields: var fields } instantiatedClass:
                {
                    var field = fields.FirstOrDefault(x => x.Name == memberName);
                    if (field is not null)
                    {
                        staticMemberAccess.MemberType = MemberType.Field;

                        if (staticMemberAccess.TypeArguments is not null)
                        {
                            AddError(TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(staticMemberAccessExpression.SourceRange));
                        }

                        if (!field.IsStatic)
                        {
                            AddError(TypeCheckerError.StaticMemberAccessOnInstanceMember(staticMemberAccessExpression.SourceRange));
                        }

                        return field.Type;
                    }

                    if (!TryInstantiateClassFunction(
                            instantiatedClass,
                            memberName,
                            typeArguments,
                            staticMemberAccessExpression.SourceRange,
                            out var function))
                    {
                        AddError(TypeCheckerError.UnknownTypeMember(staticMemberAccess.MemberName!, instantiatedClass.Signature.Name));
                        return UnknownType.Instance;
                    }

                    if (!function.IsStatic)
                    {
                        AddError(TypeCheckerError.StaticMemberAccessOnInstanceMember(staticMemberAccessExpression.SourceRange));
                    }

                    staticMemberAccess.MemberType = MemberType.Function;

                    staticMemberAccess.InstantiatedFunction = function;

                    return new FunctionObject(function.Parameters, function.ReturnType, function.MutableReturn);

                }
            case InstantiatedUnion instantiatedUnion:
                {
                    var variant = instantiatedUnion.Variants.FirstOrDefault(x => x.Name == memberName);
                    if (variant is not null)
                    {
                        staticMemberAccess.MemberType = MemberType.Variant;

                        if (staticMemberAccess.TypeArguments is not null)
                        {
                            AddError(TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(staticMemberAccessExpression.SourceRange));
                        }

                        switch (variant)
                        {
                            case TupleUnionVariant tupleVariant:
                                {
                                    var tupleVariantFunction = GetUnionTupleVariantFunction(tupleVariant, instantiatedUnion);
                                    staticMemberAccess.InstantiatedFunction = tupleVariantFunction;

                                    return new FunctionObject(
                                        tupleVariantFunction.Parameters,
                                        tupleVariantFunction.ReturnType,
                                        isMutableReturn: true);
                                }
                            case UnitUnionVariant:
                                return type;
                            case ClassUnionVariant:
                                AddError(TypeCheckerError.UnionClassVariantWithoutInitializer(staticMemberAccessExpression.SourceRange));
                                return type;
                            default:
                                throw new UnreachableException();
                        }
                    }

                    if (!TryInstantiateUnionFunction(instantiatedUnion,
                            memberName,
                            typeArguments,
                            staticMemberAccessExpression.SourceRange,
                            out var function))
                    {
                        AddError(TypeCheckerError.UnknownTypeMember(staticMemberAccess.MemberName!, instantiatedUnion.Name));
                        return UnknownType.Instance;
                    }

                    if (!function.IsStatic)
                    {
                        AddError(TypeCheckerError.StaticMemberAccessOnInstanceMember(staticMemberAccessExpression.SourceRange));
                    }

                    staticMemberAccess.MemberType = MemberType.Function;
                    staticMemberAccess.InstantiatedFunction = function;

                    return new FunctionObject(
                        function.Parameters,
                        function.ReturnType,
                        function.MutableReturn);
                }
            case GenericTypeReference or GenericPlaceholder:
                AddError(TypeCheckerError.StaticMemberAccessOnGenericReference(staticMemberAccessExpression));
                return UnknownType.Instance;
            default:
                throw new UnreachableException(type.GetType().ToString());
        }
    }
}
