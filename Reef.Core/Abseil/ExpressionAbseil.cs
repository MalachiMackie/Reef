using System.Diagnostics;
using Reef.Core.LoweredExpressions;

namespace Reef.Core.Abseil;

public partial class ProgramAbseil
{
    public ILoweredExpression LowerExpression(
            Expressions.IExpression expression)
    {
        return expression switch
        {
            Expressions.ValueAccessorExpression e => LowerValueAccessorExpression(e),
            Expressions.VariableDeclarationExpression e => LowerVariableDeclarationExpression(e),
            Expressions.BinaryOperatorExpression e => LowerBinaryOperatorExpression(e),
            Expressions.UnaryOperatorExpression e => LowerUnaryOperatorExpression(e),
            Expressions.BlockExpression e => LowerBlockExpression(e), 
            Expressions.ObjectInitializerExpression e => LowerObjectInitializationExpression(e), 
            Expressions.UnionClassVariantInitializerExpression e =>
                LowerUnionClassVariantInitializerExpression(e), 
            Expressions.StaticMemberAccessExpression e => LowerStaticMemberAccess(e), 
            Expressions.MemberAccessExpression e => LowerMemberAccessExpression(e),
            Expressions.MethodCallExpression e => LowerMethodCallExpression(e),
            Expressions.MethodReturnExpression e => LowerMethodReturnExpression(e),
            _ => throw new NotImplementedException($"{expression.GetType()}")
        };
    }

    private MethodReturnExpression LowerMethodReturnExpression(
            Expressions.MethodReturnExpression e)
    {
        return new MethodReturnExpression(
                e.MethodReturn.Expression is not null
                    ? LowerExpression(e.MethodReturn.Expression)
                    : new UnitConstantExpression(true));
    }

    private MethodCallExpression LowerMethodCallExpression(Expressions.MethodCallExpression e)
    {
        var instantiatedFunction = e.MethodCall.Method switch
        {
            Expressions.MemberAccessExpression { MemberAccess.InstantiatedFunction: var fn } => fn,
            Expressions.StaticMemberAccessExpression { StaticMemberAccess.InstantiatedFunction: var fn } => fn,
            Expressions.ValueAccessorExpression { FunctionInstantiation: var fn } => fn,
            _ => null
        };

        if (instantiatedFunction is null)
        {
            throw new NotImplementedException("Calling function object");
        }

        var functionReference = GetFunctionReference(instantiatedFunction.FunctionId,
                [..instantiatedFunction.TypeArguments.Select(GetTypeReference)]);

        var arguments = new List<ILoweredExpression>(functionReference.TypeArguments.Count);

        if (e.MethodCall.Method is Expressions.MemberAccessExpression memberAccess)
        {
            var owner = LowerExpression(memberAccess.MemberAccess.Owner);
            arguments.Add(owner);
        }
        else if (!instantiatedFunction.IsStatic
                && instantiatedFunction.OwnerType is not null
                && _currentType is not null
                && EqualTypeReferences(GetTypeReference(instantiatedFunction.OwnerType), _currentType)
                && _currentFunction is not null
                && EqualTypeReferences(_currentFunction.Parameters[0], _currentType))
        {
            arguments.Add(
                    new LoadArgumentExpression(0, true, _currentType));
        }

        if (instantiatedFunction.AccessedOuterVariables.Count > 0)
        {
            throw new NotImplementedException("Calling closure");
        }

        arguments.AddRange(e.MethodCall.ArgumentList.Select(LowerExpression));

        return new MethodCallExpression(
                functionReference,
                arguments,
                e.ValueUseful,
                GetTypeReference(e.ResolvedType.NotNull()));
    }

    private ILoweredExpression LowerMemberAccessExpression(
            Expressions.MemberAccessExpression e)
    {
        var owner = LowerExpression(e.MemberAccess.Owner);
        switch (e.MemberAccess.MemberType.NotNull())
        {
            case Expressions.MemberType.Field:
                {
                    // todo: assert we're in a class variant
                    return new FieldAccessExpression(
                            owner,
                            e.MemberAccess.MemberName.NotNull().StringValue,
                            "_classVariant",
                            e.ValueUseful,
                            GetTypeReference(e.ResolvedType.NotNull()));
                }
            case Expressions.MemberType.Function:
                break;
            case Expressions.MemberType.Variant:
                throw new InvalidOperationException("Can never access a variant through instance member access");
        }

        throw new NotImplementedException($"{e.MemberAccess.MemberType}");
    }

