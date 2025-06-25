using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Xunit.Abstractions;

#pragma warning disable IDE0060 // Remove unused parameter
// ReSharper disable PossibleMultipleEnumeration

namespace NewLang.Core.Tests;

public class ParserTests(ITestOutputHelper testOutputHelper)
{
    [Theory]
    [MemberData(nameof(FailTestCases))]
    public void FailTests(
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        string source,
        IEnumerable<Token> tokens)
    {
        var result = Parser.Parse(tokens);

        result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [MemberData(nameof(PopExpressionTestCases))]
    public void PopExpressionTests(
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        string source,
        IEnumerable<Token> tokens,
        IExpression expectedExpression)
    {
        var result = Parser.PopExpression(tokens);
        result.Should().NotBeNull();

        // clear out the source spans, we don't actually care about them
        var expression = RemoveSourceSpan(result);

        try
        {
            expression.Should().BeEquivalentTo(expectedExpression, opts => opts.AllowingInfiniteRecursion());
        }
        catch
        {
            testOutputHelper.WriteLine("Expected {0}, found {1}", expectedExpression, expression);
            throw;
        }
    }

    [Theory]
    [MemberData(nameof(SingleTestCase))]
    public void SingleTest(
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        string source,
        IEnumerable<Token> tokens,
        IExpression expectedProgram)
    {
        var result = Parser.PopExpression(tokens);
        result.Should().NotBeNull();

        // clear out the source spans, we don't actually care about them
        var program = RemoveSourceSpan(result);

        try
        {
            program.Should().BeEquivalentTo(expectedProgram, opts => opts.AllowingInfiniteRecursion());
        }
        catch
        {
            testOutputHelper.WriteLine("Expected {0}, found {1}", expectedProgram, program);
            throw;
        }
    }

    [Theory]
    [MemberData(nameof(ParseTestCases))]
    public void ParseTest(
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")] string source,
        IEnumerable<Token> tokens,
        LangProgram expectedProgram)
    {
        var program = RemoveSourceSpan(Parser.Parse(tokens).ParsedProgram);

        try
        {
            program.Should().BeEquivalentTo(expectedProgram, opts => opts.AllowingInfiniteRecursion());
        }
        catch
        {
            testOutputHelper.WriteLine("Expected [{0}], found [{1}]", expectedProgram, program);
            throw;
        }
    }

    [Theory]
    [MemberData(nameof(ParseErrorTestCases))]
    public void ParseErrorTests(string source, LangProgram expectedProgram, IEnumerable<ParserError> expectedErrors)
    {
        var tokens = Tokenizer.Tokenize(source);

        var output = Parser.Parse(tokens);

        expectedProgram = RemoveSourceSpan(expectedProgram);
        expectedErrors = RemoveSourceSpan(expectedErrors.ToArray());
        var program = RemoveSourceSpan(output.ParsedProgram);
        var errors = RemoveSourceSpan(output.Errors);

        program.Should().BeEquivalentTo(expectedProgram, opts => opts.AllowingInfiniteRecursion());
        errors.Should().BeEquivalentTo(expectedErrors);
    }

    public static TheoryData<string, LangProgram, IEnumerable<ParserError>> ParseErrorTestCases()
    {
        IEnumerable<(string, LangProgram, IEnumerable<ParserError>)> data =
        [
            ("", new LangProgram([], [], [], []), []),
            (
                "var a",
                new LangProgram([VariableDeclaration("a")], [], [], []),
                []
            ),
            (
                "var ",
                new LangProgram([], [], [], []),
                [ParserError.VariableDeclaration_MissingIdentifier(Token.Var(SourceSpan.Default))]
            ),
            (
                "var ;",
                new LangProgram([], [], [], []),
                [ParserError.VariableDeclaration_InvalidIdentifier(Token.Semicolon(SourceSpan.Default))]
            ),
            (
                "var a = ",
                new LangProgram([
                    VariableDeclaration("a")
                ], [], [], []),
                [ParserError.VariableDeclaration_MissingValue(Token.Equals(SourceSpan.Default))]
            ),
            (
                "var a = ;",
                new LangProgram([
                    VariableDeclaration("a")
                ], [], [], []),
                [ParserError.VariableDeclaration_MissingValue(Token.Semicolon(SourceSpan.Default))]
            ),
            (
                "var a: = 2;",
                new LangProgram([
                    VariableDeclaration("a", Literal(2))
                ], [], [], []),
                [ParserError.VariableDeclaration_MissingType(Token.Equals(SourceSpan.Default))]
            ),
            (
                "var a: int = ;",
                new LangProgram([
                    VariableDeclaration("a", type: IntType())
                ], [], [], []),
                [ParserError.VariableDeclaration_MissingValue(Token.Semicolon(SourceSpan.Default))]
            ),
            (
                "var mut a: int = ;",
                new LangProgram([
                    VariableDeclaration("a", type: IntType(), isMutable: true)
                ], [], [], []),
                [ParserError.VariableDeclaration_MissingValue(Token.Semicolon(SourceSpan.Default))]
            ),
            (
                "var a = ; var b = 2",
                new LangProgram([
                    VariableDeclaration("a"),
                    VariableDeclaration("b", Literal(2))
                ], [], [], []),
                [ParserError.VariableDeclaration_MissingValue(Token.Semicolon(SourceSpan.Default))]
            ),
            (
                "*",
                new LangProgram([
                    Multiply(null, null)
                ], [], [], []),
                [
                    ParserError.BinaryOperator_MissingLeftValue(Token.Star(SourceSpan.Default)),
                    ParserError.BinaryOperator_MissingRightValue(Token.Star(SourceSpan.Default)),
                ]
            ),
            (
                "a *",
                new LangProgram([
                    Multiply(VariableAccessor("a"), null)
                ], [], [], []),
                [
                    ParserError.BinaryOperator_MissingRightValue(Token.Star(SourceSpan.Default)),
                ]
            ),
            (
                "* a",
                new LangProgram([
                    Multiply(null, VariableAccessor("a"))
                ], [], [], []),
                [
                    ParserError.BinaryOperator_MissingLeftValue(Token.Star(SourceSpan.Default)),
                ]
            ),
            (
                "a * ;var b = 2",
                new LangProgram([
                    Multiply(VariableAccessor("a"), null),
                    VariableDeclaration("b", Literal(2))
                ], [], [], []),
                [
                    ParserError.BinaryOperator_MissingRightValue(Token.Semicolon(SourceSpan.Default)),
                ]
            ),
            (
                "?",
                new LangProgram([
                    FallOut(null),
                ], [], [], []),
                [
                    ParserError.UnaryOperator_MissingValue(Token.QuestionMark(SourceSpan.Default)),
                ]
            ),
            (
                "?; a;",
                new LangProgram([
                    FallOut(null),
                    VariableAccessor("a")
                ], [], [], []),
                [
                    ParserError.UnaryOperator_MissingValue(Token.QuestionMark(SourceSpan.Default)),
                ]
            ),
            (
                "!",
                new LangProgram([
                    Not(null),
                ], [], [], []),
                [
                    ParserError.UnaryOperator_MissingValue(Token.Bang(SourceSpan.Default)),
                ]
            ),
            (
                "a;!",
                new LangProgram([
                    VariableAccessor("a"),
                    Not(null),
                ], [], [], []),
                [
                    ParserError.UnaryOperator_MissingValue(Token.Bang(SourceSpan.Default)),
                ]
            ),
            (
                "!;var a = 2;",
                new LangProgram([
                    Not(null),
                    VariableDeclaration("a", Literal(2))
                ], [], [], []),
                [
                    ParserError.UnaryOperator_MissingValue(Token.Semicolon(SourceSpan.Default)),
                ]
            ),
            (
                "{",
                new LangProgram([
                    Block(),
                ], [], [], []),
                [
                    ParserError.Scope_MissingClosingTag(Token.LeftBrace(SourceSpan.Default)),
                ]
            ),
            (
                ",",
                new LangProgram([], [], [], []),
                [
                    ParserError.Scope_UnexpectedComma(Token.Comma(SourceSpan.Default)),
                ]
            ),
            (
                "a;,b",
                new LangProgram([
                    VariableAccessor("a"),
                    VariableAccessor("b"),
                ], [], [], []),
                [
                    ParserError.Scope_UnexpectedComma(Token.Comma(SourceSpan.Default)),
                ]
            ),
            (
                "a b",
                new LangProgram([
                    VariableAccessor("a"),
                    VariableAccessor("b"),
                ], [], [], []),
                [
                    ParserError.Scope_EarlyTailReturnExpression(VariableAccessor("a")),
                ]
            ),
            (
                "a b; c; d e",
                new LangProgram([
                    VariableAccessor("a"),
                    VariableAccessor("b"),
                    VariableAccessor("c"),
                    VariableAccessor("d"),
                    VariableAccessor("e"),
                ], [], [], []),
                [
                    ParserError.Scope_EarlyTailReturnExpression(VariableAccessor("a")),
                    ParserError.Scope_EarlyTailReturnExpression(VariableAccessor("d")),
                ]
            ),
            (
                "class MyClass {field MyField: string field OtherField: string}",
                new LangProgram([], [], [
                    new ProgramClass(null, Token.Identifier("MyClass", SourceSpan.Default), [], [], [
                        ClassField("MyField", StringType()),
                        ClassField("OtherField", StringType()),
                    ])], []),
                [
                    ParserError.Scope_ExpectedComma(Token.Field(SourceSpan.Default)),
                ]
            ),
            (
                "class MyClass {field MyField: string, field OtherField: string",
                new LangProgram([], [], [
                    new ProgramClass(null, Token.Identifier("MyClass", SourceSpan.Default), [], [], [
                    ClassField("MyField", StringType()),
                    ClassField("OtherField", StringType()),
                ])], []),
                [
                    ParserError.Scope_MissingClosingTag(Token.StringKeyword(SourceSpan.Default)),
                ]
            ),
            (
                "{a",
                new LangProgram([
                    Block([VariableAccessor("a")]),
                ], [], [], []),
                [
                    ParserError.Scope_MissingClosingTag(Token.StringKeyword(SourceSpan.Default)),
                ]
            ),
        ];

        var theoryData = new TheoryData<string, LangProgram, IEnumerable<ParserError>>();
        foreach (var item in data)
        {
            theoryData.Add(item.Item1, item.Item2, item.Item3);
        }

        return theoryData;
    }

    public static IEnumerable<object[]> ParseTestCases()
    {
        return new (string Source, LangProgram ExpectedProgram)[]
        {
            ("var a: int = (1 + 2) * 3;", new LangProgram([
                new VariableDeclarationExpression(new VariableDeclaration(
                    Token.Identifier("a", SourceSpan.Default),
                    null,
                    new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new TupleExpression([
                            new BinaryOperatorExpression(new BinaryOperator(
                                BinaryOperatorType.Plus,
                                new ValueAccessorExpression(new ValueAccessor(
                                    ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default))),
                                new ValueAccessorExpression(new ValueAccessor(
                                    ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default))),
                                Token.Plus(SourceSpan.Default)))
                        ], SourceRange.Default),
                        new ValueAccessorExpression(new ValueAccessor(
                            ValueAccessType.Literal,
                            Token.IntLiteral(3, SourceSpan.Default))),
                        Token.Plus(SourceSpan.Default)))), SourceRange.Default)
            ], [], [], [])),
            (
                """
                union MyUnion<T> {
                    A { }
                }

                var a = new MyUnion::<string>::A {};
                """,
                new LangProgram(
                    [
                        new VariableDeclarationExpression(new VariableDeclaration(
                            Token.Identifier("a", SourceSpan.Default),
                            null,
                            null,
                            new UnionStructVariantInitializerExpression(new UnionStructVariantInitializer(
                                new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default),
                                    [new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default)], SourceRange.Default),
                                Token.Identifier("A", SourceSpan.Default),
                                [
                                    new FieldInitializer(
                                        Token.Identifier("MyField", SourceSpan.Default),
                                        new ValueAccessorExpression(new ValueAccessor(
                                            ValueAccessType.Literal, Token.StringLiteral("value", SourceSpan.Default)))
                                    ),
                                    new FieldInitializer(
                                        Token.Identifier("Field2", SourceSpan.Default),
                                        new ValueAccessorExpression(new ValueAccessor(
                                            ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default)))
                                    )
                                ]), SourceRange.Default)
                        ), SourceRange.Default)
                    ],
                    [],
                    [],
                    [
                        new ProgramUnion(
                            null,
                            Token.Identifier("MyUnion", SourceSpan.Default),
                            [Token.Identifier("T", SourceSpan.Default)],
                            [],
                            [
                                new StructUnionVariant
                                {
                                    Name = Token.Identifier("A", SourceSpan.Default),
                                    Fields =
                                    [
                                        new ClassField(null,
                                            null,
                                            null,
                                            Token.Identifier("MyField", SourceSpan.Default),
                                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                                            null
                                        )
                                    ]
                                }
                            ])
                    ])
            ),
            (
                """
                union MyUnion {
                    A { field MyField: string, field Field2: int }
                }

                var a = new MyUnion::A {
                    MyField = "value",
                    Field2 = 2
                };
                """,
                new LangProgram(
                    [
                        new VariableDeclarationExpression(new VariableDeclaration(
                            Token.Identifier("a", SourceSpan.Default),
                            null,
                            null,
                            new UnionStructVariantInitializerExpression(new UnionStructVariantInitializer(
                                new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                                Token.Identifier("A", SourceSpan.Default),
                                [
                                    new FieldInitializer(
                                        Token.Identifier("MyField", SourceSpan.Default),
                                        new ValueAccessorExpression(new ValueAccessor(
                                            ValueAccessType.Literal, Token.StringLiteral("value", SourceSpan.Default)))
                                    ),
                                    new FieldInitializer(
                                        Token.Identifier("Field2", SourceSpan.Default),
                                        new ValueAccessorExpression(new ValueAccessor(
                                            ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default)))
                                    )
                                ]), SourceRange.Default)
                        ), SourceRange.Default)
                    ],
                    [],
                    [],
                    [
                        new ProgramUnion(
                            null,
                            Token.Identifier("MyUnion", SourceSpan.Default),
                            [],
                            [],
                            [
                                new StructUnionVariant
                                {
                                    Name = Token.Identifier("A", SourceSpan.Default),
                                    Fields =
                                    [
                                        new ClassField(null,
                                            null,
                                            null,
                                            Token.Identifier("MyField", SourceSpan.Default),
                                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                                            null
                                        )
                                    ]
                                }
                            ])
                    ])
            ),
            ("class MyClass {field myFieldWithoutComma: string}",
                new LangProgram([], [],
                [
                    new ProgramClass(null, Token.Identifier("MyClass", SourceSpan.Default), [], [], [
                        new ClassField(
                            null,
                            null,
                            null,
                            Token.Identifier("myFieldWithoutComma", SourceSpan.Default),
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                            null)
                    ])
                ], [])),
            (
                """
                union MyUnion {
                }
                """,
                new LangProgram(
                    [],
                    [],
                    [],
                    [
                        new ProgramUnion(
                            null,
                            Token.Identifier("MyUnion", SourceSpan.Default),
                            [],
                            [],
                            [])
                    ])
            ),
            (
                "pub union MyUnion {}",
                new LangProgram(
                    [],
                    [],
                    [],
                    [
                        new ProgramUnion(
                            new AccessModifier(Token.Pub(SourceSpan.Default)),
                            Token.Identifier("MyUnion", SourceSpan.Default),
                            [],
                            [],
                            [])
                    ])
            ),
            (
                "union MyUnion<T1, T2,> {} ",
                new LangProgram([],
                    [],
                    [],
                    [
                        new ProgramUnion(
                            null,
                            Token.Identifier("MyUnion", SourceSpan.Default),
                            [Token.Identifier("T1", SourceSpan.Default), Token.Identifier("T2", SourceSpan.Default)],
                            [],
                            []
                        )
                    ])
            ),
            (
                "union MyUnion {fn SomeFn(){}}",
                new LangProgram([],
                    [],
                    [],
                    [
                        new ProgramUnion(
                            null,
                            Token.Identifier("MyUnion", SourceSpan.Default),
                            [],
                            [
                                new LangFunction(null, null, Token.Identifier("SomeFn", SourceSpan.Default),
                                    [], [], null, new Block([], []))
                            ],
                            []
                        )
                    ])
            ),
            (
                """
                union MyUnion {
                    A
                }
                """,
                new LangProgram(
                    [],
                    [],
                    [],
                    [
                        new ProgramUnion(
                            null,
                            Token.Identifier("MyUnion", SourceSpan.Default),
                            [],
                            [],
                            [new UnitStructUnionVariant(Token.Identifier("A", SourceSpan.Default))])
                    ])
            ),
            (
                """
                union MyUnion {
                    A,
                }
                """,
                new LangProgram(
                    [],
                    [],
                    [],
                    [
                        new ProgramUnion(
                            null,
                            Token.Identifier("MyUnion", SourceSpan.Default),
                            [],
                            [],
                            [new UnitStructUnionVariant(Token.Identifier("A", SourceSpan.Default))])
                    ])
            ),
            (
                """
                union MyUnion {
                    A,
                    B(string, int, MyClass::<string>)
                }
                """,
                new LangProgram(
                    [],
                    [],
                    [],
                    [
                        new ProgramUnion(
                            null,
                            Token.Identifier("MyUnion", SourceSpan.Default),
                            [],
                            [],
                            [
                                new UnitStructUnionVariant(Token.Identifier("A", SourceSpan.Default)),
                                new TupleUnionVariant(
                                    Token.Identifier("B", SourceSpan.Default),
                                    [
                                        new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                                        new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                                        new TypeIdentifier(
                                            Token.Identifier("MyClass", SourceSpan.Default),
                                            [new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default)]
                                        , SourceRange.Default)
                                    ])
                            ])
                    ])
            ),
            (
                """
                union MyUnion {
                    A { 
                        field MyField: string,
                    }
                }
                """,
                new LangProgram(
                    [],
                    [],
                    [],
                    [
                        new ProgramUnion(
                            null,
                            Token.Identifier("MyUnion", SourceSpan.Default),
                            [],
                            [],
                            [
                                new StructUnionVariant
                                {
                                    Name = Token.Identifier("A", SourceSpan.Default),
                                    Fields =
                                    [
                                        new ClassField(null, null, null,
                                            Token.Identifier("MyField", SourceSpan.Default),
                                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default), null)
                                    ]
                                }
                            ])
                    ])
            ),
            ("fn MyFn(mut a: int,){}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [
                        new FunctionParameter(
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Token.Identifier("a", SourceSpan.Default)
                        )
                    ],
                    null,
                    new Block([], []))
            ], [], [])),
            ("fn MyFn(mut a: int, b: int){}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [
                        new FunctionParameter(
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Token.Identifier("a", SourceSpan.Default)
                        ),
                        new FunctionParameter(
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                            null,
                            Token.Identifier("b", SourceSpan.Default)
                        )
                    ],
                    null,
                    new Block([], []))
            ], [], [])),
            ("fn MyFn(mut a: int, mut b: int){}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [
                        new FunctionParameter(
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Token.Identifier("a", SourceSpan.Default)
                        ),
                        new FunctionParameter(
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Token.Identifier("b", SourceSpan.Default)
                        )
                    ],
                    null,
                    new Block([], []))
            ], [], [])),
            ("fn MyFn(a: int,){}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [
                        new FunctionParameter(new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default), null,
                            Token.Identifier("a", SourceSpan.Default))
                    ],
                    null,
                    new Block([], []))
            ], [], [])),
            ("fn /* some comment */ MyFn(/*some comment*/a: int,)/**/{//}\r\n}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [
                        new FunctionParameter(new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default), null,
                            Token.Identifier("a", SourceSpan.Default))
                    ],
                    null,
                    new Block([], []))
            ], [], [])),
            ("class MyClass<T,> {}", new LangProgram([], [], [
                new ProgramClass(
                    null,
                    Token.Identifier("MyClass", SourceSpan.Default),
                    [Token.Identifier("T", SourceSpan.Default)],
                    [], [])
            ], [])),
            ("fn MyFn<T,>(){}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [Token.Identifier("T", SourceSpan.Default)],
                    [],
                    null,
                    new Block([], [])
                )
            ], [], [])),
            ("var a = 1;var b = 2;", new LangProgram([
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", SourceSpan.Default),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default)))), SourceRange.Default),
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("b", SourceSpan.Default),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default)
            ], [], [], [])),
            ("a = b;", new LangProgram([
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"), VariableAccessor("b"), Token.Equals(SourceSpan.Default)))
            ], [], [], [])),
            ("error();", new LangProgram([
                new MethodCallExpression(new MethodCall(
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Error(SourceSpan.Default))), []), SourceRange.Default)
            ], [], [], [])),
            ("something(a,);", new LangProgram([
                new MethodCallExpression(new MethodCall(
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("something", SourceSpan.Default))),
                    [
                        VariableAccessor("a")
                    ]), SourceRange.Default)
            ], [], [], [])),
            ("ok();", new LangProgram([
                new MethodCallExpression(new MethodCall(
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Ok(SourceSpan.Default))), []), SourceRange.Default)
            ], [], [], [])),
            ("ok().b()", new LangProgram([
                new MethodCallExpression(new MethodCall(
                    new MemberAccessExpression(new MemberAccess(
                        new MethodCallExpression(new MethodCall(
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                                Token.Ok(SourceSpan.Default))), []), SourceRange.Default),
                        Token.Identifier("b", SourceSpan.Default))), []), SourceRange.Default)
            ], [], [], [])),
            ("if (a) {} b = c;", new LangProgram(
                [
                    new IfExpressionExpression(new IfExpression(VariableAccessor("a"),
                        new BlockExpression(new Block([], []), SourceRange.Default), [], null), SourceRange.Default),
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                        VariableAccessor("b"), VariableAccessor("c"), Token.Equals(SourceSpan.Default)))
                ],
                [],
                [], [])),
            ("{} b = c;", new LangProgram(
                [
                    new BlockExpression(new Block([], []), SourceRange.Default),
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                        VariableAccessor("b"), VariableAccessor("c"), Token.Equals(SourceSpan.Default)))
                ],
                [],
                [], [])),
            ("fn MyFn() {}", new LangProgram([], [
                new LangFunction(null, null, Token.Identifier("MyFn", SourceSpan.Default), [], [], null,
                    new Block([], []))
            ], [], [])),
            ("if (a) {return b;}", new LangProgram([
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("a"),
                    new BlockExpression(new Block([new MethodReturnExpression(new MethodReturn(VariableAccessor("b")), SourceRange.Default)],
                        []), SourceRange.Default),
                    [],
                    null), SourceRange.Default)
            ], [], [], [])),
            ("fn MyFn() {if (a) {return b;}}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [],
                    null,
                    new Block([
                        new IfExpressionExpression(new IfExpression(
                            VariableAccessor("a"),
                            new BlockExpression(
                                new Block([new MethodReturnExpression(new MethodReturn(VariableAccessor("b")), SourceRange.Default)], []), SourceRange.Default),
                            [],
                            null), SourceRange.Default)
                    ], []))
            ], [], [])),
            ("fn MyFn() {if (a) {return b();}}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [],
                    null,
                    new Block([
                        new IfExpressionExpression(new IfExpression(
                            VariableAccessor("a"),
                            new BlockExpression(new Block(
                            [
                                new MethodReturnExpression(
                                    new MethodReturn(
                                        new MethodCallExpression(new MethodCall(VariableAccessor("b"), []), SourceRange.Default)), SourceRange.Default)
                            ], []), SourceRange.Default),
                            [],
                            null), SourceRange.Default)
                    ], []))
            ], [], [])),
            ("fn MyFn(): string {}", new LangProgram([], [
                new LangFunction(null, null, Token.Identifier("MyFn", SourceSpan.Default), [], [],
                    new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default), new Block([], []))
            ], [], [])),
            ("fn MyFn(): result::<int, MyErrorType> {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [],
                    new TypeIdentifier(
                        Token.Result(SourceSpan.Default),
                        [
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                            new TypeIdentifier(Token.Identifier("MyErrorType", SourceSpan.Default), [], SourceRange.Default)
                        ], SourceRange.Default),
                    new Block([], []))
            ], [], [])),
            ("fn MyFn(): Outer::<Inner::<int>> {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [],
                    new TypeIdentifier(
                        Token.Identifier("Outer", SourceSpan.Default),
                        [
                            new TypeIdentifier(Token.Identifier("Inner", SourceSpan.Default), [
                                new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default)
                            ], SourceRange.Default)
                        ], SourceRange.Default),
                    new Block([], []))
            ], [], [])),
            ("fn MyFn(): Outer::<Inner::<int>, Inner::<int>> {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [],
                    new TypeIdentifier(
                        Token.Identifier("Outer", SourceSpan.Default),
                        [
                            new TypeIdentifier(Token.Identifier("Inner", SourceSpan.Default),
                                [new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default)], SourceRange.Default),
                            new TypeIdentifier(Token.Identifier("Inner", SourceSpan.Default),
                                [new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default)], SourceRange.Default)
                        ], SourceRange.Default),
                    new Block([], []))
            ], [], [])),
            ("fn MyFn(): result::<int, MyErrorType, ThirdTypeArgument> {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [],
                    new TypeIdentifier(
                        Token.Result(SourceSpan.Default),
                        [
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                            new TypeIdentifier(Token.Identifier("MyErrorType", SourceSpan.Default), [], SourceRange.Default),
                            new TypeIdentifier(Token.Identifier("ThirdTypeArgument", SourceSpan.Default), [], SourceRange.Default)
                        ], SourceRange.Default),
                    new Block([], []))
            ], [], [])),
            ("fn MyFn() { var a = 2; }", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [],
                    null,
                    new Block([
                        new VariableDeclarationExpression(new VariableDeclaration(
                            Token.Identifier("a", SourceSpan.Default),
                            null,
                            null,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default)
                    ], [])
                )
            ], [], [])),
            ("fn MyFn(a: int) {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [
                        new FunctionParameter(new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default), null,
                            Token.Identifier("a", SourceSpan.Default))
                    ],
                    null,
                    new Block([], [])
                )
            ], [], [])),
            ("static fn MyFn() {}", new LangProgram([], [
                new LangFunction(
                    null,
                    new StaticModifier(Token.Static(SourceSpan.Default)),
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [],
                    null,
                    new Block([], [])
                )
            ], [], [])),
            ("fn MyFn<T1>() {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [Token.Identifier("T1", SourceSpan.Default)],
                    [],
                    null,
                    new Block([], [])
                )
            ], [], [])),
            ("fn MyFn<T1, T2>() {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [Token.Identifier("T1", SourceSpan.Default), Token.Identifier("T2", SourceSpan.Default)],
                    [],
                    null,
                    new Block([], [])
                )
            ], [], [])),
            ("fn MyFn<T1, T2, T3>() {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [
                        Token.Identifier("T1", SourceSpan.Default), Token.Identifier("T2", SourceSpan.Default),
                        Token.Identifier("T3", SourceSpan.Default)
                    ],
                    [],
                    null,
                    new Block([], [])
                )
            ], [], [])),
            ("fn MyFn(a: result::<int, MyType>) {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [
                        new FunctionParameter(new TypeIdentifier(
                            Token.Result(SourceSpan.Default), [
                                new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                                new TypeIdentifier(Token.Identifier("MyType", SourceSpan.Default), [], SourceRange.Default)
                            ], SourceRange.Default), null, Token.Identifier("a", SourceSpan.Default))
                    ],
                    null,
                    new Block([], [])
                )
            ], [], [])),
            ("fn MyFn(a: int, b: MyType) {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [
                        new FunctionParameter(new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default), null,
                            Token.Identifier("a", SourceSpan.Default)),
                        new FunctionParameter(new TypeIdentifier(Token.Identifier("MyType", SourceSpan.Default), [], SourceRange.Default),
                            null, Token.Identifier("b", SourceSpan.Default))
                    ],
                    null,
                    new Block([], [])
                )
            ], [], [])),
            ("fn MyFn(): int {return 1;}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [],
                    new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                    new Block(
                    [
                        new MethodReturnExpression(new MethodReturn(new ValueAccessorExpression(
                            new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default)))), SourceRange.Default)
                    ], [])
                )
            ], [], [])),
            ("class MyClass {}", new LangProgram([], [], [
                new ProgramClass(
                    null,
                    Token.Identifier("MyClass", SourceSpan.Default),
                    [],
                    [],
                    [])
            ], [])),
            ("class MyClass<T> {}", new LangProgram([], [], [
                new ProgramClass(
                    null,
                    Token.Identifier("MyClass", SourceSpan.Default),
                    [Token.Identifier("T", SourceSpan.Default)],
                    [],
                    [])
            ], [])),
            ("class MyClass<T, T2, T3> {}", new LangProgram([], [], [
                new ProgramClass(
                    null,
                    Token.Identifier("MyClass", SourceSpan.Default),
                    [
                        Token.Identifier("T", SourceSpan.Default), Token.Identifier("T2", SourceSpan.Default),
                        Token.Identifier("T3", SourceSpan.Default)
                    ],
                    [],
                    [])
            ], [])),
            ("pub class MyClass {}", new LangProgram([], [], [
                new ProgramClass(
                    new AccessModifier(Token.Pub(SourceSpan.Default)),
                    Token.Identifier("MyClass", SourceSpan.Default),
                    [],
                    [],
                    [])
            ], [])),
            ("class MyClass {pub mut field MyField: string,}", new LangProgram([], [], [
                new ProgramClass(
                    null,
                    Token.Identifier("MyClass", SourceSpan.Default),
                    [],
                    [],
                    [
                        new ClassField(
                            new AccessModifier(Token.Pub(SourceSpan.Default)),
                            null,
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Token.Identifier("MyField", SourceSpan.Default),
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default), null)
                    ])
            ], [])),
            ("class MyClass {pub static mut field MyField: string,}", new LangProgram([], [], [
                new ProgramClass(
                    null,
                    Token.Identifier("MyClass", SourceSpan.Default),
                    [],
                    [],
                    [
                        new ClassField(
                            new AccessModifier(Token.Pub(SourceSpan.Default)),
                            new StaticModifier(Token.Static(SourceSpan.Default)),
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Token.Identifier("MyField", SourceSpan.Default),
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default), null)
                    ])
            ], [])),
            ("class MyClass {mut field MyField: string,}", new LangProgram([], [], [
                new ProgramClass(
                    null,
                    Token.Identifier("MyClass", SourceSpan.Default),
                    [],
                    [],
                    [
                        new ClassField(
                            null,
                            null,
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Token.Identifier("MyField", SourceSpan.Default),
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default), null)
                    ])
            ], [])),
            ("class MyClass {field MyField: string,}", new LangProgram([], [], [
                new ProgramClass(
                    null,
                    Token.Identifier("MyClass", SourceSpan.Default),
                    [],
                    [],
                    [
                        new ClassField(
                            null,
                            null,
                            null,
                            Token.Identifier("MyField", SourceSpan.Default),
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default), null)
                    ])
            ], [])),
            ("class MyClass {pub field MyField: string,}", new LangProgram([], [], [
                new ProgramClass(
                    null,
                    Token.Identifier("MyClass", SourceSpan.Default),
                    [],
                    [],
                    [
                        new ClassField(
                            new AccessModifier(Token.Pub(SourceSpan.Default)),
                            null,
                            null,
                            Token.Identifier("MyField", SourceSpan.Default),
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default), null)
                    ])
            ], [])),
            ("class MyClass {pub mut field MyField: string, pub fn MyFn() {},}", new LangProgram([], [], [
                new ProgramClass(
                    null,
                    Token.Identifier("MyClass", SourceSpan.Default),
                    [],
                    [
                        new LangFunction(new AccessModifier(Token.Pub(SourceSpan.Default)), null,
                            Token.Identifier("MyFn", SourceSpan.Default), [], [], null, new Block([], []))
                    ],
                    [
                        new ClassField(
                            new AccessModifier(Token.Pub(SourceSpan.Default)),
                            null,
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Token.Identifier("MyField", SourceSpan.Default),
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default), null)
                    ])
            ], [])),
            ("class MyClass {field MyField: string, fn MyFn() {}}", new LangProgram([], [], [
                new ProgramClass(
                    null,
                    Token.Identifier("MyClass", SourceSpan.Default),
                    [],
                    [
                        new LangFunction(null, null, Token.Identifier("MyFn", SourceSpan.Default), [], [], null,
                            new Block([], []))
                    ],
                    [
                        new ClassField(
                            null,
                            null,
                            null,
                            Token.Identifier("MyField", SourceSpan.Default),
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default), null)
                    ])
            ], [])),
            ("pub fn DoSomething(a: int): result::<int, string> {}", new LangProgram(
                [],
                [
                    new LangFunction(
                        new AccessModifier(Token.Pub(SourceSpan.Default)),
                        null,
                        Token.Identifier("DoSomething", SourceSpan.Default),
                        [],
                        [
                            new FunctionParameter(new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default), null,
                                Token.Identifier("a", SourceSpan.Default))
                        ],
                        new TypeIdentifier(Token.Result(SourceSpan.Default),
                        [
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default)
                        ], SourceRange.Default),
                        new Block([], []))
                ],
                [], [])),
            (
                "class MyClass { static field someField: int = 3, }",
                new LangProgram(
                    [],
                    [],
                    [
                        new ProgramClass(
                            null,
                            Token.Identifier("MyClass", SourceSpan.Default),
                            [],
                            [],
                            [
                                new ClassField(
                                    null,
                                    new StaticModifier(Token.Static(SourceSpan.Default)),
                                    null,
                                    Token.Identifier("someField", SourceSpan.Default),
                                    new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                        Token.IntLiteral(3, SourceSpan.Default))))
                            ]
                        )
                    ], [])
            ),
            ("""
             pub fn DoSomething(a: int): result::<int, string> {
                 var b: int = 2;
                 
                 if (a > b) {
                     return ok(a);
                 }
                 else if (a == b) {
                     return ok(b);
                 }
                 else {
                 }
             
                 b = 3;
             
                 var thing = new Class2 {
                     A = 3
                 };
             
                 MyClass::StaticMethod();
             
                 PrivateFn::<string>();
             
                 return error("something wrong");
             }

             fn PrivateFn<T>() {
                 fn InnerFn() {
                     Println("Something");
                 }
                 Println("Message");
             }

             pub fn SomethingElse(a: int): result::<int, string> {
                 var b = DoSomething(a)?;
                 var mut c = 2;
                 
                 return b;
             }

             Println(DoSomething(5));
             Println(DoSomething(1));
             Println(SomethingElse(1));

             pub class MyClass {
                 pub fn PublicMethod() {
                 }
             
                 pub static fn StaticMethod() {
             
                 }
                 
                 field FieldA: string,
                 mut field FieldB: string,
                 pub mut field FieldC: string,
                 pub field FieldD: string,
             }

             pub class GenericClass<T> {
                 pub fn PublicMethod<T1>() {
                 }
             }

             pub class Class2 {
                 pub field A: string,
             }
             """, new LangProgram(
                [
                    new MethodCallExpression(new MethodCall(VariableAccessor("Println"),
                    [
                        new MethodCallExpression(new MethodCall(VariableAccessor("DoSomething"),
                        [
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                Token.IntLiteral(5, SourceSpan.Default)))
                        ]), SourceRange.Default)
                    ]), SourceRange.Default),
                    new MethodCallExpression(new MethodCall(VariableAccessor("Println"),
                    [
                        new MethodCallExpression(new MethodCall(VariableAccessor("DoSomething"),
                        [
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                Token.IntLiteral(1, SourceSpan.Default)))
                        ]), SourceRange.Default)
                    ]), SourceRange.Default),
                    new MethodCallExpression(new MethodCall(VariableAccessor("Println"),
                    [
                        new MethodCallExpression(new MethodCall(VariableAccessor("SomethingElse"),
                        [
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                Token.IntLiteral(1, SourceSpan.Default)))
                        ]), SourceRange.Default)
                    ]), SourceRange.Default)
                ],
                [
                    new LangFunction(
                        new AccessModifier(Token.Pub(SourceSpan.Default)),
                        null,
                        Token.Identifier("DoSomething", SourceSpan.Default),
                        [],
                        [
                            new FunctionParameter(new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default), null,
                                Token.Identifier("a", SourceSpan.Default))
                        ],
                        new TypeIdentifier(Token.Result(SourceSpan.Default),
                        [
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default)
                        ], SourceRange.Default),
                        new Block(
                            [
                                new VariableDeclarationExpression(new VariableDeclaration(
                                    Token.Identifier("b", SourceSpan.Default),
                                    null,
                                    new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                        Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default),
                                new IfExpressionExpression(new IfExpression(
                                    new BinaryOperatorExpression(new BinaryOperator(
                                        BinaryOperatorType.GreaterThan,
                                        VariableAccessor("a"),
                                        VariableAccessor("b"),
                                        Token.RightAngleBracket(SourceSpan.Default))),
                                    new BlockExpression(new Block(
                                        [
                                            new MethodReturnExpression(new MethodReturn(
                                                    new MethodCallExpression(new MethodCall(
                                                        new ValueAccessorExpression(
                                                            new ValueAccessor(ValueAccessType.Variable,
                                                                Token.Ok(SourceSpan.Default))),
                                                        [VariableAccessor("a")]), SourceRange.Default)
                                                )
                                            , SourceRange.Default)
                                        ],
                                        []), SourceRange.Default),
                                    [
                                        new ElseIf(
                                            new BinaryOperatorExpression(new BinaryOperator(
                                                BinaryOperatorType.EqualityCheck, VariableAccessor("a"),
                                                VariableAccessor("b"), Token.DoubleEquals(SourceSpan.Default))),
                                            new BlockExpression(new Block([
                                                new MethodReturnExpression(new MethodReturn(
                                                        new MethodCallExpression(new MethodCall(
                                                            new ValueAccessorExpression(
                                                                new ValueAccessor(ValueAccessType.Variable,
                                                                    Token.Ok(SourceSpan.Default))),
                                                            [VariableAccessor("b")]), SourceRange.Default)
                                                    )
                                                , SourceRange.Default)
                                            ], []), SourceRange.Default)
                                        )
                                    ],
                                    new BlockExpression(new Block([], []), SourceRange.Default)
                                ), SourceRange.Default),
                                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                                    VariableAccessor("b"),
                                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                        Token.IntLiteral(3, SourceSpan.Default))), Token.Equals(SourceSpan.Default))),
                                new VariableDeclarationExpression(new VariableDeclaration(
                                    Token.Identifier("thing", SourceSpan.Default),
                                    null,
                                    null,
                                    new ObjectInitializerExpression(new ObjectInitializer(
                                        new TypeIdentifier(Token.Identifier("Class2", SourceSpan.Default), [], SourceRange.Default),
                                        [
                                            new FieldInitializer(Token.Identifier("A", SourceSpan.Default),
                                                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                                    Token.IntLiteral(3, SourceSpan.Default))))
                                        ]), SourceRange.Default)), SourceRange.Default),
                                new MethodCallExpression(new MethodCall(
                                    new StaticMemberAccessExpression(new StaticMemberAccess(
                                        new TypeIdentifier(Token.Identifier("MyClass", SourceSpan.Default), [], SourceRange.Default),
                                        Token.Identifier("StaticMethod", SourceSpan.Default)
                                    )),
                                    []), SourceRange.Default),
                                new MethodCallExpression(new MethodCall(
                                    new GenericInstantiationExpression(new GenericInstantiation(
                                        new ValueAccessorExpression(
                                            new ValueAccessor(ValueAccessType.Variable,
                                                Token.Identifier("PrivateFn", SourceSpan.Default))),
                                        [new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default)]), SourceRange.Default),
                                    []
                                ), SourceRange.Default),
                                new MethodReturnExpression(new MethodReturn(
                                    new MethodCallExpression(new MethodCall(
                                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                                            Token.Error(SourceSpan.Default))),
                                        [
                                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                                Token.StringLiteral("something wrong", SourceSpan.Default)))
                                        ]
                                    ), SourceRange.Default)), SourceRange.Default)
                            ],
                            [])),
                    new LangFunction(
                        null,
                        null,
                        Token.Identifier("PrivateFn", SourceSpan.Default),
                        [Token.Identifier("T", SourceSpan.Default)],
                        [],
                        null,
                        new Block(
                            [
                                new MethodCallExpression(
                                    new MethodCall(VariableAccessor("Println"),
                                    [
                                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                            Token.StringLiteral("Message", SourceSpan.Default)))
                                    ]), SourceRange.Default)
                            ],
                            [
                                new LangFunction(
                                    null,
                                    null,
                                    Token.Identifier("InnerFn", SourceSpan.Default),
                                    [],
                                    [],
                                    null,
                                    new Block(
                                        [
                                            new MethodCallExpression(
                                                new MethodCall(VariableAccessor("Println"),
                                                [
                                                    new ValueAccessorExpression(
                                                        new ValueAccessor(ValueAccessType.Literal,
                                                            Token.StringLiteral("Something", SourceSpan.Default)))
                                                ]), SourceRange.Default)
                                        ],
                                        [
                                        ]))
                            ])),
                    new LangFunction(
                        new AccessModifier(Token.Pub(SourceSpan.Default)),
                        null,
                        Token.Identifier("SomethingElse", SourceSpan.Default),
                        [],
                        [
                            new FunctionParameter(new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default), null,
                                Token.Identifier("a", SourceSpan.Default))
                        ],
                        new TypeIdentifier(Token.Result(SourceSpan.Default),
                        [
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default)
                        ], SourceRange.Default),
                        new Block(
                            [
                                new VariableDeclarationExpression(new VariableDeclaration(
                                    Token.Identifier("b", SourceSpan.Default),
                                    null,
                                    null,
                                    new UnaryOperatorExpression(new UnaryOperator(
                                        UnaryOperatorType.FallOut,
                                        new MethodCallExpression(new MethodCall(
                                            VariableAccessor("DoSomething"),
                                            [
                                                VariableAccessor("a")
                                            ]), SourceRange.Default),
                                        Token.QuestionMark(SourceSpan.Default)))), SourceRange.Default),
                                new VariableDeclarationExpression(new VariableDeclaration(
                                    Token.Identifier("c", SourceSpan.Default),
                                    new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                                    null,
                                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                        Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default),
                                new MethodReturnExpression(new MethodReturn(VariableAccessor("b")), SourceRange.Default)
                            ],
                            [])
                    )
                ],
                [
                    new ProgramClass(
                        new AccessModifier(Token.Pub(SourceSpan.Default)),
                        Token.Identifier("MyClass", SourceSpan.Default),
                        [],
                        [
                            new LangFunction(
                                new AccessModifier(Token.Pub(SourceSpan.Default)),
                                null,
                                Token.Identifier("PublicMethod", SourceSpan.Default),
                                [],
                                [],
                                null,
                                new Block([], [])),
                            new LangFunction(
                                new AccessModifier(Token.Pub(SourceSpan.Default)),
                                new StaticModifier(Token.Static(SourceSpan.Default)),
                                Token.Identifier("StaticMethod", SourceSpan.Default),
                                [],
                                [],
                                null,
                                new Block([], []))
                        ],
                        [
                            new ClassField(null,
                                null,
                                null,
                                Token.Identifier("FieldA", SourceSpan.Default),
                                new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default), null),
                            new ClassField(null,
                                null,
                                new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                                Token.Identifier("FieldB", SourceSpan.Default),
                                new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default), null),
                            new ClassField(new AccessModifier(Token.Pub(SourceSpan.Default)),
                                null,
                                new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                                Token.Identifier("FieldC", SourceSpan.Default),
                                new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default), null),
                            new ClassField(new AccessModifier(Token.Pub(SourceSpan.Default)),
                                null,
                                null,
                                Token.Identifier("FieldD", SourceSpan.Default),
                                new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default), null)
                        ]),
                    new ProgramClass(
                        new AccessModifier(Token.Pub(SourceSpan.Default)),
                        Token.Identifier("GenericClass", SourceSpan.Default),
                        [Token.Identifier("T", SourceSpan.Default)],
                        [
                            new LangFunction(
                                new AccessModifier(Token.Pub(SourceSpan.Default)),
                                null,
                                Token.Identifier("PublicMethod", SourceSpan.Default),
                                [Token.Identifier("T1", SourceSpan.Default)],
                                [],
                                null,
                                new Block([], []))
                        ],
                        []
                    ),
                    new ProgramClass(
                        new AccessModifier(Token.Pub(SourceSpan.Default)),
                        Token.Identifier("Class2", SourceSpan.Default),
                        [],
                        [],
                        [
                            new ClassField(new AccessModifier(Token.Pub(SourceSpan.Default)),
                                null,
                                null,
                                Token.Identifier("A", SourceSpan.Default),
                                new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default), null)
                        ]
                    )
                ], []))
        }.Select(x => new object[] { x.Source, Tokenizer.Tokenize(x.Source), x.ExpectedProgram });
    }

    public static IEnumerable<object[]> FailTestCases()
    {
        IEnumerable<string> strings =
        [
            // missing variable declaration value 
            "var a = ",
            // missing variable declaration equals
            "var a ",
            // missing variable declaration name
            "var",
            // missing if pieces
            "if {}",
            "if () {}",
            "if (a {}",
            "if (a)",
            "if",
            // else without else body
            "if (a) {} else",
            // else if without check expression
            "if (a) {} else if",
            "if (a) {} else if (",
            "if (a) {} else if ()",
            // else if without body
            "if (a) {} else if (a)",
            // else without body
            "if (a) {} else if (a) {} else",
            // else before else if
            "if (a) {} else {} else if (a) {}",
            "if (a;) {}",
            // body has tail expression but else doesn't
            "a(",
            "a<string>()",
            "a::<,>()",
            "a::<string string>()",
            "a::<string()",
            "a(,)",
            "a(a b)",
            "a(a; b)",
            // missing semicolon,
            "{var a = 1 var b = 2;}",
            "{",
            "}",
            "?",
            "+",
            ">",
            "<",
            "*",
            "/",
            "-",
            // invalid statement
            "a;",
            "{a;}",
            "fn MyFn::<string>(){}",
            "fn MyFn<>(){}",
            "fn MyFn<,>(){}",
            "fn MyFn<A B>(){}",
            "fn MyFn<string>(){}",
            "fn MyFunction() {",
            "fn MyFunction()",
            "fn a MyFunction() {}",
            "fn MyFunction",
            "fn MyFunction(",
            "fn MyFunction(int) {}",
            "fn MyFunction(a:) {}",
            "fn MyFunction(a) {}",
            "fn MyFunction(a: result<int>) {}",
            "fn MyFunction(a: result::<,>) {}",
            "fn MyFunction(a: result::<int int>) {}",
            "fn MyFunction(,) {}",
            "fn MyFunction(a: int b: int) {}",
            // no semicolon
            "return 1",
            "pub MyClass {}",
            "class MyClass<> {}",
            "class MyClass<,> {}",
            "class MyClass<T1 T2> {}",
            "pub mut class MyClass {}",
            "static class MyClass {}",
            "class pub MyClass {}",
            "class MyClass {field myFieldWithoutSemicolon}",
            "class MyClass {field mut myField;}",
            "class MyClass {fieldName}",
            "class MyClass {field pub myField;}",
            "class MyClass {fn MyFnWithSemicolon{};}",
            "class MyClass {fn FnWithoutBody}",
            "class MyClass { class InnerClassAreNotAllowed {} }",
            "class MyClass { static field someField: int =; }",
            "class MyClass { union MyUnion {}}",
            "{union MyUnion{}}",
            "fn MyFn(){union MyUnion{}}",
            "fn SomeFn() { class NoClassesInFunctions {}}",
            "new",
            "new Thing",
            "new Thing {",
            "new Thing{ a }",
            "new Thing{ a = }",
            "new Thing{ a = 1 b = 2 }",
            "new Thing { , }",
            "new Thing { , a = 1 }",
            "union MyUnion",
            "union MyUnion {",
            "union MyUnion { A(}",
            "union MyUnion { A {}",
            "union MyUnion { A { field}}",
            "union MyUnion { A B }",
            "union MyUnion< {}",
            "a matches",
            "a matches B {",
            "a matches B { field }",
            "a matches B { SomeField: }",
            "a matches B { SomeField OtherField }",
            "a matches B { SomeField: var field }",
            "a matches B(",
            "a matches B(,)",
            "a matches B{_, _}",
            "(",
            "(a",
            "(a b)",
            "()"
        ];
        return strings.Select(x => new object[] { x, Tokenizer.Tokenize(x) });
    }

    public static IEnumerable<object[]> SingleTestCase()
    {
        return new (string Source, IExpression ExpectedProgram)[]
        {
            (
                """
                match (a) {
                    _ => b,
                }
                """,
                new MatchExpression(VariableAccessor("a"), [new MatchArm(new DiscardPattern(SourceRange.Default), VariableAccessor("b"))], SourceRange.Default)
            )
        }.Select(x => new object[] { x.Source, Tokenizer.Tokenize(x.Source), x.ExpectedProgram });
    }

    public static IEnumerable<object[]> PopExpressionTestCases()
    {
        return new (string Source, IExpression ExpectedExpression)[]
        {
            (
                "todo!",
                new ValueAccessorExpression(new ValueAccessor(
                    ValueAccessType.Variable, Token.Todo(SourceSpan.Default)))
            ),
            (
                """
                match (a) {
                    _ => b,
                }
                """,
                new MatchExpression(VariableAccessor("a"), [new MatchArm(new DiscardPattern(SourceRange.Default), VariableAccessor("b"))], SourceRange.Default)
            ),
            (
                """
                match (a) {
                    SomeClass => b,
                    _ => b
                }
                """,
                new MatchExpression(VariableAccessor("a"), [
                    new MatchArm(
                        new ClassPattern(new TypeIdentifier(Token.Identifier("SomeClass", SourceSpan.Default), [], SourceRange.Default),
                            [],
                            false,
                            null, SourceRange.Default),
                        VariableAccessor("b")),
                    new MatchArm(new DiscardPattern(SourceRange.Default), VariableAccessor("b"))
                ], SourceRange.Default)
            ),
            (
                """
                match (a) {
                    SomeClass { SomeField } => b,
                }
                """,
                new MatchExpression(VariableAccessor("a"), [
                    new MatchArm(
                        new ClassPattern(new TypeIdentifier(Token.Identifier("SomeClass", SourceSpan.Default), [], SourceRange.Default),
                            [KeyValuePair.Create(Token.Identifier("SomeField", SourceSpan.Default), (IPattern?)null)],
                            false,
                            null, SourceRange.Default),
                        VariableAccessor("b")),
                    new MatchArm(new DiscardPattern(SourceRange.Default), VariableAccessor("b"))
                ], SourceRange.Default)
            ),
            (
                """
                match (a) {
                    SomeUnion::B { SomeField: OtherUnion::C { OtherField: var d } } => d
                }
                """,
                new MatchExpression(VariableAccessor("a"), [
                    new MatchArm(
                        new UnionStructVariantPattern(
                            new TypeIdentifier(Token.Identifier("SomeUnion", SourceSpan.Default), [], SourceRange.Default),
                            Token.Identifier("B", SourceSpan.Default),
                            [
                                KeyValuePair.Create(
                                    Token.Identifier("SomeField", SourceSpan.Default),
                                    (IPattern?)new UnionStructVariantPattern(
                                        new TypeIdentifier(Token.Identifier("OtherUnion", SourceSpan.Default), [], SourceRange.Default),
                                        Token.Identifier("C", SourceSpan.Default),
                                        [
                                            KeyValuePair.Create(
                                                Token.Identifier("OtherField", SourceSpan.Default),
                                                (IPattern?)new VariableDeclarationPattern(
                                                    Token.Identifier("d", SourceSpan.Default), SourceRange.Default)
                                            )
                                        ],
                                        false,
                                        null, SourceRange.Default))
                            ],
                            false,
                            null, SourceRange.Default),
                        VariableAccessor("d")),
                    new MatchArm(new DiscardPattern(SourceRange.Default), VariableAccessor("b"))
                ], SourceRange.Default)
            ),
            (
                "if (a matches OtherUnion::B(MyUnion::A var c) var b) {}",
                new IfExpressionExpression(new IfExpression(
                    new MatchesExpression(
                        VariableAccessor("a"),
                        new UnionTupleVariantPattern(
                            new TypeIdentifier(Token.Identifier("OtherUnion", SourceSpan.Default), [], SourceRange.Default),
                            Token.Identifier("B", SourceSpan.Default),
                            [
                                new UnionVariantPattern(
                                    new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                                    Token.Identifier("A", SourceSpan.Default),
                                    Token.Identifier("c", SourceSpan.Default), SourceRange.Default)
                            ],
                            Token.Identifier("b", SourceSpan.Default), SourceRange.Default), SourceRange.Default),
                    new BlockExpression(new Block([], []), SourceRange.Default),
                    [],
                    null), SourceRange.Default)
            ),
            (
                "var b: bool = a matches int;",
                new VariableDeclarationExpression(new VariableDeclaration(
                    Token.Identifier("b", SourceSpan.Default),
                    null,
                    new TypeIdentifier(Token.Bool(SourceSpan.Default), [], SourceRange.Default),
                    new MatchesExpression(
                        VariableAccessor("a"),
                        new ClassPattern(
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                            [],
                            false,
                            null
                        , SourceRange.Default)
                    , SourceRange.Default))
                , SourceRange.Default)
            ),
            (
                "a matches string",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new ClassPattern(
                        new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                        [],
                        false,
                        null
                    , SourceRange.Default)
                , SourceRange.Default)
            ),
            (
                "a matches MyUnion::A",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionVariantPattern(
                        new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("A", SourceSpan.Default),
                        null
                    , SourceRange.Default)
                , SourceRange.Default)
            ),
            (
                "a matches var a",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new VariableDeclarationPattern(Token.Identifier("a", SourceSpan.Default), SourceRange.Default)
                , SourceRange.Default)
            ),
            (
                "a matches _",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new DiscardPattern(SourceRange.Default)
                , SourceRange.Default)
            ),
            (
                "a matches MyUnion::A(var b)",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionTupleVariantPattern(
                        new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("A", SourceSpan.Default),
                        [
                            new VariableDeclarationPattern(Token.Identifier("b", SourceSpan.Default), SourceRange.Default)
                        ],
                        null, SourceRange.Default)
                , SourceRange.Default)
            ),
            (
                "a matches MyUnion::A(var b) var c",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionTupleVariantPattern(
                        new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("A", SourceSpan.Default),
                        [
                            new VariableDeclarationPattern(Token.Identifier("b", SourceSpan.Default), SourceRange.Default)
                        ],
                        Token.Identifier("c", SourceSpan.Default), SourceRange.Default)
                , SourceRange.Default)
            ),
            (
                "a matches MyUnion::A(var b, var c, _)",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionTupleVariantPattern(
                        new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("A", SourceSpan.Default),
                        [
                            new VariableDeclarationPattern(Token.Identifier("b", SourceSpan.Default), SourceRange.Default),
                            new VariableDeclarationPattern(Token.Identifier("c", SourceSpan.Default), SourceRange.Default),
                            new DiscardPattern(SourceRange.Default)
                        ], null, SourceRange.Default)
                , SourceRange.Default)
            ),
            (
                "a matches MyUnion::A(OtherUnion::C)",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionTupleVariantPattern(
                        new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("A", SourceSpan.Default),
                        [
                            new UnionVariantPattern(
                                new TypeIdentifier(Token.Identifier("OtherUnion", SourceSpan.Default), [], SourceRange.Default),
                                Token.Identifier("C", SourceSpan.Default),
                                null
                            , SourceRange.Default)
                        ],
                        null, SourceRange.Default)
                , SourceRange.Default)
            ),
            (
                "a matches MyUnion::A(OtherUnion::C var c)",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionTupleVariantPattern(
                        new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("A", SourceSpan.Default),
                        [
                            new UnionVariantPattern(
                                new TypeIdentifier(Token.Identifier("OtherUnion", SourceSpan.Default), [], SourceRange.Default),
                                Token.Identifier("C", SourceSpan.Default),
                                Token.Identifier("c", SourceSpan.Default)
                            , SourceRange.Default)
                        ],
                        null, SourceRange.Default)
                , SourceRange.Default)
            ),
            (
                "a matches MyUnion::A(OtherUnion::C(var d))",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionTupleVariantPattern(
                        new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("A", SourceSpan.Default),
                        [
                            new UnionTupleVariantPattern(
                                new TypeIdentifier(Token.Identifier("OtherUnion", SourceSpan.Default), [], SourceRange.Default),
                                Token.Identifier("C", SourceSpan.Default),
                                [new VariableDeclarationPattern(Token.Identifier("d", SourceSpan.Default), SourceRange.Default)],
                                null
                            , SourceRange.Default)
                        ],
                        null, SourceRange.Default)
                , SourceRange.Default)
            ),
            (
                "a matches MyUnion::A { MyField }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionStructVariantPattern(
                        new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("A", SourceSpan.Default),
                        [
                            KeyValuePair.Create(Token.Identifier("MyField", SourceSpan.Default), (IPattern?)null)
                        ],
                        false,
                        null
                    , SourceRange.Default), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A { MyField } var a",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionStructVariantPattern(
                        new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("A", SourceSpan.Default),
                        [
                            KeyValuePair.Create(Token.Identifier("MyField", SourceSpan.Default), (IPattern?)null)
                        ],
                        false,
                        Token.Identifier("a", SourceSpan.Default)
                    , SourceRange.Default), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A { MyField, OtherField: var f }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionStructVariantPattern(
                        new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("A", SourceSpan.Default),
                        [
                            KeyValuePair.Create(Token.Identifier("MyField", SourceSpan.Default), (IPattern?)null),
                            KeyValuePair.Create(
                                Token.Identifier("OtherField", SourceSpan.Default),
                                (IPattern?)new VariableDeclarationPattern(Token.Identifier("f", SourceSpan.Default), SourceRange.Default)
                            )
                        ],
                        false,
                        null
                    , SourceRange.Default), SourceRange.Default)
            ),
            (
                "a matches MyClass { MyField }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new ClassPattern(
                        new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                        [
                            KeyValuePair.Create(Token.Identifier("MyField", SourceSpan.Default), (IPattern?)null)
                        ],
                        false,
                        null
                    , SourceRange.Default), SourceRange.Default)
            ),
            (
                "a matches MyClass { MyField } var a",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new ClassPattern(
                        new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                        [
                            KeyValuePair.Create(Token.Identifier("MyField", SourceSpan.Default), (IPattern?)null)
                        ],
                        false,
                        Token.Identifier("a", SourceSpan.Default)
                    , SourceRange.Default), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A { MyField, _ }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionStructVariantPattern(
                        new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("A", SourceSpan.Default),
                        [
                            KeyValuePair.Create(Token.Identifier("MyField", SourceSpan.Default), (IPattern?)null)
                        ],
                        true,
                        null
                    , SourceRange.Default), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A { MyField: MyUnion::B var f }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionStructVariantPattern(
                        new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("A", SourceSpan.Default),
                        [
                            KeyValuePair.Create(
                                Token.Identifier("MyField", SourceSpan.Default),
                                (IPattern?)new UnionVariantPattern(
                                    new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                                    Token.Identifier("B", SourceSpan.Default),
                                    Token.Identifier("f", SourceSpan.Default), SourceRange.Default))
                        ],
                        true,
                        null
                    , SourceRange.Default), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A { MyField: MyUnion::B(var c)  }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionStructVariantPattern(
                        new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("A", SourceSpan.Default),
                        [
                            KeyValuePair.Create(
                                Token.Identifier("MyField", SourceSpan.Default),
                                (IPattern?)new UnionTupleVariantPattern(
                                    new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [], SourceRange.Default),
                                    Token.Identifier("B", SourceSpan.Default),
                                    [new VariableDeclarationPattern(Token.Identifier("c", SourceSpan.Default), SourceRange.Default)],
                                    null, SourceRange.Default))
                        ],
                        true,
                        null
                    , SourceRange.Default), SourceRange.Default)
            ),
            (
                "a matches MyClass { MyField, _ }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new ClassPattern(
                        new TypeIdentifier(Token.Identifier("MyClass", SourceSpan.Default), [], SourceRange.Default),
                        [
                            KeyValuePair.Create(Token.Identifier("MyField", SourceSpan.Default), (IPattern?)null)
                        ],
                        true,
                        null
                    , SourceRange.Default)
                , SourceRange.Default)
            ),
            (
                "a matches MyClass",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new ClassPattern(
                        new TypeIdentifier(Token.Identifier("MyClass", SourceSpan.Default), [], SourceRange.Default),
                        [],
                        false,
                        null
                    , SourceRange.Default)
                , SourceRange.Default)
            ),
            (
                "a matches MyClass var b",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new ClassPattern(
                        new TypeIdentifier(Token.Identifier("MyClass", SourceSpan.Default), [], SourceRange.Default),
                        [],
                        false,
                        Token.Identifier("b", SourceSpan.Default)
                    , SourceRange.Default)
                , SourceRange.Default)
            ),


            // value access expressions
            ("a",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                    Token.Identifier("a", SourceSpan.Default)))),
            ("this",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                    Token.This(SourceSpan.Default)))),
            ("1",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(1, SourceSpan.Default)))),
            ("\"my string\"",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.StringLiteral("my string", SourceSpan.Default)))),
            ("true",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.True(SourceSpan.Default)))),
            ("false",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.False(SourceSpan.Default)))),
            ("ok",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Ok(SourceSpan.Default)))),
            ("a == b",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.EqualityCheck, VariableAccessor("a"),
                    VariableAccessor("b"), Token.DoubleEquals(SourceSpan.Default)))),
            ("ok()",
                new MethodCallExpression(new MethodCall(
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Ok(SourceSpan.Default))), []), SourceRange.Default)),
            ("(a)", new TupleExpression([VariableAccessor("a")], SourceRange.Default)),
            ("(a, b)", new TupleExpression([VariableAccessor("a"), VariableAccessor("b")], SourceRange.Default)),
            ("!a", new UnaryOperatorExpression(new UnaryOperator(
                UnaryOperatorType.Not,
                VariableAccessor("a"),
                Token.Bang(SourceSpan.Default)))),
            ("a?", new UnaryOperatorExpression(new UnaryOperator(
                UnaryOperatorType.FallOut,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                    Token.Identifier("a", SourceSpan.Default))),
                Token.QuestionMark(SourceSpan.Default)))),
            ("a??",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        Token.QuestionMark(SourceSpan.Default))),
                    Token.QuestionMark(SourceSpan.Default)))
            ),
            ("return 1", new MethodReturnExpression(
                new MethodReturn(new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(1, SourceSpan.Default)))), SourceRange.Default)),
            ("return", new MethodReturnExpression(new MethodReturn(null), SourceRange.Default)),
            // binary operator expressions
            ("a < 5", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.LessThan,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                    Token.Identifier("a", SourceSpan.Default))),
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(5, SourceSpan.Default))),
                Token.LeftAngleBracket(SourceSpan.Default)))),
            ("\"thing\" > true", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.GreaterThan,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.StringLiteral("thing", SourceSpan.Default))),
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.True(SourceSpan.Default))),
                Token.RightAngleBracket(SourceSpan.Default)))),
            ("a + b", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Plus,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.Plus(SourceSpan.Default)))),
            ("a - b", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Minus,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.Dash(SourceSpan.Default)))),
            ("a * b", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.Star(SourceSpan.Default)))),
            ("a / b", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Divide,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.ForwardSlash(SourceSpan.Default)))),
            ("var a: int = b", new VariableDeclarationExpression(
                new VariableDeclaration(
                    Token.Identifier("a", SourceSpan.Default),
                    null,
                    new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                    VariableAccessor("b")), SourceRange.Default)),
            ("var a: int", new VariableDeclarationExpression(
                new VariableDeclaration(
                    Token.Identifier("a", SourceSpan.Default),
                    null,
                    new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                    null), SourceRange.Default)),
            ("var mut a = 2", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default)),
            ("a = b",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"), VariableAccessor("b"), Token.Equals(SourceSpan.Default)))),
            ("var mut a: int = 2", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default)),
            ("var a: bool = b", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                new TypeIdentifier(Token.Bool(SourceSpan.Default), [], SourceRange.Default),
                VariableAccessor("b")), SourceRange.Default)),
            ("var a: int = b", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                VariableAccessor("b")), SourceRange.Default)),
            ("var a: string = b", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                VariableAccessor("b")), SourceRange.Default)),
            ("var a: result = b", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                new TypeIdentifier(Token.Result(SourceSpan.Default), [], SourceRange.Default),
                VariableAccessor("b")), SourceRange.Default)),
            ("var a: MyType = b", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                new TypeIdentifier(Token.Identifier("MyType", SourceSpan.Default), [], SourceRange.Default),
                VariableAccessor("b")), SourceRange.Default)),
            ("var a = 1", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(1, SourceSpan.Default)))), SourceRange.Default)),
            ("var a = true", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.True(SourceSpan.Default)))), SourceRange.Default)),
            ("var a = \"thing\"", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.StringLiteral("thing", SourceSpan.Default)))), SourceRange.Default)),
            ("{}", new BlockExpression(new Block([], []), SourceRange.Default)),
            ("{var a = 1;}", new BlockExpression(new Block([
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", SourceSpan.Default),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default)))), SourceRange.Default)
            ], []), SourceRange.Default)),
            // tail expression
            ("{var a = 1}", new BlockExpression(new Block(
            [
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", SourceSpan.Default),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default)))), SourceRange.Default)
            ], []), SourceRange.Default)),
            // tail expression
            ("{var a = 1;var b = 2}", new BlockExpression(new Block(
            [
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", SourceSpan.Default),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default)))), SourceRange.Default),
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("b", SourceSpan.Default),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default)
            ], []), SourceRange.Default)),
            ("{var a = 1; var b = 2;}", new BlockExpression(new Block([
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", SourceSpan.Default),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default)))), SourceRange.Default),
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("b", SourceSpan.Default),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default)
            ], []), SourceRange.Default)),
            ("if (a) var c = 2;", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new VariableDeclarationExpression(new VariableDeclaration(
                    Token.Identifier("c", SourceSpan.Default),
                    null,
                    null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default), [], null), SourceRange.Default)),
            ("if (a > b) {var c = \"value\";}", new IfExpressionExpression(new IfExpression(
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    VariableAccessor("a"),
                    VariableAccessor("b"),
                    Token.RightAngleBracket(SourceSpan.Default))),
                new BlockExpression(new Block([
                    new VariableDeclarationExpression(new VariableDeclaration(
                        Token.Identifier("c", SourceSpan.Default),
                        null,
                        null,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                            Token.StringLiteral("value", SourceSpan.Default)))), SourceRange.Default)
                ], []), SourceRange.Default), [], null), SourceRange.Default)),
            ("if (a) {} else {var b = 2;}", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new BlockExpression(new Block([], []), SourceRange.Default),
                [],
                new BlockExpression(new Block([
                    new VariableDeclarationExpression(new VariableDeclaration(
                        Token.Identifier("b", SourceSpan.Default),
                        null,
                        null,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                            Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default)
                ], []), SourceRange.Default)), SourceRange.Default)),
            ("if (a) {} else if (b) {}", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new BlockExpression(new Block([], []), SourceRange.Default),
                [new ElseIf(VariableAccessor("b"), new BlockExpression(new Block([], []), SourceRange.Default))],
                null), SourceRange.Default)),
            ("if (a) {} else if (b) {} else {}", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new BlockExpression(new Block([], []), SourceRange.Default),
                [
                    new ElseIf(VariableAccessor("b"), new BlockExpression(new Block([], []), SourceRange.Default))
                ],
                new BlockExpression(new Block([], []), SourceRange.Default)), SourceRange.Default)),
            ("if (a) {} else if (b) {} else if (c) {} else {}", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new BlockExpression(new Block([], []), SourceRange.Default),
                [
                    new ElseIf(VariableAccessor("b"), new BlockExpression(new Block([], []), SourceRange.Default)),
                    new ElseIf(VariableAccessor("c"), new BlockExpression(new Block([], []), SourceRange.Default))
                ],
                new BlockExpression(new Block([], []), SourceRange.Default)), SourceRange.Default)),
            ("if (a) {b} else {c}", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new BlockExpression(new Block([VariableAccessor("b")], []), SourceRange.Default),
                [],
                new BlockExpression(new Block([VariableAccessor("c")], []), SourceRange.Default)), SourceRange.Default)),
            ("if (a) b else c", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                VariableAccessor("b"),
                [],
                VariableAccessor("c")), SourceRange.Default)),
            ("if (a) {if (b) {1} else {2}} else {3}", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new BlockExpression(new Block([
                    new IfExpressionExpression(new IfExpression(
                        VariableAccessor("b"),
                        new BlockExpression(new Block(
                        [
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                Token.IntLiteral(1, SourceSpan.Default)))
                        ], []), SourceRange.Default),
                        [],
                        new BlockExpression(new Block(
                        [
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                Token.IntLiteral(2, SourceSpan.Default)))
                        ], []), SourceRange.Default)), SourceRange.Default)
                ], []), SourceRange.Default),
                [],
                new BlockExpression(new Block(
                [
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(3, SourceSpan.Default)))
                ], []), SourceRange.Default)), SourceRange.Default)),
            ("if (a) if (b) 1 else 2 else 3", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("b"),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default))),
                    [],
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default),
                [],
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(3, SourceSpan.Default)))), SourceRange.Default)),
            ("var a = if (b) 1 else 2;", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                null,
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("b"),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default))),
                    [],
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default)), SourceRange.Default)),
            ("var a = if (b) {1} else {2};", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                null,
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("b"),
                    new BlockExpression(new Block(
                    [
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                            Token.IntLiteral(1, SourceSpan.Default)))
                    ], []), SourceRange.Default),
                    [],
                    new BlockExpression(new Block(
                    [
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                            Token.IntLiteral(2, SourceSpan.Default)))
                    ], []), SourceRange.Default)), SourceRange.Default)), SourceRange.Default)),
            ("a()", new MethodCallExpression(new MethodCall(VariableAccessor("a"), []), SourceRange.Default)),
            ("a.b::<int>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(new GenericInstantiation(
                    new MemberAccessExpression(new MemberAccess(
                        new ValueAccessorExpression(new ValueAccessor(
                            ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        Token.Identifier("b", SourceSpan.Default)
                    )),
                    []
                ), SourceRange.Default),
                []
            ), SourceRange.Default)),
            ("a::<string>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(new GenericInstantiation(
                    new ValueAccessorExpression(new ValueAccessor(
                        ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    [new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default)]), SourceRange.Default),
                []), SourceRange.Default)),
            ("a::<string, int>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(new GenericInstantiation(new ValueAccessorExpression(
                    new ValueAccessor(
                        ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default)
                    )), [
                    new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                    new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default)
                ]), SourceRange.Default), []), SourceRange.Default)),
            ("a::<string, int, result::<int>>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(new GenericInstantiation(new ValueAccessorExpression(
                    new ValueAccessor(
                        ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default)
                    )), [
                    new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                    new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default),
                    new TypeIdentifier(Token.Result(SourceSpan.Default),
                        [new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default)], SourceRange.Default)
                ]), SourceRange.Default),
                []), SourceRange.Default)),
            ("a(b)", new MethodCallExpression(new MethodCall(VariableAccessor("a"), [
                VariableAccessor("b")
            ]), SourceRange.Default)),
            ("a(b, c)", new MethodCallExpression(new MethodCall(VariableAccessor("a"), [
                VariableAccessor("b"), VariableAccessor("c")
            ]), SourceRange.Default)),
            ("a(b, c > d, e)", new MethodCallExpression(new MethodCall(VariableAccessor("a"), [
                VariableAccessor("b"),
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.GreaterThan, VariableAccessor("c"),
                    VariableAccessor("d"), Token.RightAngleBracket(SourceSpan.Default))),
                VariableAccessor("e")
            ]), SourceRange.Default)),
            ("a.b",
                new MemberAccessExpression(new MemberAccess(VariableAccessor("a"),
                    Token.Identifier("b", SourceSpan.Default)))),
            ("a.b()",
                new MethodCallExpression(new MethodCall(
                    new MemberAccessExpression(new MemberAccess(VariableAccessor("a"),
                        Token.Identifier("b", SourceSpan.Default))), []), SourceRange.Default)),
            ("a?.b", new MemberAccessExpression(new MemberAccess(
                new UnaryOperatorExpression(new UnaryOperator(UnaryOperatorType.FallOut, VariableAccessor("a"),
                    Token.QuestionMark(SourceSpan.Default))),
                Token.Identifier("b", SourceSpan.Default)))),
            ("a.b?", new UnaryOperatorExpression(new UnaryOperator(
                UnaryOperatorType.FallOut,
                new MemberAccessExpression(new MemberAccess(VariableAccessor("a"),
                    Token.Identifier("b", SourceSpan.Default))),
                Token.QuestionMark(SourceSpan.Default)))),
            ("a * b.c", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                VariableAccessor("a"),
                new MemberAccessExpression(new MemberAccess(VariableAccessor("b"),
                    Token.Identifier("c", SourceSpan.Default))),
                Token.Star(SourceSpan.Default)))),
            ("b.c * a", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                new MemberAccessExpression(new MemberAccess(VariableAccessor("b"),
                    Token.Identifier("c", SourceSpan.Default))),
                VariableAccessor("a"),
                Token.Star(SourceSpan.Default)))),
            ("new Thing {}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("Thing", SourceSpan.Default), [], SourceRange.Default),
                []), SourceRange.Default)),
            ("new Thing {A = a}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("Thing", SourceSpan.Default), [], SourceRange.Default),
                [new FieldInitializer(Token.Identifier("A", SourceSpan.Default), VariableAccessor("a"))]), SourceRange.Default)),
            ("myFn(a,)", new MethodCallExpression(new MethodCall(
                new ValueAccessorExpression(new ValueAccessor(
                    ValueAccessType.Variable,
                    Token.Identifier("myFn", SourceSpan.Default))),
                [
                    new ValueAccessorExpression(new ValueAccessor(
                        ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default)))
                ]), SourceRange.Default)),
            ("new SomeType::<string,>{}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("SomeType", SourceSpan.Default), [
                    new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default)
                ], SourceRange.Default),
                []), SourceRange.Default)),
            ("SomeFn::<string,>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(
                    new GenericInstantiation(
                        new ValueAccessorExpression(new ValueAccessor(
                            ValueAccessType.Variable,
                            Token.Identifier("SomeFn", SourceSpan.Default))),
                        [new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default)]), SourceRange.Default),
                []
            ), SourceRange.Default)),
            ("new Thing {A = a,}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("Thing", SourceSpan.Default), [], SourceRange.Default),
                [new FieldInitializer(Token.Identifier("A", SourceSpan.Default), VariableAccessor("a"))]), SourceRange.Default)),
            ("new Thing {A = a, B = b}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("Thing", SourceSpan.Default), [], SourceRange.Default),
                [
                    new FieldInitializer(Token.Identifier("A", SourceSpan.Default), VariableAccessor("a")),
                    new FieldInitializer(Token.Identifier("B", SourceSpan.Default), VariableAccessor("b"))
                ]), SourceRange.Default)),
            ("MyType::CallMethod",
                new StaticMemberAccessExpression(new StaticMemberAccess(
                    new TypeIdentifier(Token.Identifier("MyType", SourceSpan.Default), [], SourceRange.Default),
                    Token.Identifier("CallMethod", SourceSpan.Default)))),
            ("MyType::StaticField.InstanceField", new MemberAccessExpression(
                new MemberAccess(
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("MyType", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("StaticField", SourceSpan.Default))),
                    Token.Identifier("InstanceField", SourceSpan.Default)
                ))),
            ("string::CallMethod",
                new StaticMemberAccessExpression(new StaticMemberAccess(
                    new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                    Token.Identifier("CallMethod", SourceSpan.Default)))),
            ("result::<string>::CallMethod", new StaticMemberAccessExpression(new StaticMemberAccess(
                new TypeIdentifier(Token.Result(SourceSpan.Default),
                    [new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default)], SourceRange.Default),
                Token.Identifier("CallMethod", SourceSpan.Default)))),
            // ____binding strength tests
            // __greater than
            ( // greater than
                "a > b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.RightAngleBracket(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a > b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.RightAngleBracket(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a > b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.Star(SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // divide
                "a > b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // plus
                "a > b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.Plus(SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // minus
                "a > b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.Dash(SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // fallOut
                "a > b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.QuestionMark(SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // value assignment check
                "a > b = c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.ValueAssignment,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.RightAngleBracket(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.Equals(SourceSpan.Default)))
            ),
            ( // equality check
                "a > b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.RightAngleBracket(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a > b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Token.Identifier("c", SourceSpan.Default)
                    )),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // static member access
                "a > b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // not
                "a > !b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    VariableAccessor("a"),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("b"),
                        Token.Bang(SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            // __Less than
            ( // greater than
                "a < b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.LeftAngleBracket(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a < b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.LeftAngleBracket(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a < b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.Star(SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // divide
                "a < b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // plus
                "a < b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.Plus(SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // minus
                "a < b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.Dash(SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // fallOut
                "a < b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.QuestionMark(SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ("a < b = c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new BinaryOperatorExpression(
                        new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.LeftAngleBracket(SourceSpan.Default))),
                    VariableAccessor("c"), Token.Equals(SourceSpan.Default)))),
            ( // equality check
                "a < b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.LeftAngleBracket(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a < b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // static member access
                "a < b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("c", SourceSpan.Default)
                    )),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // not
                "a < !b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    VariableAccessor("a"),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("b"),
                        Token.Bang(SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            // __multiply
            ( // greater than
                "a * b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.Star(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a * b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.Star(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a * b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.Star(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "a * b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.Star(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // plus
                "a * b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.Star(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a * b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.Star(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a * b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    VariableAccessor("a"),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("b"),
                        Token.QuestionMark(SourceSpan.Default))),
                    Token.Star(SourceSpan.Default)))
            ),
            ("a * b = c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new BinaryOperatorExpression(
                        new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Star(SourceSpan.Default))),
                    VariableAccessor("c"), Token.Equals(SourceSpan.Default)))),
            ( // equality check
                "a * b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.Star(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a * b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // static member access
                "a * b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("c", SourceSpan.Default)
                    )),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // not
                "a * !b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    VariableAccessor("a"),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("b"),
                        Token.Bang(SourceSpan.Default))),
                    Token.Star(SourceSpan.Default)))
            ),
            // __divide
            ( // greater than
                "a / b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a / b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a / b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "a / b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // plus
                "a / b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a / b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a / b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.QuestionMark(SourceSpan.Default))),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ("a / b = c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new BinaryOperatorExpression(
                        new BinaryOperator(
                            BinaryOperatorType.Divide,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.ForwardSlash(SourceSpan.Default))),
                    VariableAccessor("c"), Token.Equals(SourceSpan.Default)))),
            ( // equality check
                "a / b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.ForwardSlash(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a / b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // static member access
                "a / b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("c", SourceSpan.Default)
                    )),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // not
                "a / !b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    VariableAccessor("a"),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("b"),
                        Token.Bang(SourceSpan.Default))),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            // __plus
            ( // greater than
                "a + b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.Plus(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a + b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.Plus(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a + b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.Star(SourceSpan.Default))),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // divide
                "a + b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // plus
                "a + b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.Plus(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a + b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.Plus(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a + b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.QuestionMark(SourceSpan.Default))),
                    Token.Plus(SourceSpan.Default)))
            ),
            ("a + b = c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new BinaryOperatorExpression(
                        new BinaryOperator(
                            BinaryOperatorType.Plus,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Plus(SourceSpan.Default))),
                    VariableAccessor("c"), Token.Equals(SourceSpan.Default)))),
            ( // equality check
                "a + b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.Plus(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a + b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // static member access
                "a + b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("c", SourceSpan.Default)
                    )),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // not
                "a + !b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    VariableAccessor("a"),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("b"),
                        Token.Bang(SourceSpan.Default))),
                    Token.Plus(SourceSpan.Default)))
            ),
            // __minus
            ( // greater than
                "a - b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.Dash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a - b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.Dash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a - b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.Star(SourceSpan.Default))),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // divide
                "a - b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // plus
                "a - b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.Dash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a - b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.Dash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a - b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.QuestionMark(SourceSpan.Default))),
                    Token.Dash(SourceSpan.Default)))
            ),
            ("a - b = c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new BinaryOperatorExpression(
                        new BinaryOperator(
                            BinaryOperatorType.Minus,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Dash(SourceSpan.Default))),
                    VariableAccessor("c"), Token.Equals(SourceSpan.Default)))),
            ( // equality check
                "a - b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.Dash(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a - b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // static member access
                "a - b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("c", SourceSpan.Default)
                    )),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // not
                "a - !b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    VariableAccessor("a"),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("b"),
                        Token.Bang(SourceSpan.Default))),
                    Token.Dash(SourceSpan.Default)))
            ),
            // __FallOut
            ( // fallout
                "a??",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(SourceSpan.Default))),
                    Token.QuestionMark(SourceSpan.Default)))
            ),
            ( // less than
                "a? < b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(SourceSpan.Default))),
                    VariableAccessor("b"),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // greater than
                "a? > b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(SourceSpan.Default))),
                    VariableAccessor("b"),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // plus
                "a? + b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(SourceSpan.Default))),
                    VariableAccessor("b"),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a? - b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(SourceSpan.Default))),
                    VariableAccessor("b"),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // multiply
                "a? * b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(SourceSpan.Default))),
                    VariableAccessor("b"),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "a? / b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(SourceSpan.Default))),
                    VariableAccessor("b"),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // assignment
                "a? = c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.ValueAssignment,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.Equals(SourceSpan.Default)))),
            ( // equality check
                "a? == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a?.c",
                new MemberAccessExpression(new MemberAccess(
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(SourceSpan.Default))),
                    Token.Identifier("c", SourceSpan.Default)))
            ),
            ( // not
                "!b?",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.Not,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("b"),
                        Token.QuestionMark(SourceSpan.Default))),
                    Token.Bang(SourceSpan.Default)))
            ),
            // __ value assignment
            ( // greater than
                "a = b > c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.RightAngleBracket(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // less than
                "a = b < c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.LeftAngleBracket(SourceSpan.Default)))
                    , Token.Equals(SourceSpan.Default)))
            ),
            ( // multiply
                "a = b * c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.Star(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // divide
                "a = b / c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.ForwardSlash(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // plus
                "a = b + c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.Plus(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // minus
                "a = b - c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.Dash(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // fallOut
                "a = b?",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.QuestionMark(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // value assignment
                "a = b = c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new BinaryOperatorExpression(
                        new BinaryOperator(BinaryOperatorType.ValueAssignment,
                            VariableAccessor("a"),
                            VariableAccessor("b"), Token.Equals(SourceSpan.Default))),
                    VariableAccessor("c"), Token.Equals(SourceSpan.Default)))),
            ( // equality check
                "a = b == c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.DoubleEquals(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // member access
                "a = b.c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.Equals(SourceSpan.Default)))
            ),
            ( // static member access
                "a = b::c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("c", SourceSpan.Default)
                    )),
                    Token.Equals(SourceSpan.Default)))
            ),
            ( // not
                "a = !b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("b"),
                        Token.Bang(SourceSpan.Default))),
                    Token.Equals(SourceSpan.Default)))
            ),
            // __ equality check
            ( // greater than
                "a == b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.RightAngleBracket(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // less than
                "a == b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.LeftAngleBracket(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // multiply
                "a == b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.Star(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // divide
                "a == b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // plus
                "a == b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.Plus(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // minus
                "a == b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("c", SourceSpan.Default))),
                        Token.Dash(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // fallOut
                "a == b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Token.Identifier("b", SourceSpan.Default))),
                        Token.QuestionMark(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // value assignment
                "a == b = c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.DoubleEquals(SourceSpan.Default))),
                    VariableAccessor("c"), Token.Equals(SourceSpan.Default)))
            ),
            ( // equality check
                "a == b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.DoubleEquals(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a == b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // static member access
                "a == b::c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.EqualityCheck,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("c", SourceSpan.Default)
                    )),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // not
                "a == !b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    VariableAccessor("a"),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("b"),
                        Token.Bang(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            // __Member Access
            ( // greater than
                "a.b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Token.Identifier("b", SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a.b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Token.Identifier("b", SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a.b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Token.Identifier("b", SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "a.b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Token.Identifier("b", SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // plus
                "a.b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Token.Identifier("b", SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a.b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Token.Identifier("b", SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a.b?",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Token.Identifier("b", SourceSpan.Default))),
                    Token.QuestionMark(SourceSpan.Default)))
            ),
            ( // value assignment
                "a.b = c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.ValueAssignment,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Token.Identifier("b", SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.Equals(SourceSpan.Default)))
            ),
            ( // equality check
                "a.b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Token.Identifier("b", SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a.b.c",
                new MemberAccessExpression(new MemberAccess(
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Token.Identifier("b", SourceSpan.Default))),
                    Token.Identifier("c", SourceSpan.Default)))
            ),
            // __Static Member Access
            ( // greater than
                "a::b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("b", SourceSpan.Default)
                    )),
                    VariableAccessor("c"),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a::b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("b", SourceSpan.Default)
                    )),
                    VariableAccessor("c"),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a::b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("b", SourceSpan.Default)
                    )),
                    VariableAccessor("c"),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "a::b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("b", SourceSpan.Default)
                    )),
                    VariableAccessor("c"),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // plus
                "a::b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("b", SourceSpan.Default)
                    )),
                    VariableAccessor("c"),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a::b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("b", SourceSpan.Default)
                    )),
                    VariableAccessor("c"),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a::b?",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("b", SourceSpan.Default)
                    )),
                    Token.QuestionMark(SourceSpan.Default)))
            ),
            ( // value assignment
                "a::b = c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.ValueAssignment,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("b", SourceSpan.Default)
                    )),
                    VariableAccessor("c"),
                    Token.Equals(SourceSpan.Default)))
            ),
            ( // equality check
                "a::b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("b", SourceSpan.Default)
                    )),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a::b.c",
                new MemberAccessExpression(new MemberAccess(
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), [], SourceRange.Default),
                        Token.Identifier("b", SourceSpan.Default)
                    )),
                    Token.Identifier("c", SourceSpan.Default)))
            ),
            // __Not
            ( // fallout
                "!a?",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.Not,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(SourceSpan.Default))),
                    Token.Bang(SourceSpan.Default)))
            ),
            ( // less than
                "!a < b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("a"),
                        Token.Bang(SourceSpan.Default))),
                    VariableAccessor("b"),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // greater than
                "!a > b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("a"),
                        Token.Bang(SourceSpan.Default))),
                    VariableAccessor("b"),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // plus
                "!a + b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("a"),
                        Token.Bang(SourceSpan.Default))),
                    VariableAccessor("b"),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "!a - b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("a"),
                        Token.Bang(SourceSpan.Default))),
                    VariableAccessor("b"),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // multiply
                "!a * b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("a"),
                        Token.Bang(SourceSpan.Default))),
                    VariableAccessor("b"),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "!a / b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("a"),
                        Token.Bang(SourceSpan.Default))),
                    VariableAccessor("b"),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // assignment
                "!a = c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.ValueAssignment,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("a"),
                        Token.Bang(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.Equals(SourceSpan.Default)))),
            ( // equality check
                "!a == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("a"),
                        Token.Bang(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // member access
                "!a.c",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.Not,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.Bang(SourceSpan.Default)))
            ),
            ( // not
                "!!b",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.Not,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("b"),
                        Token.Bang(SourceSpan.Default))),
                    Token.Bang(SourceSpan.Default)))
            )
        }.Select(x => new object[] { x.Source, Tokenizer.Tokenize(x.Source), x.ExpectedExpression });
    }

    private static ValueAccessorExpression Literal(int value)
    {
        return new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(value, SourceSpan.Default)));
    }

    private static ValueAccessorExpression Literal(string value)
    {
        return new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral(value, SourceSpan.Default)));
    }

    private static BinaryOperatorExpression Multiply(IExpression? left, IExpression? right)
    {
        return new BinaryOperatorExpression(new BinaryOperator(
            BinaryOperatorType.Multiply,
            left,
            right,
            Token.Star(SourceSpan.Default)));
    }

    private static UnaryOperatorExpression FallOut(IExpression? value)
    {
        return new UnaryOperatorExpression(new UnaryOperator(UnaryOperatorType.FallOut, value,
            Token.QuestionMark(SourceSpan.Default)));
    }
    
    private static UnaryOperatorExpression Not(IExpression? value)
    {
        return new UnaryOperatorExpression(new UnaryOperator(UnaryOperatorType.Not, value,
            Token.QuestionMark(SourceSpan.Default)));
    }

    private static BlockExpression Block(IReadOnlyList<IExpression>? expressions = null)
    {
        return new BlockExpression(new Block(expressions ?? [], []), SourceRange.Default);
    }

    private static VariableDeclarationExpression VariableDeclaration(
        string name,
        IExpression? value = null,
        TypeIdentifier? type = null,
        bool isMutable = false)
    {
        return new VariableDeclarationExpression(new VariableDeclaration(
            Token.Identifier(name, SourceSpan.Default),
            isMutable
                ? new MutabilityModifier(Token.Mut(SourceSpan.Default))
                : null,
            type,
            value), SourceRange.Default);
    }

    private static TypeIdentifier IntType()
    {
        return new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default);
    }
    
    private static TypeIdentifier StringType()
    {
        return new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default);
    }

    public static ClassField ClassField(
        string name,
        TypeIdentifier type,
        bool isMutable = false,
        bool isStatic = false,
        bool isPublic = false,
        IExpression? value = null)
    {
        return new ClassField(
            AccessModifier: isPublic ? new AccessModifier(Token.Pub(SourceSpan.Default)) : null,
            StaticModifier: isStatic ? new StaticModifier(Token.Static(SourceSpan.Default)) : null,
            MutabilityModifier: isMutable ? new MutabilityModifier(Token.Mut(SourceSpan.Default)) : null,
            Name: Token.Identifier(name, SourceSpan.Default),
            Type: type,
            InitializerValue: value);
    }

    private static ValueAccessorExpression VariableAccessor(string name)
    {
        return new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
            Token.Identifier(name, SourceSpan.Default)));
    }

    private static IReadOnlyList<ParserError> RemoveSourceSpan(IReadOnlyList<ParserError> errors)
    {
        return [..errors.Select(RemoveSourceSpan)];
    }

    private static ParserError RemoveSourceSpan(ParserError error)
    {
        return error with { Range = SourceRange.Default };
    }

    private static LangFunction RemoveSourceSpan(LangFunction function)
    {
        return new LangFunction(
            RemoveSourceSpan(function.AccessModifier),
            RemoveSourceSpan(function.StaticModifier),
            RemoveSourceSpan(function.Name),
            [..function.TypeArguments.Select(RemoveSourceSpan)],
            [..function.Parameters.Select(RemoveSourceSpan)],
            RemoveSourceSpan(function.ReturnType),
            RemoveSourceSpan(function.Block)
        );
    }

    private static Block RemoveSourceSpan(Block block)
    {
        return new Block(
            [..block.Expressions.Select(RemoveSourceSpan)!],
            [..block.Functions.Select(RemoveSourceSpan)]);
    }

    private static AccessModifier? RemoveSourceSpan(AccessModifier? accessModifier)
    {
        return accessModifier is null
            ? null
            : new AccessModifier(RemoveSourceSpan(accessModifier.Token));
    }

    [return: NotNullIfNotNull(nameof(typeIdentifier))]
    private static TypeIdentifier? RemoveSourceSpan(TypeIdentifier? typeIdentifier)
    {
        if (typeIdentifier is null)
        {
            return null;
        }

        return new TypeIdentifier(RemoveSourceSpan(typeIdentifier.Identifier),
            [..typeIdentifier.TypeArguments.Select(RemoveSourceSpan)!], SourceRange.Default);
    }

    private static FunctionParameter RemoveSourceSpan(FunctionParameter parameter)
    {
        return new FunctionParameter(
            RemoveSourceSpan(parameter.Type),
            RemoveSourceSpan(parameter.MutabilityModifier),
            RemoveSourceSpan(parameter.Identifier));
    }

    [return: NotNullIfNotNull(nameof(expression))]
    private static IExpression? RemoveSourceSpan(IExpression? expression)
    {
        return expression switch
        {
            null => null,
            ValueAccessorExpression valueAccessorExpression => new ValueAccessorExpression(
                RemoveSourceSpan(valueAccessorExpression.ValueAccessor)),
            UnaryOperatorExpression unaryOperatorExpression => new UnaryOperatorExpression(
                RemoveSourceSpan(unaryOperatorExpression.UnaryOperator)),
            BinaryOperatorExpression binaryOperatorExpression => new BinaryOperatorExpression(
                RemoveSourceSpan(binaryOperatorExpression.BinaryOperator)),
            VariableDeclarationExpression variableDeclarationExpression => new VariableDeclarationExpression(
                RemoveSourceSpan(variableDeclarationExpression.VariableDeclaration), SourceRange.Default),
            IfExpressionExpression ifExpressionExpression => new IfExpressionExpression(
                RemoveSourceSpan(ifExpressionExpression.IfExpression), SourceRange.Default),
            BlockExpression blockExpression => new BlockExpression(RemoveSourceSpan(blockExpression.Block), SourceRange.Default),
            MethodCallExpression methodCallExpression => new MethodCallExpression(
                RemoveSourceSpan(methodCallExpression.MethodCall), SourceRange.Default),
            MethodReturnExpression methodReturnExpression => new MethodReturnExpression(
                RemoveSourceSpan(methodReturnExpression.MethodReturn), SourceRange.Default),
            ObjectInitializerExpression objectInitializerExpression => new ObjectInitializerExpression(
                RemoveSourceSpan(objectInitializerExpression.ObjectInitializer), SourceRange.Default),
            MemberAccessExpression memberAccessExpression => new MemberAccessExpression(
                RemoveSourceSpan(memberAccessExpression.MemberAccess)),
            StaticMemberAccessExpression staticMemberAccessExpression => new StaticMemberAccessExpression(
                RemoveSourceSpan(staticMemberAccessExpression.StaticMemberAccess)),
            GenericInstantiationExpression genericInstantiationExpression => new GenericInstantiationExpression(
                RemoveSourceSpan(genericInstantiationExpression.GenericInstantiation), SourceRange.Default),
            UnionStructVariantInitializerExpression unionStructVariantInitializerExpression => new
                UnionStructVariantInitializerExpression(
                    RemoveSourceSpan(unionStructVariantInitializerExpression.UnionInitializer), SourceRange.Default),
            MatchesExpression matchesExpression => RemoveSourceSpan(matchesExpression),
            TupleExpression tupleExpression => RemoveSourceSpan(tupleExpression),
            MatchExpression matchExpression => RemoveSourceSpan(matchExpression),
            _ => throw new NotImplementedException(expression.GetType().ToString())
        };
    }

    private static MatchExpression RemoveSourceSpan(MatchExpression matchExpression)
    {
        return new MatchExpression(
            RemoveSourceSpan(matchExpression.Value),
            [..matchExpression.Arms.Select(RemoveSourceSpan)], SourceRange.Default);
    }

    private static MatchArm RemoveSourceSpan(MatchArm matchArm)
    {
        return new MatchArm(
            RemoveSourceSpan(matchArm.Pattern),
            RemoveSourceSpan(matchArm.Expression));
    }

    private static TupleExpression RemoveSourceSpan(TupleExpression tupleExpression)
    {
        return new TupleExpression([..tupleExpression.Values.Select(RemoveSourceSpan)!], SourceRange.Default);
    }

    private static MatchesExpression RemoveSourceSpan(
        MatchesExpression matchesExpression)
    {
        return new MatchesExpression(
            RemoveSourceSpan(matchesExpression.ValueExpression),
            RemoveSourceSpan(matchesExpression.Pattern),
            SourceRange.Default);
    }

    private static IPattern RemoveSourceSpan(IPattern pattern)
    {
        return pattern switch
        {
            DiscardPattern discardPattern => discardPattern,
            VariableDeclarationPattern variablePattern => new VariableDeclarationPattern(
                RemoveSourceSpan(variablePattern.VariableName), SourceRange.Default),
            UnionVariantPattern unionVariantPattern => new UnionVariantPattern(
                RemoveSourceSpan(unionVariantPattern.Type),
                RemoveSourceSpan(unionVariantPattern.VariantName),
                unionVariantPattern.VariableName is null ? null : RemoveSourceSpan(unionVariantPattern.VariableName)
            , SourceRange.Default),
            UnionTupleVariantPattern unionTupleVariantPattern => new UnionTupleVariantPattern(
                RemoveSourceSpan(unionTupleVariantPattern.Type),
                RemoveSourceSpan(unionTupleVariantPattern.VariantName),
                [..unionTupleVariantPattern.TupleParamPatterns.Select(RemoveSourceSpan)],
                unionTupleVariantPattern.VariableName is null
                    ? null
                    : RemoveSourceSpan(unionTupleVariantPattern.VariableName), SourceRange.Default),
            ClassPattern classPattern => new ClassPattern(RemoveSourceSpan(classPattern.Type),
                [
                    ..classPattern.FieldPatterns.Select(x =>
                        KeyValuePair.Create(RemoveSourceSpan(x.Key),
                            x.Value is not null ? RemoveSourceSpan(x.Value) : null))
                ],
                classPattern.RemainingFieldsDiscarded,
                classPattern.VariableName is null ? null : RemoveSourceSpan(classPattern.VariableName), SourceRange.Default),
            UnionStructVariantPattern unionStructVariantPattern => new UnionStructVariantPattern(
                RemoveSourceSpan(unionStructVariantPattern.Type),
                RemoveSourceSpan(unionStructVariantPattern.VariantName),
                [
                    ..unionStructVariantPattern.FieldPatterns.Select(x =>
                        KeyValuePair.Create(RemoveSourceSpan(x.Key),
                            x.Value is not null ? RemoveSourceSpan(x.Value) : null))
                ],
                unionStructVariantPattern.RemainingFieldsDiscarded,
                unionStructVariantPattern.VariableName is null
                    ? null
                    : RemoveSourceSpan(unionStructVariantPattern.VariableName), SourceRange.Default),
            _ => throw new NotImplementedException($"{pattern}")
        };
    }


    private static UnionStructVariantInitializer RemoveSourceSpan(
        UnionStructVariantInitializer unionStructVariantInitializer)
    {
        return new UnionStructVariantInitializer(
            RemoveSourceSpan(unionStructVariantInitializer.UnionType),
            RemoveSourceSpan(unionStructVariantInitializer.VariantIdentifier),
            [..unionStructVariantInitializer.FieldInitializers.Select(RemoveSourceSpan)]);
    }

    private static GenericInstantiation RemoveSourceSpan(GenericInstantiation genericInstantiation)
    {
        return new GenericInstantiation(
            RemoveSourceSpan(genericInstantiation.Value),
            [..genericInstantiation.GenericArguments.Select(RemoveSourceSpan)!]);
    }

    private static MemberAccess RemoveSourceSpan(MemberAccess memberAccess)
    {
        return new MemberAccess(RemoveSourceSpan(memberAccess.Owner),
            RemoveSourceSpan(memberAccess.MemberName));
    }

    private static StaticMemberAccess RemoveSourceSpan(StaticMemberAccess staticMemberAccess)
    {
        return new StaticMemberAccess(RemoveSourceSpan(staticMemberAccess.Type),
            RemoveSourceSpan(staticMemberAccess.MemberName));
    }

    private static ObjectInitializer RemoveSourceSpan(ObjectInitializer objectInitializer)
    {
        return new ObjectInitializer(
            RemoveSourceSpan(objectInitializer.Type),
            [..objectInitializer.FieldInitializers.Select(RemoveSourceSpan)]);
    }

    private static FieldInitializer RemoveSourceSpan(FieldInitializer fieldInitializer)
    {
        return new FieldInitializer(
            RemoveSourceSpan(fieldInitializer.FieldName),
            RemoveSourceSpan(fieldInitializer.Value));
    }

    private static MethodCall RemoveSourceSpan(MethodCall methodCall)
    {
        return new MethodCall(
            RemoveSourceSpan(methodCall.Method),
            [..methodCall.ParameterList.Select(RemoveSourceSpan)!]);
    }

    private static IfExpression RemoveSourceSpan(IfExpression ifExpression)
    {
        return new IfExpression(
            RemoveSourceSpan(ifExpression.CheckExpression),
            RemoveSourceSpan(ifExpression.Body),
            [..ifExpression.ElseIfs.Select(RemoveSourceSpan)],
            RemoveSourceSpan(ifExpression.ElseBody));
    }

    private static ElseIf RemoveSourceSpan(ElseIf elseIf)
    {
        return new ElseIf(RemoveSourceSpan(elseIf.CheckExpression), RemoveSourceSpan(elseIf.Body));
    }

    private static VariableDeclaration RemoveSourceSpan(VariableDeclaration variableDeclaration)
    {
        return new VariableDeclaration(
            RemoveSourceSpan(variableDeclaration.VariableNameToken),
            RemoveSourceSpan(variableDeclaration.MutabilityModifier),
            RemoveSourceSpan(variableDeclaration.Type),
            RemoveSourceSpan(variableDeclaration.Value));
    }

    private static MutabilityModifier? RemoveSourceSpan(MutabilityModifier? mutabilityModifier)
    {
        return mutabilityModifier is null
            ? null
            : new MutabilityModifier(RemoveSourceSpan(mutabilityModifier.Modifier));
    }

    private static BinaryOperator RemoveSourceSpan(BinaryOperator binaryOperator)
    {
        return binaryOperator with
        {
            Left = RemoveSourceSpan(binaryOperator.Left),
            Right = RemoveSourceSpan(binaryOperator.Right),
            OperatorToken = RemoveSourceSpan(binaryOperator.OperatorToken)
        };
    }

    private static UnaryOperator RemoveSourceSpan(UnaryOperator unaryOperator)
    {
        return unaryOperator with
        {
            OperatorToken = RemoveSourceSpan(unaryOperator.OperatorToken),
            Operand = RemoveSourceSpan(unaryOperator.Operand)
        };
    }

    private static ValueAccessor RemoveSourceSpan(ValueAccessor valueAccessor)
    {
        return valueAccessor with { Token = RemoveSourceSpan(valueAccessor.Token) };
    }

    private static Token RemoveSourceSpan(Token token)
    {
        return token with { SourceSpan = SourceSpan.Default };
    }

    private static StringToken RemoveSourceSpan(StringToken token)
    {
        return token with { SourceSpan = SourceSpan.Default };
    }

    private static LangProgram RemoveSourceSpan(LangProgram program)
    {
        return new LangProgram(
            [..program.Expressions.Select(RemoveSourceSpan)!],
            [..program.Functions.Select(RemoveSourceSpan)],
            [..program.Classes.Select(RemoveSourceSpan)],
            [..program.Unions.Select(RemoveSourceSpan)]);
    }

    private static ProgramUnion RemoveSourceSpan(ProgramUnion union)
    {
        return new ProgramUnion(
            RemoveSourceSpan(union.AccessModifier),
            RemoveSourceSpan(union.Name),
            [..union.GenericArguments.Select(RemoveSourceSpan)],
            [..union.Functions.Select(RemoveSourceSpan)],
            [..union.Variants.Select(RemoveSourceSpan)]
        );
    }

    private static IProgramUnionVariant RemoveSourceSpan(IProgramUnionVariant variant)
    {
        return variant switch
        {
            UnitStructUnionVariant unitStructVariant => new UnitStructUnionVariant(
                RemoveSourceSpan(unitStructVariant.Name)),
            TupleUnionVariant tupleUnionVariant => new TupleUnionVariant(
                RemoveSourceSpan(tupleUnionVariant.Name),
                [..tupleUnionVariant.TupleMembers.Select(RemoveSourceSpan)!]),
            StructUnionVariant structUnionVariant => new StructUnionVariant
            {
                Name = RemoveSourceSpan(structUnionVariant.Name),
                Fields = [..structUnionVariant.Fields.Select(RemoveSourceSpan)]
            },
            _ => throw new UnreachableException()
        };
    }

    private static ProgramClass RemoveSourceSpan(ProgramClass @class)
    {
        return new ProgramClass(
            RemoveSourceSpan(@class.AccessModifier),
            RemoveSourceSpan(@class.Name),
            [..@class.TypeArguments.Select(RemoveSourceSpan)],
            [..@class.Functions.Select(RemoveSourceSpan)],
            [..@class.Fields.Select(RemoveSourceSpan)]);
    }

    private static ClassField RemoveSourceSpan(ClassField field)
    {
        return new ClassField(
            RemoveSourceSpan(field.AccessModifier),
            RemoveSourceSpan(field.StaticModifier),
            RemoveSourceSpan(field.MutabilityModifier),
            RemoveSourceSpan(field.Name),
            RemoveSourceSpan(field.Type),
            RemoveSourceSpan(field.InitializerValue));
    }

    private static StaticModifier? RemoveSourceSpan(StaticModifier? staticModifier)
    {
        return staticModifier is null
            ? null
            : new StaticModifier(RemoveSourceSpan(staticModifier.Token));
    }

    private static MethodReturn RemoveSourceSpan(MethodReturn methodReturn)
    {
        return new MethodReturn(RemoveSourceSpan(methodReturn.Expression));
    }
}