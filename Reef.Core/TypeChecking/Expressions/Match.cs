using System.Diagnostics;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
private ITypeReference TypeCheckMatchExpression(MatchExpression matchExpression)
    {
        matchExpression.Value.ValueUseful = true;
        var valueType = TypeCheckExpression(matchExpression.Value);

        ITypeReference? foundType = null;

        foreach (var arm in matchExpression.Arms)
        {
            var patternVariables = TypeCheckPattern(valueType, arm.Pattern);

            var anyMutableVariables = false;
            using var _ = PushScope();
            foreach (var variable in patternVariables)
            {
                anyMutableVariables |= variable.Mutable;
                variable.Instantiated = true;
            }

            if (anyMutableVariables)
            {
                ExpectAssignableExpression(matchExpression.Value);
            }

            if (arm.Expression is not null)
            {
                arm.Expression.ValueUseful = true;
            }

            var armType = arm.Expression is null
                ? UnknownType.Instance
                : TypeCheckExpression(arm.Expression);
            
            foundType ??= armType;

            ExpectExpressionType(foundType, arm.Expression);

            foreach (var variable in patternVariables)
            {
                variable.Instantiated = false;
            }
        }

        return foundType ?? throw new UnreachableException("Parser checked match expression has at least one arm");
    }
}
