using System.Diagnostics;
using Reef.Core.Expressions;
using Reef.Core.LoweredExpressions.New;
using MethodCall = Reef.Core.LoweredExpressions.New.MethodCall;

namespace Reef.Core.Abseil.New;

public partial class NewProgramAbseil
{
    private uint _controlFlowDepth;

    private interface IExpressionResult
    {
        IOperand ToOperand();
    }

    private sealed record OperandResult(IOperand Value) : IExpressionResult
    {
        public IOperand ToOperand() => Value;
    }

    private sealed record PlaceResult(IPlace Value) : IExpressionResult
    {
        public IOperand ToOperand() => new Copy(Value);
    }
    
    private IExpressionResult NewLowerExpression(Expressions.IExpression expression, IPlace? destination)
    {
        switch (expression)
        {
            case Expressions.BinaryOperatorExpression binaryOperatorExpression:
                return LowerBinaryExpression(binaryOperatorExpression, destination);
            case Expressions.BlockExpression blockExpression:
                return LowerBlock(blockExpression, destination);
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
                return LowerMemberAccess(memberAccessExpression, destination);
            case Expressions.MethodCallExpression methodCallExpression:
                return LowerMethodCall(methodCallExpression, destination);
            case Expressions.MethodReturnExpression methodReturnExpression:
                LowerReturn(methodReturnExpression);
                return new OperandResult(new UnitConstant());
            case Expressions.ObjectInitializerExpression objectInitializerExpression:
                return LowerObjectInitializer(objectInitializerExpression, destination); 
            case Expressions.StaticMemberAccessExpression staticMemberAccessExpression:
                return LowerStaticMemberAccess(staticMemberAccessExpression, destination);
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
                return new OperandResult(new UnitConstant());
            case Expressions.WhileExpression whileExpression:
                throw new NotImplementedException();
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }
    }
    
