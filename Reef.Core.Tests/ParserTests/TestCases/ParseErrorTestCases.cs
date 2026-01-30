namespace Reef.Core.Tests.ParserTests.TestCases;

using static ExpressionHelpers;

public static class ParseErrorTestCases
{
    public static TheoryData<string, LangProgram, IEnumerable<ParserError>> TestCases()
    {
        IEnumerable<(string, LangProgram, IEnumerable<ParserError>)> data =
        [
            (
                "use ::",
                Program("ParseErrorTestCases"),
                [ParserError.ExpectedToken(null, TokenType.Identifier)]
            ),
            (
                "use ::something::",
                Program("ParseErrorTestCases"),
                [ParserError.ExpectedToken(null, TokenType.Identifier)]
            ),
            (
                "use something",
                Program("ParseErrorTestCases"),
                [ParserError.ExpectedToken(null, TokenType.DoubleColon)]
            ),
            (
                "use something::A",
                Program("ParserErrorTestCases", moduleImports: [ModuleImport(["something", "A"])]),
                [ParserError.ExpectedToken(null, TokenType.Semicolon)]
            ),
            (
                "a[0",
                Program(
                    "ParseErrorTestCases",
                    [IndexExpression(VariableAccessor("a"), Literal(0))]),
                [ParserError.ExpectedToken(null, TokenType.RightSquareBracket)]
            ),
            (
                "a[",
                Program(
                    "ParseErrorTestCases",
                    [IndexExpression(VariableAccessor("a"), null)]),
                [ParserError.ExpectedExpression(null)]
            ),
            (
                "[",
                Program("ParseErrorTestCases"),
                [ParserError.ExpectedTokenOrExpression(null, TokenType.Unboxed, TokenType.Boxed, TokenType.RightSquareBracket)]
            ),
            (
                "[1",
                Program("ParseErrorTestCases"),
                [ParserError.ExpectedToken(null, TokenType.Comma, TokenType.Semicolon, TokenType.RightSquareBracket)]
            ),
            (
                "[1,",
                Program("ParseErrorTestCases", [CollectionExpression([Literal(1)])]),
                [ParserError.ExpectedTokenOrExpression(null, TokenType.RightSquareBracket)]
            ),
            (
                "[1;",
                Program("ParseErrorTestCases"),
                [ParserError.ExpectedToken(null, TokenType.IntLiteral)]
            ),
            (
                "[1;3",
                Program("ParseErrorTestCases", [FillCollectionExpression(Literal(1), Token.IntLiteral(3, SourceSpan.Default))]),
                [ParserError.ExpectedToken(null, TokenType.RightSquareBracket)]
            ),
            ("", Program("ParseErrorTestCases"), []),
            (
                "var a",
                Program("ParseErrorTestCases", [VariableDeclaration("a")]),
                []
            ),
            (
                "var ",
                Program("ParseErrorTestCases"),
                [ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Mut)]
            ),
            (
                "var ;",
                Program("ParseErrorTestCases"),
                [ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier)]
            ),
            (
                "var a = ",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a")
                ]),
                [ParserError.ExpectedExpression(null)]
            ),
            (
                "var a = ;",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a")
                ]),
                [ParserError.ExpectedExpression(Token.Semicolon(SourceSpan.Default))]
            ),
            (
                "var a: = 2;",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a", Literal(2))
                ]),
                [ParserError.ExpectedType(Token.Equals(SourceSpan.Default))]
            ),
            (
                "var a: unboxed",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a")
                ]),
                [ParserError.ExpectedTypeName(null)]
            ),
            (
                "var a: boxed",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a")
                ]),
                [ParserError.ExpectedTypeName(null)]
            ),
            (
                "var a: unboxed;",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a")
                ]),
                [ParserError.ExpectedType(Token.Semicolon(SourceSpan.Default))]
            ),
            (
                "var a: boxed;",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a")
                ]),
                [ParserError.ExpectedType(Token.Semicolon(SourceSpan.Default))]
            ),
            (
                "var a: int unboxed",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a", type: IntType())
                ]),
                [ParserError.ExpectedTypeName(null)]
            ),
            (
                "var a: int boxed",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a", type: IntType())
                ]),
                [ParserError.ExpectedTypeName(null)]
            ),
            (
                "var a: int = ;",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a", type: IntType())
                ]),
                [ParserError.ExpectedExpression(Token.Semicolon(SourceSpan.Default))]
            ),
            (
                "var mut a: int = ;",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a", type: IntType(), isMutable: true)
                ]),
                [ParserError.ExpectedExpression(Token.Semicolon(SourceSpan.Default))]
            ),
            (
                "var a = ; var b = 2",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a"),
                    VariableDeclaration("b", Literal(2))
                ]),
                [ParserError.ExpectedExpression(Token.Semicolon(SourceSpan.Default))]
            ),
            (
                "*",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedExpression(Token.Star(SourceSpan.Default)),
                ]
            ),
            (
                "a *",
                Program("ParseErrorTestCases", [
                    Multiply(VariableAccessor("a"), null)
                ]),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "* a",
                Program("ParseErrorTestCases", [
                    VariableAccessor("a")
                ]),
                [
                    ParserError.ExpectedExpression(Token.Star(SourceSpan.Default))
                ]
            ),
            (
                "a * var b = 2",
                Program("ParseErrorTestCases", [
                    Multiply(VariableAccessor("a"), VariableDeclaration("b", Literal(2))),
                ]),
                [
                    // ParserError.ExpectedExpression(Token.Var(SourceSpan.Default))
                ]
            ),
            (
                "a * ;var b = 2",
                Program("ParseErrorTestCases", [
                    Multiply(VariableAccessor("a"), null),
                    VariableDeclaration("b", Literal(2))
                ]),
                [
                    ParserError.ExpectedExpression(Token.Semicolon(SourceSpan.Default))
                ]
            ),
            (
                "?",
                Program("ParseErrorTestCases", [
                    FallOut(null)
                ]),
                [
                    ParserError.ExpectedExpression(Token.QuestionMark(SourceSpan.Default))
                ]
            ),
            (
                "? a;",
                Program("ParseErrorTestCases", [
                    FallOut(null),
                    VariableAccessor("a")
                ]),
                [
                    ParserError.ExpectedExpression(Token.QuestionMark(SourceSpan.Default)),
                    ParserError.ExpectedToken(Identifier("a"), TokenType.Semicolon)
                ]
            ),
            (
                "!",
                Program("ParseErrorTestCases", [
                    Not(null)
                ]),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "a;!",
                Program("ParseErrorTestCases", [
                    VariableAccessor("a"),
                    Not(null)
                ]),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "a * var",
                Program("ParseErrorTestCases", [
                    Multiply(VariableAccessor("a"), null)
                ]),
                [
                    ParserError.ExpectedExpression(Token.Var(SourceSpan.Default)),
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Mut)
                ]
            ),
            (
                "!;var a = 2;",
                Program("ParseErrorTestCases", [
                    Not(null),
                    VariableDeclaration("a", Literal(2))
                ]),
                [
                    ParserError.ExpectedExpression(Token.Semicolon(SourceSpan.Default))
                ]
            ),
            (
                "{",
                Program("ParseErrorTestCases", [
                    Block()
                ]),
                [
                    ParserError.ExpectedTokenOrExpression(null, TokenType.RightBrace, TokenType.Pub, TokenType.Fn, TokenType.Static, TokenType.Use)
                ]
            ),
            (
                ",",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedTokenOrExpression(Token.Comma(SourceSpan.Default), TokenType.Pub, TokenType.Static, TokenType.Fn, TokenType.Class, TokenType.Union, TokenType.Use)
                ]
            ),
            (
                "a;,b",
                Program("ParseErrorTestCases", [
                    VariableAccessor("a"),
                    VariableAccessor("b")
                ]),
                [
                    ParserError.ExpectedTokenOrExpression(Token.Comma(SourceSpan.Default), TokenType.Pub, TokenType.Static, TokenType.Fn, TokenType.Class, TokenType.Union, TokenType.Use)
                ]
            ),
            (
                "a b",
                Program("ParseErrorTestCases", [
                    VariableAccessor("a"),
                    VariableAccessor("b")
                ]),
                [
                    ParserError.ExpectedToken(Identifier("b"), TokenType.Semicolon)
                ]
            ),
            (
                "a b; c; d e",
                Program("ParseErrorTestCases", [
                    VariableAccessor("a"),
                    VariableAccessor("b"),
                    VariableAccessor("c"),
                    VariableAccessor("d"),
                    VariableAccessor("e")
                ]),
                [
                    ParserError.ExpectedToken(Identifier("b"), TokenType.Semicolon),
                    ParserError.ExpectedToken(Identifier("e"), TokenType.Semicolon),
                ]
            ),
            (
                "class MyClass {field MyField: string field OtherField: string}",
                Program("ParseErrorTestCases", [], [], [
                    new ProgramClass(null, Identifier("MyClass"), [], [], [
                        ClassField("MyField", StringType()),
                        ClassField("OtherField", StringType())
                    ])
                ], []),
                [
                    ParserError.ExpectedToken(Token.Field(SourceSpan.Default), TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "class MyClass {field MyField: string, field OtherField: string",
                Program("ParseErrorTestCases", [], [], [
                    new ProgramClass(null, Identifier("MyClass"), [], [], [
                        ClassField("MyField", StringType()),
                        ClassField("OtherField", StringType())
                    ])
                ], []),
                [
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma, TokenType.Pub, TokenType.Static, TokenType.Mut, TokenType.Field, TokenType.Fn),
                ]
            ),
            (
                "{a",
                Program("ParseErrorTestCases", [
                    Block([VariableAccessor("a")])
                ]),
                [
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Semicolon)
                ]
            ),
            (
                "var a = 2;pub",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a", Literal(2))
                ]),
                [
                    ParserError.ExpectedToken(null, TokenType.Fn, TokenType.Static, TokenType.Class, TokenType.Union, TokenType.Use),
                ]
            ),
            (
                "mut class MyClass {}",
                Program("ParseErrorTestCases", [ ], [], [Class("MyClass")], []),
                [
                    ParserError.UnexpectedModifier(Token.Mut(SourceSpan.Default), TokenType.Pub)
                ]
            ),
            (
                "mut static class MyClass {}",
                Program("ParseErrorTestCases", [], [], [Class("MyClass")], []),
                [
                    ParserError.UnexpectedModifier(Token.Mut(SourceSpan.Default), TokenType.Pub),
                    ParserError.UnexpectedModifier(Token.Static(SourceSpan.Default), TokenType.Pub),
                ]
            ),
            (
                "mut pub static class MyClass {}",
                Program("ParseErrorTestCases", [], [], [Class("MyClass", isPublic: true)], []),
                [
                    ParserError.UnexpectedModifier(Token.Mut(SourceSpan.Default), TokenType.Pub),
                    ParserError.UnexpectedModifier(Token.Static(SourceSpan.Default), TokenType.Pub),
                ]
            ),
            (
                "mut union MyUnion {}",
                Program("ParseErrorTestCases", [ ], [], [], [Union("MyUnion")]),
                [
                    ParserError.UnexpectedModifier(Token.Mut(SourceSpan.Default), TokenType.Pub),
                ]
            ),
            (
                "mut static union MyUnion {}",
                Program("ParseErrorTestCases", [], [], [], [Union("MyUnion")]),
                [
                    ParserError.UnexpectedModifier(Token.Mut(SourceSpan.Default), TokenType.Pub),
                    ParserError.UnexpectedModifier(Token.Static(SourceSpan.Default), TokenType.Pub),
                ]
            ),
            (
                "mut pub static union MyUnion {}",
                Program("ParseErrorTestCases", [], [], [], [Union("MyUnion", isPublic: true)]),
                [
                    ParserError.UnexpectedModifier(Token.Mut(SourceSpan.Default), TokenType.Pub),
                    ParserError.UnexpectedModifier(Token.Static(SourceSpan.Default), TokenType.Pub),
                ]
            ),
            (
                "mut static fn MyFn() {}",
                Program("ParseErrorTestCases", [], [Function("MyFn", isStatic: true, isMutable: true)], [], []),
                []
            ),
            (
                "mut mut static static pub pub fn MyFn() {}",
                Program("ParseErrorTestCases", [], [Function("MyFn", isStatic: true, isPublic: true, isMutable: true)], [], []),
                [
                    ParserError.DuplicateModifier(Token.Mut(SourceSpan.Default)),
                    ParserError.DuplicateModifier(Token.Static(SourceSpan.Default)),
                    ParserError.DuplicateModifier(Token.Pub(SourceSpan.Default)),
                ]
            ),
            (
                "class MyClass { field}",
                Program("ParseErrorTestCases", [], [], [
                    Class("MyClass", fields: [])
                ], []),
                [
                    ParserError.ExpectedToken(Token.RightBrace(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "class MyClass { field",
                Program("ParseErrorTestCases", [], [], [
                    Class("MyClass", fields: [])
                ], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Pub, TokenType.Comma, TokenType.Static, TokenType.Mut, TokenType.Fn, TokenType.Field),
                ]
            ),
            (
                "class MyClass { field MyField}",
                Program("ParseErrorTestCases", [], [], [
                    Class("MyClass", fields: [ClassField("MyField")])
                ], []),
                [
                    ParserError.ExpectedToken(Token.RightBrace(SourceSpan.Default), TokenType.Colon),
                ]
            ),
            (
                "class MyClass { field MyField",
                Program("ParseErrorTestCases", [], [], [
                    Class("MyClass", fields: [ClassField("MyField")])
                ], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Colon),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma, TokenType.Pub, TokenType.Static, TokenType.Fn, TokenType.Field, TokenType.Mut),
                ]
            ),
            (
                "class MyClass { field MyField:}",
                Program("ParseErrorTestCases", [], [], [
                    Class("MyClass", fields: [ClassField("MyField")])
                ], []),
                [
                    ParserError.ExpectedType(Token.RightBrace(SourceSpan.Default)),
                ]
            ),
            (
                "class MyClass { field MyField:",
                Program("ParseErrorTestCases", [], [], [
                    Class("MyClass", fields: [ClassField("MyField")])
                ], []),
                [
                    ParserError.ExpectedType(null),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma, TokenType.Pub, TokenType.Static, TokenType.Fn, TokenType.Field, TokenType.Mut),
                ]
            ),
            (
                "class MyClass { field MyField: int =}",
                Program("ParseErrorTestCases", [], [], [
                    Class("MyClass", fields: [ClassField("MyField", type: IntType())])
                ], []),
                [
                    ParserError.ExpectedExpression(Token.RightBrace(SourceSpan.Default)),
                ]
            ),
            (
                "class MyClass { field MyField: int =",
                Program("ParseErrorTestCases", [], [], [
                    Class("MyClass", fields: [ClassField("MyField", type: IntType())])
                ], []),
                [
                    ParserError.ExpectedExpression(null),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Pub, TokenType.Comma, TokenType.Static, TokenType.Fn, TokenType.Field, TokenType.Mut),
                ]
            ),
            (
                "class MyClass { field, field MyField: int }",
                Program("ParseErrorTestCases", [], [], [
                    Class("MyClass", fields: [ClassField("MyField", type: IntType())])
                ], []),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "class MyClass<> {}",
                Program("ParseErrorTestCases", [], [], [
                    Class("MyClass")
                ], []),
                []
            ),
            (
                "class MyClass<,> {}",
                Program("ParseErrorTestCases", [], [], [
                    Class("MyClass")
                ], []),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier, TokenType.RightAngleBracket),
                ]
            ),
            (
                "class MyClass<T,,,T2> {}",
                Program("ParseErrorTestCases", [], [], [
                    Class("MyClass", typeParameters: ["T", "T2"])
                ], []),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier, TokenType.RightAngleBracket),
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier, TokenType.RightAngleBracket),
                ]
            ),
            (
                "union MyUnion<> {}",
                Program("ParseErrorTestCases", [], [], [], [Union("MyUnion")]),
                []
            ),
            (
                "union MyUnion<,> {}",
                Program("ParseErrorTestCases", [], [], [], [Union("MyUnion")]),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier, TokenType.RightAngleBracket),
                ]
            ),
            (
                "union MyUnion<T,,T2> {}",
                Program("ParseErrorTestCases", [], [], [], [Union("MyUnion", typeParameters: ["T", "T2"])]),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier, TokenType.RightAngleBracket),
                ]
            ),
            (
                ".",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedExpression(Token.Dot(SourceSpan.Default)),
                ]
            ),
            (
                "a.",
                Program("ParseErrorTestCases", [
                    MemberAccess(VariableAccessor("a"), null)
                ]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier),
                ]
            ),
            (
                "a.;",
                Program("ParseErrorTestCases", [
                    MemberAccess(VariableAccessor("a"), null)
                ]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "::",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedExpression(Token.DoubleColon(SourceSpan.Default)),
                ]
            ),
            (
                "int::",
                Program("ParseErrorTestCases", [
                    StaticMemberAccess(IntType(), null)
                ]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier),
                ]
            ),
            (
                "int::;",
                Program("ParseErrorTestCases", [
                    StaticMemberAccess(IntType(), null)
                ]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "union ",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier),
                ]
            ),
            (
                "union MyUnion",
                Program("ParseErrorTestCases", [], [], [], [
                    Union("MyUnion")
                ]),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace, TokenType.LeftAngleBracket),
                ]
            ),
            (
                "union MyUnion<T> {",
                Program("ParseErrorTestCases", [], [], [], [
                    Union("MyUnion", typeParameters: ["T"])
                ]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Fn),
                ]
            ),
            (
                "union MyUnion<T> {}",
                Program("ParseErrorTestCases", [], [], [], [
                    Union("MyUnion", typeParameters: ["T"])
                ]),
                [
                ]
            ),
            (
                "union ;",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "union MyUnion;",
                Program("ParseErrorTestCases", [], [], [], [
                    Union("MyUnion")
                ]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.LeftBrace, TokenType.LeftAngleBracket),
                ]
            ),
            (
                "union MyUnion<T> {;",
                Program("ParseErrorTestCases", [], [], [], [
                    Union("MyUnion", typeParameters: ["T"])
                ]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier, TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Fn),
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Fn),
                ]
            ),
            (
                "union MyUnion<T>",
                Program("ParseErrorTestCases", [], [], [], [
                    Union("MyUnion", typeParameters: ["T"])
                ]),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace),
                ]
            ),
            (
                "union MyUnion<T>;",
                Program("ParseErrorTestCases", [], [], [], [
                    Union("MyUnion", typeParameters: ["T"])
                ]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.LeftBrace),
                ]
            ),
            (
                "class ",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier),
                ]
            ),
            (
                "class MyClass",
                Program("ParseErrorTestCases", [], [], [Class("MyClass")], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace, TokenType.LeftAngleBracket),
                ]
            ),
            (
                "class MyClass<T> {",
                Program("ParseErrorTestCases", [], [], [Class("MyClass", typeParameters: ["T"])], []),
                [
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Fn, TokenType.Field, TokenType.Mut),
                ]
            ),
            (
                "class MyClass<T> {}",
                Program("ParseErrorTestCases", [], [], [Class("MyClass", typeParameters: ["T"])], []),
                [
                ]
            ),
            (
                "class ;",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "class MyClass;",
                Program("ParseErrorTestCases", [], [], [Class("MyClass")], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.LeftBrace, TokenType.LeftAngleBracket),
                ]
            ),
            (
                "class MyClass<T> {;",
                Program("ParseErrorTestCases", [], [], [Class("MyClass", typeParameters: ["T"])], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default),
                        TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Fn, TokenType.Mut, TokenType.Field),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Mut, TokenType.Fn, TokenType.Field)
                ]
            ),
            (
                "class MyClass<T>",
                Program("ParseErrorTestCases", [], [], [Class("MyClass", typeParameters: ["T"])], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace),
                ]
            ),
            (
                "class MyClass<T>;",
                Program("ParseErrorTestCases", [], [], [Class("MyClass", typeParameters: ["T"])], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.LeftBrace),
                ]
            ),
            (
                "fn",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier),
                ]
            ),
            (
                "fn;",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "fn MyFn",
                Program("ParseErrorTestCases", [], [Function("MyFn")], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftAngleBracket, TokenType.LeftParenthesis),
                ]
            ),
            (
                "pub static fn MyFn",
                Program("ParseErrorTestCases", [], [Function("MyFn", isStatic: true, isPublic: true)], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftAngleBracket, TokenType.LeftParenthesis),
                ]
            ),
            (
                "fn MyFn;",
                Program("ParseErrorTestCases", [], [Function("MyFn")], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.LeftAngleBracket, TokenType.LeftParenthesis),
                ]
            ),
            (
                "fn MyFn<;",
                Program("ParseErrorTestCases", [], [Function("MyFn")], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier, TokenType.RightAngleBracket),
                    ParserError.ExpectedToken(null, TokenType.RightAngleBracket)
                ]
            ),
            (
                "fn MyFn<",
                Program("ParseErrorTestCases", [], [Function("MyFn")], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.RightAngleBracket),
                ]
            ),
            (
                "fn MyFn<T>",
                Program("ParseErrorTestCases", [], [Function("MyFn", typeParameters: ["T"])], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftParenthesis),
                ]
            ),
            (
                "fn MyFn(",
                Program("ParseErrorTestCases", [], [Function("MyFn")], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Mut, TokenType.RightParenthesis),
                ]
            ),
            (
                "fn MyFn(a",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a")])], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Colon),
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis, TokenType.Comma)
                ]
            ),
            (
                "fn MyFn(a,",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a")])], [], []),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Colon),
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Mut, TokenType.RightParenthesis)
                ]
            ),
            (
                "fn MyFn(a: ",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a")])], [], []),
                [
                    ParserError.ExpectedType(null),
                    ParserError.ExpectedToken(null, TokenType.Comma, TokenType.RightParenthesis)
                ]
            ),
            (
                "fn MyFn(mut",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [])], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier),
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis)
                ]
            ),
            (
                "fn MyFn(mut a",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a", isMutable: true)])], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Colon),
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis, TokenType.Comma)
                ]
            ),
            (
                "fn MyFn(a: )",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a")])], [], []),
                [
                    ParserError.ExpectedType(Token.RightParenthesis(SourceSpan.Default)),
                    ParserError.ExpectedToken(null, TokenType.Colon, TokenType.LeftBrace)
                ]
            ),
            (
                "fn MyFn(a: int",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis, TokenType.Comma)
                ]
            ),
            (
                "fn MyFn(a: int;",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.RightParenthesis, TokenType.Comma),
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier, TokenType.Mut),
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis)
                ]
            ),
            (
                "fn MyFn(a: int)",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace, TokenType.Colon)
                ]
            ),
            (
                "fn MyFn(a: int);",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.LeftBrace, TokenType.Colon)
                ]
            ),
            (
                "fn MyFn(a: int):",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedTypeOrToken(null, TokenType.Mut)
                ]
            ),
            (
                "fn MyFn(a: int):;",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedType(Token.Semicolon(SourceSpan.Default))
                ]
            ),
            (
                "fn MyFn(a: int): int {",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a", IntType())], returnType: IntType())], [], []),
                [
                    ParserError.ExpectedTokenOrExpression(null, TokenType.RightBrace, TokenType.Pub, TokenType.Fn, TokenType.Static, TokenType.Use),
                ]
            ),
            (
                "fn MyFn(a: int): int {*",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a", IntType())], returnType: IntType())], [], []),
                [
                    ParserError.ExpectedExpression(Token.Star(SourceSpan.Default)),
                    ParserError.ExpectedTokenOrExpression(null, TokenType.RightBrace, TokenType.Pub, TokenType.Fn, TokenType.Static, TokenType.Use),
                ]
            ),
            (
                "fn MyFn(a: int) {",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedTokenOrExpression(null, TokenType.RightBrace, TokenType.Pub, TokenType.Fn, TokenType.Static, TokenType.Use)
                ]
            ),
            (
                "fn MyFn(a: int) {*",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedTokenOrExpression(null, TokenType.RightBrace, TokenType.Pub, TokenType.Fn, TokenType.Static, TokenType.Use),
                    ParserError.ExpectedExpression(Token.Star(SourceSpan.Default)),
                ]
            ),
            (
                "fn MyFn(a: int) {}",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                []
            ),
            (
                "fn MyFn<>(a: int) {}",
                Program("ParseErrorTestCases", [], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                []
            ),
            (
                "A::<",
                Program("ParseErrorTestCases", [VariableAccessor("A", [])]),
                [
                    ParserError.ExpectedTypeOrToken(null, TokenType.RightAngleBracket)
                ]
            ),
            (
                "A::<int",
                Program("ParseErrorTestCases", [VariableAccessor("A", [IntType()])]),
                [
                    ParserError.ExpectedToken(null, TokenType.RightAngleBracket, TokenType.Comma)
                ]
            ),
            (
                "A::<int,",
                Program("ParseErrorTestCases", [VariableAccessor("A", [IntType()])]),
                [
                    ParserError.ExpectedTypeOrToken(null, TokenType.RightAngleBracket)
                ]
            ),
            (
                "A::<>",
                Program("ParseErrorTestCases", [VariableAccessor("A", [])]),
                []
            ),
            (
                "::<",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedTokenOrExpression(Token.Turbofish(SourceSpan.Default), TokenType.Pub, TokenType.Fn, TokenType.Class, TokenType.Static, TokenType.Union, TokenType.Use)
                ]
            ),
            (
                "(1)::<",
                Program("ParseErrorTestCases", [Tuple(Literal(1))]),
                [
                    ParserError.ExpectedTokenOrExpression(
                        Token.Turbofish(SourceSpan.Default),
                        TokenType.Pub, TokenType.Fn, TokenType.Class, TokenType.Static, TokenType.Union, TokenType.Use)
                ]
            ),
            (
                "1::<",
                Program("ParseErrorTestCases", [Literal(1)]),
                [ParserError.ExpectedTokenOrExpression(Token.Turbofish(SourceSpan.Default), TokenType.Pub, TokenType.Fn, TokenType.Class, TokenType.Static, TokenType.Union, TokenType.Use)]
            ),
            (
                "var a = SomeFn::<string>;",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a", VariableAccessor("SomeFn", [StringType()]))
                ]),
                []
            ),
            (
                "matches",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedExpression(Token.Matches(SourceSpan.Default))
                ]
            ),
            (
                "a matches",
                Program("ParseErrorTestCases", [Matches(VariableAccessor("a"))]),
                [
                    ParserError.ExpectedPattern(null)
                ]
            ),
            (
                "a matches;",
                Program("ParseErrorTestCases", [Matches(VariableAccessor("a"))]),
                [
                    ParserError.ExpectedPattern(Token.Semicolon(SourceSpan.Default))
                ]
            ),
            (
                "a matches _",
                Program("ParseErrorTestCases", [Matches(VariableAccessor("a"), DiscardPattern())]),
                [
                ]
            ),
            (
                "a matches A::",
                Program("ParseErrorTestCases", [Matches(VariableAccessor("a"), UnionVariantPattern(NamedTypeIdentifier("A")))]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier)
                ]
            ),
            (
                "a matches A::C",
                Program("ParseErrorTestCases", [Matches(VariableAccessor("a"), UnionVariantPattern(NamedTypeIdentifier("A"), "C"))]),
                [
                ]
            ),
            (
                "a matches A::C var",
                Program("ParseErrorTestCases", [Matches(VariableAccessor("a"), UnionVariantPattern(NamedTypeIdentifier("A"), "C"))]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Mut)
                ]
            ),
            (
                "a matches A::C var;",
                Program("ParseErrorTestCases", [Matches(VariableAccessor("a"), UnionVariantPattern(NamedTypeIdentifier("A"), "C"))]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier, TokenType.Mut)
                ]
            ),
            (
                "a matches A::C var d",
                Program("ParseErrorTestCases", [Matches(VariableAccessor("a"), UnionVariantPattern(NamedTypeIdentifier("A"), variantName: "C", variableName: "d"))]),
                []
            ),
            (
                "a matches A::C(",
                Program("ParseErrorTestCases", [Matches(VariableAccessor("a"), UnionTupleVariantPattern(NamedTypeIdentifier("A"), "C"))]),
                [
                    ParserError.ExpectedPatternOrToken(null, TokenType.RightParenthesis)
                ]
            ),
            (
                "a matches A::C(_",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionTupleVariantPattern(NamedTypeIdentifier("A"), "C", [DiscardPattern()])
                    )]),
                [
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis, TokenType.Comma)
                ]
            ),
            (
                "a matches A::C()",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionTupleVariantPattern(NamedTypeIdentifier("A"), "C", [])
                    )]),
                [
                ]
            ),
            (
                "a matches A::C(B::D var d) var c",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionTupleVariantPattern(NamedTypeIdentifier("A"), "C", [
                            UnionVariantPattern(NamedTypeIdentifier("B"), "D", variableName: "d")
                        ], variableName: "c")
                    )]),
                [
                ]
            ),
            (
                "a matches A::C() var",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionTupleVariantPattern(NamedTypeIdentifier("A"), "C", [])
                    )]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Mut)
                ]
            ),
            (
                "a matches A::C() var;",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionTupleVariantPattern(NamedTypeIdentifier("A"), "C", [])
                    )]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier, TokenType.Mut)
                ]
            ),
            (
                "a matches A::C {",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [])
                    )]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Underscore, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A::C { SomeField:",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [("SomeField", null)])
                    )]),
                [
                    ParserError.ExpectedPattern(null),
                    ParserError.ExpectedToken(null, TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A::C { SomeField OtherField }",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(
                            NamedTypeIdentifier("A"),
                            "C",
                            [
                                ("SomeField", VariableDeclarationPattern("SomeField")),
                                ("OtherField", VariableDeclarationPattern("OtherField"))
                            ])
                    )]),
                [
                    ParserError.ExpectedToken(Identifier("OtherField"), TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A::C { SomeField:;",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [("SomeField", null)])
                    )]),
                [
                    ParserError.ExpectedPattern(Token.Semicolon(SourceSpan.Default)),
                    ParserError.ExpectedToken(null, TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A::C {} var",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [])
                    )]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Mut)
                ]
            ),
            (
                "a matches A::C {} var c",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [], "c")
                    )]),
                []
            ),
            (
                "a matches A::C {_, SomeField}",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [("SomeField", VariableDeclarationPattern("SomeField"))], fieldsDiscarded: true)
                    )]),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.RightBrace)
                ]
            ),
            (
                "a matches A::C {_, SomeField",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [("SomeField", VariableDeclarationPattern("SomeField"))], fieldsDiscarded: true)
                    )]),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.RightBrace),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma),
                ]
            ),
            (
                "class MyClass{};",
                Program("ParseErrorTestCases", [], [], [
                    Class("MyClass")
                ], []),
                []
            ),
            (
                "a matches A::C {_}",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [], fieldsDiscarded: true)
                    )]),
                []
            ),
            (
                "a matches A::C {} var ;",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [])
                    )]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier, TokenType.Mut)
                ]
            ),
            (
                "a matches A {",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), [])
                    )]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Underscore, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A { SomeField:",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), [("SomeField", null)])
                    )]),
                [
                    ParserError.ExpectedPattern(null),
                    ParserError.ExpectedToken(null, TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A { SomeField OtherField }",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), [
                            ("SomeField", VariableDeclarationPattern("SomeField")),
                            ("OtherField", VariableDeclarationPattern("OtherField"))])
                    )]),
                [
                    ParserError.ExpectedToken(Identifier("OtherField"), TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A { SomeField:;",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), [("SomeField", null)])
                    )]),
                [
                    ParserError.ExpectedPattern(Token.Semicolon(SourceSpan.Default)),
                    ParserError.ExpectedToken(null, TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A {} var",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"))
                    )]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Mut)
                ]
            ),
            (
                "a matches A {} var c",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), variableName: "c")
                    )]),
                []
            ),
            (
                "a matches A {_, SomeField}",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), [("SomeField", VariableDeclarationPattern("SomeField"))], fieldsDiscarded: true)
                    )]),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.RightBrace)
                ]
            ),
            (
                "a matches A {_, SomeField",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), [("SomeField", VariableDeclarationPattern("SomeField"))], fieldsDiscarded: true)
                    )]),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.RightBrace),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma),
                ]
            ),
            (
                "a matches A {_}",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), fieldsDiscarded: true)
                    )]),
                []
            ),
            (
                "a matches A {_, _}",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), fieldsDiscarded: true)
                    )]),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.RightBrace),
                ]
            ),
            (
                "a matches A {} var ;",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"))
                    )]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier, TokenType.Mut)
                ]
            ),
            (
                "a matches A",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        TypePattern(NamedTypeIdentifier("A"))
                    )]),
                [
                ]
            ),
            (
                "a matches A var",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        TypePattern(NamedTypeIdentifier("A"))
                    )]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Mut)
                ]
            ),
            (
                "a matches A var;",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        TypePattern(NamedTypeIdentifier("A"))
                    )]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier, TokenType.Mut)
                ]
            ),
            (
                "a matches A var d",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        TypePattern(NamedTypeIdentifier("A"), "d")
                    )]),
                []
            ),
            (
                "a matches var d",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        VariableDeclarationPattern("d")
                    )]),
                []
            ),
            (
                "a matches var",
                Program("ParseErrorTestCases", [
                    Matches( VariableAccessor("a") )]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Mut)
                ]
            ),
            (
                "a matches var ;",
                Program("ParseErrorTestCases", [
                    Matches( VariableAccessor("a") )]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier, TokenType.Mut)
                ]
            ),
            (
                "a matches A { ; }",
                Program("ParseErrorTestCases", [
                    Matches(VariableAccessor("a"), ClassPattern(NamedTypeIdentifier("A")))]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier)
                ]
            ),
            (
                "match",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftParenthesis)
                ]
            ),
            (
                "match (",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "match (a",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis)
                ]
            ),
            (
                "match (a)",
                Program("ParseErrorTestCases", [Match(VariableAccessor("a"))]),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace)
                ]
            ),
            (
                "match (a) {}",
                Program("ParseErrorTestCases", [
                    Match(VariableAccessor("a"))]),
                []
            ),
            (
                "match (a) {",
                Program("ParseErrorTestCases", [
                    Match(VariableAccessor("a"))]),
                [
                    ParserError.ExpectedPatternOrToken(null, TokenType.RightBrace)
                ]
            ),
            (
                "match (a) {;",
                Program("ParseErrorTestCases", [
                    Match(VariableAccessor("a"))]),
                [
                    ParserError.ExpectedPattern(Token.Semicolon(SourceSpan.Default)),
                    ParserError.ExpectedToken(null, TokenType.RightBrace),
                ]
            ),
            (
                """
                match (a) {
                    _
                }
                """,
                Program("ParseErrorTestCases", [
                    Match(VariableAccessor("a"), [
                        MatchArm(DiscardPattern())
                    ])]),
                [
                    ParserError.ExpectedToken(Token.RightBrace(SourceSpan.Default), TokenType.EqualsArrow)
                ]
            ),
            (
                """
                match (a) {
                    _ =>
                }
                """,
                Program("ParseErrorTestCases", [
                    Match(VariableAccessor("a"), [
                        MatchArm(DiscardPattern())
                    ])]),
                [
                    ParserError.ExpectedExpression(Token.RightBrace(SourceSpan.Default))
                ]
            ),
            (
                """
                match (a) {
                    _ =>,
                    _ => {}
                }
                """,
                Program("ParseErrorTestCases", [
                    Match(VariableAccessor("a"), [
                        MatchArm(DiscardPattern()),
                        MatchArm(DiscardPattern(), Block())
                    ])]),
                [
                    ParserError.ExpectedExpression(Token.Comma(SourceSpan.Default))
                ]
            ),
            (
                """
                match (a) {
                    _ => {}
                    _ => {}
                }
                """,
                Program("ParseErrorTestCases", [
                    Match(VariableAccessor("a"), [
                        MatchArm(DiscardPattern(), Block()),
                        MatchArm(DiscardPattern(), Block())
                    ])]),
                [
                    ParserError.ExpectedToken(Token.Underscore(SourceSpan.Default), TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                """
                match (a) {
                    _ => {},
                    _ => {},
                }
                """,
                Program("ParseErrorTestCases", [
                    Match(VariableAccessor("a"), [
                        MatchArm(DiscardPattern(), Block()),
                        MatchArm(DiscardPattern(), Block())
                    ])]),
                [
                ]
            ),
            (
                "new",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedType(null)
                ]
            ),
            (
                "new A",
                Program("ParseErrorTestCases", [ObjectInitializer(NamedTypeIdentifier("A"))]),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace, TokenType.DoubleColon)
                ]
            ),
            (
                "new A {",
                Program("ParseErrorTestCases", [ObjectInitializer(NamedTypeIdentifier("A"))]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.RightBrace)
                ]
            ),
            (
                "new A {}",
                Program("ParseErrorTestCases", [ObjectInitializer(NamedTypeIdentifier("A"))]),
                [
                ]
            ),
            (
                "new A {SomeField",
                Program("ParseErrorTestCases", [ObjectInitializer(NamedTypeIdentifier("A"), [FieldInitializer("SomeField")])]),
                [
                    ParserError.ExpectedToken(null, TokenType.Equals),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma)
                ]
            ),
            (
                "new A {SomeField=",
                Program("ParseErrorTestCases", [ObjectInitializer(NamedTypeIdentifier("A"), [FieldInitializer("SomeField")])]),
                [
                    ParserError.ExpectedExpression(null),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma)
                ]
            ),
            (
                "new A {SomeField=a",
                Program("ParseErrorTestCases", [ObjectInitializer(NamedTypeIdentifier("A"), [FieldInitializer("SomeField", VariableAccessor("a"))])]),
                [
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma)
                ]
            ),
            (
                "new A {SomeField=a}",
                Program("ParseErrorTestCases", [ObjectInitializer(NamedTypeIdentifier("A"), [FieldInitializer("SomeField", VariableAccessor("a"))])]),
                []
            ),
            (
                "new A {SomeField=a OtherField=b}",
                Program("ParseErrorTestCases", [ObjectInitializer(NamedTypeIdentifier("A"), [
                    FieldInitializer("SomeField", VariableAccessor("a")),
                    FieldInitializer("OtherField", VariableAccessor("b")),
                ])]),
                [
                    ParserError.ExpectedToken(Identifier("OtherField"), TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "new A {SomeField=a, OtherField=b}",
                Program("ParseErrorTestCases", [ObjectInitializer(NamedTypeIdentifier("A"), [
                    FieldInitializer("SomeField", VariableAccessor("a")),
                    FieldInitializer("OtherField", VariableAccessor("b")),
                ])]),
                [
                ]
            ),
            (
                "new A::",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier)
                ]
            ),
            (
                "new A::B",
                Program("ParseErrorTestCases", [
                    UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B")
                ]),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace)
                ]
            ),
            (
                "new A::B {",
                Program("ParseErrorTestCases", [UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B")]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.RightBrace)
                ]
            ),
            (
                "new A::B {}",
                Program("ParseErrorTestCases", [UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B")]),
                [
                ]
            ),
            (
                "new A::B {SomeField",
                Program("ParseErrorTestCases", [UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B", [FieldInitializer("SomeField")])]),
                [
                    ParserError.ExpectedToken(null, TokenType.Equals),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma)
                ]
            ),
            (
                "new A::B {SomeField=",
                Program("ParseErrorTestCases", [UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B", [FieldInitializer("SomeField")])]),
                [
                    ParserError.ExpectedExpression(null),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma)
                ]
            ),
            (
                "new A::B {SomeField=a",
                Program("ParseErrorTestCases", [UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B", [FieldInitializer("SomeField", VariableAccessor("a"))])]),
                [
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma)
                ]
            ),
            (
                "new A::B {SomeField=a}",
                Program("ParseErrorTestCases", [UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B", [FieldInitializer("SomeField", VariableAccessor("a"))])]),
                []
            ),
            (
                "new A::B {SomeField=a OtherField=b}",
                Program("ParseErrorTestCases", [UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B", [
                    FieldInitializer("SomeField", VariableAccessor("a")),
                    FieldInitializer("OtherField", VariableAccessor("b")),
                ])]),
                [
                    ParserError.ExpectedToken(Identifier("OtherField"), TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "new A::B {SomeField=a, OtherField=b}",
                Program("ParseErrorTestCases", [UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B", [
                    FieldInitializer("SomeField", VariableAccessor("a")),
                    FieldInitializer("OtherField", VariableAccessor("b")),
                ])]),
                [
                ]
            ),
            (
                "if",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftParenthesis),
                ]
            ),
            (
                "if (",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedExpression(null),
                ]
            ),
            (
                "if ()",
                Program("ParseErrorTestCases"),
                [
                    ParserError.ExpectedExpression(Token.RightParenthesis(SourceSpan.Default)),
                ]
            ),
            (
                "if (a)",
                Program("ParseErrorTestCases", [IfExpression(VariableAccessor("a"), null)]),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "if (a) {}",
                Program("ParseErrorTestCases", [IfExpression(VariableAccessor("a"), Block())]),
                [
                ]
            ),
            (
                "if (a) {} else",
                Program("ParseErrorTestCases", [IfExpression(VariableAccessor("a"), Block())]),
                [
                    ParserError.ExpectedTokenOrExpression(null, TokenType.If)
                ]
            ),
            (
                "if (a) {} else {}",
                Program("ParseErrorTestCases", [IfExpression(VariableAccessor("a"), Block(), Block())]),
                [
                ]
            ),
            (
                "if (a) {} else if",
                Program("ParseErrorTestCases", [IfExpression(VariableAccessor("a"), Block())]),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftParenthesis)
                ]
            ),
            (
                "if (a) {} else if (",
                Program("ParseErrorTestCases", [IfExpression(VariableAccessor("a"), Block())]),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "if (a) {} else if (b",
                Program("ParseErrorTestCases", [
                    IfExpression(
                        VariableAccessor("a"),
                        Block(),
                        elseIfs: [])]),
                [
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis)
                ]
            ),
            (
                "a matches MyClass;",
                Program("ParseErrorTestCases", [Matches(VariableAccessor("a"), TypePattern(NamedTypeIdentifier("MyClass")))]),
                []
            ),
            (
                "if (a) {} else if (b)",
                Program("ParseErrorTestCases", [IfExpression(VariableAccessor("a"), Block(), elseIfs: [ElseIf(VariableAccessor("b"))])]),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "if (a) {} else if (b) {}",
                Program("ParseErrorTestCases", [IfExpression(VariableAccessor("a"), Block(), elseIfs: [ElseIf(VariableAccessor("b"), Block())])]),
                [
                ]
            ),
            (
                "if (a) {} else if (b) {} else if (c)",
                Program("ParseErrorTestCases", [
                    IfExpression(
                        VariableAccessor("a"),
                        Block(),
                        elseIfs: [
                            ElseIf(VariableAccessor("b"), Block()),
                            ElseIf(VariableAccessor("c"))
                        ])]),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "if (a) {} else if (b) {} else if (c) {} else {}",
                Program("ParseErrorTestCases", [IfExpression(
                    VariableAccessor("a"),
                    Block(),
                    elseBody: Block(),
                    elseIfs: [
                        ElseIf(VariableAccessor("b"), Block()),
                        ElseIf(VariableAccessor("c"), Block())
                    ])]),
                [
                ]
            ),
            (
                "if (a) {} else {} else if (b) {}",
                Program("ParseErrorTestCases", [
                    IfExpression(
                        VariableAccessor("a"),
                        Block(),
                        elseBody: Block()),
                    IfExpression(VariableAccessor("b"), Block())
                ]),
                [
                    ParserError.ExpectedTokenOrExpression(Token.Else(SourceSpan.Default), TokenType.Pub, TokenType.Fn, TokenType.Class, TokenType.Static, TokenType.Union, TokenType.Use)
                ]
            ),
            (
                "var a: (, string)",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a", type: TupleTypeIdentifier(null, [StringType()]))
                ]),
                [ParserError.ExpectedTypeOrToken(Token.Comma(SourceSpan.Default), TokenType.RightParenthesis)]
            ),
            (
                "var a: (int, string,)",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a", type: TupleTypeIdentifier(null, [IntType(), StringType()]))
                ]),
                []
            ),
            (
                "var a: ()",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a", type: UnitTypeIdentifier())
                ]),
                []
            ),
            (
                "var a: Fn()",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a", type: FnTypeIdentifier())
                ]),
                []
            ),
            (
                "var a: Fn(): int",
                Program("ParseErrorTestCases", [
                        VariableDeclaration("a", type: FnTypeIdentifier(returnType: IntType()))
                    ],
                    [], [], []),
                []
            ),
            (
                "var a: Fn(): mut int",
                Program("ParseErrorTestCases", [
                        VariableDeclaration("a", type: FnTypeIdentifier(returnType: IntType(), returnMutabilityModifier: Token.Mut(SourceSpan.Default)))
                    ],
                    [], [], []),
                []
            ),
            (
                "var a: Fn(int, string,)",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a", type: FnTypeIdentifier([
                        FnTypeIdentifierParameter(IntType()),
                        FnTypeIdentifierParameter(StringType()),
                    ]))
                ]),
                []
            ),
            (
                "var a: Fn(int string)",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a", type: FnTypeIdentifier([
                        FnTypeIdentifierParameter(IntType()),
                        FnTypeIdentifierParameter(StringType()),
                    ]))
                ]),
                [ParserError.ExpectedToken(Identifier("string"), TokenType.Comma, TokenType.RightParenthesis)]
            ),
            (
                "var a: Fn(mut int)",
                Program("ParseErrorTestCases", [
                    VariableDeclaration("a", type: FnTypeIdentifier([FnTypeIdentifierParameter(IntType(), isMut: true)]))
                ]),
                []
            ),
            (
                "a matches var mut b",
                Program("ParseErrorTestCases", [Matches(
                    VariableAccessor("a"),
                    VariableDeclarationPattern("b", isMut: true))]),
                []
            ),
            (
                "a matches MyType var mut b",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        TypePattern(NamedTypeIdentifier("MyType"), "b", isMutableVariable: true))
                ]),
                []
            ),
            (
                "a matches MyUnion::A var mut b",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionVariantPattern(NamedTypeIdentifier("MyUnion"), "A", "b", isMutableVariable: true))
                ]),
                []
            ),
            (
                "a matches MyUnion::A(_) var mut b",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionTupleVariantPattern(
                            NamedTypeIdentifier("MyUnion"),
                            "A",
                            [DiscardPattern()],
                            "b",
                            isMutableVariable: true))
                ]),
                []
            ),
            (
                "a matches MyUnion::A{_} var mut b",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(
                            NamedTypeIdentifier("MyUnion"),
                            "A",
                            [],
                            "b",
                            fieldsDiscarded: true,
                            isMutableVariable: true))
                ]),
                []
            ),
            (
                "a matches MyClass{_} var mut b",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(
                            NamedTypeIdentifier("MyClass"),
                            [],
                            "b",
                            fieldsDiscarded: true,
                            isMutableVariable: true))
                ]),
                []
            ),
            (
                "a matches var mut",
                Program("ParseErrorTestCases", [Matches(
                    VariableAccessor("a"))]),
                [ParserError.ExpectedToken(null, TokenType.Identifier)]
            ),
            (
                "a matches MyType var mut",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        TypePattern(NamedTypeIdentifier("MyType")))
                ]),
                [ParserError.ExpectedToken(null, TokenType.Identifier)]
            ),
            (
                "a matches MyUnion::A var mut",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionVariantPattern(NamedTypeIdentifier("MyUnion"), "A"))
                ]),
                [ParserError.ExpectedToken(null, TokenType.Identifier)]
            ),
            (
                "a matches MyUnion::A(_) var mut",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionTupleVariantPattern(
                            NamedTypeIdentifier("MyUnion"),
                            "A",
                            [DiscardPattern()]))
                ]),
                [ParserError.ExpectedToken(null, TokenType.Identifier)]
            ),
            (
                "a matches MyUnion::A{_} var mut",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(
                            NamedTypeIdentifier("MyUnion"),
                            "A",
                            [],
                            fieldsDiscarded: true))
                ]),
                [ParserError.ExpectedToken(null, TokenType.Identifier)]
            ),
            (
                "a matches MyClass{_} var mut",
                Program("ParseErrorTestCases", [
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(
                            NamedTypeIdentifier("MyClass"),
                            [],
                            fieldsDiscarded: true))
                ]),
                [ParserError.ExpectedToken(null, TokenType.Identifier)]
            ),
            (
                "while",
                Program("ParseErrorTestCases"),
                [ParserError.ExpectedToken(null, TokenType.LeftParenthesis)]
            ),
            (
                "while(",
                Program("ParseErrorTestCases"),
                [ParserError.ExpectedExpression(null)]
            ),
            (
                "while(true",
                Program("ParseErrorTestCases", [
                    While(True())
                ]),
                [ParserError.ExpectedToken(null, TokenType.RightParenthesis)]
            ),
            (
                "while(true)",
                Program("ParseErrorTestCases", [
                    While(True())
                ]),
                [ParserError.ExpectedExpression(null)]
            ),
            (
                "unboxed",
                Program("ParseErrorTestCases", []),
                [ParserError.ExpectedTypeName(null)]
            ),
            (
                "boxed",
                Program("ParseErrorTestCases", []),
                [ParserError.ExpectedTypeName(null)]
            )
        ];

        var theoryData = new TheoryData<string, LangProgram, IEnumerable<ParserError>>();
        foreach (var item in data)
        {
            theoryData.Add(item.Item1, item.Item2, item.Item3);
        }

        return theoryData;
    }
}