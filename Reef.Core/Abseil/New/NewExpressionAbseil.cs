using System.Diagnostics;
using Reef.Core.Expressions;
using Reef.Core.LoweredExpressions.New;

namespace Reef.Core.Abseil.New;

public partial class NewProgramAbseil
{
    private IOperand NewLowerExpression(Expressions.IExpression expression)
    {
        switch (expression)
        {
            case Expressions.BinaryOperatorExpression binaryOperatorExpression:
                throw new NotImplementedException();
            case Expressions.BlockExpression blockExpression:
                throw new NotImplementedException();
            case Expressions.BreakExpression breakExpression:
                throw new NotImplementedException();
            case Expressions.ContinueExpression continueExpression:
                throw new NotImplementedException();
            case Expressions.IfExpressionExpression ifExpressionExpression:
                throw new NotImplementedException();
            case Expressions.MatchesExpression matchesExpression:
                throw new NotImplementedException();
            case Expressions.MatchExpression matchExpression:
                throw new NotImplementedException();
            case Expressions.MemberAccessExpression memberAccessExpression:
                throw new NotImplementedException();
            case Expressions.MethodCallExpression methodCallExpression:
                throw new NotImplementedException();
            case Expressions.MethodReturnExpression methodReturnExpression:
                throw new NotImplementedException();
            case Expressions.ObjectInitializerExpression objectInitializerExpression:
                throw new NotImplementedException();
            case Expressions.StaticMemberAccessExpression staticMemberAccessExpression:
                throw new NotImplementedException();
            case Expressions.TupleExpression tupleExpression:
                throw new NotImplementedException();
            case Expressions.UnaryOperatorExpression unaryOperatorExpression:
                throw new NotImplementedException();
            case Expressions.UnionClassVariantInitializerExpression unionClassVariantInitializerExpression:
                throw new NotImplementedException();
            case Expressions.ValueAccessorExpression valueAccessorExpression:
                return LowerValueAccessor(valueAccessorExpression);
            case Expressions.VariableDeclarationExpression variableDeclarationExpression:
                LowerVariableDeclaration(variableDeclarationExpression);
                break;
            case Expressions.WhileExpression whileExpression:
                throw new NotImplementedException();
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }

        return null!;
    }

