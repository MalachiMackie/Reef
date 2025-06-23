using FluentAssertions;

namespace NewLang.Core.Tests;

public class TokenizerTests
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void Tests(string source, IEnumerable<Token> expectedTokens)
    {
        var result = Tokenizer.Tokenize(source);

        result.Should().BeEquivalentTo(expectedTokens);
    }
    
    [Theory]
    [MemberData(nameof(SingleTestCases))]
    public void SingleTests(string source, IEnumerable<Token> expectedTokens)
    {
        var result = Tokenizer.Tokenize(source);

        result.Should().BeEquivalentTo(expectedTokens);
    }

    [Fact]
    public void Should_Throw_When_SymbolCharacterIsFound()
    {
        const string source = "@";

        var act = () => Tokenizer.Tokenize(source).Count();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_TokenizeNonEnglishCharacters()
    {
        const string source = "漢";
        
        var result = Tokenizer.Tokenize(source);

        result.Should().BeEquivalentTo([Token.Identifier("漢", new SourceSpan(new SourcePosition(0, 0, 0), 1))]);
    }

    [Fact]
    public void Perf()
    {
        Tokenizer.Tokenize(LargeSource).Count().Should().BeGreaterThan(0);
    }

    public static IEnumerable<object[]> SingleTestCases()
    {
        return [
            [
                """
                pub fn DoSomething(a: int): result<int, string> {
                    var b = 2;
                
                    if (a == b) {
                        return ok(a);
                    }
                }
                """,
                new[]
                {
                    // line 0
                    Token.Pub(new SourceSpan(new SourcePosition(0, 0, 0), 3)),
                    Token.Fn(new SourceSpan(new SourcePosition(4, 0, 4), 2)),
                    Token.Identifier("DoSomething", new SourceSpan(new SourcePosition(7, 0, 7), 11)),
                    Token.LeftParenthesis(new SourceSpan(new SourcePosition(18, 0, 18), 1)),
                    Token.Identifier("a", new SourceSpan(new SourcePosition(19, 0, 19), 1)),
                    Token.Colon(new SourceSpan(new SourcePosition(20, 0, 20), 1)),
                    Token.IntKeyword(new SourceSpan(new SourcePosition(22, 0, 22), 3)),
                    Token.RightParenthesis(new SourceSpan(new SourcePosition(25, 0, 25), 1)),
                    Token.Colon(new SourceSpan(new SourcePosition(26, 0, 26), 1)),
                    Token.Result(new SourceSpan(new SourcePosition(28, 0, 28), 6)),
                    Token.LeftAngleBracket(new SourceSpan(new SourcePosition(34, 0, 34), 1)),
                    Token.IntKeyword(new SourceSpan(new SourcePosition(35, 0, 35), 3)),
                    Token.Comma(new SourceSpan(new SourcePosition(38, 0, 38), 1)),
                    Token.StringKeyword(new SourceSpan(new SourcePosition(40, 0, 40), 6)),
                    Token.RightAngleBracket(new SourceSpan(new SourcePosition(46, 0, 46), 1)),
                    Token.LeftBrace(new SourceSpan(new SourcePosition(48, 0, 48), 1)),
                    // line 1
                    Token.Var(new SourceSpan(new SourcePosition(55, 1, 4), 3)),
                    Token.Identifier("b", new SourceSpan(new SourcePosition(59, 1, 8), 1)),
                    Token.Equals(new SourceSpan(new SourcePosition(61, 1, 10), 1)),
                    Token.IntLiteral(2, new SourceSpan(new SourcePosition(63, 1, 12), 1)),
                    Token.Semicolon(new SourceSpan(new SourcePosition(64, 1, 13), 1)),
                    // line 2
                    Token.If(new SourceSpan(new SourcePosition(73, 3, 4), 2)),
                    Token.LeftParenthesis(new SourceSpan(new SourcePosition(76, 3, 7), 1)),
                    Token.Identifier("a", new SourceSpan(new SourcePosition(77, 3, 8), 1)),
                    Token.DoubleEquals(new SourceSpan(new SourcePosition(79, 3, 10), 2)),
                    Token.Identifier("b", new SourceSpan(new SourcePosition(82, 3, 13), 1)),
                    Token.RightParenthesis(new SourceSpan(new SourcePosition(83, 3, 14), 1)),
                    Token.LeftBrace(new SourceSpan(new SourcePosition(85, 3, 16), 1)),
                    // line 4
                    Token.Return(new SourceSpan(new SourcePosition(96, 4, 8), 6)),
                    Token.Ok(new SourceSpan(new SourcePosition(103, 4, 15), 2)),
                    Token.LeftParenthesis(new SourceSpan(new SourcePosition(105, 4, 17), 1)),
                    Token.Identifier("a", new SourceSpan(new SourcePosition(106, 4, 18), 1)),
                    Token.RightParenthesis(new SourceSpan(new SourcePosition(107, 4, 19), 1)),
                    Token.Semicolon(new SourceSpan(new SourcePosition(108, 4, 20), 1)),
                    // line 5
                    Token.RightBrace(new SourceSpan(new SourcePosition(115, 5, 4), 1)),
                    // line 6
                    Token.RightBrace(new SourceSpan(new SourcePosition(118, 6, 0), 1))
                }
            ]
        ];
    }
    

    public static IEnumerable<object[]> TestCases()
    {
        return
        [
            // empty source
            ["", Array.Empty<Token>()],
            [" ", Array.Empty<Token>()],
            ["  ", Array.Empty<Token>()],
            ["\t\t", Array.Empty<Token>()],
            
            // single tokens
            ["::", new[] { Token.DoubleColon(new SourceSpan(new SourcePosition(0, 0, 0), 2)) }],
            ["pub", new[] { Token.Pub(new SourceSpan(new SourcePosition(0, 0, 0), 3)) }],
            ["union", new[] { Token.Union(new SourceSpan(new SourcePosition(0, 0, 0), 5)) }],
            ["static", new[] { Token.Static(new SourceSpan(new SourcePosition(0, 0, 0), 6)) }],
            ["matches", new[] { Token.Matches(new SourceSpan(new SourcePosition(0, 0, 0), 7)) }],
            ["match", new[] { Token.Match(new SourceSpan(new SourcePosition(0, 0, 0), 5)) }],
            ["_", new[] { Token.Underscore(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            ["class", new[] { Token.Class(new SourceSpan(new SourcePosition(0, 0, 0), 5)) }],
            ["// some comment here", new [] { Token.SingleLineComment(" some comment here", new SourceSpan(new SourcePosition(0, 0, 0), 20)) }],
            [
                "// some comment here\r\nfn",
                new []
                {
                    Token.SingleLineComment(" some comment here", new SourceSpan(new SourcePosition(0, 0, 0), 20)),
                    Token.Fn(new SourceSpan(new SourcePosition(22, 1, 0), 2))
                }
            ],
            ["""
             /*
             multi line
             comment
             */
             """, new [] { Token.MultiLineComment("\r\nmulti line\r\ncomment\r\n", new SourceSpan(new SourcePosition(0, 0, 0), 27)) }],
            [
                """
                 /*
                 multi line
                 comment
                 */ fn
                 """, new []
                {
                    Token.MultiLineComment("\r\nmulti line\r\ncomment\r\n", new SourceSpan(new SourcePosition(0, 0, 0), 27)),
                    Token.Fn(new SourceSpan(new SourcePosition(28, 3, 3), 2))
                }
            ],
            [
                """
                fn
                /*
                some contents
                here
                */
                fn
                """,
                new []
                {
                    Token.Fn(new SourceSpan(new SourcePosition(0, 0, 0), 2)),
                    Token.MultiLineComment("\r\nsome contents\r\nhere\r\n", new SourceSpan(new SourcePosition(4, 1, 0), 27)),
                    Token.Fn(new SourceSpan(new SourcePosition(33, 5, 0), 2))
                }
            ],
            ["field", new[] { Token.Field(new SourceSpan(new SourcePosition(0, 0, 0), 5)) }],
            ["fn", new[] { Token.Fn(new SourceSpan(new SourcePosition(0, 0, 0), 2)) }],
            ["int", new[] { Token.IntKeyword(new SourceSpan(new SourcePosition(0, 0, 0), 3)) }],
            ["new", new[] { Token.New(new SourceSpan(new SourcePosition(0, 0, 0), 3)) }],
            ["if", new[] { Token.If(new SourceSpan(new SourcePosition(0, 0, 0), 2)) }],
            ["else", new[] { Token.Else(new SourceSpan(new SourcePosition(0, 0, 0), 4)) }],
            ["var", new[] { Token.Var(new SourceSpan(new SourcePosition(0, 0, 0), 3)) }],
            ["=", new[] { Token.Equals(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            ["==", new[] { Token.DoubleEquals(new SourceSpan(new SourcePosition(0, 0, 0), 2)) }],
            ["string", new[] { Token.StringKeyword(new SourceSpan(new SourcePosition(0, 0, 0), 6)) }],
            ["return", new[] { Token.Return(new SourceSpan(new SourcePosition(0, 0, 0), 6)) }],
            ["mut", new[] { Token.Mut(new SourceSpan(new SourcePosition(0, 0, 0), 3)) }],
            ["(", new[] { Token.LeftParenthesis(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            [")", new[] { Token.RightParenthesis(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            ["{", new[] { Token.LeftBrace(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            ["}", new[] { Token.RightBrace(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            ["<", new[] { Token.LeftAngleBracket(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            [">", new[] { Token.RightAngleBracket(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            [",", new[] { Token.Comma(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            ["hello", new[] { Token.Identifier("hello", new SourceSpan(new SourcePosition(0, 0, 0), 5)) }],
            ["hello_", new[] { Token.Identifier("hello_", new SourceSpan(new SourcePosition(0, 0, 0), 6)) }],
            [":", new[] { Token.Colon(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            ["::<", new[] { Token.Turbofish(new SourceSpan(new SourcePosition(0, 0, 0), 3)) }],
            ["=>", new[] { Token.EqualsArrow(new SourceSpan(new SourcePosition(0, 0, 0), 2)) }],
            [";", new[] { Token.Semicolon(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            ["result", new[] { Token.Result(new SourceSpan(new SourcePosition(0, 0, 0), 6)) }],
            ["todo!", new[] { Token.Todo(new SourceSpan(new SourcePosition(0, 0, 0), 5)) }],
            ["this", new[] { Token.This(new SourceSpan(new SourcePosition(0, 0, 0), 4)) }],
            ["ok", new[] { Token.Ok(new SourceSpan(new SourcePosition(0, 0, 0), 2)) }],
            ["error", new[] { Token.Error(new SourceSpan(new SourcePosition(0, 0, 0), 5)) }],
            ["*", new[] { Token.Star(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            ["!", new[] { Token.Bang(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            ["-", new[] { Token.Dash(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            ["/", new[] { Token.ForwardSlash(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            ["+", new[] { Token.Plus(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            [".", new[] { Token.Dot(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            // all single char identifiers
            .."abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
                .Select(ch => ch.ToString())
                .Select(ch => new object[] {ch, new [] {Token.Identifier(ch, new SourceSpan(new SourcePosition(0, 0, 0), 1))}}),
            [
                "\"hello this is a string\"",
                new[] { Token.StringLiteral("hello this is a string", new SourceSpan(new SourcePosition(0, 0, 0), 24)) }
            ],
            ["5", new[] { Token.IntLiteral(5, new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            ["?", new[] { Token.QuestionMark(new SourceSpan(new SourcePosition(0, 0, 0), 1)) }],
            ["true", new[] { Token.True(new SourceSpan(new SourcePosition(0, 0, 0), 4)) }],
            ["false", new[] { Token.False(new SourceSpan(new SourcePosition(0, 0, 0), 5)) }],
            ["bool", new[] { Token.Bool(new SourceSpan(new SourcePosition(0, 0, 0), 4)) }],
            // new line
            ["\r\nbool", new[] { Token.Bool(new SourceSpan(new SourcePosition(2, 1, 0), 4)) }],
            ["\r\n\r\nbool", new[] { Token.Bool(new SourceSpan(new SourcePosition(4, 2, 0), 4)) }],
            ["\r\n\r\n  bool", new[] { Token.Bool(new SourceSpan(new SourcePosition(6, 2, 2), 4)) }],
            ["\r\n  \r\n  bool", new[] { Token.Bool(new SourceSpan(new SourcePosition(8, 2, 2), 4)) }],
            
            // single token padding tests
            [" fn ", new[] { Token.Fn(new SourceSpan(new SourcePosition(1, 0, 1), 2)) }],
            ["fn ", new[] { Token.Fn(new SourceSpan(new SourcePosition(0, 0, 0), 2)) }],
            [" fn", new[] { Token.Fn(new SourceSpan(new SourcePosition(1, 0, 1), 2)) }],
            ["\tfn\t", new[] { Token.Fn(new SourceSpan(new SourcePosition(1, 0, 1), 2)) }],

            // two tokens separated by whitespace
            [
                "pub fn",
                new[]
                {
                    Token.Pub(new SourceSpan(new SourcePosition(0, 0, 0), 3)),
                    Token.Fn(new SourceSpan(new SourcePosition(4, 0, 4), 2))
                }
            ],
            [
                "pub 5",
                new[]
                {
                    Token.Pub(new SourceSpan(new SourcePosition(0, 0, 0), 3)),
                    Token.IntLiteral(5, new SourceSpan(new SourcePosition(4, 0, 4), 1))
                }
            ],
            [
                "5 pub",
                new[]
                {
                    Token.IntLiteral(5, new SourceSpan(new SourcePosition(0, 0, 0), 1)),
                    Token.Pub(new SourceSpan(new SourcePosition(2, 0, 2), 3))
                }
            ],
            [
                "\"string\" pub",
                new[]
                {
                    Token.StringLiteral("string", new SourceSpan(new SourcePosition(0, 0, 0), 8)),
                    Token.Pub(new SourceSpan(new SourcePosition(9, 0, 9), 3))
                }
            ],
            
            // multiple tokens without separation
            [
                "int)",
                new[]
                {
                    Token.IntKeyword(new SourceSpan(new SourcePosition(0, 0, 0), 3)),
                    Token.RightParenthesis(new SourceSpan(new SourcePosition(3, 0, 3), 1))
                }
            ],
            [
                "a)",
                new[]
                {
                    Token.Identifier("a", new SourceSpan(new SourcePosition(0, 0, 0), 1)),
                    Token.RightParenthesis(new SourceSpan(new SourcePosition(1, 0, 1), 1))
                }
            ],
            [
                "i)",
                new[]
                {
                    Token.Identifier("i", new SourceSpan(new SourcePosition(0, 0, 0), 1)),
                    Token.RightParenthesis(new SourceSpan(new SourcePosition(1, 0, 1), 1))
                }
            ],
            [
                "a)?;", new[]
                {
                    Token.Identifier("a", new SourceSpan(new SourcePosition(0, 0, 0), 1)),
                    Token.RightParenthesis(new SourceSpan(new SourcePosition(1, 0, 1), 1)),
                    Token.QuestionMark(new SourceSpan(new SourcePosition(2, 0, 2), 1)),
                    Token.Semicolon(new SourceSpan(new SourcePosition(3, 0, 3), 1))
                }
            ],
            
            // two tokens padding tests
            [
                " pub fn ",
                new[]
                {
                    Token.Pub(new SourceSpan(new SourcePosition(1, 0, 1), 3)),
                    Token.Fn(new SourceSpan(new SourcePosition(5, 0, 5), 2))
                }
            ],
            [
                "pub fn ",
                new[]
                {
                    Token.Pub(new SourceSpan(new SourcePosition(0, 0, 0), 3)),
                    Token.Fn(new SourceSpan(new SourcePosition(4, 0, 4), 2))
                }
            ],
            [
                " pub fn",
                new[]
                {
                    Token.Pub(new SourceSpan(new SourcePosition(1, 0, 1), 3)),
                    Token.Fn(new SourceSpan(new SourcePosition(5, 0, 5), 2))
                }
            ],
            [
                " pub  fn",
                new[]
                {
                    Token.Pub(new SourceSpan(new SourcePosition(1, 0, 1), 3)),
                    Token.Fn(new SourceSpan(new SourcePosition(6, 0, 6), 2))
                }
            ],
            [
                "\tpub\tfn\t",
                new[]
                {
                    Token.Pub(new SourceSpan(new SourcePosition(1, 0, 1), 3)),
                    Token.Fn(new SourceSpan(new SourcePosition(5, 0, 5), 2))
                }
            ],
            
            // full source
            [
                """
                pub fn DoSomething(a: int): result<int, string> {
                    var b = 2;
                
                    if (a == b) {
                        return ok(a);
                    }
                }
                """,
                new[]
                {
                    // line 0
                    Token.Pub(new SourceSpan(new SourcePosition(0, 0, 0), 3)),
                    Token.Fn(new SourceSpan(new SourcePosition(4, 0, 4), 2)),
                    Token.Identifier("DoSomething", new SourceSpan(new SourcePosition(7, 0, 7), 11)),
                    Token.LeftParenthesis(new SourceSpan(new SourcePosition(18, 0, 18), 1)),
                    Token.Identifier("a", new SourceSpan(new SourcePosition(19, 0, 19), 1)),
                    Token.Colon(new SourceSpan(new SourcePosition(20, 0, 20), 1)),
                    Token.IntKeyword(new SourceSpan(new SourcePosition(22, 0, 22), 3)),
                    Token.RightParenthesis(new SourceSpan(new SourcePosition(25, 0, 25), 1)),
                    Token.Colon(new SourceSpan(new SourcePosition(26, 0, 26), 1)),
                    Token.Result(new SourceSpan(new SourcePosition(28, 0, 28), 6)),
                    Token.LeftAngleBracket(new SourceSpan(new SourcePosition(34, 0, 34), 1)),
                    Token.IntKeyword(new SourceSpan(new SourcePosition(35, 0, 35), 3)),
                    Token.Comma(new SourceSpan(new SourcePosition(38, 0, 38), 1)),
                    Token.StringKeyword(new SourceSpan(new SourcePosition(40, 0, 40), 6)),
                    Token.RightAngleBracket(new SourceSpan(new SourcePosition(46, 0, 46), 1)),
                    Token.LeftBrace(new SourceSpan(new SourcePosition(48, 0, 48), 1)),
                    // line 1
                    Token.Var(new SourceSpan(new SourcePosition(55, 1, 4), 3)),
                    Token.Identifier("b", new SourceSpan(new SourcePosition(59, 1, 8), 1)),
                    Token.Equals(new SourceSpan(new SourcePosition(61, 1, 10), 1)),
                    Token.IntLiteral(2, new SourceSpan(new SourcePosition(63, 1, 12), 1)),
                    Token.Semicolon(new SourceSpan(new SourcePosition(64, 1, 13), 1)),
                    // line 2
                    Token.If(new SourceSpan(new SourcePosition(73, 3, 4), 2)),
                    Token.LeftParenthesis(new SourceSpan(new SourcePosition(76, 3, 7), 1)),
                    Token.Identifier("a", new SourceSpan(new SourcePosition(77, 3, 8), 1)),
                    Token.DoubleEquals(new SourceSpan(new SourcePosition(79, 3, 10), 2)),
                    Token.Identifier("b", new SourceSpan(new SourcePosition(82, 3, 13), 1)),
                    Token.RightParenthesis(new SourceSpan(new SourcePosition(83, 3, 14), 1)),
                    Token.LeftBrace(new SourceSpan(new SourcePosition(85, 3, 16), 1)),
                    // line 4
                    Token.Return(new SourceSpan(new SourcePosition(96, 4, 8), 6)),
                    Token.Ok(new SourceSpan(new SourcePosition(103, 4, 15), 2)),
                    Token.LeftParenthesis(new SourceSpan(new SourcePosition(105, 4, 17), 1)),
                    Token.Identifier("a", new SourceSpan(new SourcePosition(106, 4, 18), 1)),
                    Token.RightParenthesis(new SourceSpan(new SourcePosition(107, 4, 19), 1)),
                    Token.Semicolon(new SourceSpan(new SourcePosition(108, 4, 20), 1)),
                    // line 5
                    Token.RightBrace(new SourceSpan(new SourcePosition(115, 5, 4), 1)),
                    // line 6
                    Token.RightBrace(new SourceSpan(new SourcePosition(118, 6, 0), 1))
                }
            ]
        ];
    }
    
    private const string MediumSource = """
                                        pub fn DoSomething(a: int): result<int, string> {
                                            var b = 2;
                                            
                                            if (a > b) {
                                                return ok(a);
                                            }
                                            else if (a == b) {
                                                return ok(b);
                                            }
                                            
                                            return error("something wrong");
                                        }

                                        pub fn SomethingElse(a: int): result<int, string> {
                                            b = DoSomething(a)?;
                                            
                                            return b;
                                        }

                                        Println(DoSomething(5));
                                        Println(DoSomething(1));
                                        Println(SomethingElse(1));

                                        """;

    private const string LargeSource = $"""
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       """;
}