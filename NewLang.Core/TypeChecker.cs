using System.Text;

namespace NewLang.Core;

public class TypeChecker
{
    public static void TypeCheck(LangProgram program)
    {
        new TypeChecker(program).TypeCheckInner();
    }

    private TypeChecker(LangProgram program)
    {
        _program = program;
    }

    private readonly LangProgram _program;
    private readonly Dictionary<string, ClassSignature> _types = ClassSignature.BuiltInTypes.ToDictionary(x => x.Name);
    
    // todo: generic placeholders in scope?
    private readonly Stack<TypeCheckingScope> _typeCheckingScopes = new ();
    private Dictionary<string, Variable> ScopedVariables => _typeCheckingScopes.Peek().Variables;
    private Dictionary<string, FunctionSignature> ScopedFunctions => _typeCheckingScopes.Peek().Functions;
    private ITypeReference ExpectedReturnType => _typeCheckingScopes.Peek().ExpectedReturnType;

    private record TypeCheckingScope(
        Dictionary<string, Variable> Variables,
        Dictionary<string, FunctionSignature> Functions,
        ITypeReference ExpectedReturnType);

    private IDisposable PushScope(ITypeReference? expectedReturnType = null)
    {
        var currentScope = _typeCheckingScopes.Peek();

        _typeCheckingScopes.Push(new TypeCheckingScope(
            new Dictionary<string, Variable>(currentScope.Variables),
            new Dictionary<string, FunctionSignature>(currentScope.Functions),
            expectedReturnType ?? currentScope.ExpectedReturnType));

        return new ScopeDisposable(PopScope);
    }

    private void PopScope()
    {
        _typeCheckingScopes.Pop();
    }

