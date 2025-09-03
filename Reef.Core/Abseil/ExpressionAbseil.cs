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
            Expressions.TupleExpression e => LowerTupleExpression(e),
            _ => throw new NotImplementedException($"{expression.GetType()}")
        };
    }

    private ILoweredExpression LowerTupleExpression(
            Expressions.TupleExpression e)
    {
        if (e.Values.Count == 1)
        {
            return LowerExpression(e.Values[0]);
        }

        var tupleType = GetTypeReference(e.ResolvedType.NotNull()) as LoweredConcreteTypeReference;
        Debug.Assert(tupleType is not null, "tuple type is not concrete");

        return new CreateObjectExpression(
            tupleType,
            "_tupleVariant",
            e.ValueUseful,
            e.Values.Index().ToDictionary(x => $"Item{x.Index}", x => LowerExpression(x.Item)));
    }

    private MethodReturnExpression LowerMethodReturnExpression(
            Expressions.MethodReturnExpression e)
    {
        return new MethodReturnExpression(
                e.MethodReturn.Expression is not null
                    ? LowerExpression(e.MethodReturn.Expression)
                    : new UnitConstantExpression(true));
    }

    private CreateObjectExpression CreateClosureObject(
        TypeChecking.TypeChecker.InstantiatedFunction instantiatedFunction)
    {
        Debug.Assert(instantiatedFunction.ClosureTypeId.HasValue);

        var closureType = _types[instantiatedFunction.ClosureTypeId.Value];
        var closureTypeReference = new LoweredConcreteTypeReference(
                closureType.Name,
                closureType.Id,
                []);

        var fieldInitializers = new Dictionary<string, ILoweredExpression>();

        Debug.Assert(_currentFunction.HasValue);

        foreach (var variable in instantiatedFunction.AccessedOuterVariables)
        {
            switch (variable)
            {
                case TypeChecking.TypeChecker.LocalVariable localVariable:
                    {
                        if (localVariable.ContainingFunction != _currentFunction.Value.FunctionSignature)
                        {
                            Debug.Assert(localVariable.ContainingFunction is not null);
                            Debug.Assert(localVariable.ContainingFunction.LocalsTypeId.HasValue);
                            Debug.Assert(_currentFunction.Value.FunctionSignature.ClosureTypeId.HasValue);

                            var currentClosureType = _types[
                                _currentFunction.Value.FunctionSignature.ClosureTypeId.Value
                            ];
                            var currentClosureTypeReference = new LoweredConcreteTypeReference(
                                    currentClosureType.Name,
                                    currentClosureType.Id,
                                    []);

                            Debug.Assert(
                                    EqualTypeReferences(_currentFunction.Value.LoweredMethod.Parameters[0],
                                        currentClosureTypeReference));

                            var otherLocalsType = _types[
                                localVariable.ContainingFunction.LocalsTypeId.Value
                            ];
                            var otherLocalsTypeReference = new LoweredConcreteTypeReference(
                                    otherLocalsType.Name,
                                    otherLocalsType.Id,
                                    []);

                            fieldInitializers.TryAdd(
                                otherLocalsType.Name,
                                new FieldAccessExpression(
                                    new LoadArgumentExpression(
                                        0,
                                        true,
                                        currentClosureTypeReference),
                                    otherLocalsType.Name,
                                    "_classVariant",
                                    true,
                                    otherLocalsTypeReference));

                            break;
                        }
                        Debug.Assert(_currentFunction.Value.FunctionSignature.LocalsTypeId.HasValue);
                        var localsType = _types[_currentFunction.Value.FunctionSignature.LocalsTypeId.Value];

                        fieldInitializers.TryAdd(
                            localsType.Name,
                            new LocalVariableAccessor(
                                "__locals",
                                true,
                                new LoweredConcreteTypeReference(
                                    localsType.Name,
                                    localsType.Id,
                                    [])));
                        break;
                    }
                case TypeChecking.TypeChecker.ThisVariable:
                case TypeChecking.TypeChecker.FieldVariable:
                    {
                        Debug.Assert(_currentType is not null);

                        if (_currentFunction.Value.FunctionSignature.ClosureTypeId.HasValue)
                        {
                            var currentClosureType = _types[_currentFunction.Value.FunctionSignature.ClosureTypeId.Value];
                            var currentClosureTypeReference = new LoweredConcreteTypeReference(
                                    currentClosureType.Name,
                                    currentClosureType.Id,
                                    []);

                            Debug.Assert(
                                EqualTypeReferences(
                                    _currentFunction.Value.LoweredMethod.Parameters[0],
                                    currentClosureTypeReference));

                            fieldInitializers.TryAdd(
                                "this",
                                new FieldAccessExpression(
                                    new LoadArgumentExpression(
                                        0, true, currentClosureTypeReference),
                                    "this",
                                    "_classVariant",
                                    true,
                                    _currentType));
                            break;
                        }

                        Debug.Assert(
                            EqualTypeReferences(
                                _currentFunction.Value.LoweredMethod.Parameters[0],
                                _currentType));
                        fieldInitializers.TryAdd(
                            "this",
                            new LoadArgumentExpression(
                                0,
                                true,
                                _currentType));
                        break;
                    }
                case TypeChecking.TypeChecker.FunctionSignatureParameter parameter:
                    {
                        if (parameter.ContainingFunction != _currentFunction.Value.FunctionSignature)
                        {
                            Debug.Assert(parameter.ContainingFunction is not null);
                            Debug.Assert(parameter.ContainingFunction.LocalsTypeId.HasValue);
                            Debug.Assert(_currentFunction.Value.FunctionSignature.ClosureTypeId.HasValue);

                            var currentClosureType = _types[
                                _currentFunction.Value.FunctionSignature.ClosureTypeId.Value
                            ];
                            var currentClosureTypeReference = new LoweredConcreteTypeReference(
                                    currentClosureType.Name,
                                    currentClosureType.Id,
                                    []);

                            Debug.Assert(
                                    EqualTypeReferences(_currentFunction.Value.LoweredMethod.Parameters[0],
                                        currentClosureTypeReference));

                            var otherLocalsType = _types[
                                parameter.ContainingFunction.LocalsTypeId.Value
                            ];
                            var otherLocalsTypeReference = new LoweredConcreteTypeReference(
                                    otherLocalsType.Name,
                                    otherLocalsType.Id,
                                    []);

                            fieldInitializers.TryAdd(
                                otherLocalsType.Name,
                                new FieldAccessExpression(
                                    new LoadArgumentExpression(
                                        0,
                                        true,
                                        currentClosureTypeReference),
                                    otherLocalsType.Name,
                                    "_classVariant",
                                    true,
                                    otherLocalsTypeReference));

                            break;
                        }
                        Debug.Assert(_currentFunction.Value.FunctionSignature.LocalsTypeId.HasValue);
                        var localsType = _types[_currentFunction.Value.FunctionSignature.LocalsTypeId.Value];

                        fieldInitializers.TryAdd(
                            localsType.Name,
                            new LocalVariableAccessor(
                                "__locals",
                                true,
                                new LoweredConcreteTypeReference(
                                    localsType.Name,
                                    localsType.Id,
                                    [])));
                        break;
                    }
            }
        }

        return new CreateObjectExpression(
                    closureTypeReference,
                    "_classVariant",
                    true,
                    fieldInitializers);
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

        var arguments = new List<ILoweredExpression>(e.MethodCall.ArgumentList.Count);

        if (e.MethodCall.Method is Expressions.MemberAccessExpression memberAccess)
        {
            var owner = LowerExpression(memberAccess.MemberAccess.Owner);
            arguments.Add(owner);
        }
        else if (instantiatedFunction.ClosureTypeId.HasValue)
        {
            var createClosure = CreateClosureObject(instantiatedFunction);
            arguments.Add(createClosure);
        }
        else if (!instantiatedFunction.IsStatic
                && instantiatedFunction.OwnerType is not null
                && _currentType is not null
                && EqualTypeReferences(GetTypeReference(instantiatedFunction.OwnerType), _currentType)
                && _currentFunction is not null
                && EqualTypeReferences(_currentFunction.Value.LoweredMethod.Parameters[0], _currentType))
        {
            arguments.Add(
                    new LoadArgumentExpression(0, true, _currentType));
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
                {
                    var fn = e.MemberAccess.InstantiatedFunction.NotNull();
                    return new CreateObjectExpression(
                        (GetTypeReference(e.ResolvedType.NotNull()) as LoweredConcreteTypeReference).NotNull(),
                        "_classVariant",
                        e.ValueUseful,
                        new()
                        {
                            {
                                "FunctionReference",
                                new FunctionReferenceConstantExpression(
                                    GetFunctionReference(
                                        fn.FunctionId,
                                        [..fn.TypeArguments.Select(GetTypeReference)]),
                                    true,
                                    new LoweredFunctionType(
                                        [..fn.Parameters.Select(x => GetTypeReference(x.Type))],
                                        GetTypeReference(fn.ReturnType)))
                            },
                            {
                                "FunctionParameter",
                                owner
                            }
                        });
                }
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

        var dataType = _types[concreteTypeReference.DefinitionId];

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

            var dataType = _types[type.DefinitionId];
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

        if (e.StaticMemberAccess.MemberType == Expressions.MemberType.Function)
        {
            var fn = e.StaticMemberAccess.InstantiatedFunction.NotNull();
            return new CreateObjectExpression(
                (GetTypeReference(e.ResolvedType.NotNull()) as LoweredConcreteTypeReference).NotNull(),
                "_classVariant",
                e.ValueUseful,
                new()
                {
                    {
                        "FunctionReference",
                        new FunctionReferenceConstantExpression(
                            GetFunctionReference(
                                fn.FunctionId,
                                [..fn.TypeArguments.Select(GetTypeReference)]),
                            true,
                            new LoweredFunctionType(
                                [..fn.Parameters.Select(x => GetTypeReference(x.Type))],
                                GetTypeReference(fn.ReturnType)))
                    }
                });
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

        var referencedInClosure = e.VariableDeclaration.Variable!.ReferencedInClosure;

        if (e.VariableDeclaration.Value is null)
        {
            if (referencedInClosure)
            {
                // the variable has been consumed into the locals variable, and we're not assigning a value, so just put a unit constant here
                return new UnitConstantExpression(e.ValueUseful);
            }
            return new VariableDeclarationExpression(variableName, e.ValueUseful);
        }

        var loweredValue = LowerExpression(e.VariableDeclaration.Value);

        if (!referencedInClosure)
        {
            return new VariableDeclarationAndAssignmentExpression(
                    variableName,
                    loweredValue,
                    e.ValueUseful);
        }

        var localsTypeId = _currentFunction.NotNull().FunctionSignature.LocalsTypeId
            .NotNull();
        var localsType = _types[localsTypeId];
        
        var localsFieldAssignment = new FieldAssignmentExpression(
            new LocalVariableAccessor("__locals",
                true,
                new LoweredConcreteTypeReference(
                    localsType.Name,
                    localsType.Id,
                    [])),
            "_classVariant",
            variableName,
            loweredValue,
            // hard code this to false, because either `e.ValueUseful` was false,
            // or we're going to replace the value with a block
            false,
            loweredValue.ResolvedType);

        if (e.ValueUseful)
        {
            // because the value of this variable declaration expression is useful
            // (ie used in the parent expression), and we have just changed what that
            // resulting value would be (value declarations return unit, but field
            // assignments return the assigned value), we need to stick this assignment
            // in a block and put a unit constant back

            // I'm not sure if this is going to bite me in the butt because of any specific
            // block semantics (ie dropping values at the end of a block)
            var unit = new UnitConstantExpression(ValueUseful: true);
            return new BlockExpression(
                [
                    localsFieldAssignment,
                    unit
                ],
                unit.ResolvedType,
                ValueUseful: true);
        }

        return localsFieldAssignment;
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
            { ValueAccessor.AccessType: Expressions.ValueAccessType.Variable, FunctionInstantiation: {} fn} => FunctionAccess(fn, (e.ResolvedType as TypeChecking.TypeChecker.FunctionObject).NotNull(), e.ValueUseful),
            _ => throw new NotImplementedException($"{e}")
        };

        ILoweredExpression FunctionAccess(
                TypeChecking.TypeChecker.InstantiatedFunction fn,
                TypeChecking.TypeChecker.FunctionObject typeReference,
                bool valueUseful)
        {
            var method = _methods.Keys.First(x => x.Id == fn.FunctionId);

            var functionObjectParameters = new Dictionary<string, ILoweredExpression>
            {
                {
                    "FunctionReference",
                    new FunctionReferenceConstantExpression(
                            GetFunctionReference(
                                fn.FunctionId,
                                [..fn.TypeArguments.Select(GetTypeReference)]),
                            true,
                            new LoweredFunctionType(
                                method.Parameters,
                                method.ReturnType))
                }
            };

            if (fn.ClosureTypeId.HasValue)
            {
                functionObjectParameters.Add("FunctionParameter", CreateClosureObject(fn));
            }

            return new CreateObjectExpression(
                (GetTypeReference(typeReference) as LoweredConcreteTypeReference).NotNull(),
                "_classVariant",
                valueUseful,
                functionObjectParameters);
        }

        ILoweredExpression VariableAccess(
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
                            return new LocalVariableAccessor(
                                    variable.Name.StringValue,
                                    valueUseful,
                                    resolvedType);
                        }

                        var currentFunction = _currentFunction.NotNull();
                        var containingFunction = localVariable.ContainingFunction.NotNull();
                        var containingFunctionLocals = _types[containingFunction.LocalsTypeId.NotNull()];
                        var localsTypeReference = new LoweredConcreteTypeReference(
                                        containingFunctionLocals.Name,
                                        containingFunctionLocals.Id,
                                        []);
                        if (containingFunction.Id == currentFunction.FunctionSignature.Id)
                        {
                            return new FieldAccessExpression(
                                new LocalVariableAccessor(
                                    "__locals",
                                    true,
                                    localsTypeReference),
                                localVariable.Name.StringValue,
                                "_classVariant",
                                e.ValueUseful,
                                resolvedType);
                        }
                        var closureTypeId = _currentFunction.NotNull()
                                .FunctionSignature.ClosureTypeId.NotNull();
                        var closureType = _types[closureTypeId];

                        return new FieldAccessExpression(
                            new FieldAccessExpression(
                                new LoadArgumentExpression(
                                    0,
                                    true,
                                    new LoweredConcreteTypeReference(
                                        closureType.Name,
                                        closureTypeId,
                                        [])),
                                containingFunctionLocals.Name,
                                "_classVariant",
                                true,
                                localsTypeReference),
                            localVariable.Name.StringValue,
                            "_classVariant",
                            e.ValueUseful,
                            resolvedType);
                    }
                case TypeChecking.TypeChecker.ThisVariable thisVariable:
                    {
                        Debug.Assert(_currentFunction is not null);
                        Debug.Assert(_currentType is not null); 
                        if (thisVariable.ReferencedInClosure
                                && _currentFunction.Value.FunctionSignature.ClosureTypeId.HasValue)
                        {
                            var closureType = _types[_currentFunction.Value.FunctionSignature.ClosureTypeId.Value];
                            var closureTypeReference = new LoweredConcreteTypeReference(
                                        closureType.Name,
                                        closureType.Id,
                                        []);
                            Debug.Assert(_currentFunction.Value.LoweredMethod.Parameters.Count > 0);
                            Debug.Assert(
                                EqualTypeReferences(
                                    _currentFunction.Value.LoweredMethod.Parameters[0],
                                    closureTypeReference));
                            return new FieldAccessExpression(
                                new LoadArgumentExpression(
                                    0,
                                    true,
                                    closureTypeReference),
                                "this",
                                "_classVariant",
                                valueUseful,
                                resolvedType);
                        }

                        Debug.Assert(_currentFunction.Value.LoweredMethod.Parameters.Count > 0);
                        Debug.Assert(EqualTypeReferences(
                                    _currentFunction.Value.LoweredMethod.Parameters[0],
                                    _currentType));

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

                        if (_currentFunction.Value.FunctionSignature.ClosureTypeId.HasValue)
                        {
                            var loweredMethod = _currentFunction.Value.LoweredMethod;
                            var fnSignature = _currentFunction.Value.FunctionSignature;
                            var closureType = _types[fnSignature.ClosureTypeId.Value];
                            var closureTypeReference = new LoweredConcreteTypeReference(closureType.Name, closureType.Id, []);

                            // we're a closure, so reference the value through the this field
                            // of the closure type
                            Debug.Assert(loweredMethod.Parameters.Count > 0);
                            Debug.Assert(
                                    EqualTypeReferences(
                                        loweredMethod.Parameters[0],
                                        closureTypeReference));
                            return new FieldAccessExpression(
                                new FieldAccessExpression(
                                    new LoadArgumentExpression(
                                        0,
                                        true,
                                        closureTypeReference),
                                    "this",
                                    "_classVariant",
                                    true,
                                    _currentType),
                                fieldVariable.Name.StringValue,
                                "_classVariant",
                                valueUseful,
                                resolvedType);
                        }

                        if (_currentFunction.Value.LoweredMethod.Parameters.Count == 0
                                || !EqualTypeReferences(
                                    _currentFunction.Value.LoweredMethod.Parameters[0],
                                    _currentType))
                        {
                            throw new InvalidOperationException("Expected to be in instance function");
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
                        if (!argument.ReferencedInClosure)
                        {
                            if (argument.ContainingFunction.AccessedOuterVariables.Count > 0
                                    || (argument.ContainingFunction.OwnerType is not null
                                        && !argument.ContainingFunction.IsStatic))
                            {
                                argumentIndex++;
                            }

                            return new LoadArgumentExpression(argumentIndex, valueUseful, resolvedType);
                        }

                        var currentFunction = _currentFunction.NotNull();
                        var containingFunction = argument.ContainingFunction.NotNull();
                        var containingFunctionLocals = _types[containingFunction.LocalsTypeId.NotNull()];
                        var localsTypeReference = new LoweredConcreteTypeReference(
                                        containingFunctionLocals.Name,
                                        containingFunctionLocals.Id,
                                        []);
                        if (containingFunction.Id == currentFunction.FunctionSignature.Id)
                        {
                            return new FieldAccessExpression(
                                new LocalVariableAccessor(
                                    "__locals",
                                    true,
                                    localsTypeReference),
                                argument.Name.StringValue,
                                "_classVariant",
                                e.ValueUseful,
                                resolvedType);
                        }
                        var closureTypeId = _currentFunction.NotNull()
                                .FunctionSignature.ClosureTypeId.NotNull();
                        var closureType = _types[closureTypeId];

                        return new FieldAccessExpression(
                            new FieldAccessExpression(
                                new LoadArgumentExpression(
                                    0,
                                    true,
                                    new LoweredConcreteTypeReference(
                                        closureType.Name,
                                        closureTypeId,
                                        [])),
                                containingFunctionLocals.Name,
                                "_classVariant",
                                true,
                                localsTypeReference),
                            argument.Name.StringValue,
                            "_classVariant",
                            e.ValueUseful,
                            resolvedType);
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
                    var containingFunction = localVariable.ContainingFunction;
                    Debug.Assert(_currentFunction.HasValue);
                    Debug.Assert(containingFunction is not null);
                    Debug.Assert(containingFunction.LocalsTypeId.HasValue);
                    var localsType = _types[containingFunction.LocalsTypeId.Value];
                    var localsTypeReference = new LoweredConcreteTypeReference(
                        localsType.Name,
                        localsType.Id,
                        []);

                    if (_currentFunction.Value.FunctionSignature == containingFunction)
                    {
                        return new FieldAssignmentExpression(
                            new LocalVariableAccessor(
                                "__locals",
                                true,
                                localsTypeReference),
                            "_classVariant",
                            localVariable.Name.StringValue,
                            LowerExpression(right),
                            valueUseful,
                            resolvedType);
                    }

                    Debug.Assert(_currentFunction.Value.FunctionSignature.ClosureTypeId.HasValue);
                    var closureType = _types[_currentFunction.Value.FunctionSignature.ClosureTypeId.Value];
                    var closureTypeReference = new LoweredConcreteTypeReference(
                            closureType.Name,
                            closureType.Id,
                            []);

                    Debug.Assert(_currentFunction.Value.LoweredMethod.Parameters.Count > 0);
                    Debug.Assert(EqualTypeReferences(
                            closureTypeReference,
                            _currentFunction.Value.LoweredMethod.Parameters[0]));

                    return new FieldAssignmentExpression(
                        new FieldAccessExpression(
                            new LoadArgumentExpression(
                                0, true, closureTypeReference),
                            localsType.Name,
                            "_classVariant",
                            true,
                            localsTypeReference),
                        "_classVariant",
                        localVariable.Name.StringValue,
                        LowerExpression(right),
                        valueUseful,
                        resolvedType);
                }

                return new LocalAssignmentExpression(
                        localVariable.Name.StringValue,
                        LowerExpression(right),
                        resolvedType,
                        valueUseful);
            }

            if (variable is TypeChecking.TypeChecker.FieldVariable fieldVariable)
            {
                Debug.Assert(_currentType is not null);
                if (fieldVariable.IsStaticField)
                {
                    return new StaticFieldAssignmentExpression(
                        _currentType,
                        fieldVariable.Name.StringValue,
                        LowerExpression(right),
                        valueUseful,
                        resolvedType);
                }

                if (fieldVariable.ReferencedInClosure
                    && _currentFunction is
                    {
                        FunctionSignature: { ClosureTypeId: not null} functionSignature
                    })
                {
                    var closureType = _types[functionSignature.ClosureTypeId.Value];
                    var closureTypeReference = new LoweredConcreteTypeReference(
                            closureType.Name,
                            closureType.Id,
                            []);

                    return new FieldAssignmentExpression(
                        new FieldAccessExpression(
                            new LoadArgumentExpression(
                                0,
                                true,
                                closureTypeReference),
                            "this",
                            "_classVariant",
                            true,
                            _currentType),
                        "_classVariant",
                        fieldVariable.Name.StringValue,
                        LowerExpression(right),
                        valueUseful,
                        resolvedType);
                }

                Debug.Assert(fieldVariable.ContainingSignature.Id == _currentType.DefinitionId);

                

                Debug.Assert(_currentFunction is not null);
                Debug.Assert(_currentFunction.Value.LoweredMethod.Parameters.Count > 0);
                Debug.Assert(EqualTypeReferences(
                            _currentFunction.Value.LoweredMethod.Parameters[0],
                            _currentType));

                return new FieldAssignmentExpression(
                    new LoadArgumentExpression(0, true, _currentType),
                    "_classVariant",
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
                "_classVariant",
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
