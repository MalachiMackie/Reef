using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NewLang.Core;

// todo: try pool allocations 
public class Tokenizer
{
    private static readonly SearchValues<char> Digits = SearchValues.Create("0123456789");

    public IEnumerable<Token> Tokenize(string sourceStr)
    {
        if (sourceStr.Length == 0)
        {
            yield break;
        }

        var position = new SourcePosition { Start = 0, LineNumber = 0, LinePosition = 0 };
        var endIndex = sourceStr.Length - 1;
        var nextToken = EatToken(sourceStr.AsSpan()[(int)position.Start..(endIndex + 1)], position);
        while (nextToken.HasValue)
        {
            var (token, nextPosition) = nextToken.Value;
            position = nextPosition;
            yield return token;

            var nextSource = sourceStr.AsSpan()[(int)position.Start..(endIndex + 1)];
            if (nextSource.IsEmpty)
            {
                break;
            }
            
            nextToken = EatToken(nextSource, position);
        }
    }

    private readonly List<int> _notTokens = [];
    
    private (Token Token, SourcePosition NextPosition)? EatToken(
        ReadOnlySpan<char> source,
        in SourcePosition startPosition)
    {
        var sourceTrimmed = source.TrimStart();
        if (sourceTrimmed.Length == 0)
        {
            return null;
        }

        Span<TokenType?> potentialTokens = stackalloc TokenType?[MaxPotentialTokenTypes];
        var potentialTokensCount = GetPotentiallyValidTokenTypes(sourceTrimmed[0], ref potentialTokens);
        if (potentialTokensCount == 0)
        {
            return null;
        }
        
        for (uint i = 1; i < source.Length; i++)
        {
            var part = source[..((int)i + 1)];
            var trimmed = part.TrimStart();
            // 1 because first char is checked above
            if (trimmed.Length <= 1)
            {
                continue;
            }
            
            var trimmedCharacters = part[..^trimmed.Length];

            var position = GetNextPosition(startPosition, trimmedCharacters);
            
            _notTokens.Clear();
            for (var j = 0; j < potentialTokens.Length; j++)
            {
                var type = potentialTokens[j];
                if (type.HasValue && !IsPotentiallyValid(type.Value, trimmed))
                {
                    _notTokens.Add(j);
                }
            }
            
            if (_notTokens.Count == potentialTokensCount)
            {
                // previously we had at least one potential token, now we have none. need to resolve to one.
                // this will happen in the case of `int)`
                var tokenSource = trimmed[..^1];
                var token = ResolveToken(tokenSource, potentialTokens, position);
                
                // this assumes that a token can't cross the new line boundary
                var nextPosition = position with
                {
                    Start = position.Start + (uint)tokenSource.Length,
                    LinePosition = position.LinePosition + (uint)tokenSource.Length
                };
                return (token, nextPosition);
            }

            foreach (var index in _notTokens)
            {
                potentialTokensCount--;
                potentialTokens[index] = null;
            }
        }

        var allNull = true;
        foreach (var token in potentialTokens)
        {
            if (token.HasValue)
            {
                allNull = false;
                break;
            }
        }
        if (allNull)
        {
            return null;
        }

        var outerTrimmedChars = source[..^sourceTrimmed.Length];

        var trimmedPosition = GetNextPosition(startPosition, outerTrimmedChars);

        return (ResolveToken(sourceTrimmed, potentialTokens, trimmedPosition),
            startPosition with { Start = startPosition.Start + (uint)source.Length });
    }

    private static SourcePosition GetNextPosition(in SourcePosition start, in ReadOnlySpan<char> trimmedCharacters)
    {
        var outerLinePosition = start.LinePosition;
        var outerNewLines = 0;
        var lastNewLineIndex = -1;
        for (var i = 0; i < trimmedCharacters.Length; i++)
        {
            if (trimmedCharacters[i] == '\n')
            {
                outerNewLines++;
                lastNewLineIndex = i;
            }
        }
        if (outerNewLines > 0)
        {
            if (lastNewLineIndex == trimmedCharacters.Length - 1)
            {
                // the last trimmed character was a new line, so the start of `part` is the beginning of the line
                outerLinePosition = 0;
            }
            else
            {
                // we passed over a new line, but there are more whitespace characters after the new line
                outerLinePosition = (uint)(trimmedCharacters.Length - (lastNewLineIndex + 1));
            }
        }
        else
        {
            // didn't pass over a new line, so just increment the line position by the number of whitespace chars we trimmed
            outerLinePosition += (uint)trimmedCharacters.Length;
        }

        return new SourcePosition(
            start.Start + (uint)trimmedCharacters.Length,
            start.LineNumber + (uint)outerNewLines,
            outerLinePosition);
    }

