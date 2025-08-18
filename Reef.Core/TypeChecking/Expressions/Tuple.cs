using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    private ITypeReference TypeCheckTupleExpression(TupleExpression tuple)
    {
        foreach (var value in tuple.Values)
        {
            value.ValueUseful = true;
        }
        
        if (tuple.Values.Count == 1)
        {
            return TypeCheckExpression(tuple.Values[0]);
        }

        var types = tuple.Values.Select(value => (TypeCheckExpression(value), value.SourceRange)).ToArray();

        return InstantiateTuple(types, tuple.SourceRange);
    }
}
