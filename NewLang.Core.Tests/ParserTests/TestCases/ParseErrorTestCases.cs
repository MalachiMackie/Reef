namespace NewLang.Core.Tests.ParserTests.TestCases;

using static ParserHelpers;

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
                    ParserError.ExpectedToken(Token.Identifier("a", SourceSpan.Default), TokenType.Semicolon)
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
                    ParserError.UnexpectedToken(Token.Comma(SourceSpan.Default))
                ]
            ),
            (
                "a;,b",
                new LangProgram([
                    VariableAccessor("a"),
                    VariableAccessor("b")
                ], [], [], []),
                [
                    ParserError.UnexpectedToken(Token.Comma(SourceSpan.Default))
                ]
            ),
            (
                "a b",
                new LangProgram([
                    VariableAccessor("a"),
                    VariableAccessor("b")
                ], [], [], []),
                [
                    ParserError.ExpectedToken(Token.Identifier("b", SourceSpan.Default), TokenType.Semicolon)
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
                    ParserError.ExpectedToken(Token.Identifier("b", SourceSpan.Default), TokenType.Semicolon),
                    ParserError.ExpectedToken(Token.Identifier("e", SourceSpan.Default), TokenType.Semicolon),
                ]
            ),
            (
                "class MyClass {field MyField: string field OtherField: string}",
                new LangProgram([], [], [
                    new ProgramClass(null, Token.Identifier("MyClass", SourceSpan.Default), [], [], [
                        ClassField("MyField", StringType()),
                        ClassField("OtherField", StringType())
                    ])
                ], []),
                [
                    ParserError.ExpectedToken(Token.Field(SourceSpan.Default), TokenType.Comma)
                ]
            ),
            (
                "class MyClass {field MyField: string, field OtherField: string",
                new LangProgram([], [], [
                    new ProgramClass(null, Token.Identifier("MyClass", SourceSpan.Default), [], [], [
                        ClassField("MyField", StringType()),
                        ClassField("OtherField", StringType())
                    ])
                ], []),
                [
                    ParserError.ExpectedToken(null, TokenType.RightBrace)
                ]
            ),
            (
                "{a",
                new LangProgram([
                    Block([VariableAccessor("a")])
                ], [], [], []),
                [
                    ParserError.ExpectedToken(null, TokenType.RightBrace)
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
                    ParserError.Class_UnexpectedModifier(Token.Mut(SourceSpan.Default))
                ]
            ),
            (
                "mut static class MyClass {}",
                new LangProgram([], [], [Class("MyClass")], []),
                [
                    ParserError.Class_UnexpectedModifier(Token.Static(SourceSpan.Default)),
                    ParserError.Class_UnexpectedModifier(Token.Mut(SourceSpan.Default)),
                ]
            ),
            (
                "mut pub static class MyClass {}",
                new LangProgram([], [], [Class("MyClass", isPublic: true)], []),
                [
                    ParserError.Class_UnexpectedModifier(Token.Static(SourceSpan.Default)),
                    ParserError.Class_UnexpectedModifier(Token.Mut(SourceSpan.Default)),
                ]
            ),
            (
                "mut union MyUnion {}",
                new LangProgram([ ], [], [], [Union("MyUnion")]),
                [
                    ParserError.Union_UnexpectedModifier(Token.Mut(SourceSpan.Default))
                ]
            ),
            (
                "mut static union MyUnion {}",
                new LangProgram([], [], [], [Union("MyUnion")]),
                [
                    ParserError.Union_UnexpectedModifier(Token.Static(SourceSpan.Default)),
                    ParserError.Union_UnexpectedModifier(Token.Mut(SourceSpan.Default)),
                ]
            ),
            (
                "mut pub static union MyUnion {}",
                new LangProgram([], [], [], [Union("MyUnion", isPublic: true)]),
                [
                    ParserError.Union_UnexpectedModifier(Token.Static(SourceSpan.Default)),
                    ParserError.Union_UnexpectedModifier(Token.Mut(SourceSpan.Default)),
                ]
            ),
            (
                "mut static fn MyFn() {}",
                new LangProgram([], [Function("MyFn", isStatic: true)], [], []),
                [
                    ParserError.Function_UnexpectedModifier(Token.Mut(SourceSpan.Default)),
                ]
            ),
            (
                "mut mut static static pub pub fn MyFn() {}",
                new LangProgram([], [Function("MyFn", isStatic: true, isPublic: true)], [], []),
                [
                    ParserError.Function_UnexpectedModifier(Token.Mut(SourceSpan.Default)),
                    ParserError.Scope_DuplicateModifier(Token.Mut(SourceSpan.Default)),
                    ParserError.Scope_DuplicateModifier(Token.Static(SourceSpan.Default)),
                    ParserError.Scope_DuplicateModifier(Token.Pub(SourceSpan.Default)),
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
                    ParserError.ExpectedToken(null, TokenType.RightBrace),
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
                    ParserError.ExpectedToken(null, TokenType.RightBrace),
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
                    ParserError.ExpectedToken(null, TokenType.RightBrace),
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
                    ParserError.ExpectedToken(null, TokenType.RightBrace),
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
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "class MyClass<T,,,T2> {}",
                new LangProgram([], [], [
                    Class("MyClass", genericParameters: ["T", "T2"])
                ], []),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier),
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier),
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
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier),
                ]
            ),
            (
                "union MyUnion<T,,T2> {}",
                new LangProgram([], [], [], [Union("MyUnion", genericParameters: ["T", "T2"])]),
                [
                    ParserError.ExpectedToken(Token.Comma(SourceSpan.Default), TokenType.Identifier),
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
                    Union("MyUnion", genericParameters: ["T"])
                ]),
                [
                    ParserError.ExpectedToken(null, TokenType.Identifier, TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Fn),
                ]
            ),
            (
                "union MyUnion<T> {}",
                new LangProgram([], [], [], [
                    Union("MyUnion", genericParameters: ["T"])
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
                    Union("MyUnion", genericParameters: ["T"])
                ]),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.Identifier, TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Fn),
                ]
            ),
            (
                "union MyUnion<T>",
                new LangProgram([], [], [], [
                    Union("MyUnion", genericParameters: ["T"])
                ]),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace),
                ]
            ),
            (
                "union MyUnion<T>;",
                new LangProgram([], [], [], [
                    Union("MyUnion", genericParameters: ["T"])
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
                new LangProgram([], [], [Class("MyClass", genericParameters: ["T"])], []),
                [
                    ParserError.ExpectedToken(null, TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Fn, TokenType.Field, TokenType.Mut),
                ]
            ),
            (
                "class MyClass<T> {}",
                new LangProgram([], [], [Class("MyClass", genericParameters: ["T"])], []),
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
                new LangProgram([], [], [Class("MyClass", genericParameters: ["T"])], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default),
                        TokenType.RightBrace, TokenType.Pub, TokenType.Static, TokenType.Fn, TokenType.Mut, TokenType.Field),
                ]
            ),
            (
                "class MyClass<T>",
                new LangProgram([], [], [Class("MyClass", genericParameters: ["T"])], []),
                [
                    ParserError.ExpectedToken(null, TokenType.LeftBrace),
                ]
            ),
            (
                "class MyClass<T>;",
                new LangProgram([], [], [Class("MyClass", genericParameters: ["T"])], []),
                [
                    ParserError.ExpectedToken(Token.Semicolon(SourceSpan.Default), TokenType.LeftBrace),
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
}