    private static Token ResolveToken(
        ReadOnlySpan<char> source,
        Span<TokenType?> tokenTypes,
        SourcePosition position)
    {
        var allNull = true;
        foreach (var tokenType in tokenTypes)
        {
            if (tokenType.HasValue)
            {
                allNull = false;
            }
            else
            {
                continue;
            }
            
            // skip identifier so that keywords take precedent
            if (tokenType == TokenType.Identifier)
            {
                continue;
            }

            if (TryResolveToken(source, tokenType.Value, position, out var token))
            {
                return token.Value;
            }
        }

        if (allNull)
        {
            throw new UnreachableException();
        }

        if (!TryResolveToken(source, TokenType.Identifier, position, out var identifierToken))
        {
            throw new InvalidOperationException($"invalid token \"{source}\"");
        }

        return identifierToken.Value;
    }

    private static bool TryResolveToken(
        ReadOnlySpan<char> source,
        TokenType type,
        SourcePosition position,
        [NotNullWhen(true)] out Token? token)
    {
        token = type switch
        {
            TokenType.Identifier when source.Length > 0 && !source.ContainsAnyExcept(ValidIdentifierTokens) && !char.IsDigit(source[0]) => Token.Identifier(source.ToString(), new SourceSpan(position, (uint)source.Length)),
            TokenType.If when source is "if" => Token.If(new SourceSpan(position, (uint)source.Length)),
            TokenType.Mut when source is "mut" => Token.Mut(new SourceSpan(position, (uint)source.Length)),
            TokenType.DoubleColon when source is "::" => Token.DoubleColon(new SourceSpan(position, (uint)source.Length)),
            TokenType.Class when source is "class" => Token.Class(new SourceSpan(position, (uint)source.Length)),
            TokenType.Field when source is "field" => Token.Field(new SourceSpan(position, (uint)source.Length)),
            TokenType.LeftParenthesis when source is "(" => Token.LeftParenthesis(new SourceSpan(position,
                (uint)source.Length)),
            TokenType.RightParenthesis when source is ")" => Token.RightParenthesis(new SourceSpan(position,
                (uint)source.Length)),
            TokenType.Semicolon when source is ";" => Token.Semicolon(new SourceSpan(position, (uint)source.Length)),
            TokenType.LeftBrace when source is "{" => Token.LeftBrace(new SourceSpan(position, (uint)source.Length)),
            TokenType.RightBrace when source is "}" => Token.RightBrace(new SourceSpan(position, (uint)source.Length)),
            TokenType.Pub when source is "pub" => Token.Pub(new SourceSpan(position, (uint)source.Length)),
            TokenType.New when source is "new" => Token.New(new SourceSpan(position, (uint)source.Length)),
            TokenType.Fn when source is "fn" => Token.Fn(new SourceSpan(position, (uint)source.Length)),
            TokenType.IntKeyword when source is "int" =>
                Token.IntKeyword(new SourceSpan(position, (uint)source.Length)),
            TokenType.Colon when source is ":" => Token.Colon(new SourceSpan(position, (uint)source.Length)),
            TokenType.LeftAngleBracket when source is "<" => Token.LeftAngleBracket(new SourceSpan(position,
                (uint)source.Length)),
            TokenType.RightAngleBracket when source is ">" => Token.RightAngleBracket(new SourceSpan(position,
                (uint)source.Length)),
            TokenType.Var when source is "var" => Token.Var(new SourceSpan(position, (uint)source.Length)),
            TokenType.Equals when source is "=" => Token.Equals(new SourceSpan(position, (uint)source.Length)),
            TokenType.Comma when source is "," => Token.Comma(new SourceSpan(position, (uint)source.Length)),
            TokenType.DoubleEquals when source is "==" => Token.DoubleEquals(new SourceSpan(position,
                (uint)source.Length)),
            TokenType.Turbofish when source is "::<" => Token.Turbofish(new SourceSpan(position,
                (uint)source.Length)),
            TokenType.Else when source is "else" => Token.Else(new SourceSpan(position, (uint)source.Length)),
            TokenType.IntLiteral when int.TryParse(source, out var intValue) => Token.IntLiteral(intValue,
                new SourceSpan(position, (uint)source.Length)),
            TokenType.StringLiteral when source[0] == '"'
                                         && source[^1] == '"'
                                         && source[1..].IndexOf('"') == source.Length - 2 => Token.StringLiteral(
                source[1..^1].ToString(), new SourceSpan(position, (uint)source.Length)),
            TokenType.StringKeyword when source is "string" => Token.StringKeyword(new SourceSpan(position,
                (uint)source.Length)),
            TokenType.Result when source is "result" => Token.Result(new SourceSpan(position, (uint)source.Length)),
            TokenType.Ok when source is "ok" => Token.Ok(new SourceSpan(position, (uint)source.Length)),
            TokenType.Error when source is "error" => Token.Error(new SourceSpan(position, (uint)source.Length)),
            TokenType.QuestionMark when source is "?" => Token.QuestionMark(new SourceSpan(position,
                (uint)source.Length)),
            TokenType.Return when source is "return" => Token.Return(new SourceSpan(position, (uint)source.Length)),
            TokenType.True when source is "true" => Token.True(new SourceSpan(position, (uint)source.Length)),
            TokenType.False when source is "false" => Token.False(new SourceSpan(position, (uint)source.Length)),
            TokenType.Bool when source is "bool" => Token.Bool(new SourceSpan(position, (uint)source.Length)),
            TokenType.ForwardSlash when source is "/" => Token.ForwardSlash(new SourceSpan(position, (uint)source.Length)),
            TokenType.Star when source is "*" => Token.Star(new SourceSpan(position, (uint)source.Length)),
            TokenType.Plus when source is "+" => Token.Plus(new SourceSpan(position, (uint)source.Length)),
            TokenType.Dash when source is "-" => Token.Dash(new SourceSpan(position, (uint)source.Length)),
            TokenType.Dot when source is "." => Token.Dot(new SourceSpan(position, (uint)source.Length)),
            _ => null
        };

        return token.HasValue;
    }