    private IExpressionResult LowerStaticMemberAccess(
            StaticMemberAccessExpression e,
            IPlace? destination)
    {
        switch (e.StaticMemberAccess.MemberType)
        {
            case MemberType.Variant:
            {
                var unionType = GetTypeReference(e.OwnerType.NotNull())
                    as NewLoweredConcreteTypeReference ?? throw new UnreachableException();

                var dataType = _types[unionType.DefinitionId];
                var variantName = e.StaticMemberAccess.MemberName.NotNull().StringValue;
                var (variantIdentifier, variant) = dataType.Variants.Index()
                    .First(x => x.Item.Name == variantName);

                if (variant.Fields is [{ Name: VariantIdentifierFieldName }])
                {
                    return CreateObject(
                        unionType,
                        variantName: e.StaticMemberAccess.MemberName.NotNull().StringValue,
                        [
                            new CreateObjectField(VariantIdentifierFieldName,
                                new UIntConstant((ulong)variantIdentifier, 2))
                        ],
                        destination);
                }
                
                // we're statically accessing this variant, and there's at least one field. It must be a tuple variant
                // because you can't access a class variant directly outside creating it. We're returning a 
                // function object for this tuple create function

                if (e.ResolvedType is not TypeChecking.TypeChecker.FunctionObject)
                {
                    throw new InvalidOperationException($"Expected a function object, got a {e.ResolvedType?.GetType()}");
                }
                
                var ownerTypeArguments = unionType.TypeArguments;

                var fn = e.StaticMemberAccess.InstantiatedFunction.NotNull();

                var functionObjectType =
                    (GetTypeReference(e.ResolvedType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();

                return CreateObject(
                    functionObjectType,
                    ClassVariantName,
                    [
                        new CreateObjectField("FunctionReference", new FunctionPointerConstant(
                            GetFunctionReference(
                                fn.FunctionId,
                                [..fn.TypeArguments.Select(GetTypeReference)],
                                ownerTypeArguments))),
                    ],
                    destination);

            }
            case MemberType.Function:
            {
                var ownerTypeArguments = (GetTypeReference(e.OwnerType.NotNull()) as NewLoweredConcreteTypeReference).NotNull().TypeArguments;
                var fn = e.StaticMemberAccess.InstantiatedFunction.NotNull();

                return CreateObject(
                    (GetTypeReference(e.ResolvedType.NotNull()) as NewLoweredConcreteTypeReference).NotNull(),
                    ClassVariantName,
                    [new CreateObjectField("FunctionReference", new FunctionPointerConstant(
                        GetFunctionReference(fn.FunctionId,
                            [..fn.TypeArguments.Select(GetTypeReference)],
                            ownerTypeArguments)))],
                    destination);
            }
            case MemberType.Field:
            {
                var staticField = new StaticField(
                    (GetTypeReference(e.OwnerType.NotNull()) as NewLoweredConcreteTypeReference).NotNull(),
                    e.StaticMemberAccess.MemberName.NotNull().StringValue);

                if (destination is not null)
                {
                    _basicBlockStatements.Add(new Assign(
                        destination,
                        new Use(new Copy(staticField))));
                }
                
                return new PlaceResult(destination ?? staticField);
            }
            default:
                throw new UnreachableException();
        }
    }
    
    private IExpressionResult LowerMemberAccess(
            MemberAccessExpression e,
            IPlace? destination)
    {
        var ownerType = GetTypeReference(e.MemberAccess.OwnerType.NotNull());
        
        var ownerResult = NewLowerExpression(e.MemberAccess.Owner, null);
        
        switch (e.MemberAccess.MemberType.NotNull())
        {
            case Expressions.MemberType.Field:
            {
                IPlace ownerPlace;

                switch (ownerResult)
                {
                    case OperandResult{Value: var operand}:
                    {
                        var localName = LocalName((uint)_locals.Count);
                        _locals.Add(new NewMethodLocal(localName, null, GetTypeReference(e.MemberAccess.OwnerType.NotNull())));
            
                        ownerPlace = new Local(localName);
            
                        _basicBlockStatements.Add(new Assign(
                            ownerPlace,
                            new Use(operand)));
                        break;
                    }
                    case PlaceResult{Value: var place}:
                        ownerPlace = place;
                        break;
                    default:
                        throw new UnreachableException();
                }
                
                var field = new Field(ownerPlace, e.MemberAccess.MemberName.NotNull().StringValue, ClassVariantName);

                if (destination is not null)
                {
                    _basicBlockStatements.Add(new Assign(
                        destination,
                        new Use(new Copy(field))));
                }
                
                return new PlaceResult(destination ?? field);
            }
            case Expressions.MemberType.Function:
                {
                    var ownerTypeArguments = (GetTypeReference(e.MemberAccess.OwnerType.NotNull()) as NewLoweredConcreteTypeReference)
                        .NotNull().TypeArguments;

                    var fn = e.MemberAccess.InstantiatedFunction.NotNull();

                    var functionObjectType =
                        (GetTypeReference(e.ResolvedType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();

                    return CreateObject(
                        functionObjectType,
                        ClassVariantName,
                        [
                            new CreateObjectField("FunctionReference", new FunctionPointerConstant(
                                    GetFunctionReference(
                                        fn.FunctionId,
                                        [..fn.TypeArguments.Select(GetTypeReference)],
                                        ownerTypeArguments))),
                            new CreateObjectField("FunctionParameter", ownerResult.ToOperand())
                        ],
                        destination);
                }
            case Expressions.MemberType.Variant:
                throw new InvalidOperationException("Can never access a variant through instance member access");
            default:
                throw new UnreachableException($"{e.MemberAccess.MemberType}");
        }
    }

    private IExpressionResult LowerObjectInitializer(ObjectInitializerExpression objectInitializerExpression, IPlace? destination)
    {
        var typeReference = (GetTypeReference(objectInitializerExpression.ResolvedType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();

        return CreateObject(
            typeReference,
            ClassVariantName,
            objectInitializerExpression.ObjectInitializer.FieldInitializers.Select(x =>
                new CreateObjectField(x.FieldName.StringValue, x.Value)),
            destination);
    }

    private sealed class CreateObjectField
    {
        public string FieldName { get; }
        public IExpression? Expression { get; }
        public IOperand? Operand { get; }

        public CreateObjectField(string fieldName, IExpression? expression)
        {
            FieldName = fieldName;
            Expression = expression;
            Operand = null;
        }
        
        public CreateObjectField(string fieldName, IOperand? operand)
        {
            FieldName = fieldName;
            Operand = operand;
            Expression = null;
        }
    }

    private IExpressionResult CreateObject(
        NewLoweredConcreteTypeReference type,
        string variantName,
        IEnumerable<CreateObjectField> fields,
        IPlace? destination)
    {
        // always assign to a local, so fields get assign within the stack, then if needed, copy to it's destination
        
        var localName = LocalName((uint)_locals.Count);
        var localDestination = destination as Local ?? new Local(localName);

        if (destination is not Local)
        {
            _locals.Add(new NewMethodLocal(localName, null, type));
        }
        
        _basicBlockStatements.Add(new Assign(
            localDestination,
            new CreateObject(type)));

        foreach (var createObjectField in fields)
        {
            var field = new Field(localDestination, createObjectField.FieldName, variantName);
            if (createObjectField.Expression is {} expression)
            {
                NewLowerExpression(expression, field);
            }
            else if (createObjectField.Operand is {} operand)
            {
                _basicBlockStatements.Add(new Assign(
                    field,
                    new Use(operand)));
            }
        }

        if (destination is Local)
        {
            return new PlaceResult(destination);
        }

        if (destination is not null)
        {
            _basicBlockStatements.Add(new Assign(
                destination,
                new Use(new Copy(localDestination))));
        }

        return new PlaceResult(destination ?? localDestination);
    }

    private IExpressionResult LowerTuple(TupleExpression tupleExpression, IPlace? destination)
    {
        if (tupleExpression.Values.Count == 1)
        {
            return NewLowerExpression(tupleExpression.Values[0], destination);
        }

        var typeReference = (GetTypeReference(tupleExpression.ResolvedType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();

        return CreateObject(
            typeReference,
            ClassVariantName,
            tupleExpression.Values.Select((x, i) => new CreateObjectField($"Item{i}", x)),
            destination);
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

    private IExpressionResult LowerMethodCall(MethodCallExpression e, IPlace? destination)
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

        IReadOnlyList<IOperand> originalArguments = [..e.MethodCall.ArgumentList.Select(x => NewLowerExpression(x, destination: null).ToOperand())];

        var arguments = new List<IOperand>(e.MethodCall.ArgumentList.Count);
        NewLoweredFunctionReference functionReference;

        // calling function object instead of normal function
        if (instantiatedFunction is null)
        {
            var functionObjectResult = NewLowerExpression(e.MethodCall.Method, destination: null);
            
            var methodType = (GetTypeReference(e.MethodCall.Method.ResolvedType.NotNull()) as NewLoweredConcreteTypeReference).NotNull();

            var fn = _importedPrograms.SelectMany(x =>
                x.Methods.Where(y => y.Name == $"Function`{e.MethodCall.ArgumentList.Count + 1}__Call"))
                .First();
            
            functionReference = GetFunctionReference(
                    fn.Id,
                    [],
                    methodType.TypeArguments);

            arguments.Add(functionObjectResult.ToOperand());
            
            arguments.AddRange(originalArguments);

            var lastBasicBlock = _basicBlocks[^1];
            _basicBlockStatements = [];
            var newBasicBlock = new BasicBlock(new BasicBlockId($"bb{_basicBlocks.Count}"), _basicBlockStatements);
            _basicBlocks.Add(newBasicBlock);

            lastBasicBlock.Terminator = new MethodCall(functionReference, arguments, destination, newBasicBlock.Id);

            return new PlaceResult(destination);
        }
        
        IReadOnlyList<INewLoweredTypeReference> ownerTypeArguments = [];
        if (e.MethodCall.Method is MemberAccessExpression memberAccess)
        {
            var owner = NewLowerExpression(memberAccess.MemberAccess.Owner, null);
            arguments.Add(owner.ToOperand());
            ownerTypeArguments = GetTypeReference(memberAccess.MemberAccess.Owner.ResolvedType.NotNull()).NotNull() is NewLoweredConcreteTypeReference concrete
                ? concrete.TypeArguments
                : throw new UnreachableException("Shouldn't ever be able to call a method on a generic parameter");
        }
        else if (instantiatedFunction.ClosureTypeId is not null)
        {
            var createClosure = CreateClosureObject(instantiatedFunction);
            arguments.Add(createClosure.ToOperand());
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

            return new PlaceResult(destination);
        }
    }

    private IExpressionResult CreateClosureObject(TypeChecking.TypeChecker.InstantiatedFunction fn)
    {
        throw new NotImplementedException();
    }

    private IExpressionResult LowerBlock(BlockExpression blockExpression, IPlace? destination)
    {
        IExpressionResult? result = null;
        foreach (var innerExpression in blockExpression.Block.Expressions)
        {
            result = NewLowerExpression(innerExpression, destination: null);
        }

        // if no result, then it must just be a unit constant
        result ??= new OperandResult(new UnitConstant());

        if (destination is not null)
        {
            _basicBlockStatements.Add(new Assign(
                destination,
                new Use(result.ToOperand())));
        }

        return result;
    }

    private IExpressionResult LowerUnaryOperator(UnaryOperatorExpression unaryOperatorExpression, IPlace? destination)
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
                new UnaryOperation(valueOperand.ToOperand(), unaryOperatorExpression.UnaryOperator.OperatorType switch
                {
                    UnaryOperatorType.Not => UnaryOperationKind.Not,
                    _ => throw new UnreachableException()
                })));

        return new PlaceResult(destination);
    }

    private IExpressionResult LowerValueAccessor(ValueAccessorExpression e, IPlace? destination)
    {
        if (e is { ValueAccessor.AccessType: Expressions.ValueAccessType.Variable, FunctionInstantiation: { } fn })
        {
            // function access already assigns the value to destination, so handle it separately
            return FunctionAccess(fn, (e.ResolvedType as TypeChecking.TypeChecker.FunctionObject).NotNull(),
                destination);
        }
        
        var operand = e switch
        {
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: StringToken { StringValue: var stringLiteral } } } => new OperandResult(new StringConstant(stringLiteral)),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: IntToken { Type: TokenType.IntLiteral, IntValue: var intValue} }, ResolvedType: var resolvedType} =>
                new OperandResult(IsIntSigned(resolvedType.NotNull())
                    ? new IntConstant(intValue, GetIntSize(resolvedType.NotNull()))
                    : new UIntConstant((ulong)intValue, GetIntSize(resolvedType.NotNull()))),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token.Type: TokenType.True }} => new OperandResult(new BoolConstant(true)),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token.Type: TokenType.False }} => new OperandResult(new BoolConstant(false)),
            { ValueAccessor.AccessType: Expressions.ValueAccessType.Variable, ReferencedVariable: {} variable} => VariableAccess(variable, e.ValueUseful),
            _ => throw new UnreachableException($"{e}")
        };

        if (destination is not null)
        {
            _basicBlockStatements.Add(new Assign(destination, new Use(operand.ToOperand())));
        }

        return operand;
        
        IExpressionResult FunctionAccess(
                TypeChecking.TypeChecker.InstantiatedFunction innerFn,
                TypeChecking.TypeChecker.FunctionObject typeReference,
                IPlace? innerDestination)
        {
            var ownerTypeArguments = _currentType?.TypeArguments ?? [];

            var functionObjectParameters = new List<CreateObjectField>
            {
                new ("FunctionReference",
                    new FunctionPointerConstant(
                        GetFunctionReference(
                            innerFn.FunctionId,
                            [..innerFn.TypeArguments.Select(GetTypeReference)],
                            ownerTypeArguments)))
            };
            
            if (innerFn.ClosureTypeId is not null)
            {
                functionObjectParameters.Add(new CreateObjectField("FunctionParameter", CreateClosureObject(innerFn).ToOperand()));
            }
            else if (innerFn is { IsStatic: false, OwnerType: not null }
                     && _currentType is not null
                     && EqualTypeReferences(GetTypeReference(innerFn.OwnerType), _currentType)
                     && _currentFunction is not null
                     && EqualTypeReferences(_currentFunction.Value.LoweredMethod.ParameterLocals[0].Type, _currentType))
            {
                functionObjectParameters.Add(new CreateObjectField("FunctionParameter", new Copy(new Local(ParameterLocalName(0)))));
            }

            return CreateObject(
                (GetTypeReference(typeReference) as NewLoweredConcreteTypeReference).NotNull(),
                ClassVariantName,
                functionObjectParameters,
                innerDestination);
        }

        IExpressionResult VariableAccess(
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
                            return new PlaceResult(new Local(local.CompilerGivenName));
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

                        if (thisVariable.ReferencedInClosure
                                && _currentFunction.Value.FunctionSignature.ClosureTypeId is not null)
                        {
                            var closureType = _types[_currentFunction.Value.FunctionSignature.ClosureTypeId];
                            var closureTypeReference = new  NewLoweredConcreteTypeReference(
                                        closureType.Name,
                                        closureType.Id,
                                        []);
                            Debug.Assert(_currentFunction.Value.LoweredMethod.ParameterLocals.Count > 0);
                            Debug.Assert(
                                EqualTypeReferences(
                                    _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type,
                                    closureTypeReference));

                            return new PlaceResult(
                                new Field(
                                    new Local(ParameterLocalName(0)),
                                    ClosureThisFieldName,
                                    ClassVariantName));
                        }

                        Debug.Assert(_currentFunction.Value.LoweredMethod.ParameterLocals.Count > 0);
                        Debug.Assert(EqualTypeReferences(
                                    _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type,
                                    _currentType));

                        return new PlaceResult(new Local(ParameterLocalName(0)));
                    }
                case TypeChecking.TypeChecker.FieldVariable fieldVariable
                    when fieldVariable.ContainingSignature.Id == _currentType?.DefinitionId
                        && _currentFunction is not null:
                {
                    if (fieldVariable.IsStaticField)
                    {
                        return new PlaceResult(new StaticField(_currentType, fieldVariable.Name.StringValue));
                    }
                    
                    if (_currentFunction.Value.FunctionSignature.ClosureTypeId is not null)
                    {
                        var loweredMethod = _currentFunction.Value.LoweredMethod;
                        var fnSignature = _currentFunction.Value.FunctionSignature;
                        var closureType = _types[fnSignature.ClosureTypeId];
                        var closureTypeReference = new NewLoweredConcreteTypeReference(closureType.Name, closureType.Id, []);
                    
                        // we're a closure, so reference the value through the "this" field
                        // of the closure type
                        Debug.Assert(loweredMethod.ParameterLocals.Count > 0);
                        Debug.Assert(
                                EqualTypeReferences(
                                    loweredMethod.ParameterLocals[0].Type,
                                    closureTypeReference));

                        return new PlaceResult(new Field(
                            new Field(
                                new Local(ParameterLocalName(0)),
                                ClosureThisFieldName,
                                ClassVariantName),
                            fieldVariable.Name.StringValue,
                            ClassVariantName)
                        );
                    }
                    
                    if (_currentFunction.Value.LoweredMethod.ParameterLocals.Count == 0
                            || !EqualTypeReferences(
                                _currentFunction.Value.LoweredMethod.ParameterLocals[0].Type,
                                _currentType))
                    {
                        throw new InvalidOperationException("Expected to be in instance function");
                    }
                    
                    // todo: assert we're in a class and have _classVariant

                    return new PlaceResult(
                        new Field(
                            new Local(ParameterLocalName(0)),
                            fieldVariable.Name.StringValue,
                            ClassVariantName));
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

                        return new PlaceResult(new Local(ParameterLocalName(argumentIndex)));
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
                        return new PlaceResult(new Field(new Local(LocalsObjectLocalName), argument.Name.StringValue,
                            ClassVariantName));
                    }

                    return new PlaceResult(new Field(
                        new Field(
                            new Local(ParameterLocalName(0)),
                            containingFunctionLocals.Name,
                            ClassVariantName),
                        argument.Name.StringValue,
                        ClassVariantName));
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
        NewLowerExpression(e.VariableDeclaration.Value, destination: new Field(new Local(LocalsObjectLocalName), FieldName: variableName, ClassVariantName));
        
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
    
    private IExpressionResult LowerBinaryExpression(BinaryOperatorExpression binaryOperatorExpression, IPlace? destination)
    {
        if (binaryOperatorExpression.BinaryOperator.OperatorType == BinaryOperatorType.ValueAssignment)
        {
            var leftResult = NewLowerExpression(binaryOperatorExpression.BinaryOperator.Left.NotNull(), destination: null);
            if (leftResult is not PlaceResult { Value: var leftPlace })
            {
                throw new InvalidOperationException("Value Assignment left operand must be a place");
            }
        
            NewLowerExpression(binaryOperatorExpression.BinaryOperator.Right.NotNull(), destination: leftPlace);

            if (destination is not null)
            {
                _basicBlockStatements.Add(new Assign(destination, new Use(new Copy(leftPlace))));
            }

            return new PlaceResult(destination ?? leftPlace);
        }
        
        if (destination is null)
        {
            var localName = $"_local{_locals.Count}";
            _locals.Add(new NewMethodLocal(localName, null, GetTypeReference(binaryOperatorExpression.ResolvedType.NotNull())));
            destination = new Local(localName);
        }
        
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
                    leftOperand.ToOperand(),
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
                
                _basicBlockStatements.Add(new Assign(destination, new Use(rightOperand.ToOperand())));
                
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
                    leftOperand.ToOperand(),
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
                _basicBlockStatements.Add(new Assign(destination, new Use(rightOperand.ToOperand())));
                
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
                    new BinaryOperation(leftOperand.ToOperand(), rightOperand.ToOperand(), binaryOperatorKind)));
                break;
            }
        }

        return new PlaceResult(destination);
    }
}