    private IOperand LowerValueAccessor(ValueAccessorExpression e)
    {
        return e switch
        {
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: StringToken { StringValue: var stringLiteral } } } => new StringConstant(stringLiteral),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: IntToken { Type: TokenType.IntLiteral, IntValue: var intValue} }, ResolvedType: var resolvedType} =>
                IsIntSigned(resolvedType.NotNull())
                    ? new IntConstant(intValue, GetIntSize(resolvedType.NotNull()))
                    : new UIntConstant((ulong)intValue, GetIntSize(resolvedType.NotNull())),// IntCase(intValue),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token.Type: TokenType.True }} => new BoolConstant(true),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token.Type: TokenType.False }} => new BoolConstant(false),
            { ValueAccessor.AccessType: Expressions.ValueAccessType.Variable, ReferencedVariable: {} variable} => VariableAccess(variable, e.ValueUseful),
            { ValueAccessor.AccessType: Expressions.ValueAccessType.Variable, FunctionInstantiation: {} fn} => FunctionAccess(fn, (e.ResolvedType as TypeChecking.TypeChecker.FunctionObject).NotNull(), e.ValueUseful),
            _ => throw new UnreachableException($"{e}")
        };

        IOperand FunctionAccess(
                TypeChecking.TypeChecker.InstantiatedFunction fn,
                TypeChecking.TypeChecker.FunctionObject typeReference,
                bool valueUseful)
        {
            var method = _methods.Keys.First(x => x.Id == fn.FunctionId);

            var ownerTypeArguments = _currentType?.TypeArguments ?? [];

            throw new NotImplementedException();

            // var functionObjectParameters = new Dictionary<string, ILoweredExpression>
            // {
            //     {
            //         "FunctionReference",
            //         new FunctionReferenceConstantExpression(
            //                 GetFunctionReference(
            //                     fn.FunctionId,
            //                     [..fn.TypeArguments.Select(GetTypeReference)],
            //                     ownerTypeArguments),
            //                 true,
            //                 new LoweredFunctionPointer(
            //                     method.Parameters,
            //                     method.ReturnType))
            //     }
            // };
            //
            // if (fn.ClosureTypeId is not null)
            // {
            //     functionObjectParameters.Add("FunctionParameter", CreateClosureObject(fn));
            // }
            // else if (fn is { IsStatic: false, OwnerType: not null }
            //          && _currentType is not null
            //          && EqualTypeReferences(GetTypeReference(fn.OwnerType), _currentType)
            //          && _currentFunction is not null
            //          && EqualTypeReferences(_currentFunction.Value.LoweredMethod.Parameters[0], _currentType))
            // {
            //     functionObjectParameters.Add("FunctionParameter", new LoadArgumentExpression(0, true, _currentType));
            // }
            //
            // return new CreateObjectExpression(
            //     (GetTypeReference(typeReference) as LoweredConcreteTypeReference).NotNull(),
            //     "_classVariant",
            //     valueUseful,
            //     functionObjectParameters);
        }

        IOperand VariableAccess(
            TypeChecking.TypeChecker.IVariable variable,
            bool valueUseful)
        {
            var resolvedType = GetTypeReference(e.ResolvedType.NotNull());
            switch (variable)
            {
                case TypeChecking.TypeChecker.LocalVariable localVariable:
                    {
                        if (!localVariable.ReferencedInClosure)
                        {
                            var local = _locals.First(x => x.UserGivenName == variable.Name.StringValue);
                            return new Copy(new Local(local.CompilerGivenName));
                            // return new LocalVariableAccessor(
                            //         variable.Name.StringValue,
                            //         valueUseful,
                            //         resolvedType);
                        }

                        throw new NotImplementedException();

                        // var currentFunction = _currentFunction.NotNull();
                        // var containingFunction = localVariable.ContainingFunction.NotNull();
                        // var containingFunctionLocals = _types[containingFunction.LocalsTypeId.NotNull()];
                        // var localsTypeReference = new LoweredConcreteTypeReference(
                        //                 containingFunctionLocals.Name,
                        //                 containingFunctionLocals.Id,
                        //                 []);
                        // if (containingFunction.Id == currentFunction.FunctionSignature.Id)
                        // {
                        //     return new FieldAccessExpression(
                        //         new LocalVariableAccessor(
                        //             "__locals",
                        //             true,
                        //             localsTypeReference),
                        //         localVariable.Name.StringValue,
                        //         "_classVariant",
                        //         e.ValueUseful,
                        //         resolvedType);
                        // }
                        // var closureTypeId = _currentFunction.NotNull()
                        //         .FunctionSignature.ClosureTypeId.NotNull();
                        // var closureType = _types[closureTypeId];
                        //
                        // return new FieldAccessExpression(
                        //     new FieldAccessExpression(
                        //         new LoadArgumentExpression(
                        //             0,
                        //             true,
                        //             new LoweredConcreteTypeReference(
                        //                 closureType.Name,
                        //                 closureTypeId,
                        //                 [])),
                        //         containingFunctionLocals.Name,
                        //         "_classVariant",
                        //         true,
                        //         localsTypeReference),
                        //     localVariable.Name.StringValue,
                        //     "_classVariant",
                        //     e.ValueUseful,
                        //     resolvedType);
                    }
                case TypeChecking.TypeChecker.ThisVariable thisVariable:
                    {
                        Debug.Assert(_currentFunction is not null);
                        Debug.Assert(_currentType is not null);

                        throw new NotImplementedException();
                        // if (thisVariable.ReferencedInClosure
                        //         && _currentFunction.Value.FunctionSignature.ClosureTypeId is not null)
                        // {
                        //     var closureType = _types[_currentFunction.Value.FunctionSignature.ClosureTypeId];
                        //     var closureTypeReference = new LoweredConcreteTypeReference(
                        //                 closureType.Name,
                        //                 closureType.Id,
                        //                 []);
                        //     Debug.Assert(_currentFunction.Value.LoweredMethod.Parameters.Count > 0);
                        //     Debug.Assert(
                        //         EqualTypeReferences(
                        //             _currentFunction.Value.LoweredMethod.Parameters[0],
                        //             closureTypeReference));
                        //     return new FieldAccessExpression(
                        //         new LoadArgumentExpression(
                        //             0,
                        //             true,
                        //             closureTypeReference),
                        //         "this",
                        //         "_classVariant",
                        //         valueUseful,
                        //         resolvedType);
                        // }

                        // Debug.Assert(_currentFunction.Value.LoweredMethod.Parameters.Count > 0);
                        // Debug.Assert(EqualTypeReferences(
                        //             _currentFunction.Value.LoweredMethod.Parameters[0],
                        //             _currentType));
                        //
                        // return new LoadArgumentExpression(
                        //         0, valueUseful, resolvedType);
                    }
                case TypeChecking.TypeChecker.FieldVariable fieldVariable
                    when fieldVariable.ContainingSignature.Id == _currentType?.DefinitionId
                        && _currentFunction is not null:
                {
                    throw new NotImplementedException();
                        // if (fieldVariable.IsStaticField)
                        // {
                        //     return new StaticFieldAccessExpression(
                        //             _currentType,
                        //             fieldVariable.Name.StringValue,
                        //             valueUseful,
                        //             resolvedType);
                        // }
                        //
                        // if (_currentFunction.Value.FunctionSignature.ClosureTypeId is not null)
                        // {
                        //     var loweredMethod = _currentFunction.Value.LoweredMethod;
                        //     var fnSignature = _currentFunction.Value.FunctionSignature;
                        //     var closureType = _types[fnSignature.ClosureTypeId];
                        //     var closureTypeReference = new LoweredConcreteTypeReference(closureType.Name, closureType.Id, []);
                        //
                        //     // we're a closure, so reference the value through the "this" field
                        //     // of the closure type
                        //     Debug.Assert(loweredMethod.Parameters.Count > 0);
                        //     Debug.Assert(
                        //             EqualTypeReferences(
                        //                 loweredMethod.Parameters[0],
                        //                 closureTypeReference));
                        //     return new FieldAccessExpression(
                        //         new FieldAccessExpression(
                        //             new LoadArgumentExpression(
                        //                 0,
                        //                 true,
                        //                 closureTypeReference),
                        //             "this",
                        //             "_classVariant",
                        //             true,
                        //             _currentType),
                        //         fieldVariable.Name.StringValue,
                        //         "_classVariant",
                        //         valueUseful,
                        //         resolvedType);
                        // }
                        //
                        // if (_currentFunction.Value.LoweredMethod.Parameters.Count == 0
                        //         || !EqualTypeReferences(
                        //             _currentFunction.Value.LoweredMethod.Parameters[0],
                        //             _currentType))
                        // {
                        //     throw new InvalidOperationException("Expected to be in instance function");
                        // }
                        //
                        // // todo: assert we're in a class and have _classVariant
                        //
                        // return new FieldAccessExpression(
                        //     new LoadArgumentExpression(
                        //         0,
                        //         true,
                        //         _currentType),
                        //     fieldVariable.Name.StringValue,
                        //     "_classVariant",
                        //     valueUseful,
                        //     resolvedType);
                    }
                case TypeChecking.TypeChecker.FunctionSignatureParameter argument:
                {
                    throw new NotImplementedException();
                        // Debug.Assert(_currentFunction is not null);
                        //
                        // var argumentIndex = argument.ParameterIndex;
                        // if (!argument.ReferencedInClosure)
                        // {
                        //     if (argument.ContainingFunction.AccessedOuterVariables.Count > 0
                        //             || (argument.ContainingFunction.OwnerType is not null
                        //                 && !argument.ContainingFunction.IsStatic))
                        //     {
                        //         argumentIndex++;
                        //     }
                        //
                        //     return new LoadArgumentExpression(argumentIndex, valueUseful, resolvedType);
                        // }
                        //
                        // var currentFunction = _currentFunction.NotNull();
                        // var containingFunction = argument.ContainingFunction.NotNull();
                        // var containingFunctionLocals = _types[containingFunction.LocalsTypeId.NotNull()];
                        // var localsTypeReference = new LoweredConcreteTypeReference(
                        //                 containingFunctionLocals.Name,
                        //                 containingFunctionLocals.Id,
                        //                 []);
                        // if (containingFunction.Id == currentFunction.FunctionSignature.Id)
                        // {
                        //     return new FieldAccessExpression(
                        //         new LocalVariableAccessor(
                        //             "__locals",
                        //             true,
                        //             localsTypeReference),
                        //         argument.Name.StringValue,
                        //         "_classVariant",
                        //         e.ValueUseful,
                        //         resolvedType);
                        // }
                        // var closureTypeId = _currentFunction.NotNull()
                        //         .FunctionSignature.ClosureTypeId.NotNull();
                        // var closureType = _types[closureTypeId];
                        //
                        // return new FieldAccessExpression(
                        //     new FieldAccessExpression(
                        //         new LoadArgumentExpression(
                        //             0,
                        //             true,
                        //             new LoweredConcreteTypeReference(
                        //                 closureType.Name,
                        //                 closureTypeId,
                        //                 [])),
                        //         containingFunctionLocals.Name,
                        //         "_classVariant",
                        //         true,
                        //         localsTypeReference),
                        //     argument.Name.StringValue,
                        //     "_classVariant",
                        //     e.ValueUseful,
                        //     resolvedType);
                    }
            }

            throw new UnreachableException($"{variable.GetType()}");
        }
    }

    private static byte GetIntSize(TypeChecking.TypeChecker.ITypeReference type)
    {
        if (type is not TypeChecking.TypeChecker.InstantiatedClass klass)
        {
            throw new InvalidOperationException($"{type} must be instantiated class");
        }
        var typeId = klass.Signature.Id;
        if (typeId == DefId.Int64 || typeId == DefId.UInt64)
        {
            return 8;
        }

        if (typeId == DefId.Int32 || typeId == DefId.UInt32)
        {
            return 4;
        }

        if (typeId == DefId.Int16 || typeId == DefId.UInt16)
        {
            return 2;
        }

        if (typeId == DefId.Int8 || typeId == DefId.UInt8)
        {
            return 1;
        }

        throw new UnreachableException();
    }
    
    private static bool IsIntSigned(TypeChecking.TypeChecker.ITypeReference type)
    {
        if (type is not TypeChecking.TypeChecker.InstantiatedClass klass)
        {
            throw new InvalidOperationException($"{type} must be instantiated class");
        }
        var typeId = klass.Signature.Id;
        if (new[] { DefId.Int8, DefId.Int16, DefId.Int32, DefId.Int64 }.Contains(typeId))
        {
            return false;
        }
        if (new[] { DefId.UInt8, DefId.UInt16, DefId.UInt32, DefId.UInt64 }.Contains(typeId))
        {
            return true;
        }

        throw new UnreachableException();
    }

    private void LowerVariableDeclaration(VariableDeclarationExpression e)
    {
        var variableName = e.VariableDeclaration.Variable.NotNull()
            .Name.StringValue;

        var referencedInClosure = e.VariableDeclaration.Variable!.ReferencedInClosure;

        if (e.VariableDeclaration.Value is null)
        {
            // noop
            return;
        }

        var loweredValueOperand = NewLowerExpression(e.VariableDeclaration.Value).NotNull();

        if (!referencedInClosure)
        {
            
            var variable = _locals.First(x =>
                x.UserGivenName == e.VariableDeclaration.Variable.NotNull().Name.StringValue);
            _basicBlockStatements.Add(new Assign(new Local(variable.CompilerGivenName), new Use(loweredValueOperand)));
            return;
        }

        var localsTypeId = _currentFunction.NotNull().FunctionSignature.LocalsTypeId
            .NotNull();
        var localsType = _types[localsTypeId];

        // _basicBlockStatements.Add(new Assign(, new FieldAccess(new Local(LocalsObjectLocalName), variableName, ClassVariantName)));
        _basicBlockStatements.Add(new Assign(new Field(LocalsObjectLocalName, FieldName: variableName, ClassVariantName), new Use(loweredValueOperand)));
        
        // var localsFieldAssignment = new FieldAssignmentExpression(
        //     new LocalVariableAccessor("__locals",
        //         true,
        //         new LoweredConcreteTypeReference(
        //             localsType.Name,
        //             localsType.Id,
        //             [])),
        //     "_classVariant",
        //     variableName,
        //     loweredValue,
        //     // hard code this to false, because either `e.ValueUseful` was false,
        //     // or we're going to replace the value with a block
        //     false,
        //     loweredValue.ResolvedType);
    }
    
    private IOperand LowerBinaryExpression(Expressions.BinaryOperatorExpression binaryOperatorExpression)
    {
        var leftOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Left.NotNull());
        var rightOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Right.NotNull());

        switch (binaryOperatorExpression.BinaryOperator.OperatorType)
        {
            case BinaryOperatorType.ValueAssignment:
                // return;
            case BinaryOperatorType.BooleanAnd:
                // return;
            case BinaryOperatorType.BooleanOr:
                // return;
            default:
            {
                var binaryOperatorKind = binaryOperatorExpression.BinaryOperator.OperatorType switch
                {
                    BinaryOperatorType.LessThan => BinaryOperationKind.LessThan,
                    BinaryOperatorType.GreaterThan => BinaryOperationKind.GreaterThan,
                    BinaryOperatorType.Plus => BinaryOperationKind.Add,
                    BinaryOperatorType.Minus => BinaryOperationKind.Subtract,
                    BinaryOperatorType.Multiply => BinaryOperationKind.Multiply,
                    BinaryOperatorType.Divide => BinaryOperationKind.Divide,
                    BinaryOperatorType.EqualityCheck => BinaryOperationKind.Equal,
                    BinaryOperatorType.NegativeEqualityCheck => BinaryOperationKind.NotEqual,
                    _ => throw new ArgumentOutOfRangeException()
                };
                
                // _basicBlockStatements.Add(new Assign(,
                //     new Local(),
                //     new BinaryOperation(leftOperand, rightOperand, BinaryOperationKind.LessThan)));
                break;
            }
        }

        return null!;
    }
}