    private BlockExpression LowerBlockExpression(Expressions.BlockExpression e)
    {
        foreach (var method in e.Block.Functions)
        {
            throw new NotImplementedException();
        }

        return new BlockExpression(
                [..e.Block.Expressions.Select(LowerExpression)],
                GetTypeReference(e.ResolvedType.NotNull()),
                e.ValueUseful);
    }

    private CreateObjectExpression LowerUnionClassVariantInitializerExpression(
            Expressions.UnionClassVariantInitializerExpression e)
    {
        var type = GetTypeReference(e.ResolvedType.NotNull());
        if (type is not LoweredConcreteTypeReference concreteTypeReference)
        {
            throw new UnreachableException();
        }

        var dataType = _types.First(x => x.Id == concreteTypeReference.DefinitionId);

        var variantIdentifier = dataType.Variants.Index()
            .First(x => x.Item.Name == e.UnionInitializer.VariantIdentifier.StringValue).Index;

        var fieldInitailizers = e.UnionInitializer.FieldInitializers.ToDictionary(
                x => x.FieldName.StringValue,
                x => LowerExpression(x.Value.NotNull()));

        fieldInitailizers["_variantIdentifier"] = new IntConstantExpression(
                ValueUseful: true,
                variantIdentifier);

        return new(
                concreteTypeReference,
                e.UnionInitializer.VariantIdentifier.StringValue,
                e.ValueUseful,
                fieldInitailizers);
    }

    private ILoweredExpression LowerStaticMemberAccess(
            Expressions.StaticMemberAccessExpression e)
    {
        if (e.StaticMemberAccess.MemberType == Expressions.MemberType.Variant)
        {
            var type = GetTypeReference(e.ResolvedType.NotNull())
                as LoweredConcreteTypeReference ?? throw new UnreachableException();

            var dataType = _types.First(x => x.Id == type.DefinitionId);
            var variantName = e.StaticMemberAccess.MemberName.NotNull().StringValue;
            var variantIdentifier = dataType.Variants.Index()
                .First(x => x.Item.Name == variantName).Index;

            var fieldInitailizers = new Dictionary<string, ILoweredExpression>()
            {
                {
                    "_variantIdentifier",
                    new IntConstantExpression(
                        ValueUseful: true,
                        variantIdentifier)
                }
            };

            return new CreateObjectExpression(
                    type,
                    variantName,
                    e.ValueUseful,
                    fieldInitailizers);
        }

        throw new NotImplementedException(e.ToString());
    }

    private CreateObjectExpression LowerObjectInitializationExpression(
            Expressions.ObjectInitializerExpression e)
    {
        var type = GetTypeReference(e.ResolvedType.NotNull());
        if (type is not LoweredConcreteTypeReference concreteTypeReference)
        {
            throw new UnreachableException();
        }

        var fieldInitailizers = e.ObjectInitializer.FieldInitializers.ToDictionary(
                x => x.FieldName.StringValue,
                x => LowerExpression(x.Value.NotNull()));
        
        return new(concreteTypeReference,
                "_classVariant",
                e.ValueUseful,
                fieldInitailizers);
    }

    private ILoweredExpression LowerVariableDeclarationExpression(Expressions.VariableDeclarationExpression e)
    {
        var variableName = e.VariableDeclaration.Variable.NotNull()
            .Name.StringValue;

        if (e.VariableDeclaration.Value is null)
        {
            return new VariableDeclarationExpression(variableName, e.ValueUseful);
        }

        return new VariableDeclarationAndAssignmentExpression(
                variableName,
                LowerExpression(e.VariableDeclaration.Value),
                e.ValueUseful);
    }

