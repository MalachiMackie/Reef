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
        _errors.Add(TypeCheckerError.TypeIsNotExpression(e.SourceRange, e.TypeIdentifier));
        
        return InstantiatedClass.Never;
    }
    
    private uint _loopDepth;
    
    private InstantiatedClass TypeCheckContinueExpression(ContinueExpression continueExpression)
    {
        if (_loopDepth == 0)
        {
            _errors.Add(TypeCheckerError.ContinueUsedOutsideOfLoop(continueExpression));
        }

        return InstantiatedClass.Never;
    }
    
    private InstantiatedClass TypeCheckBreakExpression(BreakExpression breakExpression)
    {
        if (_loopDepth == 0)
        {
            _errors.Add(TypeCheckerError.BreakUsedOutsideOfLoop(breakExpression));
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
                        or FunctionSignatureParameter { Mutable: true })
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

                    var ownerType = owner.ResolvedType;
                    while (ownerType is GenericTypeReference generic)
                    {
                        ownerType = generic.ResolvedType ?? throw new NotImplementedException();
                    }

                    if (ownerType is not InstantiatedClass { Fields: var fields })
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
            case IndexExpression indexExpression:
            {
                var owner = indexExpression.Collection;

                var isOwnerAssignable = ExpectAssignableExpression(owner, report: false);

                if (isOwnerAssignable)
                {
                    return true;
                }

                if (report)
                    _errors.Add(TypeCheckerError.NonMutableMemberOwnerAssignment(owner));
                return false;
            }
        }

        if (report)
            _errors.Add(TypeCheckerError.ExpressionNotAssignable(expression));

        return false;
    }
}
