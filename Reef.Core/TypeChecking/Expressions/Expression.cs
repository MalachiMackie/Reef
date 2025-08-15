using System.Diagnostics;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker {

    private ITypeReference TypeCheckExpression(
        IExpression expression,
        bool allowUninstantiatedVariable = false)
    {
        var expressionType = expression switch
        {
            VariableDeclarationExpression variableDeclarationExpression => TypeCheckVariableDeclaration(
                variableDeclarationExpression),
            ValueAccessorExpression valueAccessorExpression => TypeCheckValueAccessor(valueAccessorExpression,
                allowUninstantiatedVariable),
            MethodReturnExpression methodReturnExpression => TypeCheckMethodReturn(methodReturnExpression),
            MethodCallExpression methodCallExpression => TypeCheckMethodCall(methodCallExpression),
            BlockExpression blockExpression => TypeCheckBlock(blockExpression.Block),
            IfExpressionExpression ifExpressionExpression => TypeCheckIfExpression(ifExpressionExpression.IfExpression),
            BinaryOperatorExpression binaryOperatorExpression => TypeCheckBinaryOperatorExpression(
                binaryOperatorExpression),
            ObjectInitializerExpression objectInitializerExpression => TypeCheckObjectInitializer(
                objectInitializerExpression),
            MemberAccessExpression memberAccessExpression => TypeCheckMemberAccess(memberAccessExpression),
            StaticMemberAccessExpression staticMemberAccessExpression => TypeCheckStaticMemberAccess(
                staticMemberAccessExpression),
            UnaryOperatorExpression unaryOperatorExpression => TypeCheckUnaryOperator(
                unaryOperatorExpression.UnaryOperator),
            UnionClassVariantInitializerExpression unionClassVariantInitializerExpression =>
                TypeCheckUnionClassVariantInitializer(
                    unionClassVariantInitializerExpression.UnionInitializer),
            MatchesExpression matchesExpression => TypeCheckMatchesExpression(
                matchesExpression),
            TupleExpression tupleExpression => TypeCheckTupleExpression(tupleExpression),
            MatchExpression matchExpression => TypeCheckMatchExpression(matchExpression),
            _ => throw new UnreachableException($"{expression.ExpressionType}")
        };

        expression.ResolvedType = expressionType;

        return expressionType;
    }

    private bool ExpectAssignableExpression(IExpression expression, bool report = true)
    {
        switch (expression)
        {
            case ValueAccessorExpression
            {
                ValueAccessor: { AccessType: ValueAccessType.Variable, Token: StringToken valueToken }
            }:
            {
                if (!TryGetScopedVariable(valueToken, out var variable))
                {
                    _errors.Add(TypeCheckerError.SymbolNotFound(valueToken));
                    return false;
                }
                if (variable is LocalVariable { Instantiated: false }
                    or LocalVariable { Mutable: true }
                    or FieldVariable { Mutable: true }
                    or FunctionSignatureParameter { Mutable: true})
                {
                    return true;
                }

                if (report)
                {
                    _errors.Add(TypeCheckerError.NonMutableAssignment(variable.Name.StringValue,
                        new SourceRange(valueToken.SourceSpan, valueToken.SourceSpan)));
                }
                return false;

            }
            case MemberAccessExpression memberAccess:
            {
                var owner = memberAccess.MemberAccess.Owner;

                if (memberAccess.MemberAccess.MemberName is null)
                {
                    return false;
                }
            
                var isOwnerAssignable = ExpectAssignableExpression(owner, report: false);

                if (owner.ResolvedType is not InstantiatedClass { Fields: var fields })
                {
                    if (report)
                        _errors.Add(TypeCheckerError.ExpressionNotAssignable(memberAccess));
                    return false;
                }

                var field = fields.FirstOrDefault(x => x.Name == memberAccess.MemberAccess.MemberName.StringValue);
                if (field is null)
                {
                    if (report)
                        _errors.Add(TypeCheckerError.ExpressionNotAssignable(memberAccess));
                    return false;
                }

                if (!field.IsMutable)
                {
                    if (report)
                        _errors.Add(TypeCheckerError.NonMutableMemberAssignment(memberAccess));
                    return false;
                }

                if (isOwnerAssignable)
                {
                    return true;
                }

                if (report)
                    _errors.Add(TypeCheckerError.NonMutableMemberOwnerAssignment(owner));
                return false;

            }
            case StaticMemberAccessExpression staticMemberAccess:
            {
                var ownerType = GetTypeReference(staticMemberAccess.StaticMemberAccess.Type);

                if (staticMemberAccess.StaticMemberAccess.MemberName is null)
                {
                    return false;
                }

                if (ownerType is not InstantiatedClass { Fields: var fields })
                {
                    if (report)
                        _errors.Add(TypeCheckerError.ExpressionNotAssignable(staticMemberAccess));
                    return false;
                }

                var staticField = fields.FirstOrDefault(x =>
                    x.Name == staticMemberAccess.StaticMemberAccess.MemberName.StringValue && x.IsStatic);
                if (staticField is null)
                {
                    if (report)
                        _errors.Add(TypeCheckerError.ExpressionNotAssignable(staticMemberAccess));
                    return false;
                }

                if (staticField.IsMutable)
                {
                    return true;
                }

                if (report)
                    _errors.Add(TypeCheckerError.NonMutableMemberAssignment(staticMemberAccess));
                return false;

            }
        }

        if (report)
            _errors.Add(TypeCheckerError.ExpressionNotAssignable(expression));
        
        return false;
    }
}