    private static readonly SearchValues<char> ValidIdentifierTokens =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_");

    // maximum potential token types at the same time
    private const int MaxPotentialTokenTypes = 4;
    private static int GetPotentiallyValidTokenTypes(char firstChar, ref Span<TokenType?> tokens)
    {
        var i = 0;
        switch (firstChar)
        {
            case 'i':
                tokens[i++] = TokenType.IntKeyword;
                tokens[i++] = TokenType.If;
                tokens[i++] = TokenType.Identifier;
                break;
            case 'm':
                tokens[i++] = TokenType.Mut;
                tokens[i++] = TokenType.Identifier;
                break;
            case '(':
                tokens[i++] = TokenType.LeftParenthesis;
                break;
            case ')':
                tokens[i++] = TokenType.RightParenthesis;
                break;
            case ';':
                tokens[i++] = TokenType.Semicolon;
                break;
            case '{':
                tokens[i++] = TokenType.LeftBrace;
                break;
            case '}':
                tokens[i++] = TokenType.RightBrace;
                break;
            case 'p':
                tokens[i++] = TokenType.Pub;
                tokens[i++] = TokenType.Identifier;
                break;
            case 'f':
                tokens[i++] = TokenType.False;
                tokens[i++] = TokenType.Fn;
                tokens[i++] = TokenType.Field;
                tokens[i++] = TokenType.Identifier;
                break;
            case ':':
                tokens[i++] = TokenType.Colon;
                tokens[i++] = TokenType.Turbofish;
                tokens[i++] = TokenType.DoubleColon;
                break;
            case '<':
                tokens[i++] = TokenType.LeftAngleBracket;
                break;
            case '>':
                tokens[i++] = TokenType.RightAngleBracket;
                break;
            case 'n':
                tokens[i++] = TokenType.New;
                tokens[i++] = TokenType.Identifier;
                break;
            case 'v':
                tokens[i++] = TokenType.Var;
                tokens[i++] = TokenType.Identifier;
                break;
            case '=':
                tokens[i++] = TokenType.Equals;
                tokens[i++] = TokenType.DoubleEquals;
                break;
            case ',':
                tokens[i++] = TokenType.Comma;
                break;
            case 'e':
                tokens[i++] = TokenType.Else;
                tokens[i++] = TokenType.Error;
                tokens[i++] = TokenType.Identifier;
                break;
            case 's':
                tokens[i++] = TokenType.StringKeyword;
                tokens[i++] = TokenType.Identifier;
                break;
            case 'r':
                tokens[i++] = TokenType.Return;
                tokens[i++] = TokenType.Result;
                tokens[i++] = TokenType.Identifier;
                break;
            case 'b':
                tokens[i++] = TokenType.Bool;
                tokens[i++] = TokenType.Identifier;
                break;
            case 'o':
                tokens[i++] = TokenType.Ok;
                tokens[i++] = TokenType.Identifier;
                break;
            case '?':
                tokens[i++] = TokenType.QuestionMark;
                break;
            case 't':
                tokens[i++] = TokenType.True;
                tokens[i++] = TokenType.Identifier;
                break;
            case '*':
                tokens[i++] = TokenType.Star;
                break;
            case '/':
                tokens[i++] = TokenType.ForwardSlash;
                break;
            case '+':
                tokens[i++] = TokenType.Plus;
                break;
            case '-':
                tokens[i++] = TokenType.Dash;
                break;
            case 'c':
                tokens[i++] = TokenType.Class;
                tokens[i++] = TokenType.Identifier;
                break;
            case '1' or '2' or '3' or '4' or '5' or '6' or '7' or '8' or '9':
                tokens[i++] = TokenType.IntLiteral;
                break;
            case '"':
                tokens[i++] = TokenType.StringLiteral;
                break;
            case '.':
                tokens[i++] = TokenType.Dot;
                break;
            default:
            {
                if (ValidIdentifierTokens.Contains(firstChar))
                {
                    tokens[i++] = TokenType.Identifier;
                }

                break;
            }
        }
        
        return i;
    }
    
