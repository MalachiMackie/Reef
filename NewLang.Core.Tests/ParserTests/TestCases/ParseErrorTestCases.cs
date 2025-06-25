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
                    ParserError.BinaryOperator_MissingRightValue(Token.Star(SourceSpan.Default))
                ]
            ),
            (
                "a *",
                new LangProgram([
                    Multiply(VariableAccessor("a"), null)
                ], [], [], []),
                [
                    ParserError.BinaryOperator_MissingRightValue(Token.Star(SourceSpan.Default))
                ]
            ),
            (
                "* a",
                new LangProgram([
                    Multiply(null, VariableAccessor("a"))
                ], [], [], []),
                [
                    ParserError.BinaryOperator_MissingLeftValue(Token.Star(SourceSpan.Default))
                ]
            ),
            (
                "a * ;var b = 2",
                new LangProgram([
                    Multiply(VariableAccessor("a"), null),
                    VariableDeclaration("b", Literal(2))
                ], [], [], []),
                [
                    ParserError.BinaryOperator_MissingRightValue(Token.Semicolon(SourceSpan.Default))
                ]
            ),
            (
                "?",
                new LangProgram([
                    FallOut(null)
                ], [], [], []),
                [
                    ParserError.UnaryOperator_MissingValue(Token.QuestionMark(SourceSpan.Default))
                ]
            ),
            (
                "?; a;",
                new LangProgram([
                    FallOut(null),
                    VariableAccessor("a")
                ], [], [], []),
                [
                    ParserError.UnaryOperator_MissingValue(Token.QuestionMark(SourceSpan.Default))
                ]
            ),
            (
                "!",
                new LangProgram([
                    Not(null)
                ], [], [], []),
                [
                    ParserError.UnaryOperator_MissingValue(Token.Bang(SourceSpan.Default))
                ]
            ),
            (
                "a;!",
                new LangProgram([
                    VariableAccessor("a"),
                    Not(null)
                ], [], [], []),
                [
                    ParserError.UnaryOperator_MissingValue(Token.Bang(SourceSpan.Default))
                ]
            ),
            (
                "!;var a = 2;",
                new LangProgram([
                    Not(null),
                    VariableDeclaration("a", Literal(2))
                ], [], [], []),
                [
                    ParserError.UnaryOperator_MissingValue(Token.Semicolon(SourceSpan.Default))
                ]
            ),
            (
                "{",
                new LangProgram([
                    Block()
                ], [], [], []),
                [
                    ParserError.Scope_MissingClosingTag(Token.LeftBrace(SourceSpan.Default))
                ]
            ),
            (
                ",",
                new LangProgram([], [], [], []),
                [
                    ParserError.Scope_UnexpectedComma(Token.Comma(SourceSpan.Default))
                ]
            ),
            (
                "a;,b",
                new LangProgram([
                    VariableAccessor("a"),
                    VariableAccessor("b")
                ], [], [], []),
                [
                    ParserError.Scope_UnexpectedComma(Token.Comma(SourceSpan.Default))
                ]
            ),
            (
                "a b",
                new LangProgram([
                    VariableAccessor("a"),
                    VariableAccessor("b")
                ], [], [], []),
                [
                    ParserError.Scope_EarlyTailReturnExpression(VariableAccessor("a"))
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
                    ParserError.Scope_EarlyTailReturnExpression(VariableAccessor("a")),
                    ParserError.Scope_EarlyTailReturnExpression(VariableAccessor("d"))
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
                    ParserError.Scope_ExpectedComma(Token.Field(SourceSpan.Default))
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
                    ParserError.Scope_MissingClosingTag(Token.StringKeyword(SourceSpan.Default))
                ]
            ),
            (
                "{a",
                new LangProgram([
                    Block([VariableAccessor("a")])
                ], [], [], []),
                [
                    ParserError.Scope_MissingClosingTag(Token.StringKeyword(SourceSpan.Default))
                ]
            ),
            (
                "var a = 2;pub",
                new LangProgram([
                    VariableDeclaration("a", Literal(2))
                ], [], [], []),
                [
                    ParserError.Scope_MissingMember(
                        Token.StringKeyword(SourceSpan.Default),
                        [Parser.Scope.ScopeType.Function, Parser.Scope.ScopeType.Class, Parser.Scope.ScopeType.Expression, Parser.Scope.ScopeType.Union])
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
        ];

        var theoryData = new TheoryData<string, LangProgram, IEnumerable<ParserError>>();
        foreach (var item in data)
        {
            theoryData.Add(item.Item1, item.Item2, item.Item3);
        }

        return theoryData;
    }
}