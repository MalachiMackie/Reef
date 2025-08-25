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
                    ExpectExpressionType(InstantiatedClass.Int, @operator.Left);
                    ExpectExpressionType(InstantiatedClass.Int, @operator.Right);

                    return InstantiatedClass.Boolean;
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

                    ExpectExpressionType(InstantiatedClass.Int, @operator.Left);
                    ExpectExpressionType(InstantiatedClass.Int, @operator.Right);

                    return InstantiatedClass.Int;
                }
            case BinaryOperatorType.EqualityCheck:
                {
                    // todo: use interface. left and right implements IEquals<T>
                    if (@operator.Left is not null)
                    {
                        ExpectType(TypeCheckExpression(@operator.Left), InstantiatedClass.Int, @operator.Left.SourceRange);
                    }
                    if (@operator.Right is not null)
                    {
                        ExpectType(TypeCheckExpression(@operator.Right), InstantiatedClass.Int, @operator.Right.SourceRange);
                    }

                    return InstantiatedClass.Boolean;
                }
            case BinaryOperatorType.BooleanAnd:
            case BinaryOperatorType.BooleanOr:
                {
                    if (@operator.Left is not null)
                    {
                        ExpectType(TypeCheckExpression(@operator.Left), InstantiatedClass.Boolean,
                            @operator.Left.SourceRange);
                    }
                    if (@operator.Right is not null)
                    {
                        ExpectType(TypeCheckExpression(@operator.Right), InstantiatedClass.Boolean,
                            @operator.Right.SourceRange);
                    }
                    return InstantiatedClass.Boolean;
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

                        if (variable is FieldVariable fieldVariable
                                && !fieldVariable.IsStaticField
                                && CurrentFunctionSignature is not { IsMutable: true })
                        {
                            _errors.Add(TypeCheckerError.MutatingInstanceInNonMutableFunction(
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
    private ITypeReference TypeCheckUnaryOperator(UnaryOperator unaryOperator)
    {
        return unaryOperator.OperatorType switch
        {
            UnaryOperatorType.FallOut => TypeCheckFallout(unaryOperator.Operand),
            UnaryOperatorType.Not => TypeCheckNot(unaryOperator.Operand),
            _ => throw new UnreachableException($"{unaryOperator.OperatorType}")
        };
    }

    private InstantiatedClass TypeCheckNot(IExpression? expression)
    {
        if (expression is not null)
        {
            expression.ValueUseful = true;
            TypeCheckExpression(expression);
        }

        ExpectExpressionType(InstantiatedClass.Boolean, expression);

        return InstantiatedClass.Boolean;
    }

    private GenericTypeReference TypeCheckFallout(IExpression? expression)
    {
        if (expression is not null)
        {
            expression.ValueUseful = true;
            TypeCheckExpression(expression);
        }

        // todo: could implement with an interface? union Result : IFallout?
        if (ExpectedReturnType is not InstantiatedUnion { Name: "result" or "option" } union)
        {
            throw new InvalidOperationException("Fallout operator is only valid for Result and Option return types");
        }

        ExpectExpressionType(ExpectedReturnType, expression);

        if (union.Name == UnionSignature.Result.Name)
        {
            return union.TypeArguments.First(x => x.GenericName == "TValue");
        }

        if (union.Name == "Option")
        {
            throw new NotImplementedException("");
        }

        throw new UnreachableException();
    }

}

