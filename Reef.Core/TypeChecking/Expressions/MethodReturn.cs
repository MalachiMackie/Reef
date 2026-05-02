using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private InstantiatedClass TypeCheckMethodReturn(
        MethodReturnExpression methodReturnExpression)
    {
        methodReturnExpression.ValueUseful = true;
        if (methodReturnExpression.MethodReturn.Expression is { } value)
        {
            value.ValueUseful = true;
            TypeCheckExpression(value);
            if (CurrentFunctionSignature!.IsMutableReturn
                && !ExpectMutableExpression(value, report: false))
            {
                AddError(TypeCheckerError.NonMutableExpressionPassedToMutableReturn(methodReturnExpression.SourceRange));
            }

            ExpectExpressionType(ExpectedReturnType ?? Unit(), value);
        }
        else
        {
            // no inner expression to check the type of, but we know the type is unit
            ExpectType(Unit(), ExpectedReturnType ?? Unit(),
                methodReturnExpression.SourceRange);
        }

        return Never();
    }
}
