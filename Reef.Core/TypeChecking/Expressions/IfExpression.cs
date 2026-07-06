using System.Diagnostics.CodeAnalysis;
using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{
    public sealed class VariableIfInstantiation
    {
        public bool InstantiatedInBody { get; set; }
        public bool InstantiatedInElse { get; set; }
        public bool InstantiatedInEachElseIf { get; set; } = true;
    }

    private static bool IsExpressionType<TExpression>(
        IExpression expression,
        [NotNullWhen(true)] out TExpression? resolvedType)
        where TExpression : class, IExpression
    {
        while (expression is TupleExpression tuple
            && tuple.Values.Count == 1)
        {
            expression = tuple.Values[0];
        }
        if (expression is TExpression tExpression)
        {
            resolvedType = tExpression;
            return true;
        }

        resolvedType = null;
        return false;
    }

    private static IReadOnlyList<LocalVariable> GetExpressionUninitializedDeclaredVariables(IExpression expression)
    {
        var variableDeclarations = new List<LocalVariable>();
        var checkExpressionQueue = new Queue<IExpression>([expression]);

        while (checkExpressionQueue.TryDequeue(out var nextExpression))
        {
            if (IsExpressionType<MatchesExpression>(nextExpression, out var matchesExpression))
            {
                variableDeclarations.AddRange(matchesExpression.DeclaredVariables.Where(x => !x.Instantiated));
            }
            else if (IsExpressionType<BinaryOperatorExpression>(nextExpression, out var binaryExpression)
                && binaryExpression.BinaryOperator.OperatorType == BinaryOperatorType.BooleanAnd)
            {
                if (binaryExpression.BinaryOperator.Left is not null)
                    checkExpressionQueue.Enqueue(binaryExpression.BinaryOperator.Left);
                if (binaryExpression.BinaryOperator.Right is not null)
                    checkExpressionQueue.Enqueue(binaryExpression.BinaryOperator.Right);
            }
        }

        return variableDeclarations;
    }

    private ITypeReference TypeCheckIfExpression(
        IfExpression ifExpression)
    {
        // scope around the entire if expression. Variables declared in the check expression (e.g. with matches) will be
        // conditionally available in the body
        using var _ = PushScope();

        ifExpression.CheckExpression.ValueUseful = true;
        TypeCheckExpression(ifExpression.CheckExpression);

        ExpectExpressionType(Boolean(), ifExpression.CheckExpression);

        var variableDeclarations = GetExpressionUninitializedDeclaredVariables(ifExpression.CheckExpression);

        var uninstantiatedVariables =
            GetScopedVariables().OfType<LocalVariable>()
            .Where(x => !x.Instantiated)
            .ToDictionary(x => x, _ => new VariableIfInstantiation());

        foreach (var variable in variableDeclarations)
        {
            variable.Instantiated = true;
        }

        ITypeReference expectedBranchType = Unit();

        if (ifExpression.Body is not null)
        {
            expectedBranchType = TypeCheckExpression(ifExpression.Body);
        }

        foreach (var (variable, variableInstantiation) in uninstantiatedVariables)
        {
            variableInstantiation.InstantiatedInBody = variable.Instantiated;
            variable.Instantiated = false;
        }

        foreach (var variable in variableDeclarations)
        {
            variable.Instantiated = false;
        }

        foreach (var elseIf in ifExpression.ElseIfs)
        {
            using var __ = PushScope();
            elseIf.CheckExpression.ValueUseful = true;
            TypeCheckExpression(elseIf.CheckExpression);

            ExpectExpressionType(Boolean(), elseIf.CheckExpression);

            variableDeclarations = GetExpressionUninitializedDeclaredVariables(elseIf.CheckExpression);

            foreach (var variable in variableDeclarations)
            {
                variable.Instantiated = true;
            }

            if (elseIf.Body is not null)
            {
                var bodyType = TypeCheckExpression(elseIf.Body);
                if (expectedBranchType is InstantiatedClass { Signature.Id: var id } && id == DefId.Never)
                {
                    expectedBranchType = bodyType;
                }
                ExpectExpressionType(expectedBranchType, elseIf.Body);
            }

            foreach (var (variable, variableInstantiation) in uninstantiatedVariables)
            {
                variableInstantiation.InstantiatedInEachElseIf &= variable.Instantiated;
                variable.Instantiated = false;
            }

            foreach (var variable in variableDeclarations)
            {
                variable.Instantiated = false;
            }
        }

        if (ifExpression.ElseBody is not null)
        {
            using var __ = PushScope();
            var bodyType = TypeCheckExpression(ifExpression.ElseBody);
            if (expectedBranchType is InstantiatedClass { Signature.Id: var id } && id == DefId.Never)
            {
                expectedBranchType = bodyType;
            }
            ExpectExpressionType(expectedBranchType, ifExpression.ElseBody);

            foreach (var (variable, variableInstantiation) in uninstantiatedVariables)
            {
                variableInstantiation.InstantiatedInElse = variable.Instantiated;
                variable.Instantiated = false;
            }
        }

        foreach (var (variable, variableInstantiation) in uninstantiatedVariables)
        {
            // if variable was instantiated in each branch, then it is instantiated
            variable.Instantiated = ifExpression.Body is not null && variableInstantiation.InstantiatedInBody
                                                                  && ifExpression.ElseBody is not null &&
                                                                      variableInstantiation.InstantiatedInElse
                                                                  && (ifExpression.ElseIfs.Count == 0 ||
                                                                      variableInstantiation.InstantiatedInEachElseIf);
        }

        return expectedBranchType;
    }
}
