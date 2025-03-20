using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace NewLang.Core;

public class Parser
{
    private static readonly SearchValues<char> Digits = SearchValues.Create("0123456789");

    public IEnumerable<Token> Parse(string sourceStr)
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

            nextToken = EatToken(sourceStr.AsSpan()[(int)position.Start..(endIndex + 1)], position);
        }
    }

    private static (Token Token, SourcePosition NextPosition)? EatToken(
        ReadOnlySpan<char> source,
        SourcePosition startPosition)
    {
        if (source.IsWhiteSpace())
        {
            return null;
        }

        var potentialTokens = Enum.GetValues<TokenType>().ToList();

        for (uint i = 0; i < source.Length; i++)
        {
            var part = source[..((int)i + 1)];
            if (part.IsWhiteSpace())
            {
                continue;
            }

            var trimmed = part.TrimStart();
            var trimmedCharacters = part[..^trimmed.Length];

            var position = GetNextPosition(startPosition, trimmedCharacters);

            var nextPotentialTokens = new List<TokenType>();
            foreach (var type in potentialTokens)
            {
                if (IsPotentiallyValid(type, trimmed))
                {
                    nextPotentialTokens.Add(type);
                }
            }

            if (nextPotentialTokens.Count == 0)
            {
                // previously we had multiple potential tokens, now we have none. need to resolve to one.
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

            potentialTokens = nextPotentialTokens;
        }

        if (potentialTokens.Count == 0)
        {
            return null;
        }

        var outerTrimmed = source.TrimStart();
        var outerTrimmedChars = source[..^outerTrimmed.Length];

        var trimmedPosition = GetNextPosition(startPosition, outerTrimmedChars);

        return (ResolveToken(outerTrimmed, potentialTokens, trimmedPosition),
            startPosition with { Start = startPosition.Start + (uint)source.Length });
    }

    private static SourcePosition GetNextPosition(SourcePosition start, ReadOnlySpan<char> trimmedCharacters)
    {
        var outerLinePosition = start.LinePosition;
        var outerNewLines = trimmedCharacters.Count('\n');
        if (outerNewLines > 0)
        {
            var lastNewLineIndex = trimmedCharacters.LastIndexOf('\n');
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
        IEnumerable<TokenType> tokenTypes,
        SourcePosition position)
    {
        foreach (var tokenType in tokenTypes)
        {
            if (tokenType == TokenType.Identifier)
            {
                continue;
            }

            if (TryResolveToken(source, tokenType, position, out var token))
            {
                return token.Value;
            }
        }

        return Token.Identifier(source.ToString(), new SourceSpan(position, (uint)source.Length));
    }

    private static bool TryResolveToken(
        ReadOnlySpan<char> source,
        TokenType type,
        SourcePosition position,
        [NotNullWhen(true)] out Token? token)
    {
        token = type switch
        {
            TokenType.Identifier => Token.Identifier(source.ToString(), new SourceSpan(position, (uint)source.Length)),
            TokenType.If when source is "if" => Token.If(new SourceSpan(position, (uint)source.Length)),
            TokenType.LeftParenthesis when source is "(" => Token.LeftParenthesis(new SourceSpan(position,
                (uint)source.Length)),
            TokenType.RightParenthesis when source is ")" => Token.RightParenthesis(new SourceSpan(position,
                (uint)source.Length)),
            TokenType.Semicolon when source is ";" => Token.Semicolon(new SourceSpan(position, (uint)source.Length)),
            TokenType.LeftBrace when source is "{" => Token.LeftBrace(new SourceSpan(position, (uint)source.Length)),
            TokenType.RightBrace when source is "}" => Token.RightBrace(new SourceSpan(position, (uint)source.Length)),
            TokenType.Pub when source is "pub" => Token.Pub(new SourceSpan(position, (uint)source.Length)),
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
            _ => null
        };

        return token.HasValue;
    }

    private static readonly SearchValues<char> AlphaNumeric =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFHIJKLMNOPQRSTUVWXYZ0123456789");

    private static bool IsPotentiallyValid(TokenType type, ReadOnlySpan<char> source)
    {
        return type switch
        {
            TokenType.Identifier => !source.ContainsAnyExcept(AlphaNumeric),
            TokenType.If => "if".AsSpan().StartsWith(source) && source.Length <= "if".Length,
            TokenType.LeftParenthesis => source is "(",
            TokenType.RightParenthesis => source is ")",
            TokenType.Semicolon => source is ";",
            TokenType.LeftBrace => source is "{",
            TokenType.RightBrace => source is "}",
            TokenType.Pub => "pub".AsSpan().StartsWith(source) && source.Length <= "pub".Length,
            TokenType.Fn => "fn".AsSpan().StartsWith(source) && source.Length <= "fn".Length,
            TokenType.IntKeyword => "int".AsSpan().StartsWith(source) && source.Length <= "int".Length,
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
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        bool MatchesStringLiteral(ReadOnlySpan<char> stringSource)
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