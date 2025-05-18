namespace NewLang.Core;

public class TypeChecker
{
    public static void TypeCheck(LangProgram program)
    {
        var types = LangType.BuiltInTypes.ToDictionary(x => x.Name);

        foreach (var @class in program.Classes)
        {
            var name = GetIdentifierName(@class.Name);
            types.Add(name, new LangType
            {
                Name = name,
                GenericParameters = @class.TypeArguments.Select(GetIdentifierName).ToArray()
            });
        }

        var functionTypes = new Dictionary<string, FunctionType>();
        
        foreach (var fn in program.Functions)
        {
            functionTypes[fn.Name.StringValue] = TypeCheckFunctionSignature(fn, functionTypes, types);
        }

        foreach (var fn in program.Functions)
        {
            TypeCheckFunctionBody(fn, functionTypes, types);
        }

        var variables = new Dictionary<string, Variable>();

        foreach (var expression in program.Expressions)
        {
            TypeCheckExpression(expression, types, variables, functionTypes, expectedReturnType: null);
        }
    }

    private static void TypeCheckFunctionBody(LangFunction function,
        Dictionary<string, FunctionType> functions,
        Dictionary<string, LangType> types)
    {
        TypeCheckBlock(function.Block, types, functions, function.ReturnType is null ? null : GetType(function.ReturnType, types));
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private static FunctionType TypeCheckFunctionSignature(LangFunction function, Dictionary<string, FunctionType> functions, Dictionary<string, LangType> types)
    {
        if (functions.ContainsKey(function.Name.StringValue))
        {
            throw new InvalidOperationException($"Function with name {function.Name.StringValue} already defined");
        }
        
        return new FunctionType
        {
            Name = function.Name.StringValue,
            ReturnType = function.ReturnType is null ? LangType.Unit : GetType(function.ReturnType, types),
            GenericParameters = function.TypeArguments.Select(x => x.StringValue).ToArray(),
            Parameters = function.Parameters.Select(x => GetType(x.Type, types)).ToArray()
        };
    }

    private static void TypeCheckBlock(Block block, Dictionary<string, LangType> types, Dictionary<string, FunctionType> functions, LangType? expectedReturnType)
    {
        var variables = new Dictionary<string, Variable>();
        var innerFunctions = new Dictionary<string, FunctionType>(functions);
        
        foreach (var fn in block.Functions)
        {
            innerFunctions[fn.Name.StringValue] = TypeCheckFunctionSignature(fn, innerFunctions, types);
        }
        
        foreach (var fn in block.Functions)
        {
            TypeCheckFunctionBody(fn, innerFunctions, types);
        }

        foreach (var expression in block.Expressions)
        {
            TypeCheckExpression(expression, types, variables, innerFunctions, expectedReturnType);
        }
    }

    private static LangType TypeCheckExpression(
        IExpression expression,
        Dictionary<string, LangType> types,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionType> functions,
        LangType? expectedReturnType)
    {
        return expression switch
        {
            VariableDeclarationExpression variableDeclarationExpression => TypeCheckVariableDeclaration(
                variableDeclarationExpression, types, variables, functions, expectedReturnType),
            ValueAccessorExpression valueAccessorExpression => TypeCheckValueAccessor(valueAccessorExpression, variables, functions),
            MethodReturnExpression methodReturnExpression => TypeCheckMethodReturn(methodReturnExpression, types, variables, functions, expectedReturnType),
            MethodCallExpression methodCallExpression => TypeCheckMethodCall(methodCallExpression.MethodCall, types, variables, functions, expectedReturnType),
            _ => throw new NotImplementedException($"{expression.ExpressionType}")
        };
    }

    private static LangType TypeCheckMethodCall(
        MethodCall methodCall,
        Dictionary<string, LangType> types,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionType> functions,
        LangType? expectedReturnType)
    {
        var methodType = TypeCheckExpression(methodCall.Method, types, variables, functions, expectedReturnType);

        if (methodType is not FunctionType functionType)
        {
            throw new InvalidOperationException($"{methodType} is not callable");
        }

        if (methodCall.ParameterList.Count != functionType.Parameters.Count)
        {
            throw new InvalidOperationException($"Expected {functionType.Parameters.Count} parameters, got {methodCall.ParameterList.Count}");
        }

        for (var i = 0; i < functionType.Parameters.Count; i++)
        {
            var expectedParameterType = functionType.Parameters[i];
            var givenParameterType = TypeCheckExpression(methodCall.ParameterList[i], types, variables, functions, expectedParameterType);

            if (expectedParameterType != givenParameterType)
            {
                throw new InvalidOperationException(
                    $"Expected parameter type {expectedParameterType}, got {givenParameterType}");
            }
        }

        return functionType.ReturnType;
    }

    private static LangType TypeCheckMethodReturn(
        MethodReturnExpression methodReturnExpression,
        Dictionary<string, LangType> types,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionType> functions,
        LangType? expectedReturnType)
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
        
        return LangType.Never;
    }

