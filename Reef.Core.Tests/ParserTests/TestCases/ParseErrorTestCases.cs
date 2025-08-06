namespace Reef.Core.Tests.ParserTests.TestCases;

using static ExpressionHelpers;

public static class ParseErrorTestCases
{
    public static TheoryData<string, LangProgram, IEnumerable<ParserError>> TestCases()
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
                [ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Mut)]
            ),
            (
                "var ;",
                new LangProgram([], [], [], []),
                [ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier)]
            ),
            (
                "var a = ",
                new LangProgram([
                    VariableDeclaration("a")
                ], [], [], []),
                [ParserError.ExpectedExpression(null)]
            ),
            (
                "var a = ;",
                new LangProgram([
                    VariableDeclaration("a")
                ], [], [], []),
                [ParserError.ExpectedExpression(Token.Semicolon(SourceSpan.Default))]
            ),
            (
                "var a: = 2;",
                new LangProgram([
                    VariableDeclaration("a", Literal(2))
                ], [], [], []),
                [ParserError.ExpectedType(Token.Equals(SourceSpan.Default))]
            ),
            (
                "var a: int = ;",
                new LangProgram([
                    VariableDeclaration("a", type: IntType())
                ], [], [], []),
                [ParserError.ExpectedExpression(Token.Semicolon(SourceSpan.Default))]
            ),
            (
                "var mut a: int = ;",
                new LangProgram([
                    VariableDeclaration("a", type: IntType(), isMutable: true)
                ], [], [], []),
                [ParserError.ExpectedExpression(Token.Semicolon(SourceSpan.Default))]
            ),
            (
                "var a = ; var b = 2",
                new LangProgram([
                    VariableDeclaration("a"),
                    VariableDeclaration("b", Literal(2))
                ], [], [], []),
                [ParserError.ExpectedExpression(Token.Semicolon(SourceSpan.Default))]
            ),
            (
                "*",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedExpression(Token.Star(SourceSpan.Default)),
                ]
            ),
            (
                "a *",
                new LangProgram([
                    Multiply(VariableAccessor("a"), null)
                ], [], [], []),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "* a",
                new LangProgram([
                    VariableAccessor("a")
                ], [], [], []),
                [
                    ParserError.ExpectedExpression(Token.Star(SourceSpan.Default))
                ]
            ),
            (
                "a * var b = 2",
                new LangProgram([
                    Multiply(VariableAccessor("a"), VariableDeclaration("b", Literal(2))),
                ], [], [], []),
                [
                    // ParserError.ExpectedExpression(Token.Var(SourceSpan.Default))
                ]
            ),
            (
                "a * ;var b = 2",
                new LangProgram([
                    Multiply(VariableAccessor("a"), null),
                    VariableDeclaration("b", Literal(2))
                ], [], [], []),
                [
                    ParserError.ExpectedExpression(Token.Semicolon(SourceSpan.Default))
                ]
            ),
            (
                "?",
                new LangProgram([
                    FallOut(null)
                ], [], [], []),
                [
                    ParserError.ExpectedExpression(Token.QuestionMark(SourceSpan.Default))
                ]
            ),
            (
                "? a;",
                new LangProgram([
                    FallOut(null),
                    VariableAccessor("a")
                ], [], [], []),
                [
                    ParserError.ExpectedExpression(Token.QuestionMark(SourceSpan.Default)),
                    ParserError.ExpectedToken(Identifier("a"), TokenType.Semicolon)
                ]
            ),
            (
                "!",
                new LangProgram([
                    Not(null)
                ], [], [], []),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "a;!",
                new LangProgram([
                    VariableAccessor("a"),
                    Not(null)
                ], [], [], []),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "a * var",
                new LangProgram([
                    Multiply(VariableAccessor("a"), null)
                ], [], [], []),
                [
                    ParserError.ExpectedExpression(Token.Var(SourceSpan.Default)),
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Mut)
                ]
            ),
            (
                "!;var a = 2;",
                new LangProgram([
                    Not(null),
                    VariableDeclaration("a", Literal(2))
                ], [], [], []),
                [
                    ParserError.ExpectedExpression(Token.Semicolon(SourceSpan.Default))
                ]
            ),
            (
                "{",
                new LangProgram([
                    Block()
                ], [], [], []),
                [
                    ParserError.ExpectedTokenOrExpression(null, TokenType.RightBrace, TokenType.Pub, TokenType.Fn, TokenType.Static)
                ]
            ),
            (
                ",",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedTokenOrExpression(Token.Comma(SourceSpan.Default), TokenType.Pub, TokenType.Static, TokenType.Fn, TokenType.Class, TokenType.Union)
                ]
            ),
            (
                "a;,b",
                new LangProgram([
                    VariableAccessor("a"),
                    VariableAccessor("b")
                ], [], [], []),
                [
                    ParserError.ExpectedTokenOrExpression(Token.Comma(SourceSpan.Default), TokenType.Pub, TokenType.Static, TokenType.Fn, TokenType.Class, TokenType.Union)
                ]
            ),
            (
                "a b",
                new LangProgram([
                    VariableAccessor("a"),
                    VariableAccessor("b")
                ], [], [], []),
                [
                    ParserError.ExpectedToken(Identifier("b"), TokenType.Semicolon)
                ]
            ),
            (
                "a b; c; d e",
                new LangProgram([
                    VariableAccessor("a"),
                    VariableAccessor("b"),
                    VariableAccessor("c"),
                    VariableAccessor("d"),
                    VariableAccessor("e")
                ], [], [], []),
                [
                    ParserError.ExpectedToken(Identifier("b"), TokenType.Semicolon),
                    ParserError.ExpectedToken(Identifier("e"), TokenType.Semicolon),
                ]
            ),
            (
                "class MyClass {field MyField: string field OtherField: string}",
                new LangProgram([], [], [
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
                new LangProgram([], [], [
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
                new LangProgram([
                    Block([VariableAccessor("a")])
                ], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Semicolon)
                ]
            ),
            (
                "var a = 2;pub",
                new LangProgram([
                    VariableDeclaration("a", Literal(2))
                ], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Fn, TokenType.Static, TokenType.Class, TokenType.Union),
                ]
            ),
            (
                "mut class MyClass {}",
                new LangProgram([ ], [], [Class("MyClass")], []),
                [
                    ParserError.UnexpectedModifier(Token.Mut(SourceSpan.Default), TokenType.Pub)
                ]
            ),
            (
                "mut static class MyClass {}",
                new LangProgram([], [], [Class("MyClass")], []),
                [
                    ParserError.UnexpectedModifier(Token.Mut(SourceSpan.Default), TokenType.Pub),
                    ParserError.UnexpectedModifier(Token.Static(SourceSpan.Default), TokenType.Pub),
                ]
            ),
            (
                "mut pub static class MyClass {}",
                new LangProgram([], [], [Class("MyClass", isPublic: true)], []),
                [
                    ParserError.UnexpectedModifier(Token.Mut(SourceSpan.Default), TokenType.Pub),
                    ParserError.UnexpectedModifier(Token.Static(SourceSpan.Default), TokenType.Pub),
                ]
            ),
            (
                "mut union MyUnion {}",
                new LangProgram([ ], [], [], [Union("MyUnion")]),
                [
                    ParserError.UnexpectedModifier(Token.Mut(SourceSpan.Default), TokenType.Pub),
                ]
            ),
            (
                "mut static union MyUnion {}",
                new LangProgram([], [], [], [Union("MyUnion")]),
                [
                    ParserError.UnexpectedModifier(Token.Mut(SourceSpan.Default), TokenType.Pub),
                    ParserError.UnexpectedModifier(Token.Static(SourceSpan.Default), TokenType.Pub),
                ]
            ),
            (
                "mut pub static union MyUnion {}",
                new LangProgram([], [], [], [Union("MyUnion", isPublic: true)]),
                [
                    ParserError.UnexpectedModifier(Token.Mut(SourceSpan.Default), TokenType.Pub),
                    ParserError.UnexpectedModifier(Token.Static(SourceSpan.Default), TokenType.Pub),
                ]
            ),
            (
                "mut static fn MyFn() {}",
                new LangProgram([], [Function("MyFn", isStatic: true, isMutable: true)], [], []),
                []
            ),
            (
                "mut mut static static pub pub fn MyFn() {}",
                new LangProgram([], [Function("MyFn", isStatic: true, isPublic: true, isMutable: true)], [], []),
                [
                    ParserError.DuplicateModifier(Token.Mut(SourceSpan.Default)),
                    ParserError.DuplicateModifier(Token.Static(SourceSpan.Default)),
                    ParserError.DuplicateModifier(Token.Pub(SourceSpan.Default)),
                ]
            ),
            (
                "class MyClass { field}",
                new LangProgram([], [], [
                    Class("MyClass", fields: [])
                ], []),
                [
                    ParserError.ExpectedToken(Token.RightBrace(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "class MyClass { field",
                new LangProgram([], [], [
                    Class("MyClass", fields: [])
                ], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Pub, TokenType.Comma, TokenType.Static, TokenType.Mut, TokenType.Fn, TokenType.Field),
                ]
            ),
            (
                "class MyClass { field MyField}",
                new LangProgram([], [], [
                    Class("MyClass", fields: [ClassField("MyField")])
                ], []),
                [
                    ParserError.ExpectedToken(Token.RightBrace(SourceSpan.Default), TokenType.Colon),
                ]
            ),
            (
                "class MyClass { field MyField",
                new LangProgram([], [], [
                    Class("MyClass", fields: [ClassField("MyField")])
                ], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Colon),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma, TokenType.Pub, TokenType.Static, TokenType.Fn, TokenType.Field, TokenType.Mut),
                ]
            ),
            (
                "class MyClass { field MyField:}",
                new LangProgram([], [], [
                    Class("MyClass", fields: [ClassField("MyField")])
                ], []),
                [
                    ParserError.ExpectedType(Token.RightBrace(SourceSpan.Default)),
                ]
            ),
            (
                "class MyClass { field MyField:",
                new LangProgram([], [], [
                    Class("MyClass", fields: [ClassField("MyField")])
                ], []),
                [
                    ParserError.ExpectedType(null),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma, TokenType.Pub, TokenType.Static, TokenType.Fn, TokenType.Field, TokenType.Mut),
                ]
            ),
            (
                "class MyClass { field MyField: int =}",
                new LangProgram([], [], [
                    Class("MyClass", fields: [ClassField("MyField", type: IntType())])
                ], []),
                [
                    ParserError.ExpectedExpression(Token.RightBrace(SourceSpan.Default)),
                ]
            ),
            (
                "class MyClass { field MyField: int =",
                new LangProgram([], [], [
                    Class("MyClass", fields: [ClassField("MyField", type: IntType())])
                ], []),
                [
                    ParserError.ExpectedExpression(null),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Pub, TokenType.Comma, TokenType.Static, TokenType.Fn, TokenType.Field, TokenType.Mut),
                ]
            ),
            (
                "class MyClass { field, field MyField: int }",
                new LangProgram([], [], [
                    Class("MyClass", fields: [ClassField("MyField", type: IntType())])
                ], []),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "class MyClass<> {}",
                new LangProgram([], [], [
                    Class("MyClass")
                ], []),
                []
            ),
            (
                "class MyClass<,> {}",
                new LangProgram([], [], [
                    Class("MyClass")
                ], []),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier, TokenType.RightAngleBracket),
                ]
            ),
            (
                "class MyClass<T,,,T2> {}",
                new LangProgram([], [], [
                    Class("MyClass", typeParameters: ["T", "T2"])
                ], []),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier, TokenType.RightAngleBracket),
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier, TokenType.RightAngleBracket),
                ]
            ),
            (
                "union MyUnion<> {}",
                new LangProgram([], [], [], [Union("MyUnion")]),
                []
            ),
            (
                "union MyUnion<,> {}",
                new LangProgram([], [], [], [Union("MyUnion")]),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier, TokenType.RightAngleBracket),
                ]
            ),
            (
                "union MyUnion<T,,T2> {}",
                new LangProgram([], [], [], [Union("MyUnion", typeParameters: ["T", "T2"])]),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier, TokenType.RightAngleBracket),
                ]
            ),
            (
                ".",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedExpression(Token.Dot(SourceSpan.Default)),
                ]
            ),
            (
                "a.",
                new LangProgram([
                    MemberAccess(VariableAccessor("a"), null)
                ], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier),
                ]
            ),
            (
                "a.;",
                new LangProgram([
                    MemberAccess(VariableAccessor("a"), null)
                ], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "::",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedExpression(Token.DoubleColon(SourceSpan.Default)),
                ]
            ),
            (
                "int::",
                new LangProgram([
                    StaticMemberAccess(IntType(), null)
                ], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier),
                ]
            ),
            (
                "int::;",
                new LangProgram([
                    StaticMemberAccess(IntType(), null)
                ], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "union ",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier),
                ]
            ),
            (
                "union MyUnion",
                new LangProgram([], [], [], [
                    Union("MyUnion")
                ]),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace, TokenType.LeftAngleBracket),
                ]
            ),
            (
                "union MyUnion<T> {",
                new LangProgram([], [], [], [
                    Union("MyUnion", typeParameters: ["T"])
                ]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Fn),
                ]
            ),
            (
                "union MyUnion<T> {}",
                new LangProgram([], [], [], [
                    Union("MyUnion", typeParameters: ["T"])
                ]),
                [
                ]
            ),
            (
                "union ;",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "union MyUnion;",
                new LangProgram([], [], [], [
                    Union("MyUnion")
                ]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.LeftBrace, TokenType.LeftAngleBracket),
                ]
            ),
            (
                "union MyUnion<T> {;",
                new LangProgram([], [], [], [
                    Union("MyUnion", typeParameters: ["T"])
                ]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier, TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Fn),
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Fn),
                ]
            ),
            (
                "union MyUnion<T>",
                new LangProgram([], [], [], [
                    Union("MyUnion", typeParameters: ["T"])
                ]),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace),
                ]
            ),
            (
                "union MyUnion<T>;",
                new LangProgram([], [], [], [
                    Union("MyUnion", typeParameters: ["T"])
                ]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.LeftBrace),
                ]
            ),
            (
                "class ",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier),
                ]
            ),
            (
                "class MyClass",
                new LangProgram([], [], [Class("MyClass")], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace, TokenType.LeftAngleBracket),
                ]
            ),
            (
                "class MyClass<T> {",
                new LangProgram([], [], [Class("MyClass", typeParameters: ["T"])], []),
                [
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Fn, TokenType.Field, TokenType.Mut),
                ]
            ),
            (
                "class MyClass<T> {}",
                new LangProgram([], [], [Class("MyClass", typeParameters: ["T"])], []),
                [
                ]
            ),
            (
                "class ;",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "class MyClass;",
                new LangProgram([], [], [Class("MyClass")], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.LeftBrace, TokenType.LeftAngleBracket),
                ]
            ),
            (
                "class MyClass<T> {;",
                new LangProgram([], [], [Class("MyClass", typeParameters: ["T"])], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default),
                        TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Fn, TokenType.Mut, TokenType.Field),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Mut, TokenType.Fn, TokenType.Field)
                ]
            ),
            (
                "class MyClass<T>",
                new LangProgram([], [], [Class("MyClass", typeParameters: ["T"])], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace),
                ]
            ),
            (
                "class MyClass<T>;",
                new LangProgram([], [], [Class("MyClass", typeParameters: ["T"])], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.LeftBrace),
                ]
            ),
            (
                "fn",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier),
                ]
            ),
            (
                "fn;",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "fn MyFn",
                new LangProgram([], [Function("MyFn")], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftAngleBracket, TokenType.LeftParenthesis),
                ]
            ),
            (
                "pub static fn MyFn",
                new LangProgram([], [Function("MyFn", isStatic: true, isPublic: true)], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftAngleBracket, TokenType.LeftParenthesis),
                ]
            ),
            (
                "fn MyFn;",
                new LangProgram([], [Function("MyFn")], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.LeftAngleBracket, TokenType.LeftParenthesis),
                ]
            ),
            (
                "fn MyFn<;",
                new LangProgram([], [Function("MyFn")], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier, TokenType.RightAngleBracket),
                    ParserError.ExpectedToken(null, TokenType.RightAngleBracket)
                ]
            ),
            (
                "fn MyFn<",
                new LangProgram([], [Function("MyFn")], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.RightAngleBracket),
                ]
            ),
            (
                "fn MyFn<T>",
                new LangProgram([], [Function("MyFn", typeParameters: ["T"])], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftParenthesis),
                ]
            ),
            (
                "fn MyFn(",
                new LangProgram([], [Function("MyFn")], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Mut, TokenType.RightParenthesis),
                ]
            ),
            (
                "fn MyFn(a",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a")])], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Colon),
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis, TokenType.Comma)
                ]
            ),
            (
                "fn MyFn(a,",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a")])], [], []),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Colon),
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Mut, TokenType.RightParenthesis)
                ]
            ),
            (
                "fn MyFn(a: ",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a")])], [], []),
                [
                    ParserError.ExpectedType(null),
                    ParserError.ExpectedToken(null, TokenType.Comma, TokenType.RightParenthesis)
                ]
            ),
            (
                "fn MyFn(mut",
                new LangProgram([], [Function("MyFn", parameters: [])], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier),
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis)
                ]
            ),
            (
                "fn MyFn(mut a",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a", isMutable: true)])], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Colon),
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis, TokenType.Comma)
                ]
            ),
            (
                "fn MyFn(a: )",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a")])], [], []),
                [
                    ParserError.ExpectedType(Token.RightParenthesis(SourceSpan.Default)),
                    ParserError.ExpectedToken(null, TokenType.Colon, TokenType.LeftBrace)
                ]
            ),
            (
                "fn MyFn(a: int",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis, TokenType.Comma)
                ]
            ),
            (
                "fn MyFn(a: int;",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.RightParenthesis, TokenType.Comma),
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier, TokenType.Mut),
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis)
                ]
            ),
            (
                "fn MyFn(a: int)",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace, TokenType.Colon)
                ]
            ),
            (
                "fn MyFn(a: int);",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.LeftBrace, TokenType.Colon)
                ]
            ),
            (
                "fn MyFn(a: int):",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedType(null)
                ]
            ),
            (
                "fn MyFn(a: int):;",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedType(Token.Semicolon(SourceSpan.Default))
                ]
            ),
            (
                "fn MyFn(a: int): int {",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a", IntType())], returnType: IntType())], [], []),
                [
                    ParserError.ExpectedTokenOrExpression(null, TokenType.RightBrace, TokenType.Pub, TokenType.Fn, TokenType.Static),
                ]
            ),
            (
                "fn MyFn(a: int): int {*",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a", IntType())], returnType: IntType())], [], []),
                [
                    ParserError.ExpectedExpression(Token.Star(SourceSpan.Default)),
                    ParserError.ExpectedTokenOrExpression(null, TokenType.RightBrace, TokenType.Pub, TokenType.Fn, TokenType.Static),
                ]
            ),
            (
                "fn MyFn(a: int) {",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedTokenOrExpression(null, TokenType.RightBrace, TokenType.Pub, TokenType.Fn, TokenType.Static)
                ]
            ),
            (
                "fn MyFn(a: int) {*",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                [
                    ParserError.ExpectedTokenOrExpression(null, TokenType.RightBrace, TokenType.Pub, TokenType.Fn, TokenType.Static),
                    ParserError.ExpectedExpression(Token.Star(SourceSpan.Default)),
                ]
            ),
            (
                "fn MyFn(a: int) {}",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                []
            ),
            (
                "fn MyFn<>(a: int) {}",
                new LangProgram([], [Function("MyFn", parameters: [FunctionParameter("a", IntType())])], [], []),
                []
            ),
            (
                "A::<",
                new LangProgram([GenericInstantiation(VariableAccessor("A"))], [], [], []),
                [
                    ParserError.ExpectedTypeOrToken(null, TokenType.RightAngleBracket)
                ]
            ),
            (
                "A::<int",
                new LangProgram([GenericInstantiation(VariableAccessor("A"), [IntType()])], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.RightAngleBracket, TokenType.Comma)
                ]
            ),
            (
                "A::<int,",
                new LangProgram([GenericInstantiation(VariableAccessor("A"), [IntType()])], [], [], []),
                [
                    ParserError.ExpectedTypeOrToken(null, TokenType.RightAngleBracket)
                ]
            ),
            (
                "A::<>",
                new LangProgram([GenericInstantiation(VariableAccessor("A"))], [], [], []),
                []
            ),
            (
                "::<",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedExpression(Token.Turbofish(SourceSpan.Default))
                ]
            ),
            (
                "(1)::<int>",
                new LangProgram([GenericInstantiation(Tuple(IntLiteral(1)), [IntType()])], [], [], []),
                [
                ]
            ),
            (
                "matches",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedExpression(Token.Matches(SourceSpan.Default))
                ]
            ),
            (
                "a matches",
                new LangProgram([Matches(VariableAccessor("a"))], [], [], []),
                [
                    ParserError.ExpectedPattern(null)
                ]
            ),
            (
                "a matches;",
                new LangProgram([Matches(VariableAccessor("a"))], [], [], []),
                [
                    ParserError.ExpectedPattern(Token.Semicolon(SourceSpan.Default))
                ]
            ),
            (
                "a matches _",
                new LangProgram([Matches(VariableAccessor("a"), DiscardPattern())], [], [], []),
                [
                ]
            ),
            (
                "a matches A::",
                new LangProgram([Matches(VariableAccessor("a"), UnionVariantPattern(NamedTypeIdentifier("A")))], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier)
                ]
            ),
            (
                "a matches A::C",
                new LangProgram([Matches(VariableAccessor("a"), UnionVariantPattern(NamedTypeIdentifier("A"), "C"))], [], [], []),
                [
                ]
            ),
            (
                "a matches A::C var",
                new LangProgram([Matches(VariableAccessor("a"), UnionVariantPattern(NamedTypeIdentifier("A"), "C"))], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier)
                ]
            ),
            (
                "a matches A::C var;",
                new LangProgram([Matches(VariableAccessor("a"), UnionVariantPattern(NamedTypeIdentifier("A"), "C"))], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier)
                ]
            ),
            (
                "a matches A::C var d",
                new LangProgram([Matches(VariableAccessor("a"), UnionVariantPattern(NamedTypeIdentifier("A"), variantName: "C", variableName: "d"))], [], [], []),
                []
            ),
            (
                "a matches A::C(",
                new LangProgram([Matches(VariableAccessor("a"), UnionTupleVariantPattern(NamedTypeIdentifier("A"), "C"))], [], [], []),
                [
                    ParserError.ExpectedPatternOrToken(null, TokenType.RightParenthesis)
                ]
            ),
            (
                "a matches A::C(_",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        UnionTupleVariantPattern(NamedTypeIdentifier("A"), "C", [DiscardPattern()])
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis, TokenType.Comma)
                ]
            ),
            (
                "a matches A::C()",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        UnionTupleVariantPattern(NamedTypeIdentifier("A"), "C", [])
                    )], [], [], []),
                [
                ]
            ),
            (
                "a matches A::C(B::D var d) var c",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        UnionTupleVariantPattern(NamedTypeIdentifier("A"), "C", [
                            UnionVariantPattern(NamedTypeIdentifier("B"), "D", variableName: "d")
                        ], variableName: "c")
                    )], [], [], []),
                [
                ]
            ),
            (
                "a matches A::C() var",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        UnionTupleVariantPattern(NamedTypeIdentifier("A"), "C", [])
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier)
                ]
            ),
            (
                "a matches A::C() var;",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        UnionTupleVariantPattern(NamedTypeIdentifier("A"), "C", [])
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier)
                ]
            ),
            (
                "a matches A::C {",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [])
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Underscore, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A::C { SomeField:",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [("SomeField", null)])
                    )], [], [], []),
                [
                    ParserError.ExpectedPattern(null),
                    ParserError.ExpectedToken(null, TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A::C { SomeField OtherField }",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(
                            NamedTypeIdentifier("A"),
                            "C",
                            [
                                ("SomeField", VariableDeclarationPattern("SomeField")),
                                ("OtherField", VariableDeclarationPattern("OtherField"))
                            ])
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(Identifier("OtherField"), TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A::C { SomeField:;",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [("SomeField", null)])
                    )], [], [], []),
                [
                    ParserError.ExpectedPattern(Token.Semicolon(SourceSpan.Default)),
                    ParserError.ExpectedToken(null, TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A::C {} var",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [])
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier)
                ]
            ),
            (
                "a matches A::C {} var c",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [], "c")
                    )], [], [], []),
                []
            ),
            (
                "a matches A::C {_, SomeField}",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [("SomeField", VariableDeclarationPattern("SomeField"))], fieldsDiscarded: true)
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.RightBrace)
                ]
            ),
            (
                "a matches A::C {_, SomeField",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [("SomeField", VariableDeclarationPattern("SomeField"))], fieldsDiscarded: true)
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.RightBrace),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma),
                ]
            ),
            (
                "class MyClass{};",
                new LangProgram([], [], [
                    Class("MyClass")
                ], []),
                []
            ),
            (
                "a matches A::C {_}",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [], fieldsDiscarded: true)
                    )], [], [], []),
                []
            ),
            (
                "a matches A::C {} var ;",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        UnionClassVariantPattern(NamedTypeIdentifier("A"), "C", [])
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier)
                ]
            ),
            (
                "a matches A {",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), [])
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.Underscore, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A { SomeField:",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), [("SomeField", null)])
                    )], [], [], []),
                [
                    ParserError.ExpectedPattern(null),
                    ParserError.ExpectedToken(null, TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A { SomeField OtherField }",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), [
                            ("SomeField", VariableDeclarationPattern("SomeField")),
                            ("OtherField", VariableDeclarationPattern("OtherField"))])
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(Identifier("OtherField"), TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A { SomeField:;",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), [("SomeField", null)])
                    )], [], [], []),
                [
                    ParserError.ExpectedPattern(Token.Semicolon(SourceSpan.Default)),
                    ParserError.ExpectedToken(null, TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "a matches A {} var",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"))
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier)
                ]
            ),
            (
                "a matches A {} var c",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), variableName: "c")
                    )], [], [], []),
                []
            ),
            (
                "a matches A {_, SomeField}",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), [("SomeField", VariableDeclarationPattern("SomeField"))], fieldsDiscarded: true)
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.RightBrace)
                ]
            ),
            (
                "a matches A {_, SomeField",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), [("SomeField", VariableDeclarationPattern("SomeField"))], fieldsDiscarded: true)
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.RightBrace),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma),
                ]
            ),
            (
                "a matches A {_}",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), fieldsDiscarded: true)
                    )], [], [], []),
                []
            ),
            (
                "a matches A {_, _}",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"), fieldsDiscarded: true)
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.RightBrace),
                ]
            ),
            (
                "a matches A {} var ;",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        ClassPattern(NamedTypeIdentifier("A"))
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier)
                ]
            ),
            (
                "a matches A",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        TypePattern(NamedTypeIdentifier("A"))
                    )], [], [], []),
                [
                ]
            ),
            (
                "a matches A var",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        TypePattern(NamedTypeIdentifier("A"))
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier)
                ]
            ),
            (
                "a matches A var;",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        TypePattern(NamedTypeIdentifier("A"))
                    )], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier)
                ]
            ),
            (
                "a matches A var d",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        TypePattern(NamedTypeIdentifier("A"), "d")
                    )], [], [], []),
                []
            ),
            (
                "a matches var d",
                new LangProgram([
                    Matches(
                        VariableAccessor("a"),
                        VariableDeclarationPattern("d")
                    )], [], [], []),
                []
            ),
            (
                "a matches var",
                new LangProgram([
                    Matches( VariableAccessor("a") )], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier)
                ]
            ),
            (
                "a matches var ;",
                new LangProgram([
                    Matches( VariableAccessor("a") )], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier)
                ]
            ),
            (
                "a matches A { ; }",
                new LangProgram([
                    Matches(VariableAccessor("a"), ClassPattern(NamedTypeIdentifier("A")))], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier)
                ]
            ),
            (
                "match",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftParenthesis)
                ]
            ),
            (
                "match (",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "match (a",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis)
                ]
            ),
            (
                "match (a)",
                new LangProgram([Match(VariableAccessor("a"))], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace)
                ]
            ),
            (
                "match (a) {}",
                new LangProgram([
                    Match(VariableAccessor("a"))], [], [], []),
                []
            ),
            (
                "match (a) {",
                new LangProgram([
                    Match(VariableAccessor("a"))], [], [], []),
                [
                    ParserError.ExpectedPatternOrToken(null, TokenType.RightBrace)
                ]
            ),
            (
                "match (a) {;",
                new LangProgram([
                    Match(VariableAccessor("a"))], [], [], []),
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
                new LangProgram([
                    Match(VariableAccessor("a"), [
                        MatchArm(DiscardPattern())
                    ])], [], [], []),
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
                new LangProgram([
                    Match(VariableAccessor("a"), [
                        MatchArm(DiscardPattern())
                    ])], [], [], []),
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
                new LangProgram([
                    Match(VariableAccessor("a"), [
                        MatchArm(DiscardPattern()),
                        MatchArm(DiscardPattern(), Block())
                    ])], [], [], []),
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
                new LangProgram([
                    Match(VariableAccessor("a"), [
                        MatchArm(DiscardPattern(), Block()),
                        MatchArm(DiscardPattern(), Block())
                    ])], [], [], []),
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
                new LangProgram([
                    Match(VariableAccessor("a"), [
                        MatchArm(DiscardPattern(), Block()),
                        MatchArm(DiscardPattern(), Block())
                    ])], [], [], []),
                [
                ]
            ),
            (
                "new",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedType(null)
                ]
            ),
            (
                "new A",
                new LangProgram([ObjectInitializer(NamedTypeIdentifier("A"))], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace, TokenType.DoubleColon)
                ]
            ),
            (
                "new A {",
                new LangProgram([ObjectInitializer(NamedTypeIdentifier("A"))], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.RightBrace)
                ]
            ),
            (
                "new A {}",
                new LangProgram([ObjectInitializer(NamedTypeIdentifier("A"))], [], [], []),
                [
                ]
            ),
            (
                "new A {SomeField",
                new LangProgram([ObjectInitializer(NamedTypeIdentifier("A"), [FieldInitializer("SomeField")])], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Equals),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma)
                ]
            ),
            (
                "new A {SomeField=",
                new LangProgram([ObjectInitializer(NamedTypeIdentifier("A"), [FieldInitializer("SomeField")])], [], [], []),
                [
                    ParserError.ExpectedExpression(null),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma)
                ]
            ),
            (
                "new A {SomeField=a",
                new LangProgram([ObjectInitializer(NamedTypeIdentifier("A"), [FieldInitializer("SomeField", VariableAccessor("a"))])], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma)
                ]
            ),
            (
                "new A {SomeField=a}",
                new LangProgram([ObjectInitializer(NamedTypeIdentifier("A"), [FieldInitializer("SomeField", VariableAccessor("a"))])], [], [], []),
                []
            ),
            (
                "new A {SomeField=a OtherField=b}",
                new LangProgram([ObjectInitializer(NamedTypeIdentifier("A"), [
                    FieldInitializer("SomeField", VariableAccessor("a")),
                    FieldInitializer("OtherField", VariableAccessor("b")),
                ])], [], [], []),
                [
                    ParserError.ExpectedToken(Identifier("OtherField"), TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "new A {SomeField=a, OtherField=b}",
                new LangProgram([ObjectInitializer(NamedTypeIdentifier("A"), [
                    FieldInitializer("SomeField", VariableAccessor("a")),
                    FieldInitializer("OtherField", VariableAccessor("b")),
                ])], [], [], []),
                [
                ]
            ),
            (
                "new A::",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier)
                ]
            ),
            (
                "new A::B",
                new LangProgram([
                    UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B")
                ], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace)
                ]
            ),
            (
                "new A::B {",
                new LangProgram([UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B")], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.RightBrace)
                ]
            ),
            (
                "new A::B {}",
                new LangProgram([UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B")], [], [], []),
                [
                ]
            ),
            (
                "new A::B {SomeField",
                new LangProgram([UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B", [FieldInitializer("SomeField")])], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.Equals),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma)
                ]
            ),
            (
                "new A::B {SomeField=",
                new LangProgram([UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B", [FieldInitializer("SomeField")])], [], [], []),
                [
                    ParserError.ExpectedExpression(null),
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma)
                ]
            ),
            (
                "new A::B {SomeField=a",
                new LangProgram([UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B", [FieldInitializer("SomeField", VariableAccessor("a"))])], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Comma)
                ]
            ),
            (
                "new A::B {SomeField=a}",
                new LangProgram([UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B", [FieldInitializer("SomeField", VariableAccessor("a"))])], [], [], []),
                []
            ),
            (
                "new A::B {SomeField=a OtherField=b}",
                new LangProgram([UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B", [
                    FieldInitializer("SomeField", VariableAccessor("a")),
                    FieldInitializer("OtherField", VariableAccessor("b")),
                ])], [], [], []),
                [
                    ParserError.ExpectedToken(Identifier("OtherField"), TokenType.Comma, TokenType.RightBrace)
                ]
            ),
            (
                "new A::B {SomeField=a, OtherField=b}",
                new LangProgram([UnionClassVariantInitializer(NamedTypeIdentifier("A"), "B", [
                    FieldInitializer("SomeField", VariableAccessor("a")),
                    FieldInitializer("OtherField", VariableAccessor("b")),
                ])], [], [], []),
                [
                ]
            ),
            (
                "if",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftParenthesis),
                ]
            ),
            (
                "if (",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedExpression(null),
                ]
            ),
            (
                "if ()",
                new LangProgram([], [], [], []),
                [
                    ParserError.ExpectedExpression(Token.RightParenthesis(SourceSpan.Default)),
                ]
            ),
            (
                "if (a)",
                new LangProgram([IfExpression(VariableAccessor("a"), null)], [], [], []),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "if (a) {}",
                new LangProgram([IfExpression(VariableAccessor("a"), Block())], [], [], []),
                [
                ]
            ),
            (
                "if (a) {} else",
                new LangProgram([IfExpression(VariableAccessor("a"), Block())], [], [], []),
                [
                    ParserError.ExpectedTokenOrExpression(null, TokenType.If)
                ]
            ),
            (
                "if (a) {} else {}",
                new LangProgram([IfExpression(VariableAccessor("a"), Block(), Block())], [], [], []),
                [
                ]
            ),
            (
                "if (a) {} else if",
                new LangProgram([IfExpression(VariableAccessor("a"), Block())], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftParenthesis)
                ]
            ),
            (
                "if (a) {} else if (",
                new LangProgram([IfExpression(VariableAccessor("a"), Block())], [], [], []),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "if (a) {} else if (b",
                new LangProgram([
                    IfExpression(
                        VariableAccessor("a"),
                        Block(),
                        elseIfs: [])], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.RightParenthesis)
                ]
            ),
            (
                "a matches MyClass;",
                new LangProgram([Matches(VariableAccessor("a"), TypePattern(NamedTypeIdentifier("MyClass")))], [], [], []),
                []
            ),
            (
                "if (a) {} else if (b)",
                new LangProgram([IfExpression(VariableAccessor("a"), Block(), elseIfs: [ElseIf(VariableAccessor("b"))])], [], [], []),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "if (a) {} else if (b) {}",
                new LangProgram([IfExpression(VariableAccessor("a"), Block(), elseIfs: [ElseIf(VariableAccessor("b"), Block())])], [], [], []),
                [
                ]
            ),
            (
                "if (a) {} else if (b) {} else if (c)",
                new LangProgram([
                    IfExpression(
                        VariableAccessor("a"),
                        Block(),
                        elseIfs: [
                            ElseIf(VariableAccessor("b"), Block()),
                            ElseIf(VariableAccessor("c"))
                        ])], [], [], []),
                [
                    ParserError.ExpectedExpression(null)
                ]
            ),
            (
                "if (a) {} else if (b) {} else if (c) {} else {}",
                new LangProgram([IfExpression(
                    VariableAccessor("a"),
                    Block(),
                    elseBody: Block(),
                    elseIfs: [
                        ElseIf(VariableAccessor("b"), Block()),
                        ElseIf(VariableAccessor("c"), Block())
                    ])], [], [], []),
                [
                ]
            ),
            (
                "if (a) {} else {} else if (b) {}",
                new LangProgram([
                    IfExpression(
                        VariableAccessor("a"),
                        Block(),
                        elseBody: Block()),
                    IfExpression(VariableAccessor("b"), Block())
                ], [], [], []),
                [
                    ParserError.ExpectedTokenOrExpression(Token.Else(SourceSpan.Default), TokenType.Pub, TokenType.Fn, TokenType.Class, TokenType.Static, TokenType.Union)
                ]
            ),
            (
                "var a: (, string)",
                new LangProgram([
                    VariableDeclaration("a", type: TupleTypeIdentifier([StringType()]))
                ], [], [], []),
                [ParserError.ExpectedTypeOrToken(Token.Comma(SourceSpan.Default), TokenType.RightParenthesis)]
            ),
            (
                "var a: (int, string,)",
                new LangProgram([
                    VariableDeclaration("a", type: TupleTypeIdentifier([IntType(), StringType()]))
                ], [], [], []),
                []
            ),
            (
                "var a: ()",
                new LangProgram([
                    VariableDeclaration("a", type: UnitTypeIdentifier())
                ], [], [], []),
                []
            ),
            (
                "var a: Fn()",
                new LangProgram([
                    VariableDeclaration("a", type: FnTypeIdentifier())
                ], [], [], []),
                []
            ),
            (
                "var a: Fn(): int",
                new LangProgram([
                        VariableDeclaration("a", type: FnTypeIdentifier(returnType: IntType()))
                    ],
                    [], [], []),
                []
            ),
            (
                "var a: Fn(int, string,)",
                new LangProgram([
                    VariableDeclaration("a", type: FnTypeIdentifier([
                        FnTypeIdentifierParameter(IntType()),
                        FnTypeIdentifierParameter(StringType()),
                    ]))
                ], [], [], []),
                []
            ),
            (
                "var a: Fn(int string)",
                new LangProgram([
                    VariableDeclaration("a", type: FnTypeIdentifier([
                        FnTypeIdentifierParameter(IntType()),
                        FnTypeIdentifierParameter(StringType()),
                    ]))
                ], [], [], []),
                [ParserError.ExpectedToken(Identifier("string"), TokenType.Comma, TokenType.RightParenthesis)]
            ),
            (
                "var a: Fn(mut int)",
                new LangProgram([
                    VariableDeclaration("a", type: FnTypeIdentifier([FnTypeIdentifierParameter(IntType(), isMut: true)]))
                ], [], [], []),
                []
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