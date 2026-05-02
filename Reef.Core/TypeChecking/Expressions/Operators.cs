using System.Diagnostics;
using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{

    private ITypeReference TypeCheckBinaryOperatorExpression(
            BinaryOperatorExpression binaryOperatorExpression)
    {
        var @operator = binaryOperatorExpression.BinaryOperator;
        if (@operator.Left is not null)
            @operator.Left.ValueUseful = true;
        if (@operator.Right is not null)
            @operator.Right.ValueUseful = true;


        switch (@operator.OperatorType)
        {
            case BinaryOperatorType.LessThan:
            case BinaryOperatorType.GreaterThan:
                {
                    if (@operator.Left is not null)
                        TypeCheckExpression(@operator.Left);
                    if (@operator.Right is not null)
                        TypeCheckExpression(@operator.Right);

                    if (ExpectExpressionType(IntTypes(), @operator.Left))
                    {
                        ExpectExpressionType(@operator.Left.NotNull().ResolvedType.NotNull(), @operator.Right);
                    }

                    return Boolean();
                }
            case BinaryOperatorType.Plus:
            case BinaryOperatorType.Minus:
            case BinaryOperatorType.Multiply:
            case BinaryOperatorType.Divide:
                {
                    if (@operator.Left is not null)
                        TypeCheckExpression(@operator.Left);
                    if (@operator.Right is not null)
                        TypeCheckExpression(@operator.Right);

                    if (ExpectExpressionType(IntTypes(), @operator.Left))
                    {
                        ExpectExpressionType(@operator.Left.NotNull().ResolvedType.NotNull(), @operator.Right);
                        return @operator.Left.NotNull().ResolvedType.NotNull();
                    }

                    // this is the failure case
                    return Int32();
                }
            case BinaryOperatorType.NegativeEqualityCheck:
            case BinaryOperatorType.EqualityCheck:
                {
                    // todo: use interface. left and right implements IEquals<T>
                    if (@operator.Left is not null)
                        TypeCheckExpression(@operator.Left);
                    if (@operator.Right is not null)
                        TypeCheckExpression(@operator.Right);

                    if (ExpectExpressionType([.. IntTypes(), Boolean()], @operator.Left))
                    {
                        ExpectExpressionType(@operator.Left.NotNull().ResolvedType.NotNull(), @operator.Right);
                    }

                    return Boolean();
                }
            case BinaryOperatorType.BooleanAnd:
            case BinaryOperatorType.BooleanOr:
                {
                    if (@operator.Left is not null)
                    {
                        ExpectType(TypeCheckExpression(@operator.Left), Boolean(),
                            @operator.Left.SourceRange);
                    }
                    if (@operator.Right is not null)
                    {
                        ExpectType(TypeCheckExpression(@operator.Right), Boolean(),
                            @operator.Right.SourceRange);
                    }
                    return Boolean();
                }
            case BinaryOperatorType.ValueAssignment:
                {
                    ITypeReference leftType = UnknownType.Instance;
                    if (@operator.Left is not null)
                    {
                        leftType = TypeCheckExpression(@operator.Left, allowUninstantiatedVariable: true);
                        // we don't actually want the result of this value
                        @operator.Left.ValueUseful = false;
                        if (leftType is not UnknownType)
                        {
                            ExpectAssignableExpression(@operator.Left);
                        }
                    }
                    var rightType = @operator.Right is null
                        ? UnknownType.Instance
                        : TypeCheckExpression(@operator.Right);

                    if (@operator.Left is ValueAccessorExpression
                        {
                            ValueAccessor: { AccessType: ValueAccessType.Variable, Token: StringToken variableName },
                        } && leftType is not UnknownType)
                    {
                        var variable = GetScopedVariable(variableName.StringValue);

                        if (variable is LocalVariable { Instantiated: false } localVariable)
                        {
                            localVariable.Instantiated = true;
                            if (localVariable.Type is UnknownInferredType { ResolvedType: null } unknownInferredType)
                            {
                                unknownInferredType.ResolvedType = rightType;
                            }
                        }

                        if (variable is FieldVariable { IsStaticField: false }
                            && CurrentFunctionSignature is not { IsMutable: true })
                        {
                            AddError(TypeCheckerError.MutatingInstanceInNonMutableFunction(
                                CurrentFunctionSignature!.Name,
                                binaryOperatorExpression.SourceRange));
                        }
                    }

                    ExpectExpressionType(leftType, @operator.Right);

                    return leftType;
                }
            default:
                throw new UnreachableException(@operator.OperatorType.ToString());
        }
    }
    private ITypeReference TypeCheckUnaryOperator(UnaryOperator unaryOperator, IExpression unaryExpression)
    {
        return unaryOperator.OperatorType switch
        {
            UnaryOperatorType.FallOut => TypeCheckFallout(unaryOperator.Operand, unaryExpression),
            UnaryOperatorType.Not => TypeCheckNot(unaryOperator.Operand),
            UnaryOperatorType.Negate => TypeCheckNegate(unaryOperator.Operand),
            _ => throw new UnreachableException($"{unaryOperator.OperatorType}")
        };
    }

    private ITypeReference TypeCheckNegate(IExpression? expression)
    {
        if (expression is not null)
        {
            expression.ValueUseful = true;
            TypeCheckExpression(expression);
        }

        ExpectExpressionType(
            [Int8(), Int16(), Int32(), Int64()],
            expression);

        return expression?.ResolvedType ?? new UnspecifiedSizedIntType()
        {
            Boxed = false,
        };
    }

    private InstantiatedClass TypeCheckNot(IExpression? expression)
    {
        if (expression is not null)
        {
            expression.ValueUseful = true;
            TypeCheckExpression(expression);
        }

        ExpectExpressionType(Boolean(), expression);

        return Boolean();
    }

    private ITypeReference TypeCheckFallout(IExpression? operandExpression, IExpression falloutExpression)
    {
        if (operandExpression is not null)
        {
            operandExpression.ValueUseful = true;
            TypeCheckExpression(operandExpression);
        }

        InstantiatedUnion? returnTypeResultUnion = null;

        // todo: could implement with an interface? union Result : IFallout?
        if (ExpectedReturnType is InstantiatedUnion { Signature.Id: var resultId } union && resultId == DefId.Result)
        {
            returnTypeResultUnion = union;
        }
        else
        {
            AddError(TypeCheckerError.UnsupportedFalloutReturnType(ExpectedReturnType ?? Unit(), falloutExpression.SourceRange));
        }

        ITypeReference? synthesizedReturnType = null;
        if (returnTypeResultUnion is not null)
        {
            var expectedErrorType = returnTypeResultUnion.TypeArguments.First(x => x.GenericName == "TError")
                .ResolvedType.NotNull();

            // synthesize a return type with only the error generic populated so that we don't
            // check the value generic. This is so the following successfully type checks:
            // fn SomeFn(): result::<string, int> {
            //     var someResult: result::<int, int> = todo!;
            //     var a: int = someResult?;
            // }
            var resultType = InstantiateUnion(
                returnTypeResultUnion.Signature,
                [],
                boxingSpecifier: returnTypeResultUnion.Boxed
                    ? Token.Boxed(SourceSpan.Default)
                    : Token.Unboxed(SourceSpan.Default),
                sourceRange: SourceRange.Default);
            resultType.TypeArguments.First(x => x.GenericName == "TError")
                .ResolvedType = expectedErrorType;
            synthesizedReturnType = resultType;
        }

        if (operandExpression is { ResolvedType: InstantiatedUnion { TypeArguments: [{ GenericName: "TValue" } valueGeneric, ..], Signature.Id: var id } } && id == DefId.Result)
        {
            if (synthesizedReturnType is not null)
            {
                ExpectExpressionType(synthesizedReturnType, operandExpression);
            }
            return valueGeneric;
        }
        else
        {
            if (operandExpression is { ResolvedType: { } resolvedType })
            {
                AddError(TypeCheckerError.UnsupportedFalloutOperandType(resolvedType, falloutExpression.SourceRange));
                return resolvedType;
            }
        }

        // if expression is null or it's type is incorrect, then return the value type from the return type
        if (returnTypeResultUnion is not null)
        {
            return returnTypeResultUnion.TypeArguments.First(x => x.GenericName == "TValue");
        }

        return UnknownType.Instance;
    }

}
