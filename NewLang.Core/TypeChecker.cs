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
        
        foreach (var fn in program.Functions)
        {
            TypeCheckFunctionDeclaration(fn, types);
        }

        var variables = new Dictionary<string, Variable>();

        foreach (var expression in program.Expressions)
        {
            TypeCheckExpression(expression, types, variables, expectedReturnType: null);
        }
    }

    private static void TypeCheckFunctionDeclaration(LangFunction function, Dictionary<string, LangType> types)
    {
        TypeCheckBlock(function.Block, types, function.ReturnType is null ? null : GetType(function.ReturnType, types));
    }

    private static void TypeCheckBlock(Block block, Dictionary<string, LangType> types, LangType? expectedReturnType)
    {
        var variables = new Dictionary<string, Variable>();
        
        foreach (var fn in block.Functions)
        {
            TypeCheckFunctionDeclaration(fn, types);
        }

        foreach (var expression in block.Expressions)
        {
            TypeCheckExpression(expression, types, variables, expectedReturnType);
        }
    }

    private static LangType TypeCheckExpression(
        IExpression expression,
        Dictionary<string, LangType> types,
        Dictionary<string, Variable> variables,
        LangType? expectedReturnType)
    {
        return expression switch
        {
            VariableDeclarationExpression variableDeclarationExpression => TypeCheckVariableDeclaration(
                variableDeclarationExpression, types, variables, expectedReturnType),
            ValueAccessorExpression valueAccessorExpression => TypeCheckValueAccessor(valueAccessorExpression, variables),
            MethodReturnExpression methodReturnExpression => TypeCheckMethodReturn(methodReturnExpression, types, variables, expectedReturnType),
            _ => throw new NotImplementedException($"{expression.ExpressionType}")
        };
    }

    private static LangType TypeCheckMethodReturn(
        MethodReturnExpression methodReturnExpression,
        Dictionary<string, LangType> types,
        Dictionary<string, Variable> variables,
        LangType? expectedReturnType)
    {
        var returnExpressionType = methodReturnExpression.MethodReturn.Expression is null
            ? null
            : TypeCheckExpression(methodReturnExpression.MethodReturn.Expression, types, variables, expectedReturnType);
        
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

    private static LangType TypeCheckValueAccessor(ValueAccessorExpression valueAccessorExpression, Dictionary<string, Variable> variables)
    {
        return valueAccessorExpression.ValueAccessor switch
        {
            {AccessType: ValueAccessType.Literal, Token: IntToken {Type: TokenType.IntLiteral}} => LangType.Int,
            {AccessType: ValueAccessType.Literal, Token: StringToken {Type: TokenType.StringLiteral}} => LangType.String,
            {AccessType: ValueAccessType.Variable, Token: StringToken {Type: TokenType.Identifier, StringValue: var variableName}} =>
                TypeCheckVariableAccess(variableName, variables),
            _ => throw new NotImplementedException($"{valueAccessorExpression}")
        };
        
    }

    private static LangType TypeCheckVariableAccess(string variableName, Dictionary<string, Variable> variables)
    {
        if (!variables.TryGetValue(variableName, out var value))
        {
            throw new InvalidOperationException("No variable found");
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
                var valueType = TypeCheckExpression(value, resolvedTypes, variables, expectedReturnType);
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
}