    private static bool IsPotentiallyValid(TokenType type, ReadOnlySpan<char> source)
    {
        return type switch
        {
            TokenType.Identifier => !source.ContainsAnyExcept(ValidIdentifierTokens) && !char.IsDigit(source[0]),
            TokenType.If => "if".AsSpan().StartsWith(source) && source.Length <= "if".Length,
            TokenType.LeftParenthesis => source is "(",
            TokenType.RightParenthesis => source is ")",
            TokenType.Semicolon => source is ";",
            TokenType.LeftBrace => source is "{",
            TokenType.Mut => "mut".AsSpan().StartsWith(source) && source.Length <= "mut".Length,
            TokenType.New => "new".AsSpan().StartsWith(source) && source.Length <= "new".Length,
            TokenType.Class => "class".AsSpan().StartsWith(source) && source.Length <= "class".Length,
            TokenType.RightBrace => source is "}",
            TokenType.Pub => "pub".AsSpan().StartsWith(source) && source.Length <= "pub".Length,
            TokenType.Fn => "fn".AsSpan().StartsWith(source) && source.Length <= "fn".Length,
            TokenType.Field => "field".AsSpan().StartsWith(source) && source.Length <= "field".Length,
            TokenType.IntKeyword => "int".AsSpan().StartsWith(source) && source.Length <= "int".Length,
            TokenType.Turbofish => "::<".AsSpan().StartsWith(source) && source.Length <= "::<".Length,
            TokenType.Colon => source is ":",
            TokenType.LeftAngleBracket => source is "<",
            TokenType.RightAngleBracket => source is ">",
            TokenType.Var => "var".AsSpan().StartsWith(source) && source.Length <= "var".Length,
            TokenType.Equals => source is "=",
            TokenType.Comma => source is ",",
            TokenType.DoubleEquals => "==".AsSpan().StartsWith(source) && source.Length <= "==".Length,
            TokenType.Else => "else".AsSpan().StartsWith(source) && source.Length <= "else".Length,
            TokenType.IntLiteral => !source.ContainsAnyExcept(Digits),
            TokenType.StringLiteral => MatchesStringLiteral(source),
            TokenType.StringKeyword => "string".AsSpan().StartsWith(source) && source.Length <= "string".Length,
            TokenType.Result => "result".AsSpan().StartsWith(source) && source.Length <= "result".Length,
            TokenType.Ok => "ok".AsSpan().StartsWith(source) && source.Length <= "ok".Length,
            TokenType.Error => "error".AsSpan().StartsWith(source) && source.Length <= "error".Length,
            TokenType.QuestionMark => source is "?",
            TokenType.Return => "return".AsSpan().StartsWith(source) && source.Length <= "return".Length,
            TokenType.True => "true".AsSpan().StartsWith(source) && source.Length <= "true".Length,
            TokenType.False => "false".AsSpan().StartsWith(source) && source.Length <= "false".Length,
            TokenType.Bool => "bool".AsSpan().StartsWith(source) && source.Length <= "bool".Length,
            TokenType.DoubleColon => "::".AsSpan().StartsWith(source) && source.Length <= "::".Length,
            TokenType.Star => source is "*",
            TokenType.ForwardSlash => source is "/",
            TokenType.Plus => source is "+",
            TokenType.Dash => source is "-",
            TokenType.Dot => source is ".",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        static bool MatchesStringLiteral(ReadOnlySpan<char> stringSource)
        {
            if (stringSource[0] != '"')
            {
                return false;
            }

            // single quote mark will be the beginning of a string literal
            if (stringSource.Length == 1)
            {
                return true;
            }

            var rest = stringSource[1..];

            var nextQuoteIndex = rest.IndexOf('"');

            // the next quote either doesn't exist, or is at the end of the source
            return nextQuoteIndex < 0 || nextQuoteIndex == rest.Length - 1;
        }
    }
}