    private class ScopeDisposable(Action onDispose) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                throw new InvalidOperationException("Scope already disposed");
            }
            _disposed = true;
            onDispose();
        }
    }

    private void TypeCheckInner()
    {
        // initial scope
        _typeCheckingScopes.Push(new TypeCheckingScope(new (), new(), InstantiatedClass.Unit));

        SetupSignatures();

        foreach (var @class in _program.Classes)
        {
            var classSignature = _types[@class.Name.StringValue];
            var classGenericPlaceholders =
                @class.TypeArguments.ToDictionary<StringToken, string, ITypeSignature>(x => x.StringValue, _ => classSignature);

            foreach (var field in @class.Fields)
            {
                var isStatic = field.StaticModifier is not null;

                if (isStatic)
                {
                    // todo: static constructor?
                    if (field.InitializerValue is null)
                    {
                        throw new InvalidOperationException("Expected field initializer for static field");
                    }

                    var valueType = TypeCheckExpression(field.InitializerValue, classGenericPlaceholders);
                    var expectedType = GetTypeReference(field.Type, classGenericPlaceholders);

                    if (!Equals(valueType, expectedType))
                    {
                        throw new InvalidOperationException($"Expected {expectedType}");
                    }
                }
                else if (field.InitializerValue is not null)
                {
                    throw new InvalidOperationException("Instance fields cannot have initializers");
                }
            }

            foreach (var function in @class.Functions)
            {
                var fnSignature = classSignature.Functions.First(x => x.Name == function.Name.StringValue);
                TypeCheckFunctionBody(function, fnSignature , classGenericPlaceholders);
            }
        }

        foreach (var function in _program.Functions)
        {
            TypeCheckFunctionBody(function, ScopedFunctions[function.Name.StringValue], []);
        }
        
        foreach (var expression in _program.Expressions)
        {
            TypeCheckExpression(expression, []);
        }
        
        // foreach (var (@class, typeDefinition, instanceFields, staticFields, functions) in classMembers)
        // {
        //     var genericPlaceholders = typeDefinition.GenericParameters.ToDictionary(x => x, x => typeDefinition);
        //     
        //     foreach (var field in @class.Fields)
        //     {
        //         var typeField = new TypeField
        //         {
        //             Name = field.Name.StringValue,
        //             Type = GetTypeReference(field.Type, genericPlaceholders),
        //             IsMutable = field.MutabilityModifier is not null,
        //             IsPublic = field.AccessModifier is {Token.Type: TokenType.Pub}
        //         };
        //         
        //         if (field.StaticModifier is not null)
        //         {
        //             if (field.InitializerValue is null)
        //             {
        //                 throw new InvalidOperationException("Static members must be initialized");
        //             }
        //
        //             // todo: should be able to call static functions here?
        //             var valueType = TypeCheckExpression(field.InitializerValue, genericPlaceholders);
        //             if (!Equals(valueType, typeField.Type))
        //             {
        //                 throw new InvalidOperationException($"Expected {typeField.Type} but found {valueType}");
        //             }
        //             staticFields.Add(typeField);
        //         }
        //         else
        //         {
        //             if (field.InitializerValue is not null)
        //             {
        //                 throw new InvalidOperationException("Instance members must not be initialized");
        //             }
        //             instanceFields.Add(typeField);
        //         }
        //     }
        // }

        // foreach (var fn in _program.Functions)
        // {
        //     Functions[fn.Name.StringValue] = TypeCheckFunctionSignature(fn, []);
        // }

        // foreach (var fn in _program.Functions)
        // {
        //     TypeCheckFunctionBody(fn, []);
        // }



    }

    private void SetupSignatures()
    {
        // class signatures
        
        // class function signatures
        
        // class fields
        
        // global functions

        var classes = new List<(ProgramClass, ClassSignature, List<FunctionSignature>, List<TypeField> fields, List<TypeField> staticFields)>();

        foreach (var @class in _program.Classes)
        {
            var name = @class.Name.StringValue;
            var functions = new List<FunctionSignature>();
            var fields = new List<TypeField>();
            var staticFields = new List<TypeField>();
            var signature = new ClassSignature()
            {
                Name = name,
                GenericParameters = [..@class.TypeArguments.Select(x => x.StringValue)],
                Functions = functions,
                Fields = fields,
                StaticFields = staticFields
            };
            
            classes.Add((@class, signature, functions, fields, staticFields));

            if (!_types.TryAdd(name, signature))
            {
                throw new InvalidOperationException($"Class with name {name} already defined");
            }
        }
        
        foreach (var (@class, classSignature, functions, fields, staticFields) in classes)
        {
            var classGenericPlaceholders = new Dictionary<string, ITypeSignature>();
            foreach (var genericParameter in classSignature.GenericParameters)
            {
                if (!classGenericPlaceholders.TryAdd(genericParameter, classSignature))
                {
                    throw new InvalidOperationException($"Generic name already defined {genericParameter}");
                }
            }
            
            foreach (var fn in @class.Functions)
            {
                // todo: check function name collisions. also function overloading
                functions.Add(TypeCheckFunctionSignature(fn, classGenericPlaceholders));
            }

            foreach (var field in @class.Fields)
            {
                var typeField = new TypeField
                {
                    Name = field.Name.StringValue,
                    Type = GetTypeReference(field.Type, classGenericPlaceholders),
                    IsMutable = field.MutabilityModifier is { Modifier.Type: TokenType.Mut },
                    IsPublic = field.AccessModifier is { Token.Type: TokenType.Pub }
                };

                if (field.StaticModifier is not null)
                {
                    if (staticFields.Any(y => y.Name == typeField.Name))
                    {
                        throw new InvalidOperationException($"Static field with name {field.Name} already defined");
                    }
                    staticFields.Add(typeField);
                }
                else
                {
                    if (fields.Any(y => y.Name == typeField.Name))
                    {
                        throw new InvalidOperationException($"Field with name {field.Name} already defined");
                    }
                    fields.Add(typeField);
                }
            }
        }

        foreach (var fn in _program.Functions)
        {
            // var parameters = new List<KeyValuePair<string, ITypeReference>>();
            //
            var name = fn.Name.StringValue;
            // var fnSignature = new FunctionSignature(
            //     name,
            //     [..fn.TypeArguments.Select(x => x.StringValue)],
            //     parameters)
            // {
            //     ReturnType = null!
            // };
            //
            // var genericPlaceholders = fnSignature.GenericParameters.ToDictionary<string, string, ITypeSignature>(x => x, _ => fnSignature);
            //
            // fnSignature.ReturnType = fn.ReturnType is null
            //     ? InstantiatedClass.Unit
            //     : GetTypeReference(fn.ReturnType, genericPlaceholders);
            //
            // foreach (var parameter in fn.Parameters)
            // {
            //     parameters.Add(KeyValuePair.Create(parameter.Identifier.StringValue, GetTypeReference(parameter.Type, genericPlaceholders)));
            // }
            
            // todo: function overloading
            if (!ScopedFunctions.TryAdd(name, TypeCheckFunctionSignature(fn, [])))
            {
                throw new InvalidOperationException($"Function with name {name} already defined");
            }
        }

        foreach (var classSignature in _types.Values)
        {
            if (classSignature.GenericParameters.Any(x => 
                    _types.ContainsKey(x) 
                    || classSignature.Functions.Any(y => y.Name == x)
                    || _program.Functions.Any(y => y.Name.StringValue == x)))
            {
                throw new InvalidOperationException("Generic name collision");
            }

            foreach (var fn in classSignature.Functions)
            {
                if (fn.GenericParameters.Any(x => 
                        _types.ContainsKey(x)
                        || classSignature.Functions.Any(y => y.Name == x)
                        || _program.Functions.Any(y => y.Name.StringValue == x)))
                {
                    throw new InvalidOperationException("Generic name collision");
                }
            }
        }
    }


    private void TypeCheckFunctionBody(LangFunction function,
        FunctionSignature fnSignature,
        Dictionary<string, ITypeSignature> genericPlaceholders)
    {
        var innerGenericPlaceholders = new Dictionary<string, ITypeSignature>(genericPlaceholders);
        foreach (var genericParameter in fnSignature.GenericParameters)
        {
            innerGenericPlaceholders[genericParameter] = fnSignature;
        }

        using var _ = PushScope(fnSignature.ReturnType);
        foreach (var parameter in fnSignature.Arguments)
        {
            ScopedVariables[parameter.Key] = new Variable(
                parameter.Key,
                parameter.Value,
                Instantiated: true);
        }
        
        TypeCheckBlock(function.Block,
            innerGenericPlaceholders);
    }

    private FunctionSignature TypeCheckFunctionSignature(LangFunction fn, Dictionary<string, ITypeSignature> genericPlaceholders)
    {
        var parameters = new List<KeyValuePair<string, ITypeReference>>();
                        
        var name = fn.Name.StringValue;
        var fnSignature = new FunctionSignature(
            name,
            [..fn.TypeArguments.Select(x => x.StringValue)],
            parameters)
        {
            ReturnType = null!
        };

        var innerGenericPlaceholders = new Dictionary<string, ITypeSignature>(genericPlaceholders);
        foreach (var genericParameter in fnSignature.GenericParameters)
        {
            if (!innerGenericPlaceholders.TryAdd(genericParameter, fnSignature))
            {
                throw new InvalidOperationException($"Generic parameter {genericParameter} already defined");
            }
        }

        fnSignature.ReturnType = fn.ReturnType is null
            ? InstantiatedClass.Unit
            : GetTypeReference(fn.ReturnType, innerGenericPlaceholders);

        foreach (var parameter in fn.Parameters)
        {
            var paramName = parameter.Identifier.StringValue;
            if (parameters.Any(x => x.Key == paramName))
            {
                throw new InvalidOperationException($"Parameter with {paramName} already defined");
            }
            parameters.Add(KeyValuePair.Create(parameter.Identifier.StringValue, GetTypeReference(parameter.Type, innerGenericPlaceholders)));
        }
        
        // todo: check function name collisions. also function overloading
        return fnSignature;
    }

    private ITypeReference TypeCheckBlock(
        Block block,
        Dictionary<string, ITypeSignature> genericPlaceholders)
    {
        using var _ = PushScope();
        
        foreach (var fn in block.Functions)
        {
            ScopedFunctions[fn.Name.StringValue] = TypeCheckFunctionSignature(fn, genericPlaceholders);
        }
        
        foreach (var fn in block.Functions)
        {
            TypeCheckFunctionBody(fn, ScopedFunctions[fn.Name.StringValue], genericPlaceholders);
        }

        foreach (var expression in block.Expressions)
        {
            TypeCheckExpression(expression, genericPlaceholders);
        }

        // todo: tail expressions
        return InstantiatedClass.Unit;
    }

    private ITypeReference TypeCheckExpression(
        IExpression expression,
        Dictionary<string, ITypeSignature> genericPlaceholders)
    {
        return expression switch
        {
            VariableDeclarationExpression variableDeclarationExpression => TypeCheckVariableDeclaration(
                variableDeclarationExpression, genericPlaceholders),
            ValueAccessorExpression valueAccessorExpression => TypeCheckValueAccessor(valueAccessorExpression),
            MethodReturnExpression methodReturnExpression => TypeCheckMethodReturn(methodReturnExpression, genericPlaceholders),
            MethodCallExpression methodCallExpression => TypeCheckMethodCall(methodCallExpression.MethodCall, genericPlaceholders),
            BlockExpression blockExpression => TypeCheckBlock(blockExpression.Block, genericPlaceholders),
            IfExpressionExpression ifExpressionExpression => TypeCheckIfExpression(ifExpressionExpression.IfExpression, genericPlaceholders),
            BinaryOperatorExpression binaryOperatorExpression => TypeCheckBinaryOperatorExpression(binaryOperatorExpression.BinaryOperator, genericPlaceholders),
            ObjectInitializerExpression objectInitializerExpression => TypeCheckObjectInitializer(objectInitializerExpression.ObjectInitializer, genericPlaceholders),
            MemberAccessExpression memberAccessExpression => TypeCheckMemberAccess(memberAccessExpression.MemberAccess, genericPlaceholders),
            StaticMemberAccessExpression staticMemberAccessExpression => TypeCheckStaticMemberAccess(staticMemberAccessExpression.StaticMemberAccess, genericPlaceholders),
            GenericInstantiationExpression genericInstantiationExpression => TypeCheckGenericInstantiation(genericInstantiationExpression.GenericInstantiation, genericPlaceholders),
            _ => throw new NotImplementedException($"{expression.ExpressionType}")
        };
    }

    private ITypeReference TypeCheckGenericInstantiation(GenericInstantiation genericInstantiation, Dictionary<string, ITypeSignature> genericPlaceholders)
    {
        var valueType = TypeCheckExpression(genericInstantiation.Value, genericPlaceholders);

        if (valueType is not InstantiatedFunction instantiatedFunction)
        {
            throw new InvalidOperationException("Expected function");
        }

        if (genericInstantiation.GenericArguments.Count != instantiatedFunction.Signature.GenericParameters.Count)
        {
            throw new InvalidOperationException($"Expected {instantiatedFunction.Signature.GenericParameters.Count} type arguments but found {genericInstantiation.GenericArguments.Count}");
        }

        var genericParameters = genericInstantiation.GenericArguments.Select(x => GetTypeReference(x, genericPlaceholders))
            .Zip(instantiatedFunction.Signature.GenericParameters)
            .ToDictionary(x => x.Second, x => x.First);
        
        return new InstantiatedFunction
        {
            Signature = instantiatedFunction.Signature,
            TypeArguments = genericParameters
        };
    }

    private ITypeReference TypeCheckMemberAccess(
        MemberAccess memberAccess,
        Dictionary<string, ITypeSignature> genericPlaceholders)
    {
        var ownerExpression = memberAccess.Owner;
        var ownerType = TypeCheckExpression(ownerExpression, genericPlaceholders);

        if (ownerType is not InstantiatedClass { Signature: ClassSignature classSignature, TypeArguments: var ownerTypeArguments })
        {
            // todo: generic argument constraints with interfaces?
            throw new InvalidOperationException("Can only access members on instantiated types");
        }

        var fieldType = classSignature.Fields.FirstOrDefault(x => x.Name == memberAccess.MemberName.StringValue)?.Type;

        if (fieldType is GenericTypeReference { GenericName: var fieldGenericName })
        {
            return ownerTypeArguments[fieldGenericName];
        }

        if (fieldType is InstantiatedClass)
        {
            return fieldType;
        }
        
        var functionSignature = classSignature.Functions.FirstOrDefault(x => x.Name == memberAccess.MemberName.StringValue);

        if (functionSignature is null)
        {
            throw new InvalidOperationException($"No member named {memberAccess.MemberName.StringValue}");
        }

        return new InstantiatedFunction()
        {
            Signature = functionSignature,
            TypeArguments = []
        };
    }

    private ITypeReference TypeCheckStaticMemberAccess(
        StaticMemberAccess staticMemberAccess,
        Dictionary<string, ITypeSignature> genericPlaceholders)
    {
        var type = GetTypeReference(staticMemberAccess.Type, genericPlaceholders);

        if (type is not InstantiatedClass { Signature: ClassSignature classSignature})
        {
            throw new InvalidOperationException("Can only access static members on instantiated types");
        }
        
        var field = classSignature.StaticFields.FirstOrDefault(x => x.Name == staticMemberAccess.MemberName.StringValue)
            ?? throw new InvalidOperationException($"No member with name {staticMemberAccess.MemberName.StringValue}");

        return field.Type;
    }

    private ITypeReference TypeCheckObjectInitializer(
        ObjectInitializer objectInitializer,
        Dictionary<string, ITypeSignature> genericPlaceholders)
    {
        var foundType = GetTypeReference(objectInitializer.Type, genericPlaceholders);
        if (foundType is not InstantiatedClass { Signature: ClassSignature classSignature, TypeArguments: var typeArguments })
        {
            // todo: more checks
            throw new InvalidOperationException($"Type {foundType} cannot be initialized");
        }

        if (objectInitializer.FieldInitializers.GroupBy(x => x.FieldName.StringValue)
            .Any(x => x.Count() > 1))
        {
            throw new InvalidOperationException("Field can only be initialized once");
        }

        if (objectInitializer.FieldInitializers.Count != classSignature.Fields.Count)
        {
            throw new InvalidOperationException("Not all fields were initialized");
        }
        
        var fields = classSignature.Fields.ToDictionary(x => x.Name);
        
        foreach (var fieldInitializer in objectInitializer.FieldInitializers)
        {
            if (!fields.TryGetValue(fieldInitializer.FieldName.StringValue, out var field))
            {
                throw new InvalidOperationException($"No field named {fieldInitializer.FieldName.StringValue}");
            }

            var valueType = TypeCheckExpression(fieldInitializer.Value, genericPlaceholders);

            if (field.Type is GenericTypeReference { OwnerType: var owner, GenericName: var genericName })
            {
                var instantiatedGenericFieldType = typeArguments[genericName];

                if (!Equals(instantiatedGenericFieldType, valueType))
                {
                    throw new InvalidOperationException($"Expected {instantiatedGenericFieldType} but got {valueType}");
                }
            }
            else
            {
                if (!Equals(field.Type, valueType))
                {
                    throw new InvalidOperationException($"Expected {field.Type} but got {valueType}");
                }
            }
        }

        return foundType;
    }

    private static bool IsTypeDefinition(ITypeReference typeReference, ClassSignature signature)
    {
        if (typeReference is InstantiatedClass { Signature: var typeDefinition } && typeDefinition == signature)
        {
            return true;
        }

        if (typeReference is GenericTypeReference genericTypeReference)
        {
        }

        return false;
    }

    private ITypeReference TypeCheckBinaryOperatorExpression(
        BinaryOperator @operator,
        Dictionary<string, ITypeSignature> genericPlaceholders)
    {
        var leftType = TypeCheckExpression(@operator.Left, genericPlaceholders);
        var rightType = TypeCheckExpression(@operator.Right, genericPlaceholders);
        switch (@operator.OperatorType)
        {
            case BinaryOperatorType.LessThan:
            case BinaryOperatorType.GreaterThan:
            {
                if (!IsTypeDefinition(leftType, ClassSignature.Int))
                    throw new InvalidOperationException("Expected int");
                if (!IsTypeDefinition(rightType, ClassSignature.Int))
                    throw new InvalidOperationException("Expected int");

                return InstantiatedClass.Boolean;
            }
            case BinaryOperatorType.Plus:
            case BinaryOperatorType.Minus:
            case BinaryOperatorType.Multiply:
            case BinaryOperatorType.Divide:
            {
                if (!IsTypeDefinition(leftType, ClassSignature.Int))
                    throw new InvalidOperationException("Expected int");
                if (!IsTypeDefinition(rightType, ClassSignature.Int))
                    throw new InvalidOperationException("Expected int");

                return InstantiatedClass.Int;
            }
            case BinaryOperatorType.EqualityCheck:
            {
                if (!leftType.Equals(rightType))
                {
                    throw new InvalidOperationException("Cannot compare values of different types");
                }

                return InstantiatedClass.Boolean;
            }
            case BinaryOperatorType.ValueAssignment:
            {
                if (!IsExpressionAssignable(@operator.Left))
                {
                    throw new InvalidOperationException($"{@operator.Left} is not assignable");
                }

                if (!Equals(leftType, rightType))
                {
                    throw new InvalidOperationException($"Expected {leftType} but found {rightType}");
                }
                
                return rightType;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static bool IsExpressionAssignable(IExpression expression)
    {
        // todo: don't allow writing to immutable fields
        return expression switch
        {
            ValueAccessorExpression { ValueAccessor.AccessType: ValueAccessType.Variable } => true,
            MemberAccessExpression => true,
            StaticMemberAccessExpression => true,
            _ => false
        };
    }
    
    private ITypeReference TypeCheckIfExpression(IfExpression ifExpression,
        Dictionary<string, ITypeSignature> genericPlaceholders)
    {
        var checkExpressionType =
            TypeCheckExpression(ifExpression.CheckExpression, genericPlaceholders);
        
        if (!IsTypeDefinition(checkExpressionType, ClassSignature.Boolean))
        {
            throw new InvalidOperationException("Expected bool");
        }

        using (PushScope())
        {
            TypeCheckExpression(ifExpression.Body, genericPlaceholders);
        }
        
        foreach (var elseIf in ifExpression.ElseIfs)
        {
            using var _ = PushScope();
            var elseIfCheckExpressionType
                = TypeCheckExpression(elseIf.CheckExpression, genericPlaceholders);
            if (!IsTypeDefinition(elseIfCheckExpressionType, ClassSignature.Boolean))
            {
                throw new InvalidOperationException("Expected bool");
            }

            TypeCheckExpression(elseIf.Body, genericPlaceholders);
        }

        if (ifExpression.ElseBody is not null)
        {
            using var _ = PushScope();
            TypeCheckExpression(ifExpression.ElseBody, genericPlaceholders);
        }
        
        // todo: tail expression
        return InstantiatedClass.Unit;
    }

    private ITypeReference TypeCheckMethodCall(
        MethodCall methodCall,
        Dictionary<string, ITypeSignature> genericPlaceholders)
    {
        var methodType = TypeCheckExpression(methodCall.Method, genericPlaceholders);

        if (methodType is not InstantiatedFunction functionType)
        {
            throw new InvalidOperationException($"{methodType} is not callable");
        }

        if (functionType.TypeArguments.Count != functionType.Signature.GenericParameters.Count)
        {
            throw new InvalidOperationException($"Expected {functionType.Signature.GenericParameters.Count} type arguments, but found {functionType.TypeArguments.Count}");
        }
        
        if (methodCall.ParameterList.Count != functionType.Signature.Arguments.Count)
        {
            throw new InvalidOperationException($"Expected {functionType.Signature.Arguments.Count} parameters, got {methodCall.ParameterList.Count}");
        }

        for (var i = 0; i < functionType.Signature.Arguments.Count; i++)
        {
            var expectedParameterType = functionType.Signature.Arguments[i].Value;

            if (expectedParameterType is GenericTypeReference { GenericName: var genericName })
            {
                if (functionType.TypeArguments.TryGetValue(genericName, out var functionGeneric))
                {
                    expectedParameterType = functionGeneric;
                }
                else
                {
                    throw new NotImplementedException("Todo: keep track of class type arguments");
                }
            }
            
            var givenParameterType = TypeCheckExpression(methodCall.ParameterList[i], genericPlaceholders);

            if (!Equals(expectedParameterType, givenParameterType))
            {
                throw new InvalidOperationException(
                    $"Expected parameter type {expectedParameterType}, got {givenParameterType}");
            }
        }

        if (functionType.Signature.ReturnType is GenericTypeReference { GenericName: var returnTypeGenericName })
        {
            if (functionType.TypeArguments.TryGetValue(returnTypeGenericName, out var returnTypeGeneric))
            {
                return returnTypeGeneric;
            }
            else
            {
                throw new NotImplementedException("Todo: keep track of class generics");
            }
        }

        return functionType.Signature.ReturnType;
    }

    private ITypeReference TypeCheckMethodReturn(
        MethodReturnExpression methodReturnExpression,
        Dictionary<string, ITypeSignature> genericPlaceholders)
    {
        var returnExpressionType = methodReturnExpression.MethodReturn.Expression is null
            ? InstantiatedClass.Unit
            : TypeCheckExpression(methodReturnExpression.MethodReturn.Expression, genericPlaceholders);
        
        if (ExpectedReturnType is null && returnExpressionType is not null)
        {
            throw new InvalidOperationException($"Expected void, got {returnExpressionType}");
        }

        if (ExpectedReturnType is not null && returnExpressionType is null)
        {
            throw new InvalidOperationException($"Expected {ExpectedReturnType}, got void");
        }

        if (!Equals(ExpectedReturnType, returnExpressionType))
        {
            throw new InvalidOperationException($"Expected {returnExpressionType}, got {ExpectedReturnType}");
        }
        
        return InstantiatedClass.Never;
    }

    private ITypeReference TypeCheckValueAccessor(ValueAccessorExpression valueAccessorExpression)
    {
        return valueAccessorExpression.ValueAccessor switch
        {
            {AccessType: ValueAccessType.Literal, Token: IntToken {Type: TokenType.IntLiteral}} => InstantiatedClass.Int,
            {AccessType: ValueAccessType.Literal, Token: StringToken {Type: TokenType.StringLiteral}} => InstantiatedClass.String,
            {AccessType: ValueAccessType.Literal, Token.Type: TokenType.True or TokenType.False } => InstantiatedClass.Boolean,
            {AccessType: ValueAccessType.Variable, Token: StringToken {Type: TokenType.Identifier, StringValue: var variableName}} =>
                TypeCheckVariableAccess(variableName),
            _ => throw new NotImplementedException($"{valueAccessorExpression}")
        };
        
    }

    private ITypeReference TypeCheckVariableAccess(
        string variableName)
    {
        if (ScopedFunctions.TryGetValue(variableName, out var function))
        {
            return new InstantiatedFunction
            {
                Signature = function,
                TypeArguments = []
            };
        }
        
        if (!ScopedVariables.TryGetValue(variableName, out var value))
        {
            throw new InvalidOperationException($"No symbol found with name {variableName}");
        }

        if (!value.Instantiated)
        {
            throw new InvalidOperationException($"{value.Name} is not instantiated");
        }

        return value.Type;
    }

    private record Variable(string Name, ITypeReference Type, bool Instantiated);

    private ITypeReference TypeCheckVariableDeclaration(
        VariableDeclarationExpression expression,
        Dictionary<string, ITypeSignature> genericPlaceholders)
    {
        var varName = expression.VariableDeclaration.VariableNameToken.StringValue;
        if (ScopedVariables.ContainsKey(varName))
        {
            throw new InvalidOperationException(
                $"Variable with name {varName} already exists");
        }

        switch (expression.VariableDeclaration)
        {
            case {Value: null, Type: null}:
                throw new InvalidOperationException("Variable declaration must have a type specifier or a value");
            case { Value: { } value, Type: var type} :
            {
                var valueType = TypeCheckExpression(value, genericPlaceholders);
                if (type is not null)
                {
                    var expectedType = GetTypeReference(type, genericPlaceholders);
                    if (!Equals(expectedType, valueType))
                    {
                        throw new InvalidOperationException($"Expected type {expectedType}, but found {valueType}");
                    }
                }

                ScopedVariables[varName] = new Variable(varName, valueType, Instantiated: true);

                break;
            }
            case { Value: null, Type: { } type }:
            {
                var langType = GetTypeReference(type, genericPlaceholders);
                ScopedVariables[varName] = new Variable(varName, langType, Instantiated: false);

                break;
            }
        }
        
        // variable declaration return type is always unit, regardless of the variable type
        return InstantiatedClass.Unit;
    }

    private ITypeReference GetTypeReference(
        TypeIdentifier typeIdentifier,
        Dictionary<string, ITypeSignature> genericPlaceholders)
    {
        if (typeIdentifier.Identifier.Type == TokenType.StringKeyword)
        {
            return InstantiatedClass.String;
        }

        if (typeIdentifier.Identifier.Type == TokenType.IntKeyword)
        {
            return InstantiatedClass.Int;
        }

        if (typeIdentifier.Identifier.Type == TokenType.Bool)
        {
            return InstantiatedClass.Boolean;
        }

        if (typeIdentifier.Identifier.Type == TokenType.Result)
        {
            if (typeIdentifier.TypeArguments.Count != 2)
            {
                throw new InvalidOperationException("Result expects 2 arguments");
            }
            
            return InstantiatedClass.Result(
                GetTypeReference(typeIdentifier.TypeArguments[0], genericPlaceholders),
                GetTypeReference(typeIdentifier.TypeArguments[1], genericPlaceholders));
        }

        if (typeIdentifier.Identifier is StringToken { Type: TokenType.Identifier } stringToken)
        {
            if (_types.TryGetValue(stringToken.StringValue, out var nameMatchingType))
            {
                if (!nameMatchingType.IsGeneric && typeIdentifier.TypeArguments.Count == 0)
                {
                    return new InstantiatedClass
                        { Signature = nameMatchingType, TypeArguments = new Dictionary<string, ITypeReference>() };
                }

                if (nameMatchingType.GenericParameters.Count != typeIdentifier.TypeArguments.Count)
                {
                    throw new InvalidOperationException($"Expected {nameMatchingType.GenericParameters.Count} type arguments, but found {typeIdentifier.TypeArguments.Count}");
                }

                return new InstantiatedClass
                {
                    Signature = nameMatchingType,
                    TypeArguments = typeIdentifier.TypeArguments
                        .Zip(nameMatchingType.GenericParameters)
                        .ToDictionary(x => x.Second, x => GetTypeReference(x.First, genericPlaceholders))
                };
            }

            if (genericPlaceholders.TryGetValue(stringToken.StringValue, out var ownerType))
            {
                return new GenericTypeReference
                {
                    OwnerType = ownerType,
                    GenericName = stringToken.StringValue
                };
            }
        }
        
        throw new InvalidOperationException($"No type found {typeIdentifier}");
    }

    public interface ITypeReference;

    private class GenericTypeReference : ITypeReference, IEquatable<GenericTypeReference>
    {
        public required ITypeSignature OwnerType { get; init; } 
        
        public required string GenericName { get; init; }

        public bool Equals(GenericTypeReference? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return OwnerType.Equals(other.OwnerType) && GenericName == other.GenericName;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == GetType() && Equals((GenericTypeReference)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(OwnerType, GenericName);
        }
        
        public static bool operator==(GenericTypeReference? left, GenericTypeReference? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(GenericTypeReference? left, GenericTypeReference? right)
        {
            return !(left == right);
        }
    }

    private class InstantiatedFunction : ITypeReference
    {
        public required FunctionSignature Signature { get; init; }
        public required Dictionary<string, ITypeReference> TypeArguments { get; init; }
    }
    
    private class InstantiatedClass : IEquatable<InstantiatedClass>, ITypeReference
    {
        public static InstantiatedClass String { get; } = new() { Signature = ClassSignature.String, TypeArguments = new Dictionary<string, ITypeReference>()};
        public static InstantiatedClass Boolean { get; } = new() { Signature = ClassSignature.Boolean, TypeArguments = new Dictionary<string, ITypeReference>()};
        
        public static InstantiatedClass Int { get; } = new() { Signature = ClassSignature.Int, TypeArguments = new Dictionary<string, ITypeReference>()};

        public static InstantiatedClass Unit { get; } = new() { Signature = ClassSignature.Unit, TypeArguments = new Dictionary<string, ITypeReference>()};
        
        public static InstantiatedClass Never { get; } = new() {Signature = ClassSignature.Never, TypeArguments = new Dictionary<string, ITypeReference>() };

        public static InstantiatedClass Result(ITypeReference value, ITypeReference error) =>
            new()
            {
                Signature = ClassSignature.Result,
                TypeArguments = new Dictionary<string, ITypeReference>
                {
                    {"TValue", value},
                    {"TError", error}
                },
            };

        public static bool operator ==(InstantiatedClass? left, InstantiatedClass? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(InstantiatedClass? left, InstantiatedClass? right)
        {
            return !(left == right);
        }
        
        public required ITypeSignature Signature { get; init; }
        
        // todo: be consistent with argument/parameter
        public required IReadOnlyDictionary<string, ITypeReference> TypeArguments { get; init; }
        
        public bool Equals(InstantiatedClass? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Signature.Equals(other.Signature)
                   && TypeArguments.Count == other.TypeArguments.Count
                   && TypeArguments.All(x =>
                       other.TypeArguments.TryGetValue(x.Key, out var otherValue) && x.Value.Equals(otherValue));
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == GetType() && Equals((InstantiatedClass)obj);
        }

        public override int GetHashCode()
        {
            var hashCode = Signature.GetHashCode();

            return TypeArguments.Aggregate(hashCode, (current, kvp) => HashCode.Combine(current, kvp.Key.GetHashCode(), kvp.Value.GetHashCode()));
        }

        public override string ToString()
        {
            var sb = new StringBuilder($"{Signature.Name}");
            if (TypeArguments.Count > 0)
            {
                sb.Append('<');
                sb.AppendJoin(",", TypeArguments.Select(x => x.Value));
                sb.Append('>');
            }
            
            return sb.ToString();
        }
    }

    // todo: name
    private interface ITypeSignature
    {
        string Name { get; }
        IReadOnlyList<string> GenericParameters { get; }
    }

    private record FunctionSignature(
        string Name,
        IReadOnlyList<string> GenericParameters,
        IReadOnlyList<KeyValuePair<string, ITypeReference>> Arguments) : ITypeSignature
    {
        // mutable due to setting up signatures and generic stuff
        public required ITypeReference ReturnType { get; set; }
    }
    
    private class ClassSignature : ITypeSignature
    {
        public static ClassSignature Unit { get; } = new() { GenericParameters = [], Name = "Unit", Fields = [], StaticFields = [], Functions = []};
        public static ClassSignature String { get; } = new() { GenericParameters = [], Name = "String", Fields = [], StaticFields = [], Functions = []};
        public static ClassSignature Int { get; } = new() { GenericParameters = [], Name = "Int", Fields = [], StaticFields = [], Functions = []};
        public static ClassSignature Boolean { get; } = new() { GenericParameters = [], Name = "Boolean", Fields = [], StaticFields = [], Functions = []};
        public static ClassSignature Never { get; } = new() { GenericParameters = [], Name = "!", Fields = [], StaticFields = [], Functions = []};
        
        // todo: unions
        public static ClassSignature Result { get; } = new() { GenericParameters = ["TValue", "TError"], Name = "Result", Fields = [], StaticFields = [], Functions = []};
        public static IEnumerable<ClassSignature> BuiltInTypes { get; } = [Unit, String, Int, Never, Result, Boolean];
        
        public required IReadOnlyList<string> GenericParameters { get; init; }
        public bool IsGeneric => GenericParameters.Count > 0;
        public required string Name { get; init; }
        public required IReadOnlyList<TypeField> Fields { get; init; }
        public required IReadOnlyList<TypeField> StaticFields { get; init; }
        public required IReadOnlyList<FunctionSignature> Functions { get; init; }
        
        // todo: namespaces
    }

    public class TypeField
    {
        public required ITypeReference Type { get; init; }
        public required string Name { get; init; }
        public required bool IsPublic { get; init; }
        public required bool IsMutable { get; init; }
    }
}