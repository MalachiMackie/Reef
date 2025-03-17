using System.Buffers;
using System.Reflection.Metadata.Ecma335;

namespace NewLang.Core;

public struct Token
{
    public required TokenType Type;
    
    // todo: can we 'overlap' these fields like an rust enum would?

    public string? StringValue;
    public int? IntValue;
    
    // todo: should probably keep index positions

    public static Token Pub() => new() { Type = TokenType.Pub };
    public static Token Fn() => new() { Type = TokenType.Fn };
    public static Token IntKeyword() => new() { Type = TokenType.IntKeyword };
    public static Token LeftParenthesis() => new() { Type = TokenType.LeftParenthesis };
    public static Token RightParenthesis() => new() { Type = TokenType.RightParenthesis };
    public static Token LeftBrace() => new() { Type = TokenType.LeftBrace };
    public static Token RightBrace() => new() { Type = TokenType.RightBrace };
    public static Token Colon() => new() { Type = TokenType.Colon };
    public static Token Semicolon() => new() { Type = TokenType.Semicolon };
    public static Token LeftAngleBracket() => new() { Type = TokenType.LeftAngleBracket };
    public static Token RightAngleBracket() => new() { Type = TokenType.RightAngleBracket };
    public static Token Comma() => new() { Type = TokenType.Comma };
    public static Token Equals() => new() { Type = TokenType.Equals };
    public static Token Var() => new() { Type = TokenType.Var };
    public static Token Identifier(string value) => new() { StringValue = value, Type = TokenType.Identifier };
    public static Token DoubleEquals() => new() { Type = TokenType.DoubleEquals };
    public static Token Else() => new() { Type = TokenType.Else };
    public static Token If() => new() { Type = TokenType.If };
    public static Token StringKeyword() => new() { Type = TokenType.StringKeyword };
    
    public static Token StringLiteral(string value) => new() { StringValue = value, Type = TokenType.StringLiteral };
    public static Token IntLiteral(int value) => new() { IntValue = value, Type = TokenType.IntLiteral };
    public static Token Result() => new() { Type = TokenType.Result };
    public static Token Ok() => new() { Type = TokenType.Ok };
    public static Token Error() => new() { Type = TokenType.Error };
    public static Token QuestionMark() => new() { Type = TokenType.QuestionMark };
    public static Token Return() => new() { Type = TokenType.Return };
}

public class Parser
{
    private static readonly SearchValues<char> Digits = SearchValues.Create("0123456789");

    public IEnumerable<Token> Parse(string source)
    {
        var tokenStart = 0;
        var foundValue = false;
        for (var i = 0; i < source.Length; i++)
        {
            var inStringToken = source[tokenStart] == '"';
            // todo: this completely ignores escaping a quote mark
            if (inStringToken)
            {
                if (source[i] == '"' && i != tokenStart)
                {
                    yield return Token.StringLiteral(source[(tokenStart + 1)..i]);
                    foundValue = false;
                }

                continue;
            }
            
            var isTokenEnd = char.IsWhiteSpace(source[i]) || i == source.Length - 1;
            foundValue |= isTokenEnd;
            if (isTokenEnd)
            {
                var potentialToken = source.AsSpan()[tokenStart..(i + 1)];
                // next token
                yield return potentialToken switch
                {
                    "pub" => Token.Pub(),
                    "fn" => Token.Fn(),
                    "int" => Token.IntKeyword(),
                    "result" => Token.Result(),
                    ":" => Token.Colon(),
                    "string" => Token.StringKeyword(),
                    "<" => Token.LeftAngleBracket(),
                    ">" => Token.RightAngleBracket(),
                    "(" => Token.LeftParenthesis(),
                    ")" => Token.RightParenthesis(),
                    "{" => Token.LeftBrace(),
                    "}" => Token.RightBrace(),
                    ";" => Token.Semicolon(),
                    "," => Token.Comma(),
                    "ok" => Token.Ok(),
                    "error" => Token.Error(),
                    "?" => Token.QuestionMark(),
                    "return" => Token.Return(),
                    _ => OtherToken(potentialToken)
                };
                foundValue = false;
            }
        }
    }

    private static Token OtherToken(ReadOnlySpan<char> things)
    {
        if (things.ContainsAnyExcept(Digits))
        { 
            // todo: not everything can be an identifier
            return Token.Identifier(things.ToString());
        }

        if (int.TryParse(things, out var result))
        {
            return Token.IntLiteral(result);
        }

        throw new Exception("It failed");
    }
}