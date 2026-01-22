using System.Diagnostics;
using Reef.Core.Expressions;

namespace Reef.Core.TypeChecking;

public partial class TypeChecker
{

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
            ContinueExpression continueExpression => TypeCheckContinueExpression(continueExpression),
            BreakExpression breakExpression => TypeCheckBreakExpression(breakExpression),
            WhileExpression whileExpression => TypeCheckWhileExpression(whileExpression),
            TypeIdentifierExpression typeIdentifierExpression => TypeCheckTypeIdentifierExpression(typeIdentifierExpression),
            CollectionExpression collectionExpression => TypeCheckCollectionExpression(collectionExpression),
            FillCollectionExpression fillCollectionExpression => TypeCheckFillCollectionExpression(fillCollectionExpression),
            IndexExpression indexExpression => TypeCheckIndexExpression(indexExpression),
            _ => throw new UnreachableException($"{expression.ExpressionType}")
        };

        expression.ResolvedType = expressionType;

        return expressionType;
    }

    private ITypeReference TypeCheckIndexExpression(IndexExpression e)
    {
        var arrayType = (TypeCheckExpression(e.Collection) as ArrayType).NotNull();
        if (e.Index is {} indexExpression)
        {
            var indexType = TypeCheckExpression(indexExpression);
            ExpectType(indexType, InstantiatedClass.UInt64, indexExpression.SourceRange);
        }

        return arrayType.ElementType;
    }

    private ArrayType TypeCheckCollectionExpression(CollectionExpression e)
    {
        ITypeReference? firstElementType = null;
        foreach (var element in e.Elements)
        {
            var elementType = TypeCheckExpression(element);
            firstElementType ??= elementType;
            ExpectType(elementType, firstElementType, element.SourceRange);
        }
        
        return new ArrayType(
            firstElementType,
            boxed: e.BoxingSpecifier?.Type switch
            {
                TokenType.Boxed => true,
                TokenType.Unboxed => false,
                null => ArrayTypeSignature.Instance.Boxed,
                _ => throw new UnreachableException(e.BoxingSpecifier.Type.ToString())
            },
            length: e.Elements.Count);
    }

    private ArrayType TypeCheckFillCollectionExpression(FillCollectionExpression e)
    {
        var elementTypeReference = TypeCheckExpression(e.Element);
        return new ArrayType(
            elementTypeReference,
            boxed: e.BoxingSpecifier?.Type switch
            {
                TokenType.Boxed => true,
                TokenType.Unboxed => false,
                null => ArrayTypeSignature.Instance.Boxed,
                _ => throw new UnreachableException(e.BoxingSpecifier.Type.ToString())
            },
            length: e.LengthSpecifier.IntValue);
    }

    private InstantiatedClass TypeCheckTypeIdentifierExpression(TypeIdentifierExpression e)
    {
        AddError(TypeCheckerError.TypeIsNotExpression(e.SourceRange, e.TypeIdentifier));
        
        return InstantiatedClass.Never;
    }
    
    private uint _loopDepth;
    
    private InstantiatedClass TypeCheckContinueExpression(ContinueExpression continueExpression)
    {
        if (_loopDepth == 0)
        {
            AddError(TypeCheckerError.ContinueUsedOutsideOfLoop(continueExpression));
        }

        return InstantiatedClass.Never;
    }
    
    private InstantiatedClass TypeCheckBreakExpression(BreakExpression breakExpression)
    {
        if (_loopDepth == 0)
        {
            AddError(TypeCheckerError.BreakUsedOutsideOfLoop(breakExpression));
        }

        return InstantiatedClass.Never;
    }

    private InstantiatedClass TypeCheckWhileExpression(WhileExpression whileExpression)
    {
        if (whileExpression.Check is not null)
        {
            TypeCheckExpression(whileExpression.Check);
            whileExpression.Check.ValueUseful = true;
            ExpectExpressionType(InstantiatedClass.Boolean, whileExpression.Check);
        }

        _loopDepth++;

        if (whileExpression.Body is not null)
        {
            TypeCheckExpression(whileExpression.Body);

            ExpectExpressionType(InstantiatedClass.Unit, whileExpression.Body);
        }

        _loopDepth--;
        
        return InstantiatedClass.Unit;
    }

    private bool ExpectMutableExpression(IExpression expression, bool report = true)
    {
        switch (expression)
        {
            case BinaryOperatorExpression:
                return true;
            case BlockExpression blockExpression:
            {
                var result = blockExpression.Block.Expressions.Count == 0
                             || !blockExpression.Block.HasTailExpression
                             || ExpectMutableExpression(blockExpression.Block.Expressions[^1], report: false);

                if (!result && report)
                {
                    AddError(TypeCheckerError.ExpressionNotAssignable(blockExpression.Block.Expressions[^1]));
                }

                return result;
            }
            case BreakExpression:
                return false;
            case CollectionExpression:
                return true;
            case ContinueExpression:
                return false;
            case FillCollectionExpression:
                return true;
            case IfExpressionExpression ifExpression:
            {
                IEnumerable<IExpression?> nullExpressions =
                [
                    ifExpression.IfExpression.Body,
                    ifExpression.IfExpression.ElseBody,
                    ..ifExpression.IfExpression.ElseIfs.Select(x => x.Body)
                ];
                var expressions = nullExpressions.Where(x => x is not null).Cast<IExpression>();

                var result = expressions.All(x => ExpectMutableExpression(x, report: false));

                if (!result && report)
                {
                    AddError(TypeCheckerError.ExpressionNotAssignable(expression));
                }

                return result;
            }
            case ValueAccessorExpression { ValueAccessor.AccessType: ValueAccessType.Literal }:
                return true;
            case ValueAccessorExpression
            {
                ValueAccessor: { AccessType: ValueAccessType.Variable, Token.Type: TokenType.Todo }
            }:
                return true;
            case ValueAccessorExpression
            {
                ValueAccessor: { AccessType: ValueAccessType.Variable, Token: StringToken valueToken }
            }:
            {
                if (!TryGetScopedVariable(valueToken, out var variable))
                {
                    if (ScopedFunctions.ContainsKey(valueToken.StringValue))
                    {
                        return true;
                    }

                    AddError(TypeCheckerError.SymbolNotFound(valueToken));
                    return false;
                }

                if (variable is LocalVariable { Instantiated: false }
                    or LocalVariable { Mutable: true }
                    or FieldVariable { Mutable: true }
                    or FunctionSignatureParameter { Mutable: true })
                {
                    return true;
                }

                if (report)
                {
                    AddError(TypeCheckerError.NonMutableAssignment(variable.Name.StringValue,
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

                var isOwnerMutable = ExpectMutableExpression(owner, report: false);

                var ownerType = owner.ResolvedType;
                while (ownerType is GenericTypeReference generic)
                {
                    ownerType = generic.ResolvedType ?? throw new NotImplementedException();
                }

                if (ownerType is not InstantiatedClass { Fields: var fields })
                {
                    if (report)
                        AddError(TypeCheckerError.ExpressionNotAssignable(memberAccess));
                    return false;
                }

                var field = fields.FirstOrDefault(x => x.Name == memberAccess.MemberAccess.MemberName.StringValue);
                if (field is null)
                {
                    if (report)
                        AddError(TypeCheckerError.ExpressionNotAssignable(memberAccess));
                    return false;
                }

                if (!field.IsMutable)
                {
                    if (report)
                        AddError(TypeCheckerError.NonMutableMemberAssignment(memberAccess));
                    return false;
                }

                if (isOwnerMutable)
                {
                    return true;
                }

                if (report)
                    AddError(TypeCheckerError.NonMutableMemberOwnerAssignment(owner));
                return false;

            }
            case MethodCallExpression methodCall:
            {
                var methodType = (methodCall.MethodCall.Method.ResolvedType as IFunction).NotNull();

                var result = methodType.MutableReturn;
                if (!result && report)
                {
                    AddError(TypeCheckerError.ExpressionNotAssignable(methodCall));
                }

                return result;
            }
            case MethodReturnExpression:
                return true;
            case ObjectInitializerExpression:
                return true;
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
                        AddError(TypeCheckerError.ExpressionNotAssignable(staticMemberAccess));
                    return false;
                }

                var staticField = fields.FirstOrDefault(x =>
                    x.Name == staticMemberAccess.StaticMemberAccess.MemberName.StringValue && x.IsStatic);
                if (staticField is null)
                {
                    if (report)
                        AddError(TypeCheckerError.ExpressionNotAssignable(staticMemberAccess));
                    return false;
                }
                
                if (staticField.IsMutable)
                {
                    return true;
                }

                if (report)
                    AddError(TypeCheckerError.NonMutableMemberAssignment(staticMemberAccess));
                return false;

            }
            case TupleExpression:
                return true;
            case TypeIdentifierExpression:
                throw new UnreachableException();
            case UnaryOperatorExpression unary:
            {
                if (unary.UnaryOperator.Operand is { } operand)
                {
                    return ExpectMutableExpression(operand, report);
                }

                return true;
            }
            case UnionClassVariantInitializerExpression:
            {
                return true;
            }
            case VariableDeclarationExpression:
                return true;
            case WhileExpression:
                // todo: need to change if we ever can return a value out of a loop 
                return true;
            case IndexExpression indexExpression:
            {
                var owner = indexExpression.Collection;

                var isOwnerMutable = ExpectMutableExpression(owner, report: false);

                if (isOwnerMutable)
                {
                    return true;
                }

                if (report)
                    AddError(TypeCheckerError.NonMutableMemberOwnerAssignment(owner));
                return false;
            }
            case MatchesExpression:
            {
                return true;
            }
            case MatchExpression match:
            {
                var expressions = match.Arms.Select(x => x.Expression)
                    .Where(x => x is not null)
                    .Cast<IExpression>();

                var result = expressions.All(x => ExpectMutableExpression(x, report: false));

                if (!result && report)
                {
                    AddError(TypeCheckerError.ExpressionNotAssignable(expression));
                }

                return result;
            }
        }

        throw new UnreachableException($"{expression.GetType()}. {expression}");
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
                        return false;
                    }
                    if (variable is LocalVariable { Instantiated: false }
                        or LocalVariable { Mutable: true }
                        or FieldVariable { Mutable: true }
                        or FunctionSignatureParameter { Mutable: true })
                    {
                        return true;
                    }

                    if (report)
                    {
                        AddError(TypeCheckerError.NonMutableAssignment(variable.Name.StringValue,
                            new SourceRange(valueToken.SourceSpan, valueToken.SourceSpan)));
                    }
                    return false;
                }
            case MemberAccessExpression memberAccess:
                {
                    if (memberAccess.MemberAccess.MemberName is null)
                    {
                        return false;
                    }
                    
                    var owner = memberAccess.MemberAccess.Owner;

                    var isOwnerMutable = ExpectMutableExpression(owner, report: false);

                    var ownerType = owner.ResolvedType;
                    while (ownerType is GenericTypeReference generic)
                    {
                        ownerType = generic.ResolvedType ?? throw new NotImplementedException();
                    }

                    if (ownerType is not InstantiatedClass { Fields: var fields })
                    {
                        if (report)
                            AddError(TypeCheckerError.ExpressionNotAssignable(memberAccess));
                        return false;
                    }

                    var field = fields.FirstOrDefault(x => x.Name == memberAccess.MemberAccess.MemberName.StringValue);
                    if (field is null)
                    {
                        if (report)
                            AddError(TypeCheckerError.ExpressionNotAssignable(memberAccess));
                        return false;
                    }

                    if (!field.IsMutable)
                    {
                        if (report)
                            AddError(TypeCheckerError.NonMutableMemberAssignment(memberAccess));
                        return false;
                    }

                    if (isOwnerMutable)
                    {
                        return true;
                    }

                    if (report)
                        AddError(TypeCheckerError.NonMutableMemberOwnerAssignment(owner));
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
                            AddError(TypeCheckerError.ExpressionNotAssignable(staticMemberAccess));
                        return false;
                    }

                    var staticField = fields.FirstOrDefault(x =>
                        x.Name == staticMemberAccess.StaticMemberAccess.MemberName.StringValue && x.IsStatic);
                    if (staticField is null)
                    {
                        if (report)
                            AddError(TypeCheckerError.ExpressionNotAssignable(staticMemberAccess));
                        return false;
                    }

                    if (staticField.IsMutable)
                    {
                        return true;
                    }

                    if (report)
                        AddError(TypeCheckerError.NonMutableMemberAssignment(staticMemberAccess));
                    return false;

                }
            case IndexExpression indexExpression:
            {
                var owner = indexExpression.Collection;

                var isOwnerMutable = ExpectMutableExpression(owner, report: false);

                if (isOwnerMutable)
                {
                    return true;
                }

                if (report)
                    AddError(TypeCheckerError.NonMutableMemberOwnerAssignment(owner));
                return false;
            }
        }
        
        if (report)
            AddError(TypeCheckerError.ExpressionNotAssignable(expression));

        return false;
    }
}
