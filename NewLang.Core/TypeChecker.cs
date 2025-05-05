namespace NewLang.Core;

public class TypeChecker
{
    public static void TypeCheck(LangProgram program)
    {
        var types = new List<LangType>
        {
            LangType.Unit
        };

        foreach (var @class in program.Classes)
        {
            types.Add(new LangType
            {
                Name = GetIdentifierName(@class.Name),
                GenericParameters = @class.TypeArguments.Select(GetIdentifierName).ToArray()
            });
        }
        
        foreach (var expression in program.Expressions)
        {
            TypeCheckExpression(expression);
        }
    }

    private static LangType TypeCheckExpression(IExpression expression)
    {
        return expression switch
        {
            VariableDeclarationExpression variableDeclarationExpression => TypeCheckVariableDeclaration(
                variableDeclarationExpression),
            _ => throw new NotImplementedException()
        };
    }

    private static string GetIdentifierName(Token token)
    {
        return token is StringToken { Type: TokenType.Identifier } stringToken
            ? stringToken.StringValue
            : throw new InvalidOperationException("Expected token name");
    }

    private static LangType TypeCheckVariableDeclaration(VariableDeclarationExpression expression)
    {
        return LangType.Unit;
    }
    
    public class LangType
    {
        public required IReadOnlyList<string> GenericParameters { get; init; }
        public bool IsGeneric => GenericParameters.Count > 0;
        public required string Name { get; init; }

        public static LangType Unit { get; } = new() { GenericParameters = [], Name = "Unit" };
    }
}