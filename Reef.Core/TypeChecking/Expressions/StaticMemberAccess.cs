using System.Diagnostics;

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
            .Select(x => (GetTypeReference(x), x.SourceRange))
            .ToArray();
        
        switch (type)
        {
            case InstantiatedClass { Fields: var fields } instantiatedClass:
            {
                var field = fields.FirstOrDefault(x => x.Name == memberName);
                if (field is not null)
                {
                    staticMemberAccess.MemberType = MemberType.Field;
                    staticMemberAccess.ItemIndex = field.FieldIndex;
                    
                    if (staticMemberAccess.TypeArguments is not null)
                    {
                        _errors.Add(TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(staticMemberAccessExpression.SourceRange));
                    }

                    if (!field.IsStatic)
                    {
                        _errors.Add(TypeCheckerError.StaticMemberAccessOnInstanceMember(staticMemberAccessExpression.SourceRange));
                    }
                    
                    return field.Type;
                }

                if (!TryInstantiateClassFunction(
                        instantiatedClass,
                        memberName,
                        typeArguments,
                        staticMemberAccessExpression.SourceRange,
                        out var function,
                        out var functionIndex))
                {
                    _errors.Add(TypeCheckerError.UnknownTypeMember(staticMemberAccess.MemberName!, instantiatedClass.Signature.Name));
                    return UnknownType.Instance;
                }

                if (!function.IsStatic)
                {
                    _errors.Add(TypeCheckerError.StaticMemberAccessOnInstanceMember(staticMemberAccessExpression.SourceRange));
                }

                staticMemberAccess.ItemIndex = functionIndex;
                staticMemberAccess.MemberType = MemberType.Function;

                staticMemberAccess.InstantiatedFunction = function;

                return new FunctionObject(function.Parameters, function.ReturnType);

            }
            case InstantiatedUnion instantiatedUnion:
            {
                var (variantIndex, variant) = instantiatedUnion.Variants.Index().FirstOrDefault(x => x.Item.Name == memberName);
                if (variant is not null)
                {
                    staticMemberAccess.MemberType = MemberType.Variant;
                    staticMemberAccess.ItemIndex = (uint)variantIndex;
                    
                    if (staticMemberAccess.TypeArguments is not null)
                    {
                        _errors.Add(TypeCheckerError.GenericTypeArgumentsOnNonFunctionValue(staticMemberAccessExpression.SourceRange));
                    }
                    
                    switch (variant)
                    {
                        case TupleUnionVariant tupleVariant:
                        {
                            var tupleVariantFunction = GetUnionTupleVariantFunction(tupleVariant, instantiatedUnion);
                            staticMemberAccess.InstantiatedFunction = tupleVariantFunction;

                            return new FunctionObject(
                                tupleVariantFunction.Parameters,
                                tupleVariantFunction.ReturnType);
                        }
                        case UnitUnionVariant:
                            return type;
                        case ClassUnionVariant:
                            _errors.Add(TypeCheckerError.UnionClassVariantWithoutInitializer(staticMemberAccessExpression.SourceRange));
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
                    _errors.Add(TypeCheckerError.UnknownTypeMember(staticMemberAccess.MemberName!, instantiatedUnion.Name));
                    return UnknownType.Instance;
                }

                if (!function.IsStatic)
                {
                    _errors.Add(TypeCheckerError.StaticMemberAccessOnInstanceMember(staticMemberAccessExpression.SourceRange));
                }

                staticMemberAccess.MemberType = MemberType.Function;
                staticMemberAccess.ItemIndex = function.FunctionIndex;
                staticMemberAccess.InstantiatedFunction = function;

                return new FunctionObject(
                    function.Parameters,
                    function.ReturnType);
            }
            case GenericTypeReference or GenericPlaceholder:
                _errors.Add(TypeCheckerError.StaticMemberAccessOnGenericReference(staticMemberAccessExpression));
                return UnknownType.Instance;
            default:
                throw new UnreachableException(type.GetType().ToString());
        }
    }
}
