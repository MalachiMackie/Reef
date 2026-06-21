using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private TypeChecking.TypeChecker.InstantiatedClass TypeCheckMatchesExpression(MatchesExpression matchesExpression)
    {
        matchesExpression.ValueExpression.ValueUseful = true;
        var valueType = TypeCheckExpression(matchesExpression.ValueExpression);

        if (matchesExpression.Pattern is null)
        {
            return Boolean();
        }

        matchesExpression.DeclaredVariables =
            TypeCheckPattern(valueType, matchesExpression.Pattern);

        if (matchesExpression.DeclaredVariables.Any<TypeChecking.TypeChecker.LocalVariable>(x => x.Mutable))
        {
            ExpectMutableExpression(matchesExpression.ValueExpression);
        }

        return Boolean();
    }
}
