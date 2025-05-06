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

        foreach (var expression in program.Expressions)
        {
            TypeCheckExpression(expression, types, expectedReturnType: null);
        }
    }

    private static void TypeCheckFunctionDeclaration(LangFunction function, Dictionary<string, LangType> types)
    {
        TypeCheckBlock(function.Block, types, function.ReturnType is null ? null : GetType(function.ReturnType, types));
    }

    private static void TypeCheckBlock(Block block, Dictionary<string, LangType> types, LangType? expectedReturnType)
    {
        foreach (var fn in block.Functions)
        {
            TypeCheckFunctionDeclaration(fn, types);
        }

        foreach (var expression in block.Expressions)
        {
            TypeCheckExpression(expression, types, expectedReturnType);
        }
    }

    private static LangType TypeCheckExpression(IExpression expression, Dictionary<string, LangType> types, LangType? expectedReturnType)
    {
        return expression switch
        {
            VariableDeclarationExpression variableDeclarationExpression => TypeCheckVariableDeclaration(
                variableDeclarationExpression, types, expectedReturnType),
            ValueAccessorExpression valueAccessorExpression => TypeCheckValueAccessor(valueAccessorExpression),
            MethodReturnExpression methodReturnExpression => TypeCheckMethodReturn(methodReturnExpression, types, expectedReturnType),
            _ => throw new NotImplementedException($"{expression.ExpressionType}")
        };
    }

    private static LangType TypeCheckMethodReturn(MethodReturnExpression methodReturnExpression, Dictionary<string, LangType> types, LangType? expectedReturnType)
    {
        var returnExpressionType = methodReturnExpression.MethodReturn.Expression is null
            ? null
            : TypeCheckExpression(methodReturnExpression.MethodReturn.Expression, types, expectedReturnType);
        
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

    private static LangType TypeCheckValueAccessor(ValueAccessorExpression valueAccessorExpression)
    {
        return valueAccessorExpression.ValueAccessor switch
        {
            {AccessType: ValueAccessType.Literal, Token: IntToken {Type: TokenType.IntLiteral}} => LangType.Int,
            {AccessType: ValueAccessType.Literal, Token: StringToken {Type: TokenType.StringLiteral}} => LangType.String,
            _ => throw new NotImplementedException($"{valueAccessorExpression}")
        };
    }

    private static string GetIdentifierName(Token token)
    {
        return token is StringToken { Type: TokenType.Identifier } stringToken
            ? stringToken.StringValue
            : throw new InvalidOperationException("Expected token name");
    }

    private static LangType TypeCheckVariableDeclaration(VariableDeclarationExpression expression, Dictionary<string, LangType> resolvedTypes, LangType? expectedReturnType)
    {
        switch (expression.VariableDeclaration)
        {
            case {Value: null, Type: null}:
                throw new InvalidOperationException("Variable declaration must have a type specifier or a value");
            case { Value: { } value, Type: { } type } :
            {
                var expectedType = GetType(type, resolvedTypes);
                var valueType = TypeCheckExpression(value, resolvedTypes, expectedReturnType);
                if (expectedType != valueType)
                {
                    throw new InvalidOperationException($"Expected type {expectedType}, but found {valueType}");
                }

                break;
            }
        }
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