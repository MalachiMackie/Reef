using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private TypeChecking.TypeChecker.InstantiatedClass TypeCheckMatchesExpression(MatchesExpression matchesExpression)
    {
        var valueType = TypeCheckExpression(matchesExpression.ValueExpression);
        matchesExpression.ValueExpression.ValueUseful = true;

        if (matchesExpression.Pattern is null)
        {
            return TypeChecking.TypeChecker.InstantiatedClass.Boolean;
        }

        matchesExpression.DeclaredVariables =
            TypeCheckPattern(valueType, matchesExpression.Pattern);

        if (matchesExpression.DeclaredVariables.Any<TypeChecking.TypeChecker.LocalVariable>(x => x.Mutable))
        {
            ExpectAssignableExpression(matchesExpression.ValueExpression);
        }

        return TypeChecking.TypeChecker.InstantiatedClass.Boolean;
    }
}