    private static LangType TypeCheckValueAccessor(ValueAccessorExpression valueAccessorExpression, Dictionary<string, Variable> variables, Dictionary<string, FunctionType> functions)
    {
        return valueAccessorExpression.ValueAccessor switch
        {
            {AccessType: ValueAccessType.Literal, Token: IntToken {Type: TokenType.IntLiteral}} => LangType.Int,
            {AccessType: ValueAccessType.Literal, Token: StringToken {Type: TokenType.StringLiteral}} => LangType.String,
            {AccessType: ValueAccessType.Variable, Token: StringToken {Type: TokenType.Identifier, StringValue: var variableName}} =>
                TypeCheckVariableAccess(variableName, variables, functions),
            _ => throw new NotImplementedException($"{valueAccessorExpression}")
        };
        
    }

    private static LangType TypeCheckVariableAccess(string variableName, Dictionary<string, Variable> variables, Dictionary<string, FunctionType> functions)
    {
        if (functions.TryGetValue(variableName, out var function))
        {
            return function;
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

    private record Variable(string Name, LangType Type, bool Instantiated);

    private static string GetIdentifierName(Token token)
    {
        return token is StringToken { Type: TokenType.Identifier } stringToken
            ? stringToken.StringValue
            : throw new InvalidOperationException("Expected token name");
    }

    private static LangType TypeCheckVariableDeclaration(
        VariableDeclarationExpression expression,
        Dictionary<string, LangType> resolvedTypes,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionType> functions,
        LangType? expectedReturnType)
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
                    var expectedType = GetType(type, resolvedTypes);
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
                var langType = GetType(type, resolvedTypes);
                variables[varName] = new Variable(varName, langType, Instantiated: false);

                break;
            }
        }
        
        // variable declaration return type is always unit, regardless of the variable type
        return LangType.Unit;
    }

    private static LangType GetType(TypeIdentifier typeIdentifier, Dictionary<string, LangType> resolvedTypes)
    {
        if (typeIdentifier.Identifier.Type == TokenType.StringKeyword)
        {
            return LangType.String;
        }

        if (typeIdentifier.Identifier.Type == TokenType.IntKeyword)
        {
            return LangType.Int;
        }

        if (typeIdentifier.Identifier is StringToken { Type: TokenType.Identifier } stringToken
            && resolvedTypes.TryGetValue(stringToken.StringValue, out var nameMatchingType))
        {
            if (!nameMatchingType.IsGeneric && typeIdentifier.TypeArguments.Count == 0)
            {
                return nameMatchingType;
            }
        }

        throw new InvalidOperationException($"No type found {typeIdentifier}");
    }
    
    public class LangType
    {
        public required IReadOnlyList<string> GenericParameters { get; init; }
        public bool IsGeneric => GenericParameters.Count > 0;
        public required string Name { get; init; }

        public static LangType Unit { get; } = new() { GenericParameters = [], Name = "Unit" };
        public static LangType String { get; } = new() { GenericParameters = [], Name = "String" };
        public static LangType Int { get; } = new() { GenericParameters = [], Name = "Int" };
        public static LangType Never { get; } = new() { GenericParameters = [], Name = "!" };
        public static IEnumerable<LangType> BuiltInTypes { get; } = [Unit, String, Int, Never];
    }

    public class FunctionType : LangType
    {
        public required IReadOnlyList<LangType> Parameters { get; init; }
        
        public required LangType ReturnType { get; init; }
    }
}