using System.Text;

namespace NewLang.Core;

public class TypeChecker
{
    public static void TypeCheck(LangProgram program)
    {
        var types = TypeDefinition.BuiltInTypes.ToDictionary(x => x.Name);
        
        // store the fields separately from the classes, so that we can set up the classes before assigning their fields.
        // this means that by the time we get to setting up the fields, all the classes that can be referenced will
        // already be set up
        var classFields = new Dictionary<ProgramClass, (List<TypeField> instanceFields, List<TypeField> staticFields)>();

        foreach (var @class in program.Classes)
        {
            var name = GetIdentifierName(@class.Name);
            var fields = new List<TypeField>();
            var staticFields = new List<TypeField>();
            types.Add(name, new TypeDefinition
            {
                Name = name,
                GenericParameters = @class.TypeArguments.Select(GetIdentifierName).ToArray(),
                Fields = fields,
                StaticFields = staticFields
            });

            classFields.Add(@class, (fields, staticFields));
        }

        foreach (var (@class, (instanceFields, staticFields)) in classFields)
        {
            foreach (var field in @class.Fields)
            {
                var typeField = new TypeField
                {
                    Name = field.Name.StringValue,
                    Type = GetInstantiatedType(field.Type, types),
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
                    var valueType = TypeCheckExpression(field.InitializerValue, types, [], [], null);
                    if (valueType != typeField.Type)
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

        var functionTypes = new Dictionary<string, FunctionTypeDefinition>();
        
        foreach (var fn in program.Functions)
        {
            functionTypes[fn.Name.StringValue] = TypeCheckFunctionSignature(fn, functionTypes, types);
        }

        foreach (var fn in program.Functions)
        {
            TypeCheckFunctionBody(fn, variables: [], functionTypes, types);
        }

        var variables = new Dictionary<string, Variable>();

        foreach (var expression in program.Expressions)
        {
            TypeCheckExpression(expression, types, variables, functionTypes, expectedReturnType: null);
        }
    }

    private static void TypeCheckFunctionBody(LangFunction function,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionTypeDefinition> functions,
        Dictionary<string, TypeDefinition> types)
    {
        var functionType = functions[function.Name.StringValue];

        var innerVariables = new Dictionary<string, Variable>(variables);
        foreach (var parameter in functionType.Parameters)
        {
            innerVariables[parameter.Name] = new Variable(
                parameter.Name,
                parameter.Type,
                Instantiated: true);
        }
        
        TypeCheckBlock(function.Block, innerVariables, types, functions, function.ReturnType is null ? null : GetInstantiatedType(function.ReturnType, types));
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private static FunctionTypeDefinition TypeCheckFunctionSignature(LangFunction function, Dictionary<string, FunctionTypeDefinition> functions, Dictionary<string, TypeDefinition> types)
    {
        if (functions.ContainsKey(function.Name.StringValue))
        {
            throw new InvalidOperationException($"Function with name {function.Name.StringValue} already defined");
        }
        
        return new FunctionTypeDefinition
        {
            Name = function.Name.StringValue,
            ReturnType = function.ReturnType is null ? InstantiatedType.Unit : GetInstantiatedType(function.ReturnType, types),
            GenericParameters = function.TypeArguments.Select(x => x.StringValue).ToArray(),
            Parameters = function.Parameters.Select(x => new FunctionTypeDefinition.Parameter(x.Identifier.StringValue, GetInstantiatedType(x.Type, types))).ToArray(),
            // todo: functions don't have fields
            Fields = [],
            StaticFields = []
        };
    }

    private static InstantiatedType TypeCheckBlock(
        Block block,
        Dictionary<string, Variable> variables,
        Dictionary<string, TypeDefinition> types,
        Dictionary<string, FunctionTypeDefinition> functions,
        InstantiatedType? expectedReturnType)
    {
        var innerVariables = new Dictionary<string, Variable>(variables);
        var innerFunctions = new Dictionary<string, FunctionTypeDefinition>(functions);
        
        foreach (var fn in block.Functions)
        {
            innerFunctions[fn.Name.StringValue] = TypeCheckFunctionSignature(fn, innerFunctions, types);
        }
        
        foreach (var fn in block.Functions)
        {
            TypeCheckFunctionBody(fn, innerVariables, innerFunctions, types);
        }

        foreach (var expression in block.Expressions)
        {
            TypeCheckExpression(expression, types, innerVariables, innerFunctions, expectedReturnType);
        }

        // todo: tail expressions
        return InstantiatedType.Unit;
    }

    private static InstantiatedType TypeCheckExpression(
        IExpression expression,
        Dictionary<string, TypeDefinition> types,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionTypeDefinition> functions,
        InstantiatedType? expectedReturnType)
    {
        return expression switch
        {
            VariableDeclarationExpression variableDeclarationExpression => TypeCheckVariableDeclaration(
                variableDeclarationExpression, types, variables, functions, expectedReturnType),
            ValueAccessorExpression valueAccessorExpression => TypeCheckValueAccessor(valueAccessorExpression, variables, functions, types),
            MethodReturnExpression methodReturnExpression => TypeCheckMethodReturn(methodReturnExpression, types, variables, functions, expectedReturnType),
            MethodCallExpression methodCallExpression => TypeCheckMethodCall(methodCallExpression.MethodCall, types, variables, functions, expectedReturnType),
            BlockExpression blockExpression => TypeCheckBlock(blockExpression.Block, variables, types, functions, expectedReturnType),
            IfExpressionExpression ifExpressionExpression => TypeCheckIfExpression(ifExpressionExpression.IfExpression, types, variables, functions, expectedReturnType),
            BinaryOperatorExpression binaryOperatorExpression => TypeCheckBinaryOperatorExpression(binaryOperatorExpression.BinaryOperator, types, variables, functions, expectedReturnType),
            ObjectInitializerExpression objectInitializerExpression => TypeCheckObjectInitializer(objectInitializerExpression.ObjectInitializer, types, variables, functions, expectedReturnType),
            MemberAccessExpression memberAccessExpression => TypeCheckMemberAccess(memberAccessExpression.MemberAccess, types, variables, functions, expectedReturnType),
            StaticMemberAccessExpression staticMemberAccessExpression => TypeCheckStaticMemberAccess(staticMemberAccessExpression.StaticMemberAccess, types),
            _ => throw new NotImplementedException($"{expression.ExpressionType}")
        };
    }

    private static InstantiatedType TypeCheckMemberAccess(
        MemberAccess memberAccess,
        Dictionary<string, TypeDefinition> types,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionTypeDefinition> functions,
        InstantiatedType? expectedReturnType)
    {
        var ownerType = TypeCheckExpression(memberAccess.Owner, types, variables, functions, expectedReturnType);

        var field = ownerType.Type.Fields.FirstOrDefault(x => x.Name == memberAccess.MemberName.StringValue)
            ?? throw new InvalidOperationException($"No field named {memberAccess.MemberName.StringValue}");

        return field.Type;
    }

    private static InstantiatedType TypeCheckStaticMemberAccess(
        StaticMemberAccess staticMemberAccess,
        Dictionary<string, TypeDefinition> types)
    {
        var type = GetInstantiatedType(staticMemberAccess.Type, types);

        var field = type.Type.StaticFields.FirstOrDefault(x => x.Name == staticMemberAccess.MemberName.StringValue)
            ?? throw new InvalidOperationException($"No member with name {staticMemberAccess.MemberName.StringValue}");

        return field.Type;
    }

    private static InstantiatedType TypeCheckObjectInitializer(
        ObjectInitializer objectInitializer,
        Dictionary<string, TypeDefinition> types,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionTypeDefinition> functions,
        InstantiatedType? expectedReturnType)
    {
        var foundType = GetInstantiatedType(objectInitializer.Type, types);
        if (!CanTypeBeInitialized(foundType))
        {
            throw new InvalidOperationException($"Type {foundType} cannot be initialized");
        }

        if (objectInitializer.FieldInitializers.GroupBy(x => x.FieldName.StringValue)
            .Any(x => x.Count() > 1))
        {
            throw new InvalidOperationException("Field can only be initialized once");
        }

        if (objectInitializer.FieldInitializers.Count != foundType.Type.Fields.Count)
        {
            throw new InvalidOperationException("Not all fields were initialized");
        }
        
        var fields = foundType.Type.Fields.ToDictionary(x => x.Name);
        
        foreach (var fieldInitializer in objectInitializer.FieldInitializers)
        {
            if (!fields.TryGetValue(fieldInitializer.FieldName.StringValue, out var field))
            {
                throw new InvalidOperationException($"No field named {fieldInitializer.FieldName.StringValue}");
            }

            var valueType = TypeCheckExpression(fieldInitializer.Value, types, variables, functions, expectedReturnType);

            if (field.Type != valueType)
            {
                throw new InvalidOperationException($"Expected {field.Type} but got {valueType}");
            }
        }

        return foundType;
    }

    private static bool CanTypeBeInitialized(InstantiatedType type)
    {
        // todo:
        return true;
    }

    private static InstantiatedType TypeCheckBinaryOperatorExpression(
        BinaryOperator @operator,
        Dictionary<string, TypeDefinition> types,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionTypeDefinition> functions,
        InstantiatedType? expectedReturnType)
    {
        var leftType = TypeCheckExpression(@operator.Left, types, variables, functions, expectedReturnType);
        var rightType = TypeCheckExpression(@operator.Right, types, variables, functions, expectedReturnType);
        switch (@operator.OperatorType)
        {
            case BinaryOperatorType.LessThan:
            case BinaryOperatorType.GreaterThan:
            {
                if (leftType.Type != TypeDefinition.Int)
                    throw new InvalidOperationException("Expected int");
                if (rightType.Type != TypeDefinition.Int)
                    throw new InvalidOperationException("Expected int");

                return InstantiatedType.Boolean;
            }
            case BinaryOperatorType.Plus:
            case BinaryOperatorType.Minus:
            case BinaryOperatorType.Multiply:
            case BinaryOperatorType.Divide:
            {
                if (leftType.Type != TypeDefinition.Int)
                    throw new InvalidOperationException("Expected int");
                if (rightType.Type != TypeDefinition.Int)
                    throw new InvalidOperationException("Expected int");

                return InstantiatedType.Int;
            }
            case BinaryOperatorType.EqualityCheck:
            {
                if (leftType != rightType)
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

                if (leftType != rightType)
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
    
    private static InstantiatedType TypeCheckIfExpression(IfExpression ifExpression,
        Dictionary<string, TypeDefinition> types,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionTypeDefinition> functions,
        InstantiatedType? expectedReturnType)
    {
        var checkExpressionType =
            TypeCheckExpression(ifExpression.CheckExpression, types, variables, functions, expectedReturnType);
        if (checkExpressionType.Type != TypeDefinition.Boolean)
        {
            throw new InvalidOperationException("Expected bool");
        }

        TypeCheckExpression(ifExpression.Body, types, variables, functions, expectedReturnType);

        foreach (var elseIf in ifExpression.ElseIfs)
        {
            var elseIfCheckExpressionType
                = TypeCheckExpression(elseIf.CheckExpression, types, variables, functions, expectedReturnType);
            if (elseIfCheckExpressionType.Type != TypeDefinition.Boolean)
            {
                throw new InvalidOperationException("Expected bool");
            }

            TypeCheckExpression(elseIf.Body, types, variables, functions, expectedReturnType);
        }

        if (ifExpression.ElseBody is not null)
        {
            TypeCheckExpression(ifExpression.ElseBody, types, variables, functions, expectedReturnType);
        }
        
        // todo: tail expression
        return InstantiatedType.Unit;
    }

    private static InstantiatedType TypeCheckMethodCall(
        MethodCall methodCall,
        Dictionary<string, TypeDefinition> types,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionTypeDefinition> functions,
        InstantiatedType? expectedReturnType)
    {
        var methodType = TypeCheckExpression(methodCall.Method, types, variables, functions, expectedReturnType);

        if (methodType.Type is not FunctionTypeDefinition functionType)
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
            var givenParameterType = TypeCheckExpression(methodCall.ParameterList[i], types, variables, functions, expectedParameterType);

            if (expectedParameterType != givenParameterType)
            {
                throw new InvalidOperationException(
                    $"Expected parameter type {expectedParameterType}, got {givenParameterType}");
            }
        }

        return functionType.ReturnType;
    }

    private static InstantiatedType TypeCheckMethodReturn(
        MethodReturnExpression methodReturnExpression,
        Dictionary<string, TypeDefinition> types,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionTypeDefinition> functions,
        InstantiatedType? expectedReturnType)
    {
        var returnExpressionType = methodReturnExpression.MethodReturn.Expression is null
            ? null
            : TypeCheckExpression(methodReturnExpression.MethodReturn.Expression, types, variables, functions, expectedReturnType);
        
        if (expectedReturnType is null && returnExpressionType is not null)
        {
            throw new InvalidOperationException($"Expected void, got {returnExpressionType}");
        }

        if (expectedReturnType is not null && returnExpressionType is null)
        {
            throw new InvalidOperationException($"Expected {expectedReturnType}, got void");
        }

        if (expectedReturnType != returnExpressionType)
        {
            throw new InvalidOperationException($"Expected {returnExpressionType}, got {expectedReturnType}");
        }
        
        return InstantiatedType.Never;
    }

    private static InstantiatedType TypeCheckValueAccessor(ValueAccessorExpression valueAccessorExpression, Dictionary<string, Variable> variables, Dictionary<string, FunctionTypeDefinition> functions, Dictionary<string, TypeDefinition> types)
    {
        return valueAccessorExpression.ValueAccessor switch
        {
            {AccessType: ValueAccessType.Literal, Token: IntToken {Type: TokenType.IntLiteral}} => InstantiatedType.Int,
            {AccessType: ValueAccessType.Literal, Token: StringToken {Type: TokenType.StringLiteral}} => InstantiatedType.String,
            {AccessType: ValueAccessType.Literal, Token.Type: TokenType.True or TokenType.False } => InstantiatedType.Boolean,
            {AccessType: ValueAccessType.Variable, Token: StringToken {Type: TokenType.Identifier, StringValue: var variableName}, TypeArguments: var typeArguments} =>
                TypeCheckVariableAccess(variableName, variables, functions, types, typeArguments),
            _ => throw new NotImplementedException($"{valueAccessorExpression}")
        };
        
    }

    private static InstantiatedType TypeCheckVariableAccess(
        string variableName,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionTypeDefinition> functions,
        Dictionary<string, TypeDefinition> types,
        IReadOnlyList<TypeIdentifier> typeArguments)
    {
        if (functions.TryGetValue(variableName, out var function))
        {
            if (function.GenericParameters.Count != typeArguments.Count)
            {
                throw new InvalidOperationException($"Expected {function.GenericParameters.Count} type parameters");
            }

            var instantiatedTypeArguments = typeArguments.Zip(function.GenericParameters)
                .ToDictionary(x => x.Second, x => GetInstantiatedType(x.First, types));
            
            return new InstantiatedType
            {
                Type = function,
                TypeArguments = instantiatedTypeArguments
            };
        }
        
        if (!variables.TryGetValue(variableName, out var value))
        {
            throw new InvalidOperationException($"No symbol found with name {variableName}");
        }

        if (!value.Instantiated)
        {
            throw new InvalidOperationException($"{value.Name} is not instantiated");
        }

        return value.Type;
    }

    private record Variable(string Name, InstantiatedType Type, bool Instantiated);

    private static string GetIdentifierName(Token token)
    {
        return token is StringToken { Type: TokenType.Identifier } stringToken
            ? stringToken.StringValue
            : throw new InvalidOperationException("Expected token name");
    }

    private static InstantiatedType TypeCheckVariableDeclaration(
        VariableDeclarationExpression expression,
        Dictionary<string, TypeDefinition> resolvedTypes,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionTypeDefinition> functions,
        InstantiatedType? expectedReturnType)
    {
        var varName = expression.VariableDeclaration.VariableNameToken.StringValue;
        if (variables.ContainsKey(varName))
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
                var valueType = TypeCheckExpression(value, resolvedTypes, variables, functions, expectedReturnType);
                if (type is not null)
                {
                    var expectedType = GetInstantiatedType(type, resolvedTypes);
                    if (expectedType != valueType)
                    {
                        throw new InvalidOperationException($"Expected type {expectedType}, but found {valueType}");
                    }
                }

                variables[varName] = new Variable(varName, valueType, Instantiated: true);

                break;
            }
            case { Value: null, Type: { } type }:
            {
                var langType = GetInstantiatedType(type, resolvedTypes);
                variables[varName] = new Variable(varName, langType, Instantiated: false);

                break;
            }
        }
        
        // variable declaration return type is always unit, regardless of the variable type
        return InstantiatedType.Unit;
    }

    private static InstantiatedType GetInstantiatedType(TypeIdentifier typeIdentifier, Dictionary<string, TypeDefinition> resolvedTypes)
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
                GetInstantiatedType(typeIdentifier.TypeArguments[0], resolvedTypes),
                GetInstantiatedType(typeIdentifier.TypeArguments[1], resolvedTypes));
        }

        if (typeIdentifier.Identifier is StringToken { Type: TokenType.Identifier } stringToken
            && resolvedTypes.TryGetValue(stringToken.StringValue, out var nameMatchingType))
        {
            if (!nameMatchingType.IsGeneric && typeIdentifier.TypeArguments.Count == 0)
            {
                return new InstantiatedType
                    { Type = nameMatchingType, TypeArguments = new Dictionary<string, InstantiatedType>() };
            }

            if (nameMatchingType.GenericParameters.Count != typeIdentifier.TypeArguments.Count)
            {
                throw new InvalidOperationException($"Expected {nameMatchingType.GenericParameters.Count} type arguments, but found {typeIdentifier.TypeArguments.Count}");
            }

            return new InstantiatedType
            {
                Type = nameMatchingType,
                TypeArguments = typeIdentifier.TypeArguments
                    .Zip(nameMatchingType.GenericParameters)
                    .ToDictionary(x => x.Second, x => GetInstantiatedType(x.First, resolvedTypes))
            };
        }

        throw new InvalidOperationException($"No type found {typeIdentifier}");
    }

    public class InstantiatedType : IEquatable<InstantiatedType>
    {
        public static InstantiatedType String { get; } = new() { Type = TypeDefinition.String, TypeArguments = new Dictionary<string, InstantiatedType>()};
        public static InstantiatedType Boolean { get; } = new() { Type = TypeDefinition.Boolean, TypeArguments = new Dictionary<string, InstantiatedType>()};
        
        public static InstantiatedType Int { get; } = new() { Type = TypeDefinition.Int, TypeArguments = new Dictionary<string, InstantiatedType>()};

        public static InstantiatedType Unit { get; } = new() { Type = TypeDefinition.Unit, TypeArguments = new Dictionary<string, InstantiatedType>()};
        
        public static InstantiatedType Never { get; } = new() {Type = TypeDefinition.Never, TypeArguments = new Dictionary<string, InstantiatedType>() };

        public static InstantiatedType Result(InstantiatedType value, InstantiatedType error) =>
            new()
            {
                Type = TypeDefinition.Result,
                TypeArguments = new Dictionary<string, InstantiatedType>
                {
                    {"TValue", value},
                    {"TError", error}
                },
            };

        public static bool operator ==(InstantiatedType? left, InstantiatedType? right)
        {
            return left?.Equals(right) ?? right is null;
        }

        public static bool operator !=(InstantiatedType? left, InstantiatedType? right)
        {
            return !(left == right);
        }
        
        public required TypeDefinition Type { get; init; }
        
        // todo: be consistent with argument/parameter
        public required IReadOnlyDictionary<string, InstantiatedType> TypeArguments { get; init; }
        
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

            return Type.Equals(other.Type)
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
            var hashCode = Type.GetHashCode();

            return TypeArguments.Aggregate(hashCode, (current, kvp) => HashCode.Combine(current, kvp.Key.GetHashCode(), kvp.Value.GetHashCode()));
        }

        public override string ToString()
        {
            var sb = new StringBuilder($"{Type.Name}");
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
        public static TypeDefinition Unit { get; } = new() { GenericParameters = [], Name = "Unit", Fields = [], StaticFields = []};
        public static TypeDefinition String { get; } = new() { GenericParameters = [], Name = "String", Fields = [], StaticFields = [] };
        public static TypeDefinition Int { get; } = new() { GenericParameters = [], Name = "Int", Fields = [], StaticFields = []};
        public static TypeDefinition Boolean { get; } = new() { GenericParameters = [], Name = "Boolean", Fields = [], StaticFields = []};
        public static TypeDefinition Never { get; } = new() { GenericParameters = [], Name = "!", Fields = [], StaticFields = []};
        
        // todo: unions
        public static TypeDefinition Result { get; } = new() { GenericParameters = ["TValue", "TError"], Name = "Result", Fields = [], StaticFields = []};
        public static IEnumerable<TypeDefinition> BuiltInTypes { get; } = [Unit, String, Int, Never, Result, Boolean];
        
        public required IReadOnlyList<string> GenericParameters { get; init; }
        public bool IsGeneric => GenericParameters.Count > 0;
        public required string Name { get; init; }
        public required IReadOnlyList<TypeField> Fields { get; init; }
        public required IReadOnlyList<TypeField> StaticFields { get; init; }
    }

    public class TypeField
    {
        public required InstantiatedType Type { get; init; }
        public required string Name { get; init; }
        public required bool IsPublic { get; init; }
        public required bool IsMutable { get; init; }
    }
    
    public class FunctionTypeDefinition : TypeDefinition
    {
        public required IReadOnlyList<Parameter> Parameters { get; init; }
        
        // todo: figure this out. This both the class the fn is in and the fn itself could be generic
        public required InstantiatedType ReturnType { get; init; }

        public record Parameter(string Name, InstantiatedType Type);
    }
}