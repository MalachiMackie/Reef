using System.Diagnostics;
using Reef.Core.Expressions;
using Reef.Core.LoweredExpressions.New;
using MethodCall = Reef.Core.LoweredExpressions.New.MethodCall;

namespace Reef.Core.Abseil.New;

public partial class NewProgramAbseil
{
    private uint _controlFlowDepth = 0;
    
    private IOperand NewLowerExpression(Expressions.IExpression expression, IPlace? destination)
    {
        switch (expression)
        {
            case Expressions.BinaryOperatorExpression binaryOperatorExpression:
                return LowerBinaryExpression(binaryOperatorExpression, destination);
            case Expressions.BlockExpression blockExpression:
                return LowerBlock(blockExpression);
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
                return LowerMethodCall(methodCallExpression, destination);
            case Expressions.MethodReturnExpression methodReturnExpression:
                LowerReturn(methodReturnExpression);
                return new UnitConstant();
            case Expressions.ObjectInitializerExpression objectInitializerExpression:
                throw new NotImplementedException();
            case Expressions.StaticMemberAccessExpression staticMemberAccessExpression:
                throw new NotImplementedException();
            case Expressions.TupleExpression tupleExpression:
                return LowerTuple(tupleExpression, destination);
            case Expressions.UnaryOperatorExpression unaryOperatorExpression:
                return LowerUnaryOperator(unaryOperatorExpression, destination);
            case Expressions.UnionClassVariantInitializerExpression unionClassVariantInitializerExpression:
                throw new NotImplementedException();
            case Expressions.ValueAccessorExpression valueAccessorExpression:
                return LowerValueAccessor(valueAccessorExpression, destination);
            case Expressions.VariableDeclarationExpression variableDeclarationExpression:
                LowerVariableDeclaration(variableDeclarationExpression);
                return new UnitConstant();
            case Expressions.WhileExpression whileExpression:
                throw new NotImplementedException();
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }
    }

