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
    private readonly Dictionary<string, TypeDefinition> _types = TypeDefinition.BuiltInTypes.ToDictionary(x => x.Name);
    
    private readonly Stack<TypeCheckingScope> _typeCheckingScopes = new ();
    private Dictionary<string, Variable> Variables => _typeCheckingScopes.Peek().Variables;
    private Dictionary<string, FunctionTypeDefinition> Functions => _typeCheckingScopes.Peek().Functions;
    private ITypeReference? ExpectedReturnType => _typeCheckingScopes.Peek().ExpectedReturnType;


    private record TypeCheckingScope(
        Dictionary<string, Variable> Variables,
        Dictionary<string, FunctionTypeDefinition> Functions,
        ITypeReference? ExpectedReturnType);

    private IDisposable PushScope(ITypeReference? expectedReturnType = null)
    {
        var currentScope = _typeCheckingScopes.Peek();

        if (currentScope.ExpectedReturnType is not null && expectedReturnType is not null)
        {
            throw new InvalidOperationException("Cannot set expected return type when one is already expected");
        }
        
        _typeCheckingScopes.Push(new TypeCheckingScope(
            new Dictionary<string, Variable>(currentScope.Variables),
            new Dictionary<string, FunctionTypeDefinition>(currentScope.Functions),
            currentScope.ExpectedReturnType ?? expectedReturnType));

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
        _typeCheckingScopes.Push(new TypeCheckingScope(new (), new(), null));
        
        // store the fields separately from the classes, so that we can set up the classes before assigning their fields.
        // this means that by the time we get to setting up the fields, all the classes that can be referenced will
        // already be set up
        var classMembers = new List<(ProgramClass, TypeDefinition type, List<TypeField> instanceFields, List<TypeField> staticFields, List<FunctionTypeDefinition> functions)>();

        foreach (var @class in _program.Classes)
        {
            var name = @class.Name.StringValue;
            var fields = new List<TypeField>();
            var staticFields = new List<TypeField>();
            var functions = new List<FunctionTypeDefinition>();
            var typeDefinition = new TypeDefinition
            {
                Name = name,
                GenericParameters = @class.TypeArguments.Select(x => x.StringValue).ToArray(),
                Fields = fields,
                StaticFields = staticFields,
                Functions = functions
            };
            _types.Add(name, typeDefinition);

            classMembers.Add((@class, typeDefinition, fields, staticFields, functions));
        }

        foreach (var (@class, typeDefinition, instanceFields, staticFields, functions) in classMembers)
        {
            var genericPlaceholders = typeDefinition.GenericParameters.ToDictionary(x => x, x => typeDefinition);
            
            foreach (var field in @class.Fields)
            {
                var typeField = new TypeField
                {
                    Name = field.Name.StringValue,
                    Type = GetTypeReference(field.Type, genericPlaceholders),
                    IsMutable = field.MutabilityModifier is not null,
                    IsPublic = field.AccessModifier is {Token.Type: TokenType.Pub}
                };
                
                if (field.StaticModifier is not null)
                {
                    if (field.InitializerValue is null)
                    {
                        throw new InvalidOperationException("Static members must be initialized");
                    }

                    // todo: should be able to call static functions here?
                    var valueType = TypeCheckExpression(field.InitializerValue, genericPlaceholders);
                    if (!Equals(valueType, typeField.Type))
                    {
                        throw new InvalidOperationException($"Expected {typeField.Type} but found {valueType}");
                    }
                    staticFields.Add(typeField);
                }
                else
                {
                    if (field.InitializerValue is not null)
                    {
                        throw new InvalidOperationException("Instance members must not be initialized");
                    }
                    instanceFields.Add(typeField);
                }
            }
        }

        foreach (var fn in _program.Functions)
        {
            Functions[fn.Name.StringValue] = TypeCheckFunctionSignature(fn, []);
        }

        foreach (var fn in _program.Functions)
        {
            TypeCheckFunctionBody(fn, []);
        }


        foreach (var expression in _program.Expressions)
        {
            TypeCheckExpression(expression, []);
        }
    }


    private void TypeCheckFunctionBody(LangFunction function,
        Dictionary<string, TypeDefinition> genericPlaceholders)
    {
        var functionType = Functions[function.Name.StringValue];
        
        var innerGenericPlaceholders = new Dictionary<string, TypeDefinition>(genericPlaceholders);
        foreach (var genericParameter in functionType.GenericParameters)
        {
            innerGenericPlaceholders[genericParameter] = functionType;
        }

        var expectedReturnType = function.ReturnType is null
            ? null
            : GetTypeReference(function.ReturnType, innerGenericPlaceholders);
        
        using var _ = PushScope(expectedReturnType);
        foreach (var parameter in functionType.Parameters)
        {
            Variables[parameter.Name] = new Variable(
                parameter.Name,
                parameter.Type,
                Instantiated: true);
        }

        
        TypeCheckBlock(function.Block,
            innerGenericPlaceholders);
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private FunctionTypeDefinition TypeCheckFunctionSignature(
        LangFunction function,
        Dictionary<string, TypeDefinition> genericPlaceholders)
    {
        if (Functions.ContainsKey(function.Name.StringValue))
        {
            throw new InvalidOperationException($"Function with name {function.Name.StringValue} already defined");
        }

        if (function.TypeArguments.Any(typeArgument => 
                genericPlaceholders.ContainsKey(typeArgument.StringValue) || _types.ContainsKey(typeArgument.StringValue)))
        {
            throw new InvalidOperationException("Type argument name is conflicting");
        }
        
        // todo: need to get a reference to FunctionTypeDefinition while creating the function type definition
        var innerGenericPlaceholders = new Dictionary<string, TypeDefinition>(genericPlaceholders);
        
        return new FunctionTypeDefinition
        {
            Name = function.Name.StringValue,
            ReturnType = function.ReturnType is null ? InstantiatedType.Unit : GetTypeReference(function.ReturnType, innerGenericPlaceholders),
            GenericParameters = function.TypeArguments.Select(x => x.StringValue).ToArray(),
            Parameters = function.Parameters.Select(x => new FunctionTypeDefinition.Parameter(x.Identifier.StringValue, GetTypeReference(x.Type, innerGenericPlaceholders))).ToArray(),
            // todo: functions don't have fields
            Fields = [],
            StaticFields = [],
            Functions = []
        };
    }

    private ITypeReference TypeCheckBlock(
        Block block,
        Dictionary<string, TypeDefinition> genericPlaceholders)
    {
        using var _ = PushScope();
        
        foreach (var fn in block.Functions)
        {
            Functions[fn.Name.StringValue] = TypeCheckFunctionSignature(fn, genericPlaceholders);
        }
        
        foreach (var fn in block.Functions)
        {
            TypeCheckFunctionBody(fn, genericPlaceholders);
        }

        foreach (var expression in block.Expressions)
        {
            TypeCheckExpression(expression, genericPlaceholders);
        }

        // todo: tail expressions
        return InstantiatedType.Unit;
    }

    private ITypeReference TypeCheckExpression(
        IExpression expression,
        Dictionary<string, TypeDefinition> genericPlaceholders)
    {
        return expression switch
        {
            VariableDeclarationExpression variableDeclarationExpression => TypeCheckVariableDeclaration(
                variableDeclarationExpression, genericPlaceholders),
            ValueAccessorExpression valueAccessorExpression => TypeCheckValueAccessor(valueAccessorExpression, genericPlaceholders),
            MethodReturnExpression methodReturnExpression => TypeCheckMethodReturn(methodReturnExpression, genericPlaceholders),
            MethodCallExpression methodCallExpression => TypeCheckMethodCall(methodCallExpression.MethodCall, genericPlaceholders),
            BlockExpression blockExpression => TypeCheckBlock(blockExpression.Block, genericPlaceholders),
            IfExpressionExpression ifExpressionExpression => TypeCheckIfExpression(ifExpressionExpression.IfExpression, genericPlaceholders),
            BinaryOperatorExpression binaryOperatorExpression => TypeCheckBinaryOperatorExpression(binaryOperatorExpression.BinaryOperator, genericPlaceholders),
            ObjectInitializerExpression objectInitializerExpression => TypeCheckObjectInitializer(objectInitializerExpression.ObjectInitializer, genericPlaceholders),
            MemberAccessExpression memberAccessExpression => TypeCheckMemberAccess(memberAccessExpression.MemberAccess, genericPlaceholders),
            StaticMemberAccessExpression staticMemberAccessExpression => TypeCheckStaticMemberAccess(staticMemberAccessExpression.StaticMemberAccess, genericPlaceholders),
            _ => throw new NotImplementedException($"{expression.ExpressionType}")
        };
    }

    private ITypeReference TypeCheckMemberAccess(
        MemberAccess memberAccess,
        Dictionary<string, TypeDefinition> genericPlaceholders)
    {
        var ownerType = TypeCheckExpression(memberAccess.Owner, genericPlaceholders);

        if (ownerType is not InstantiatedType instantiatedType)
        {
            // todo: generic argument constraints with interfaces?
            throw new InvalidOperationException("Can only access members on instantiated types");
        }

        var memberType = instantiatedType.TypeDefinition.Fields.FirstOrDefault(x => x.Name == memberAccess.MemberName.StringValue)?.Type
            ?? instantiatedType.TypeDefinition.Functions.FirstOrDefault(x => x.Name == memberAccess.MemberName.StringValue)?.ReturnType;

        if (memberType is null)
        {
            throw new InvalidOperationException($"No member named {memberAccess.MemberName.StringValue}");
        }

        return memberType;
    }

    private ITypeReference TypeCheckStaticMemberAccess(
        StaticMemberAccess staticMemberAccess,
        Dictionary<string, TypeDefinition> genericPlaceholders)
    {
        var type = GetTypeReference(staticMemberAccess.Type, genericPlaceholders);

        if (type is not InstantiatedType instantiatedType)
        {
            throw new InvalidOperationException("Can only access static members on instantiated types");
        }
        
        var field = instantiatedType.TypeDefinition.StaticFields.FirstOrDefault(x => x.Name == staticMemberAccess.MemberName.StringValue)
            ?? throw new InvalidOperationException($"No member with name {staticMemberAccess.MemberName.StringValue}");

        return field.Type;
    }

    private ITypeReference TypeCheckObjectInitializer(
        ObjectInitializer objectInitializer,
        Dictionary<string, TypeDefinition> genericPlaceholders)
    {
        var foundType = GetTypeReference(objectInitializer.Type, genericPlaceholders);
        if (foundType is not InstantiatedType instantiatedType)
        {
            // todo: more checks
            throw new InvalidOperationException($"Type {foundType} cannot be initialized");
        }

        if (objectInitializer.FieldInitializers.GroupBy(x => x.FieldName.StringValue)
            .Any(x => x.Count() > 1))
        {
            throw new InvalidOperationException("Field can only be initialized once");
        }

        if (objectInitializer.FieldInitializers.Count != instantiatedType.TypeDefinition.Fields.Count)
        {
            throw new InvalidOperationException("Not all fields were initialized");
        }
        
        var fields = instantiatedType.TypeDefinition.Fields.ToDictionary(x => x.Name);
        
        foreach (var fieldInitializer in objectInitializer.FieldInitializers)
        {
            if (!fields.TryGetValue(fieldInitializer.FieldName.StringValue, out var field))
            {
                throw new InvalidOperationException($"No field named {fieldInitializer.FieldName.StringValue}");
            }

            var valueType = TypeCheckExpression(fieldInitializer.Value, genericPlaceholders);

            if (!Equals(field.Type, valueType))
            {
                throw new InvalidOperationException($"Expected {field.Type} but got {valueType}");
            }
        }

        return foundType;
    }

    private static bool IsTypeDefinition(ITypeReference typeReference, TypeDefinition definition)
    {
        return typeReference is InstantiatedType { TypeDefinition: var typeDefinition } && typeDefinition == definition;
    }

    private ITypeReference TypeCheckBinaryOperatorExpression(
        BinaryOperator @operator,
        Dictionary<string, TypeDefinition> genericPlaceholders)
    {
        var leftType = TypeCheckExpression(@operator.Left, genericPlaceholders);
        var rightType = TypeCheckExpression(@operator.Right, genericPlaceholders);
        switch (@operator.OperatorType)
        {
            case BinaryOperatorType.LessThan:
            case BinaryOperatorType.GreaterThan:
            {
                if (!IsTypeDefinition(leftType, TypeDefinition.Int))
                    throw new InvalidOperationException("Expected int");
                if (!IsTypeDefinition(rightType, TypeDefinition.Int))
                    throw new InvalidOperationException("Expected int");

                return InstantiatedType.Boolean;
            }
            case BinaryOperatorType.Plus:
            case BinaryOperatorType.Minus:
            case BinaryOperatorType.Multiply:
            case BinaryOperatorType.Divide:
            {
                if (!IsTypeDefinition(leftType, TypeDefinition.Int))
                    throw new InvalidOperationException("Expected int");
                if (!IsTypeDefinition(rightType, TypeDefinition.Int))
                    throw new InvalidOperationException("Expected int");

                return InstantiatedType.Int;
            }
            case BinaryOperatorType.EqualityCheck:
            {
                if (!leftType.Equals(rightType))
                {
                    throw new InvalidOperationException("Cannot compare values of different types");
                }

                return InstantiatedType.Boolean;
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
        Dictionary<string, TypeDefinition> genericPlaceholders)
    {
        var checkExpressionType =
            TypeCheckExpression(ifExpression.CheckExpression, genericPlaceholders);
        
        if (!IsTypeDefinition(checkExpressionType, TypeDefinition.Boolean))
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
            if (!IsTypeDefinition(elseIfCheckExpressionType, TypeDefinition.Boolean))
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
        return InstantiatedType.Unit;
    }

    private ITypeReference TypeCheckMethodCall(
        MethodCall methodCall,
        Dictionary<string, TypeDefinition> genericPlaceholders)
    {
        var methodType = TypeCheckExpression(methodCall.Method, genericPlaceholders);

        if (methodType is not InstantiatedType {TypeDefinition: FunctionTypeDefinition functionType})
        // if (methodType.Type is not FunctionTypeDefinition functionType)
        {
            throw new InvalidOperationException($"{methodType} is not callable");
        }
        
        if (methodCall.ParameterList.Count != functionType.Parameters.Count)
        {
            throw new InvalidOperationException($"Expected {functionType.Parameters.Count} parameters, got {methodCall.ParameterList.Count}");
        }

        for (var i = 0; i < functionType.Parameters.Count; i++)
        {
            var expectedParameterType = functionType.Parameters[i].Type;
            var givenParameterType = TypeCheckExpression(methodCall.ParameterList[i], genericPlaceholders);

            if (!Equals(expectedParameterType, givenParameterType))
            {
                throw new InvalidOperationException(
                    $"Expected parameter type {expectedParameterType}, got {givenParameterType}");
            }
        }

        return functionType.ReturnType;
    }

    private ITypeReference TypeCheckMethodReturn(
        MethodReturnExpression methodReturnExpression,
        Dictionary<string, TypeDefinition> genericPlaceholders)
    {
        var returnExpressionType = methodReturnExpression.MethodReturn.Expression is null
            ? null
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
        
        return InstantiatedType.Never;
    }

    private ITypeReference TypeCheckValueAccessor(ValueAccessorExpression valueAccessorExpression,
        Dictionary<string, TypeDefinition> genericPlaceholders)
    {
        return valueAccessorExpression.ValueAccessor switch
        {
            {AccessType: ValueAccessType.Literal, Token: IntToken {Type: TokenType.IntLiteral}} => InstantiatedType.Int,
            {AccessType: ValueAccessType.Literal, Token: StringToken {Type: TokenType.StringLiteral}} => InstantiatedType.String,
            {AccessType: ValueAccessType.Literal, Token.Type: TokenType.True or TokenType.False } => InstantiatedType.Boolean,
            {AccessType: ValueAccessType.Variable, Token: StringToken {Type: TokenType.Identifier, StringValue: var variableName}, TypeArguments: var typeArguments} =>
                TypeCheckVariableAccess(variableName, typeArguments, genericPlaceholders),
            _ => throw new NotImplementedException($"{valueAccessorExpression}")
        };
        
    }

    private ITypeReference TypeCheckVariableAccess(
        string variableName,
        IReadOnlyList<TypeIdentifier> typeArguments,
        Dictionary<string, TypeDefinition> genericPlaceholders)
    {
        if (Functions.TryGetValue(variableName, out var function))
        {
            if (function.GenericParameters.Count != typeArguments.Count)
            {
                throw new InvalidOperationException($"Expected {function.GenericParameters.Count} type parameters");
            }

            var instantiatedTypeArguments = typeArguments.Zip(function.GenericParameters)
                .ToDictionary(x => x.Second, x => GetTypeReference(x.First, genericPlaceholders));
            
            return new InstantiatedType
            {
                TypeDefinition = function,
                TypeArguments = instantiatedTypeArguments
            };
        }
        
        if (!Variables.TryGetValue(variableName, out var value))
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
        Dictionary<string, TypeDefinition> genericPlaceholders)
    {
        var varName = expression.VariableDeclaration.VariableNameToken.StringValue;
        if (Variables.ContainsKey(varName))
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

                Variables[varName] = new Variable(varName, valueType, Instantiated: true);

                break;
            }
            case { Value: null, Type: { } type }:
            {
                var langType = GetTypeReference(type, genericPlaceholders);
                Variables[varName] = new Variable(varName, langType, Instantiated: false);

                break;
            }
        }
        
        // variable declaration return type is always unit, regardless of the variable type
        return InstantiatedType.Unit;
    }

    private ITypeReference GetTypeReference(
        TypeIdentifier typeIdentifier,
        Dictionary<string, TypeDefinition> genericPlaceholders)
    {
        if (typeIdentifier.Identifier.Type == TokenType.StringKeyword)
        {
            return InstantiatedType.String;
        }

        if (typeIdentifier.Identifier.Type == TokenType.IntKeyword)
        {
            return InstantiatedType.Int;
        }

        if (typeIdentifier.Identifier.Type == TokenType.Bool)
        {
            return InstantiatedType.Boolean;
        }

        if (typeIdentifier.Identifier.Type == TokenType.Result)
        {
            if (typeIdentifier.TypeArguments.Count != 2)
            {
                throw new InvalidOperationException("Result expects 2 arguments");
            }
            
            return InstantiatedType.Result(
                GetTypeReference(typeIdentifier.TypeArguments[0], genericPlaceholders),
                GetTypeReference(typeIdentifier.TypeArguments[1], genericPlaceholders));
        }

        if (typeIdentifier.Identifier is StringToken { Type: TokenType.Identifier } stringToken)
        {
            if (_types.TryGetValue(stringToken.StringValue, out var nameMatchingType))
            {
                if (!nameMatchingType.IsGeneric && typeIdentifier.TypeArguments.Count == 0)
                {
                    return new InstantiatedType
                        { TypeDefinition = nameMatchingType, TypeArguments = new Dictionary<string, ITypeReference>() };
                }

                if (nameMatchingType.GenericParameters.Count != typeIdentifier.TypeArguments.Count)
                {
                    throw new InvalidOperationException($"Expected {nameMatchingType.GenericParameters.Count} type arguments, but found {typeIdentifier.TypeArguments.Count}");
                }

                return new InstantiatedType
                {
                    TypeDefinition = nameMatchingType,
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

    public class GenericTypeReference : ITypeReference, IEquatable<GenericTypeReference>
    {
        public required TypeDefinition OwnerType { get; init; } 
        
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
    
    public class InstantiatedType : IEquatable<InstantiatedType>, ITypeReference
    {
        public static InstantiatedType String { get; } = new() { TypeDefinition = TypeDefinition.String, TypeArguments = new Dictionary<string, ITypeReference>()};
        public static InstantiatedType Boolean { get; } = new() { TypeDefinition = TypeDefinition.Boolean, TypeArguments = new Dictionary<string, ITypeReference>()};
        
        public static InstantiatedType Int { get; } = new() { TypeDefinition = TypeDefinition.Int, TypeArguments = new Dictionary<string, ITypeReference>()};

        public static InstantiatedType Unit { get; } = new() { TypeDefinition = TypeDefinition.Unit, TypeArguments = new Dictionary<string, ITypeReference>()};
        
        public static InstantiatedType Never { get; } = new() {TypeDefinition = TypeDefinition.Never, TypeArguments = new Dictionary<string, ITypeReference>() };

        public static InstantiatedType Result(ITypeReference value, ITypeReference error) =>
            new()
            {
                TypeDefinition = TypeDefinition.Result,
                TypeArguments = new Dictionary<string, ITypeReference>
                {
                    {"TValue", value},
                    {"TError", error}
                },
            };

        public static bool operator ==(InstantiatedType? left, InstantiatedType? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(InstantiatedType? left, InstantiatedType? right)
        {
            return !(left == right);
        }
        
        public required TypeDefinition TypeDefinition { get; init; }
        
        // todo: be consistent with argument/parameter
        public required IReadOnlyDictionary<string, ITypeReference> TypeArguments { get; init; }
        
        public bool Equals(InstantiatedType? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return TypeDefinition.Equals(other.TypeDefinition)
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

            return obj.GetType() == GetType() && Equals((InstantiatedType)obj);
        }

        public override int GetHashCode()
        {
            var hashCode = TypeDefinition.GetHashCode();

            return TypeArguments.Aggregate(hashCode, (current, kvp) => HashCode.Combine(current, kvp.Key.GetHashCode(), kvp.Value.GetHashCode()));
        }

        public override string ToString()
        {
            var sb = new StringBuilder($"{TypeDefinition.Name}");
            if (TypeArguments.Count > 0)
            {
                sb.Append('<');
                sb.AppendJoin(",", TypeArguments.Select(x => x.Value));
                sb.Append('>');
            }
            
            return sb.ToString();
        }
    }
    
    public class TypeDefinition
    {
        public static TypeDefinition Unit { get; } = new() { GenericParameters = [], Name = "Unit", Fields = [], StaticFields = [], Functions = []};
        public static TypeDefinition String { get; } = new() { GenericParameters = [], Name = "String", Fields = [], StaticFields = [], Functions = []};
        public static TypeDefinition Int { get; } = new() { GenericParameters = [], Name = "Int", Fields = [], StaticFields = [], Functions = []};
        public static TypeDefinition Boolean { get; } = new() { GenericParameters = [], Name = "Boolean", Fields = [], StaticFields = [], Functions = []};
        public static TypeDefinition Never { get; } = new() { GenericParameters = [], Name = "!", Fields = [], StaticFields = [], Functions = []};
        
        // todo: unions
        public static TypeDefinition Result { get; } = new() { GenericParameters = ["TValue", "TError"], Name = "Result", Fields = [], StaticFields = [], Functions = []};
        public static IEnumerable<TypeDefinition> BuiltInTypes { get; } = [Unit, String, Int, Never, Result, Boolean];
        
        public required IReadOnlyList<string> GenericParameters { get; init; }
        public bool IsGeneric => GenericParameters.Count > 0;
        public required string Name { get; init; }
        public required IReadOnlyList<TypeField> Fields { get; init; }
        public required IReadOnlyList<TypeField> StaticFields { get; init; }
        public required IReadOnlyList<FunctionTypeDefinition> Functions { get; init; }
        
        // todo: namespaces
    }

    public class TypeField
    {
        public required ITypeReference Type { get; init; }
        public required string Name { get; init; }
        public required bool IsPublic { get; init; }
        public required bool IsMutable { get; init; }
    }
    
    public class FunctionTypeDefinition : TypeDefinition
    {
        public required IReadOnlyList<Parameter> Parameters { get; init; }
        
        public required ITypeReference ReturnType { get; init; }

        public record Parameter(string Name, ITypeReference Type);
    }
}