using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private InstantiatedClass TypeCheckMethodReturn(
        MethodReturnExpression methodReturnExpression)
    {
        methodReturnExpression.ValueUseful = true;
        if (methodReturnExpression.MethodReturn.Expression is null)
        {
            // no inner expression to check the type of, but we know the type is unit
            ExpectType(InstantiatedClass.Unit, ExpectedReturnType,
                methodReturnExpression.SourceRange);
        }
        else
        {
            methodReturnExpression.MethodReturn.Expression.ValueUseful = true;
            TypeCheckExpression(methodReturnExpression.MethodReturn.Expression);
            ExpectExpressionType(ExpectedReturnType, methodReturnExpression.MethodReturn.Expression);
        }

        return InstantiatedClass.Never;
    }
}