    private ILoweredExpression LowerUnaryOperatorExpression(
            Expressions.UnaryOperatorExpression e)
    {
        var operand = LowerExpression(e.UnaryOperator.Operand.NotNull());
        switch (e.UnaryOperator.OperatorType)
        {
            case Expressions.UnaryOperatorType.FallOut:
                break;
            case Expressions.UnaryOperatorType.Not:
                return new BoolNotExpression(e.ValueUseful, operand);
        }

        throw new NotImplementedException(e.UnaryOperator.OperatorType.ToString());
    }

    private ILoweredExpression LowerValueAccessorExpression(
            Expressions.ValueAccessorExpression e)
    {

        return e switch
        {
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: StringToken { StringValue: var stringLiteral } } } => new StringConstantExpression(e.ValueUseful, stringLiteral),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: IntToken { Type: TokenType.IntLiteral, IntValue: var intValue} }} => new IntConstantExpression(e.ValueUseful, intValue),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token.Type: TokenType.True }} => new BoolConstantExpression(e.ValueUseful, true),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token.Type: TokenType.False }} => new BoolConstantExpression(e.ValueUseful, false),
            { ValueAccessor.AccessType: Expressions.ValueAccessType.Variable, ReferencedVariable: {} variable} => VariableAccess(variable, e.ValueUseful),
            _ => throw new NotImplementedException($"{e}")
        };

        ILoweredExpression VariableAccess(
                TypeChecking.TypeChecker.IVariable variable,
                bool valueUseful)
        {
            var resolvedType = GetTypeReference(e.ResolvedType.NotNull());
            switch (variable)
            {
                case TypeChecking.TypeChecker.LocalVariable localVariable:
                    {
                        if (localVariable.ReferencedInClosure)
                        {
                            throw new NotImplementedException();
                        }

                        return new LocalVariableAccessor(
                                variable.Name.StringValue,
                                valueUseful,
                                resolvedType);
                    }
                case TypeChecking.TypeChecker.ThisVariable thisVariable:
                    {
                        if (thisVariable.ReferencedInClosure)
                        {
                            throw new NotImplementedException();
                        }

                        Debug.Assert(_currentFunction is not null);
                        Debug.Assert(_currentType is not null); 
                        Debug.Assert(_currentFunction.Parameters.Count > 0);
                        Debug.Assert(EqualTypeReferences(_currentFunction.Parameters[0], _currentType));

                        return new LoadArgumentExpression(
                                0, valueUseful, resolvedType);
                    }
                case TypeChecking.TypeChecker.FieldVariable fieldVariable
                    when fieldVariable.ContainingSignature.Id == _currentType?.DefinitionId
                        && _currentFunction is not null:
                    {
                        if (fieldVariable.IsStaticField)
                        {
                            return new StaticFieldAccessExpression(
                                    _currentType,
                                    fieldVariable.Name.StringValue,
                                    valueUseful,
                                    resolvedType);
                        }

                        if (_currentFunction is null
                                || _currentFunction.Parameters.Count == 0
                                || !EqualTypeReferences(
                                    _currentFunction.Parameters[0],
                                    _currentType))
                        {
                            throw new InvalidOperationException("Expected to be in instance function");
                        }

                        if (fieldVariable.ReferencedInClosure)
                        {
                            throw new NotImplementedException();
                        }

                        // todo: assert we're in a class and have _classVariant

                        return new FieldAccessExpression(
                            new LoadArgumentExpression(
                                0,
                                true,
                                _currentType),
                            fieldVariable.Name.StringValue,
                            "_classVariant",
                            valueUseful,
                            resolvedType);
                    }
                case TypeChecking.TypeChecker.FunctionSignatureParameter argument:
                    {
                        Debug.Assert(_currentFunction is not null); 

                        var argumentIndex = argument.ParameterIndex;
                        if (argument.ReferencedInClosure)
                        {
                            throw new NotImplementedException();
                        }

                        if (argument.ContainingFunction.AccessedOuterVariables.Count > 0
                                || (argument.ContainingFunction.OwnerType is not null
                                    && !argument.ContainingFunction.IsStatic))
                        {
                            argumentIndex++;
                        }

                        return new LoadArgumentExpression(argumentIndex, valueUseful, resolvedType);
                    }
            }

            throw new NotImplementedException($"{variable.GetType()}");
        }
    }

    private ILoweredExpression LowerBinaryOperatorExpression(
            Expressions.BinaryOperatorExpression e)
    {
        if (e.BinaryOperator.OperatorType == Expressions.BinaryOperatorType.ValueAssignment)
        {
            return LowerValueAssignment(
                    e.BinaryOperator.Left.NotNull(),
                    e.BinaryOperator.Right.NotNull(),
                    e.ValueUseful,
                    GetTypeReference(e.ResolvedType.NotNull()));
        }

        var left = LowerExpression(e.BinaryOperator.Left.NotNull());
        var right = LowerExpression(e.BinaryOperator.Right.NotNull());

        return e.BinaryOperator.OperatorType switch
        {
            Expressions.BinaryOperatorType.LessThan
                => new IntLessThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.GreaterThan
                => new IntGreaterThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Plus
                => new IntPlusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Minus
                => new IntMinusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Multiply
                => new IntMultiplyExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Divide
                => new IntDivideExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.EqualityCheck
                // todo: handle more types of equality checks 
                => new IntEqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.BooleanAnd
                => new BoolAndExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.BooleanOr
                => new BoolOrExpression(e.ValueUseful, left, right),
            _ => throw new InvalidOperationException($"Invalid binary operator {e.BinaryOperator.OperatorType}"),
        };
        throw new NotImplementedException(e.ToString());
    }

    private ILoweredExpression LowerValueAssignment(
            Expressions.IExpression left,
            Expressions.IExpression right,
            bool valueUseful,
            ILoweredTypeReference resolvedType)
    {
        if (left is Expressions.ValueAccessorExpression valueAccessor)
        {
            var variable = valueAccessor.ReferencedVariable.NotNull();
            if (variable is TypeChecking.TypeChecker.LocalVariable localVariable)
            {
                if (localVariable.ReferencedInClosure)
                {
                    throw new NotImplementedException();
                }

                return new LocalAssignmentExpression(
                        localVariable.Name.StringValue,
                        LowerExpression(right),
                        resolvedType,
                        valueUseful);
            }

            if (variable is TypeChecking.TypeChecker.FieldVariable fieldVariable)
            {
                if (fieldVariable.ReferencedInClosure)
                {
                    throw new NotImplementedException();
                }

                Debug.Assert(_currentType is not null);
                Debug.Assert(fieldVariable.ContainingSignature.Id == _currentType.DefinitionId);

                if (fieldVariable.IsStaticField)
                {
                    return new StaticFieldAssignmentExpression(
                        _currentType,
                        fieldVariable.Name.StringValue,
                        LowerExpression(right),
                        valueUseful,
                        resolvedType);
                }

                Debug.Assert(_currentFunction is not null);
                Debug.Assert(_currentFunction.Parameters.Count > 0);
                Debug.Assert(EqualTypeReferences(_currentFunction.Parameters[0], _currentType));

                return new FieldAssignmentExpression(
                    new LoadArgumentExpression(0, true, _currentType),
                    fieldVariable.Name.StringValue,
                    LowerExpression(right),
                    valueUseful,
                    resolvedType);
            }

            throw new NotImplementedException(variable.ToString());
        }

        if (left is Expressions.MemberAccessExpression memberAccess)
        {
            var memberOwner = LowerExpression(memberAccess.MemberAccess.Owner);

            return new FieldAssignmentExpression(
                memberOwner,
                memberAccess.MemberAccess.MemberName.NotNull().StringValue,
                LowerExpression(right),
                valueUseful,
                resolvedType);
        }

        if (left is Expressions.StaticMemberAccessExpression staticMemberAccess)
        {
            if (GetTypeReference(staticMemberAccess.OwnerType.NotNull())
                    is not LoweredConcreteTypeReference concreteType)
            {
                throw new InvalidOperationException("Expected type to be concrete");
            }

            return new StaticFieldAssignmentExpression(
                concreteType,
                staticMemberAccess.StaticMemberAccess.MemberName.NotNull().StringValue,
                LowerExpression(right),
                valueUseful,
                resolvedType);
        }

        throw new UnreachableException();
    }
}
