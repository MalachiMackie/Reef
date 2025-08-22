using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private InstantiatedClass TypeCheckMatchesExpression(MatchesExpression matchesExpression)
    {
        matchesExpression.ValueUseful = true;
        var valueType = TypeCheckExpression(matchesExpression.ValueExpression);

        if (matchesExpression.Pattern is null)
        {
            return InstantiatedClass.Boolean;
        }

        matchesExpression.DeclaredVariables =
            TypeCheckPattern(valueType, matchesExpression.Pattern);

        if (matchesExpression.DeclaredVariables.Any(x => x.Mutable))
        {
            ExpectAssignableExpression(matchesExpression.ValueExpression);
        }

        return InstantiatedClass.Boolean;
    }
}
