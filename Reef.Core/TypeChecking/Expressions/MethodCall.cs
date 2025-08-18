using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{

private ITypeReference TypeCheckMethodCall(
        MethodCallExpression methodCallExpression)
    {
        var methodCall = methodCallExpression.MethodCall;
        methodCall.Method.ValueUseful = true;
        var methodType = TypeCheckExpression(methodCall.Method);

        if (methodType is UnknownType)
        {
            // type check arguments even if we don't know what the type is
            foreach (var argument in methodCall.ArgumentList)
            {
                TypeCheckExpression(argument);
            }
            
            return UnknownType.Instance;
        }

        if (methodType is not IFunction functionType)
        {
            throw new InvalidOperationException($"{methodType} is not callable");
        }

        if (methodCall.ArgumentList.Count != functionType.Parameters.Count)
        {
            _errors.Add(TypeCheckerError.IncorrectNumberOfMethodArguments(
                methodCallExpression, functionType.Parameters.Count));

            foreach (var argument in methodCall.ArgumentList)
            {
                TypeCheckExpression(argument);
            }

            return functionType.ReturnType;
        }

        for (var i = 0; i < functionType.Parameters.Count; i++)
        {
            var parameter = functionType.Parameters[i];
            var expectedParameterType = parameter.Type;
            var isParameterMutable = parameter.Mutable;

            var argumentExpression = methodCall.ArgumentList[i];
            argumentExpression.ValueUseful = true;
            TypeCheckExpression(argumentExpression);

            ExpectExpressionType(expectedParameterType, argumentExpression);

            if (isParameterMutable)
            {
                ExpectAssignableExpression(argumentExpression);
            }
        }

        return functionType.ReturnType;
    }
}
