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

    private ITypeReference TypeCheckIfExpression(IfExpression ifExpression)
    {
        // scope around the entire if expression. Variables declared in the check expression (e.g. with matches) will be
        // conditionally available in the body
        using var _ = PushScope();

        ifExpression.CheckExpression.ValueUseful = true;
        TypeCheckExpression(ifExpression.CheckExpression);

        ExpectExpressionType(InstantiatedClass.Boolean, ifExpression.CheckExpression);

        IReadOnlyList<LocalVariable> matchVariableDeclarations = [];

        if (ifExpression.CheckExpression is MatchesExpression { DeclaredVariables: var declaredVariables })
        {
            matchVariableDeclarations = declaredVariables;
        }

        var uninstantiatedVariables = GetScopedVariables()
            .OfType<LocalVariable>()
            .Where(x => !x.Instantiated)
            .ToDictionary(x => x, _ => new VariableIfInstantiation());

        foreach (var variable in matchVariableDeclarations)
        {
            variable.Instantiated = true;
        }

        if (ifExpression.Body is not null)
        {
            ifExpression.Body.ValueUseful = true;
            TypeCheckExpression(ifExpression.Body);
        }

        foreach (var (variable, variableInstantiation) in uninstantiatedVariables)
        {
            variableInstantiation.InstantiatedInBody = variable.Instantiated;
            variable.Instantiated = false;
        }

        foreach (var variable in matchVariableDeclarations)
        {
            variable.Instantiated = false;
        }

        foreach (var elseIf in ifExpression.ElseIfs)
        {
            using var __ = PushScope();
            elseIf.CheckExpression.ValueUseful = true;
            TypeCheckExpression(elseIf.CheckExpression);

            ExpectExpressionType(InstantiatedClass.Boolean, elseIf.CheckExpression);

            matchVariableDeclarations = elseIf.CheckExpression is MatchesExpression
            {
                DeclaredVariables: var elseIfDeclaredVariables
            }
                ? elseIfDeclaredVariables
                : [];

            foreach (var variable in matchVariableDeclarations)
            {
                variable.Instantiated = true;
            }

            if (elseIf.Body is not null)
            {
                elseIf.Body.ValueUseful = true;
                TypeCheckExpression(elseIf.Body);
            }

            foreach (var (variable, variableInstantiation) in uninstantiatedVariables)
            {
                variableInstantiation.InstantiatedInEachElseIf &= variable.Instantiated;
                variable.Instantiated = false;
            }

            foreach (var variable in matchVariableDeclarations)
            {
                variable.Instantiated = false;
            }
        }

        if (ifExpression.ElseBody is not null)
        {
            using var __ = PushScope();
            ifExpression.ElseBody.ValueUseful = true;
            TypeCheckExpression(ifExpression.ElseBody);

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

        // todo: tail expression
        return InstantiatedClass.Unit;
    }
}