    private IOperand LowerTuple(TupleExpression tupleExpression, IPlace? destination)
    {
        if (tupleExpression.Values.Count == 1)
        {
            return NewLowerExpression(tupleExpression.Values[0], destination);
        }

        var typeReference = (GetTypeReference(tupleExpression.ResolvedType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();

        var localDestination = destination as Local ?? new Local(LocalName((ushort)_locals.Count));
        
        if (destination is not Local)
        {
            var local = new NewMethodLocal(localDestination.LocalName, null, typeReference);
            _locals.Add(local);
        }

        var values = tupleExpression.Values.Select(x => NewLowerExpression(x, destination: null)).ToArray();
        
        // always assign to a local, so fields get assign within the stack, then if needed, copy to it's destination
        _basicBlockStatements.Add(new Assign(
            localDestination,
            new CreateObject(typeReference)));

        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            _basicBlockStatements.Add(new Assign(
                new Field(localDestination.LocalName, $"Item{index}", ClassVariantName),
                new Use(value)));
        }

        if (destination is not (Local or null))
        {
            _basicBlockStatements.Add(new Assign(destination, new Use(new Copy(localDestination))));
        }

        return new Copy(destination ?? localDestination);
    }

    private void LowerReturn(MethodReturnExpression methodReturnExpression)
    {
        if (methodReturnExpression.MethodReturn.Expression is not null)
        {
            NewLowerExpression(methodReturnExpression.MethodReturn.Expression, new Local(ReturnValueLocalName));
        }

        if (_controlFlowDepth > 0)
        {
            _basicBlocks[^1].Terminator = new TempGoToReturn();
            _basicBlockStatements = [];
            _basicBlocks.Add(new BasicBlock(new BasicBlockId($"bb{_basicBlocks.Count}"), _basicBlockStatements));
        }
        else
        {
            _basicBlocks[^1].Terminator = new Return();
        }
    }

    private IOperand LowerMethodCall(MethodCallExpression e, IPlace? destination)
    {
        var returnType = GetTypeReference(e.ResolvedType.NotNull());
        if (destination is null)
        {
            var localName = $"_local{_locals.Count}";
            var local = new NewMethodLocal(localName, null, returnType);
            _locals.Add(local);
            
            destination = new Local(localName);
        }
        
        var instantiatedFunction = e.MethodCall.Method switch
        {
            MemberAccessExpression { MemberAccess.InstantiatedFunction: var fn } => fn,
            StaticMemberAccessExpression { StaticMemberAccess.InstantiatedFunction: var fn } => fn,
            ValueAccessorExpression { FunctionInstantiation: var fn } => fn,
            _ => null
        };

        IReadOnlyList<IOperand> originalArguments = [..e.MethodCall.ArgumentList.Select(x => NewLowerExpression(x, destination: null))];

        var arguments = new List<IOperand>(e.MethodCall.ArgumentList.Count);
        NewLoweredFunctionReference functionReference;

        // calling function object instead of normal function
        if (instantiatedFunction is null)
        {
            var functionObjectOperand = NewLowerExpression(e.MethodCall.Method, destination: null);
            
            var methodType = (GetTypeReference(e.MethodCall.Method.ResolvedType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();

            var fn = _importedPrograms.SelectMany(x =>
                x.Methods.Where(y => y.Name == $"Function`{e.MethodCall.ArgumentList.Count + 1}__Call"))
                .First();
            
            functionReference = GetFunctionReference(
                    fn.Id,
                    [],
                    methodType.TypeArguments);

            arguments.Add(functionObjectOperand);
            
            arguments.AddRange(originalArguments);

            var lastBasicBlock = _basicBlocks[^1];
            _basicBlockStatements = [];
            var newBasicBlock = new BasicBlock(new BasicBlockId($"bb{_basicBlocks.Count}"), _basicBlockStatements);
            _basicBlocks.Add(newBasicBlock);

            lastBasicBlock.Terminator = new MethodCall(functionReference, arguments, destination, newBasicBlock.Id);

            return new Copy(destination);
        }
        
        IReadOnlyList<INewLoweredTypeReference> ownerTypeArguments = [];
        if (e.MethodCall.Method is MemberAccessExpression memberAccess)
        {
            var owner = NewLowerExpression(memberAccess.MemberAccess.Owner, null);
            arguments.Add(owner);
            ownerTypeArguments = GetTypeReference(memberAccess.MemberAccess.Owner.ResolvedType.NotNull()).NotNull() is NewLoweredConcreteTypeReference concrete
                ? concrete.TypeArguments
                : throw new UnreachableException("Shouldn't ever be able to call a method on a generic parameter");
        }
        else if (instantiatedFunction.ClosureTypeId is not null)
        {
            var createClosure = CreateClosureObject(instantiatedFunction);
            arguments.Add(createClosure);
        }
        else if (instantiatedFunction is { IsStatic: false, OwnerType: not null }
                 && _currentType is not null
                 && EqualTypeReferences(GetTypeReference(instantiatedFunction.OwnerType), _currentType)
                 && _currentFunction is not null
                 && EqualTypeReferences(_currentFunction.Value.LoweredMethod.ParameterLocals[0].Type, _currentType))
        {
            arguments.Add(new Copy(new Local(ParameterLocalName(parameterIndex: 0))));
        }

        if (e.MethodCall.Method is Expressions.StaticMemberAccessExpression staticMemberAccess)
        {
            ownerTypeArguments = (GetTypeReference(staticMemberAccess.OwnerType.NotNull())
                as NewLoweredConcreteTypeReference).NotNull().TypeArguments;
        }
        else if (e.MethodCall.Method is Expressions.ValueAccessorExpression valueAccessor)
        {
            if (_currentType is not null)
            {
                ownerTypeArguments = _currentType.TypeArguments;
            }
            else if (valueAccessor.FunctionInstantiation.NotNull()
                    .OwnerType is {} ownerType)
            {
                var ownerTypeReference = GetTypeReference(ownerType);
                if (ownerTypeReference is NewLoweredConcreteTypeReference
                    {
                        TypeArguments: var ownerReferenceTypeArguments
                    })
                {
                    ownerTypeArguments = ownerReferenceTypeArguments;
                }
            }
        }

        functionReference = GetFunctionReference(instantiatedFunction.FunctionId,
            [..instantiatedFunction.TypeArguments.Select(GetTypeReference)],
            ownerTypeArguments);

        arguments.AddRange(originalArguments);

        {
            var lastBasicBlock = _basicBlocks[^1];
            _basicBlockStatements = [];
            var newBasicBlock = new BasicBlock(new BasicBlockId($"bb{_basicBlocks.Count}"), _basicBlockStatements);
            _basicBlocks.Add(newBasicBlock);

            lastBasicBlock.Terminator = new MethodCall(functionReference, arguments, destination, newBasicBlock.Id);

            return new Copy(destination);
        }
    }

    private IOperand CreateClosureObject(TypeChecking.TypeChecker.InstantiatedFunction fn)
    {
        throw new NotImplementedException();
    }

    private IOperand LowerBlock(BlockExpression blockExpression)
    {
        IOperand? result = null;
        foreach (var innerExpression in blockExpression.Block.Expressions)
        {
            result = NewLowerExpression(innerExpression, destination: null);
        }

        // if no result, then it must just be a unit constant
        return result ?? new UnitConstant();
    }

    private IOperand LowerUnaryOperator(UnaryOperatorExpression unaryOperatorExpression, IPlace? destination)
    {
        if (unaryOperatorExpression.UnaryOperator.OperatorType == UnaryOperatorType.FallOut)
        {
            throw new NotImplementedException();
        }
        
        var valueOperand = NewLowerExpression(unaryOperatorExpression.UnaryOperator.Operand.NotNull(), destination: null).NotNull();

        if (destination is null)
        {
            var localName = $"_local{_locals.Count}";
            _locals.Add(new NewMethodLocal(localName, null, GetTypeReference(unaryOperatorExpression.ResolvedType.NotNull())));
            destination = new Local(localName);
        }

        _basicBlockStatements.Add(
            new Assign(
                destination,
                new UnaryOperation(valueOperand, unaryOperatorExpression.UnaryOperator.OperatorType switch
                {
                    UnaryOperatorType.Not => UnaryOperationKind.Not,
                    _ => throw new NotImplementedException()
                })));

        return new Copy(destination);
    }

    private IOperand LowerValueAccessor(ValueAccessorExpression e, IPlace? destination)
    {
        var operand = e switch
        {
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: StringToken { StringValue: var stringLiteral } } } => new StringConstant(stringLiteral),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: IntToken { Type: TokenType.IntLiteral, IntValue: var intValue} }, ResolvedType: var resolvedType} =>
                IsIntSigned(resolvedType.NotNull())
                    ? new IntConstant(intValue, GetIntSize(resolvedType.NotNull()))
                    : new UIntConstant((ulong)intValue, GetIntSize(resolvedType.NotNull())),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token.Type: TokenType.True }} => new BoolConstant(true),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token.Type: TokenType.False }} => new BoolConstant(false),
            { ValueAccessor.AccessType: Expressions.ValueAccessType.Variable, ReferencedVariable: {} variable} => VariableAccess(variable, e.ValueUseful),
            { ValueAccessor.AccessType: Expressions.ValueAccessType.Variable, FunctionInstantiation: {} fn} => FunctionAccess(fn, (e.ResolvedType as TypeChecking.TypeChecker.FunctionObject).NotNull(), e.ValueUseful),
            _ => throw new UnreachableException($"{e}")
        };

        if (destination is not null)
        {
            _basicBlockStatements.Add(new Assign(destination, new Use(operand)));
        }

        return operand;
        
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
                    Debug.Assert(_currentFunction is not null);
                    
                    var argumentIndex = argument.ParameterIndex;
                    if (!argument.ReferencedInClosure)
                    {
                        if (argument.ContainingFunction.AccessedOuterVariables.Count > 0
                                || (argument.ContainingFunction.OwnerType is not null
                                    && !argument.ContainingFunction.IsStatic))
                        {
                            argumentIndex++;
                        }

                        return new Copy(new Local(ParameterLocalName(argumentIndex)));
                    }
                    
                    var currentFunction = _currentFunction.NotNull();
                    var containingFunction = argument.ContainingFunction.NotNull();
                    var containingFunctionLocals = _types[containingFunction.LocalsTypeId.NotNull()];
                    var localsTypeReference = new NewLoweredConcreteTypeReference(
                                    containingFunctionLocals.Name,
                                    containingFunctionLocals.Id,
                                    []);
                    if (containingFunction.Id == currentFunction.FunctionSignature.Id)
                    {
                        return new Copy(new Field(LocalsObjectLocalName, argument.Name.StringValue, ClassVariantName));
                    }
                    var closureTypeId = _currentFunction.NotNull()
                            .FunctionSignature.ClosureTypeId.NotNull();
                    var closureType = _types[closureTypeId];

                    var referencedLocalsObjectLocalName = LocalName((uint)_locals.Count);
                    _locals.Add(new NewMethodLocal(referencedLocalsObjectLocalName, null, localsTypeReference));
                    _basicBlockStatements.Add(new Assign(
                        new Local(referencedLocalsObjectLocalName),
                        new Use(new Copy(new Field(ParameterLocalName(0), containingFunctionLocals.Name, ClassVariantName)))));

                    return new Copy(new Field(referencedLocalsObjectLocalName, argument.Name.StringValue, ClassVariantName));
                }
            }

            throw new UnreachableException($"{variable.GetType()}");
        }
    }

    private static byte GetIntSize(TypeChecking.TypeChecker.ITypeReference type)
    {
        if (type is TypeChecking.TypeChecker.UnspecifiedSizedIntType unspecifiedSizedIntType)
        {
            type = unspecifiedSizedIntType.ResolvedIntType.NotNull();
        }
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
        if (type is TypeChecking.TypeChecker.UnspecifiedSizedIntType unspecifiedSizedIntType)
        {
            type = unspecifiedSizedIntType.ResolvedIntType.NotNull();
        }
        
        if (type is not TypeChecking.TypeChecker.InstantiatedClass klass)
        {
            throw new InvalidOperationException($"{type} must be instantiated class");
        }
        var typeId = klass.Signature.Id;
        if (new[] { DefId.Int8, DefId.Int16, DefId.Int32, DefId.Int64 }.Contains(typeId))
        {
            return true;
        }
        if (new[] { DefId.UInt8, DefId.UInt16, DefId.UInt32, DefId.UInt64 }.Contains(typeId))
        {
            return false;
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

        // var loweredValueOperand = NewLowerExpression(e.VariableDeclaration.Value).NotNull();

        if (!referencedInClosure)
        {
            var variable = _locals.First(x =>
                x.UserGivenName == e.VariableDeclaration.Variable.NotNull().Name.StringValue);
            NewLowerExpression(e.VariableDeclaration.Value, destination: new Local(variable.CompilerGivenName));
            return;
        }

        var localsTypeId = _currentFunction.NotNull().FunctionSignature.LocalsTypeId
            .NotNull();
        var localsType = _types[localsTypeId];

        // _basicBlockStatements.Add(new Assign(, new FieldAccess(new Local(LocalsObjectLocalName), variableName, ClassVariantName)));
        NewLowerExpression(e.VariableDeclaration.Value, destination: new Field(LocalsObjectLocalName, FieldName: variableName, ClassVariantName));
        
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
    
    private IOperand LowerBinaryExpression(BinaryOperatorExpression binaryOperatorExpression, IPlace? destination)
    {
        IOperand resultOperand;
        if (binaryOperatorExpression.BinaryOperator.OperatorType == BinaryOperatorType.ValueAssignment)
        {
            resultOperand = LowerValueAssignment(
                binaryOperatorExpression.BinaryOperator.Left.NotNull(),
                binaryOperatorExpression.BinaryOperator.Right.NotNull());
            
            if (destination is not null)
            {
                _basicBlockStatements.Add(new Assign(destination, new Use(resultOperand)));
            }

            return resultOperand;
        }
        
        if (destination is null)
        {
            var localName = $"_local{_locals.Count}";
            _locals.Add(new NewMethodLocal(localName, null, GetTypeReference(binaryOperatorExpression.ResolvedType.NotNull())));
            destination = new Local(localName);
        }
        
        resultOperand = new Copy(destination);

        switch (binaryOperatorExpression.BinaryOperator.OperatorType)
        {
            case BinaryOperatorType.BooleanAnd:
            {
                var leftOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Left.NotNull(), null).NotNull();

                if (_basicBlocks.Count == 0)
                {
                    _basicBlocks.Add(new BasicBlock(new BasicBlockId("bb0"), _basicBlockStatements));
                }
                var previousBasicBlock = _basicBlocks[^1];

                var trueBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count}");
                var falseBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count + 1}");
                var afterBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count + 2}");
                
                previousBasicBlock.Terminator = new SwitchInt(
                    leftOperand,
                    new Dictionary<int, BasicBlockId>
                    {
                        { 0, falseBasicBlockId }
                    },
                    trueBasicBlockId);
                
                _basicBlockStatements = [];
                var trueBasicBlock = new BasicBlock(trueBasicBlockId, _basicBlockStatements)
                {
                    Terminator = new GoTo(afterBasicBlockId)
                };
                _basicBlocks.Add(trueBasicBlock);

                _controlFlowDepth++;
                var rightOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Right.NotNull(), destination: null)
                    .NotNull();
                _controlFlowDepth--;
                
                _basicBlockStatements.Add(new Assign(destination, new Use(rightOperand)));
                
                _basicBlockStatements = [new Assign(destination, new Use(new BoolConstant(false)))];
                var falseBasicBlock = new BasicBlock(falseBasicBlockId, _basicBlockStatements)
                {
                    Terminator = new GoTo(afterBasicBlockId)
                };
                _basicBlocks.Add(falseBasicBlock);

                _basicBlockStatements = [];
                _basicBlocks.Add(new BasicBlock(afterBasicBlockId, _basicBlockStatements));
                break;
            }
            case BinaryOperatorType.BooleanOr:
            {
                var leftOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Left.NotNull(), destination: null).NotNull();

                if (_basicBlocks.Count == 0)
                {
                    _basicBlocks.Add(new BasicBlock(new BasicBlockId("bb0"), _basicBlockStatements));
                }
                var previousBasicBlock = _basicBlocks[^1];

                var falseBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count}");
                var trueBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count + 1}");
                var afterBasicBlockId = new BasicBlockId($"bb{_basicBlocks.Count + 2}");
                
                previousBasicBlock.Terminator = new SwitchInt(
                    leftOperand,
                    new Dictionary<int, BasicBlockId>
                    {
                        { 0, falseBasicBlockId }
                    },
                    trueBasicBlockId);
                
                _basicBlockStatements = [];
                var falseBasicBlock = new BasicBlock(falseBasicBlockId, _basicBlockStatements)
                {
                    Terminator = new GoTo(afterBasicBlockId)
                };
                _basicBlocks.Add(falseBasicBlock);

                _controlFlowDepth++;
                var rightOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Right.NotNull(), destination: null)
                    .NotNull();
                _controlFlowDepth--;
                _basicBlockStatements.Add(new Assign(destination, new Use(rightOperand)));
                
                _basicBlockStatements = [new Assign(destination, new Use(new BoolConstant(true)))];
                var trueBasicBlock = new BasicBlock(trueBasicBlockId, _basicBlockStatements)
                {
                    Terminator = new GoTo(afterBasicBlockId)
                };
                _basicBlocks.Add(trueBasicBlock);

                _basicBlockStatements = [];
                _basicBlocks.Add(new BasicBlock(afterBasicBlockId, _basicBlockStatements));
                break;
            }
            default:
            {
                var leftOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Left.NotNull(), destination: null).NotNull();
                var rightOperand = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Right.NotNull(), destination: null).NotNull();
                
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

                _basicBlockStatements.Add(new Assign(
                    destination,
                    new BinaryOperation(leftOperand, rightOperand, binaryOperatorKind)));
                break;
            }
        }

        return resultOperand;
    }
    
    private IOperand LowerValueAssignment(
            IExpression left,
            IExpression right)
    {
        var valueOperand = NewLowerExpression(right, null).NotNull();
        if (left is Expressions.ValueAccessorExpression valueAccessor)
        {
            var variable = valueAccessor.ReferencedVariable.NotNull();
            if (variable is TypeChecking.TypeChecker.LocalVariable localVariable)
            {
                if (localVariable.ReferencedInClosure)
                {
                    var containingFunction = localVariable.ContainingFunction;
                    Debug.Assert(_currentFunction.HasValue);
                    Debug.Assert(containingFunction is not null);
                    Debug.Assert(containingFunction.LocalsTypeId is not null);
                    var localsType = _types[containingFunction.LocalsTypeId];
                    var localsTypeReference = new NewLoweredConcreteTypeReference(
                        localsType.Name,
                        localsType.Id,
                        []);

                    if (_currentFunction.Value.FunctionSignature == containingFunction)
                    {
                        _basicBlockStatements.Add(new Assign(
                            new Field(LocalsObjectLocalName, localVariable.Name.StringValue, ClassVariantName),
                            new Use(valueOperand)));
                        
                        return valueOperand;
                    }

                    Debug.Assert(_currentFunction.Value.FunctionSignature.ClosureTypeId is not null);
                    var closureType = _types[_currentFunction.Value.FunctionSignature.ClosureTypeId];
                    var closureTypeReference = new NewLoweredConcreteTypeReference(
                            closureType.Name,
                            closureType.Id,
                            []);

                    Debug.Assert(_currentFunction.Value.LoweredMethod.ParameterLocals.Count > 0);
                    Debug.Assert(EqualTypeReferences(
                            closureTypeReference,
                            _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type));

                    var localsLocal = new NewMethodLocal($"_local{_locals.Count}", null, localsTypeReference);
                    _locals.Add(localsLocal);
                    _basicBlockStatements.Add(
                        new Assign(
                            new Local(localsLocal.CompilerGivenName),
                            new Use(new Copy(new Field(_currentFunction.Value.LoweredMethod.ParameterLocals[0].CompilerGivenName, localsType.Name, ClassVariantName)))));
                    _basicBlockStatements.Add(
                        new Assign(
                            new Field(localsLocal.CompilerGivenName, localVariable.Name.StringValue, ClassVariantName),
                            new Use(valueOperand)));

                    return valueOperand;
                }

                var local = _locals.First(x => x.UserGivenName == localVariable.Name.StringValue);

                _basicBlockStatements.Add(
                    new Assign(
                        new Local(local.CompilerGivenName),
                        new Use(valueOperand)));

                return valueOperand;
            }

            if (variable is TypeChecking.TypeChecker.FieldVariable fieldVariable)
            {
                Debug.Assert(_currentType is not null);
                if (fieldVariable.IsStaticField)
                {
                    throw new NotImplementedException();
                    // return new StaticFieldAssignmentExpression(
                    //     _currentType,
                    //     fieldVariable.Name.StringValue,
                    //     LowerExpression(right),
                    //     valueUseful,
                    //     resolvedType);
                }

                if (fieldVariable.ReferencedInClosure
                    && _currentFunction is
                    {
                        FunctionSignature: { ClosureTypeId: not null} functionSignature
                    })
                {
                    var closureType = _types[functionSignature.ClosureTypeId];
                    var closureTypeReference = new NewLoweredConcreteTypeReference(
                            closureType.Name,
                            closureType.Id,
                            []);
                    
                    var thisLocal = new NewMethodLocal($"_local{_locals.Count}", null, _currentType);
                    _basicBlockStatements.Add(
                        new Assign(
                            new Local(thisLocal.CompilerGivenName),
                            new Use(new Copy(new Field(_currentFunction.NotNull().LoweredMethod.ParameterLocals[0].CompilerGivenName, ClosureThisFieldName, ClassVariantName)))));
                    _basicBlockStatements.Add(
                        new Assign(
                            new Field(thisLocal.CompilerGivenName, fieldVariable.Name.StringValue, ClassVariantName),
                            new Use(valueOperand)));

                    return valueOperand;
                }

                Debug.Assert(fieldVariable.ContainingSignature.Id == _currentType.DefinitionId);
                Debug.Assert(_currentFunction is not null);
                Debug.Assert(_currentFunction.Value.LoweredMethod.ParameterLocals.Count > 0);
                Debug.Assert(EqualTypeReferences(
                            _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type,
                            _currentType));

                _basicBlockStatements.Add(
                    new Assign(
                        new Field(_currentFunction.Value.LoweredMethod.ParameterLocals[0].CompilerGivenName, fieldVariable.Name.StringValue, ClassVariantName),
                        new Use(valueOperand)));

                return valueOperand;
            }

            throw new UnreachableException(variable.ToString());
        }

        if (left is Expressions.MemberAccessExpression memberAccess)
        {
            throw new NotImplementedException();
            // var memberOwner = NewLowerExpression(memberAccess.MemberAccess.Owner).NotNull();
            //
            // _basicBlockStatements.Add(new Assign(
            //     new Field()));
            //
            // return new FieldAssignmentExpression(
            //     memberOwner,
            //     "_classVariant",
            //     memberAccess.MemberAccess.MemberName.NotNull().StringValue,
            //     LowerExpression(right),
            //     valueUseful,
            //     resolvedType);
        }

        if (left is Expressions.StaticMemberAccessExpression staticMemberAccess)
        {
            throw new NotImplementedException();
            // if (GetTypeReference(staticMemberAccess.OwnerType.NotNull())
            //         is not LoweredConcreteTypeReference concreteType)
            // {
            //     throw new InvalidOperationException("Expected type to be concrete");
            // }
            //
            // return new StaticFieldAssignmentExpression(
            //     concreteType,
            //     staticMemberAccess.StaticMemberAccess.MemberName.NotNull().StringValue,
            //     LowerExpression(right),
            //     valueUseful,
            //     resolvedType);
        }

        throw new UnreachableException();
    }
}