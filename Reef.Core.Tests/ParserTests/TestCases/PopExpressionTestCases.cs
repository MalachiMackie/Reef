using Reef.Core.Expressions;
using static Reef.Core.Tests.ExpressionHelpers;

namespace Reef.Core.Tests.ParserTests.TestCases;

public static class PopExpressionTestCases
{
    public static IEnumerable<object[]> TestCases()
    {
        return new (string Source, IExpression ExpectedExpression)[]
        {
            (
                "todo!",
                new ValueAccessorExpression(new ValueAccessor(
                    ValueAccessType.Variable, Token.Todo(SourceSpan.Default), null))
            ),
            (
                """
                match (a) {
                    _ => b,
                }
                """,
                new MatchExpression(VariableAccessor("a"),
                    [new MatchArm(new DiscardPattern(SourceRange.Default), VariableAccessor("b"))], SourceRange.Default)
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
                        TypePattern(
                            NamedTypeIdentifier("SomeClass")),
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
                        ClassPattern(
                            NamedTypeIdentifier("SomeClass"),
                            [("SomeField", VariableDeclarationPattern("SomeField"))]),
                        VariableAccessor("b")),
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
                        UnionClassVariantPattern(
                            NamedTypeIdentifier("SomeUnion"),
                            "B",
                            [
                                (
                                    "SomeField",
                                    UnionClassVariantPattern(
                                        NamedTypeIdentifier("OtherUnion"),
                                        "C",
                                        [
                                            (
                                                "OtherField",
                                                VariableDeclarationPattern("d")
                                            )
                                        ]))
                            ]),
                        VariableAccessor("d")),
                ], SourceRange.Default)
            ),
            (
                "if (a matches OtherUnion::B(MyUnion::A var c) var b) {}",
                new IfExpressionExpression(new IfExpression(
                    new MatchesExpression(
                        VariableAccessor("a"),
                        UnionTupleVariantPattern(
                            NamedTypeIdentifier("OtherUnion"),
                            "B",
                            [
                                UnionVariantPattern(
                                    NamedTypeIdentifier("MyUnion"),
                                    "A",
                                    "c")
                            ],
                            "b"), SourceRange.Default),
                    new BlockExpression(new Block([], []), SourceRange.Default),
                    [],
                    null), SourceRange.Default)
            ),
            (
                "var b: bool = a matches int;",
                new VariableDeclarationExpression(new VariableDeclaration(
                        Identifier("b"),
                        null,
                        NamedTypeIdentifier("bool"),
                        new MatchesExpression(
                            VariableAccessor("a"),
                            TypePattern(IntType()),
                            SourceRange.Default)), SourceRange.Default)
            ),
            (
                "a matches string",
                new MatchesExpression(
                    VariableAccessor("a"),
                    TypePattern(NamedTypeIdentifier("string")), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A",
                new MatchesExpression(
                    VariableAccessor("a"),
                    UnionVariantPattern(NamedTypeIdentifier("MyUnion"), "A"), SourceRange.Default)
            ),
            (
                "a matches var a",
                new MatchesExpression(
                    VariableAccessor("a"),
                    VariableDeclarationPattern("a"), SourceRange.Default)
            ),
            (
                "a matches _",
                new MatchesExpression(
                    VariableAccessor("a"),
                    DiscardPattern(), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A(var b)",
                new MatchesExpression(
                    VariableAccessor("a"),
                    UnionTupleVariantPattern(
                        NamedTypeIdentifier("MyUnion"),
                        "A",
                        [
                            VariableDeclarationPattern("b")
                        ]), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A(var b) var c",
                new MatchesExpression(
                    VariableAccessor("a"),
                    UnionTupleVariantPattern(
                        NamedTypeIdentifier("MyUnion"),
                        "A",
                        [
                            VariableDeclarationPattern("b")
                        ],
                        "c"), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A(var b, var c, _)",
                new MatchesExpression(
                    VariableAccessor("a"),
                    UnionTupleVariantPattern(
                        NamedTypeIdentifier("MyUnion"),
                        "A",
                        [
                            VariableDeclarationPattern("b"),
                            VariableDeclarationPattern("c"),
                            new DiscardPattern(SourceRange.Default)
                        ]), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A(OtherUnion::C)",
                new MatchesExpression(
                    VariableAccessor("a"),
                    UnionTupleVariantPattern(
                        NamedTypeIdentifier("MyUnion"),
                        "A",
                        [
                            UnionVariantPattern(
                                NamedTypeIdentifier("OtherUnion"),
                                "C")
                        ]), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A(OtherUnion::C var c)",
                new MatchesExpression(
                    VariableAccessor("a"),
                    UnionTupleVariantPattern(
                        NamedTypeIdentifier("MyUnion"),
                        "A",
                        [
                            UnionVariantPattern(
                                NamedTypeIdentifier("OtherUnion"),
                                "C",
                                "c")
                        ]), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A(OtherUnion::C(var d))",
                new MatchesExpression(
                    VariableAccessor("a"),
                    UnionTupleVariantPattern(
                        NamedTypeIdentifier("MyUnion"),
                        "A",
                        [
                            UnionTupleVariantPattern(
                                NamedTypeIdentifier("OtherUnion"),
                                "C",
                                [
                                    VariableDeclarationPattern("d")
                                ])
                        ]), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A { MyField }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    UnionClassVariantPattern(
                        NamedTypeIdentifier("MyUnion"),
                        "A",
                        [
                            ("MyField", VariableDeclarationPattern("MyField"))
                        ]), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A { MyField } var a",
                new MatchesExpression(
                    VariableAccessor("a"),
                    UnionClassVariantPattern(
                        NamedTypeIdentifier("MyUnion"),
                        "A",
                        [
                            ("MyField", VariableDeclarationPattern("MyField"))
                        ],
                        "a"), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A { MyField, OtherField: var f }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    UnionClassVariantPattern(
                        NamedTypeIdentifier("MyUnion"),
                        "A",
                        [
                            ("MyField", VariableDeclarationPattern("MyField")),
                            (
                                "OtherField",
                                VariableDeclarationPattern("f")
                            )
                        ]), SourceRange.Default)
            ),
            (
                "a matches MyClass { MyField }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    ClassPattern(
                        NamedTypeIdentifier("MyClass"),
                        [
                            ("MyField", VariableDeclarationPattern("MyField"))
                        ]), SourceRange.Default)
            ),
            (
                "a matches MyClass { MyField } var a",
                new MatchesExpression(
                    VariableAccessor("a"),
                    ClassPattern(
                        NamedTypeIdentifier("MyClass"),
                        [
                            ("MyField", VariableDeclarationPattern("MyField"))
                        ],
                        "a"), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A { MyField, _ }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    UnionClassVariantPattern(
                        NamedTypeIdentifier("MyUnion"),
                        "A",
                        [
                            ("MyField", VariableDeclarationPattern("MyField"))
                        ],
                        fieldsDiscarded: true), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A { MyField: MyUnion::B var f }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    UnionClassVariantPattern(
                        NamedTypeIdentifier("MyUnion"),
                        "A",
                        [
                            (
                                "MyField",
                                UnionVariantPattern(
                                    NamedTypeIdentifier("MyUnion"),
                                    "B",
                                    "f"))
                        ]), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A { MyField: MyUnion::B(var c) }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    UnionClassVariantPattern(
                        NamedTypeIdentifier("MyUnion"),
                        "A",
                        [
                            (
                                "MyField",
                                UnionTupleVariantPattern(
                                    NamedTypeIdentifier("MyUnion"),
                                    "B",
                                    [
                                        VariableDeclarationPattern("c")
                                    ]))
                        ]), SourceRange.Default)
            ),
            (
                "a matches MyClass { MyField, _ }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    ClassPattern(
                        NamedTypeIdentifier("MyClass"),
                        [
                            ("MyField", VariableDeclarationPattern("MyField"))
                        ],
                        fieldsDiscarded: true), SourceRange.Default)
            ),
            (
                "a matches MyClass",
                new MatchesExpression(
                    VariableAccessor("a"),
                    TypePattern(
                        NamedTypeIdentifier("MyClass")), SourceRange.Default)
            ),
            (
                "a matches MyClass var b",
                new MatchesExpression(
                    VariableAccessor("a"),
                    TypePattern(
                        NamedTypeIdentifier("MyClass"),
                        "b"),
                    SourceRange.Default)
            ),


            // value access expressions
            ("a",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                    Identifier("a"), null))),
            ("this",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                    Identifier("this"), null))),
            ("1",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(1, SourceSpan.Default), null))),
            ("\"my string\"",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.StringLiteral("my string", SourceSpan.Default), null))),
            ("true",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.True(SourceSpan.Default), null))),
            ("false",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.False(SourceSpan.Default), null))),
            ("ok",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Identifier("ok"), null))),
            ("a == b",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.EqualityCheck, VariableAccessor("a"),
                    VariableAccessor("b"), Token.DoubleEquals(SourceSpan.Default)))),
            ("a != b",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.NegativeEqualityCheck, VariableAccessor("a"),
                    VariableAccessor("b"), Token.NotEquals(SourceSpan.Default)))),
            ("ok()",
                new MethodCallExpression(new MethodCall(
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("ok"), null)), []), SourceRange.Default)),
            ("(a)", new TupleExpression([VariableAccessor("a")], SourceRange.Default)),
            ("(a, b)", new TupleExpression([VariableAccessor("a"), VariableAccessor("b")], SourceRange.Default)),
            ("-a", Negate(VariableAccessor("a"))),
            ("!a", Not(VariableAccessor("a"))),
            ("a?", FallOut(VariableAccessor("a"))),
            ("a??",
                FallOut(FallOut(VariableAccessor("a")))
            ),
            ("return 1", new MethodReturnExpression(
                new MethodReturn(new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(1, SourceSpan.Default), null))), SourceRange.Default)),
            ("return", new MethodReturnExpression(new MethodReturn(null), SourceRange.Default)),
            // binary operator expressions
            ("a < 5", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.LessThan,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                    Identifier("a"), null)),
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(5, SourceSpan.Default), null)),
                Token.LeftAngleBracket(SourceSpan.Default)))),
            ("\"thing\" > true", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.GreaterThan,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.StringLiteral("thing", SourceSpan.Default), null)),
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.True(SourceSpan.Default), null)),
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
            ("a && b", BinaryOperatorExpression(BinaryOperatorType.BooleanAnd, VariableAccessor("a"), VariableAccessor("b"))),
            ("a || b", BinaryOperatorExpression(BinaryOperatorType.BooleanOr, VariableAccessor("a"), VariableAccessor("b"))),
            ("var a: int = b", new VariableDeclarationExpression(
                new VariableDeclaration(
                    Identifier("a"),
                    null,
                    NamedTypeIdentifier("int"),
                    VariableAccessor("b")), SourceRange.Default)),
            ("var a: int", new VariableDeclarationExpression(
                new VariableDeclaration(
                    Identifier("a"),
                    null,
                    NamedTypeIdentifier("int"),
                    null), SourceRange.Default)),
            ("var mut a = 2", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(2, SourceSpan.Default), null))), SourceRange.Default)),
            ("a = b",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"), VariableAccessor("b"), Token.Equals(SourceSpan.Default)))),
            ("var mut a: int = 2", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                NamedTypeIdentifier("int"),
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(2, SourceSpan.Default), null))), SourceRange.Default)),
            ("var a: bool = b", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                NamedTypeIdentifier("bool"),
                VariableAccessor("b")), SourceRange.Default)),
            ("var a: int = b", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                NamedTypeIdentifier("int"),
                VariableAccessor("b")), SourceRange.Default)),
            ("var a: string = b", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                NamedTypeIdentifier("string"),
                VariableAccessor("b")), SourceRange.Default)),
            ("var a: result = b", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                NamedTypeIdentifier("result"),
                VariableAccessor("b")), SourceRange.Default)),
            ("var a: MyType = b", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                NamedTypeIdentifier("MyType"),
                VariableAccessor("b")), SourceRange.Default)),
            ("var a = 1", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(1, SourceSpan.Default), null))), SourceRange.Default)),
            ("var a = true", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.True(SourceSpan.Default), null))), SourceRange.Default)),
            ("var a = \"thing\"", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.StringLiteral("thing", SourceSpan.Default), null))), SourceRange.Default)),
            ("{}", new BlockExpression(new Block([], []), SourceRange.Default)),
            ("{var a = 1;}", new BlockExpression(new Block([
                new VariableDeclarationExpression(new VariableDeclaration(Identifier("a"),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default), null))), SourceRange.Default)
            ], []), SourceRange.Default)),
            // tail expression
            ("{var a = 1}", new BlockExpression(new Block(
            [
                new VariableDeclarationExpression(new VariableDeclaration(Identifier("a"),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default), null))), SourceRange.Default)
            ], []), SourceRange.Default)),
            // tail expression
            ("{var a = 1;var b = 2}", new BlockExpression(new Block(
            [
                new VariableDeclarationExpression(new VariableDeclaration(Identifier("a"),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default), null))), SourceRange.Default),
                new VariableDeclarationExpression(new VariableDeclaration(Identifier("b"),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(2, SourceSpan.Default), null))), SourceRange.Default)
            ], []), SourceRange.Default)),
            ("{var a = 1; var b = 2;}", new BlockExpression(new Block([
                new VariableDeclarationExpression(new VariableDeclaration(Identifier("a"),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default), null))), SourceRange.Default),
                new VariableDeclarationExpression(new VariableDeclaration(Identifier("b"),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(2, SourceSpan.Default), null))), SourceRange.Default)
            ], []), SourceRange.Default)),
            ("if (a) var c = 2;", new IfExpressionExpression(new IfExpression(
                    VariableAccessor("a"),
                    new VariableDeclarationExpression(new VariableDeclaration(
                        Identifier("c"),
                        null,
                        null,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                            Token.IntLiteral(2, SourceSpan.Default), null))), SourceRange.Default), [], null),
                SourceRange.Default)),
            ("if (a > b) {var c = \"value\";}", new IfExpressionExpression(new IfExpression(
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    VariableAccessor("a"),
                    VariableAccessor("b"),
                    Token.RightAngleBracket(SourceSpan.Default))),
                new BlockExpression(new Block([
                    new VariableDeclarationExpression(new VariableDeclaration(
                        Identifier("c"),
                        null,
                        null,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                            Token.StringLiteral("value", SourceSpan.Default), null))), SourceRange.Default)
                ], []), SourceRange.Default), [], null), SourceRange.Default)),
            ("if (a) {} else {var b = 2;}", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new BlockExpression(new Block([], []), SourceRange.Default),
                [],
                new BlockExpression(new Block([
                    new VariableDeclarationExpression(new VariableDeclaration(
                        Identifier("b"),
                        null,
                        null,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                            Token.IntLiteral(2, SourceSpan.Default), null))), SourceRange.Default)
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
                    new BlockExpression(new Block([VariableAccessor("c")], []), SourceRange.Default)),
                SourceRange.Default)),
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
                                Token.IntLiteral(1, SourceSpan.Default), null))
                        ], []), SourceRange.Default),
                        [],
                        new BlockExpression(new Block(
                        [
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                Token.IntLiteral(2, SourceSpan.Default), null))
                        ], []), SourceRange.Default)), SourceRange.Default)
                ], []), SourceRange.Default),
                [],
                new BlockExpression(new Block(
                [
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(3, SourceSpan.Default), null))
                ], []), SourceRange.Default)), SourceRange.Default)),
            ("if (a) if (b) 1 else 2 else 3", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("b"),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default), null)),
                    [],
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(2, SourceSpan.Default), null))), SourceRange.Default),
                [],
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(3, SourceSpan.Default), null))), SourceRange.Default)),
            ("var a = if (b) 1 else 2;", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                null,
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("b"),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default), null)),
                    [],
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(2, SourceSpan.Default), null))), SourceRange.Default)), SourceRange.Default)),
            ("var a = if (b) {1} else {2};", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                null,
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("b"),
                    new BlockExpression(new Block(
                    [
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                            Token.IntLiteral(1, SourceSpan.Default), null))
                    ], []), SourceRange.Default),
                    [],
                    new BlockExpression(new Block(
                    [
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                            Token.IntLiteral(2, SourceSpan.Default), null))
                    ], []), SourceRange.Default)), SourceRange.Default)), SourceRange.Default)),
            ("a()", new MethodCallExpression(new MethodCall(VariableAccessor("a"), []), SourceRange.Default)),
            ("a.b::<int>()", new MethodCallExpression(new MethodCall(
                    new MemberAccessExpression(new MemberAccess(
                        new ValueAccessorExpression(new ValueAccessor(
                            ValueAccessType.Variable,
                            Identifier("a"), null)),
                        Identifier("b"),
                        [IntType()]
                    )),
                []
            ), SourceRange.Default)),
            ("a::<>", VariableAccessor("a", [])),
            ("a", VariableAccessor("a")),
            ("a::<string>()", new MethodCallExpression(new MethodCall(
                        new ValueAccessorExpression(new ValueAccessor(
                            ValueAccessType.Variable,
                            Identifier("a"), [StringType()])),
                []), SourceRange.Default)),
            ("a::<string, int>()", new MethodCallExpression(new MethodCall(
                new ValueAccessorExpression(
                    new ValueAccessor(
                        ValueAccessType.Variable,
                        Identifier("a"),
                        [StringType(), IntType()]
                    )), []), SourceRange.Default)),
            ("a::<string, int, result::<int>>()", new MethodCallExpression(new MethodCall(
                new ValueAccessorExpression(
                    new ValueAccessor(
                        ValueAccessType.Variable,
                        Identifier("a"),
                        [StringType(), IntType(), NamedTypeIdentifier("result", [IntType()])]
                    )),
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
                    Identifier("b"), null))),
            ("a.b()",
                new MethodCallExpression(new MethodCall(
                    new MemberAccessExpression(new MemberAccess(VariableAccessor("a"),
                        Identifier("b"), null)), []), SourceRange.Default)),
            ("a?.b", new MemberAccessExpression(new MemberAccess(
                new UnaryOperatorExpression(new UnaryOperator(UnaryOperatorType.FallOut, VariableAccessor("a"),
                    Token.QuestionMark(SourceSpan.Default))),
                Identifier("b"), null))),
            ("a.b?", new UnaryOperatorExpression(new UnaryOperator(
                UnaryOperatorType.FallOut,
                new MemberAccessExpression(new MemberAccess(VariableAccessor("a"),
                    Identifier("b"), null)),
                Token.QuestionMark(SourceSpan.Default)))),
            ("a * b.c", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                VariableAccessor("a"),
                new MemberAccessExpression(new MemberAccess(VariableAccessor("b"),
                    Identifier("c"), null)),
                Token.Star(SourceSpan.Default)))),
            ("b.c * a", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                new MemberAccessExpression(new MemberAccess(VariableAccessor("b"),
                    Identifier("c"), null)),
                VariableAccessor("a"),
                Token.Star(SourceSpan.Default)))),
            ("new Thing {}", new ObjectInitializerExpression(new ObjectInitializer(
                NamedTypeIdentifier("Thing"),
                []), SourceRange.Default)),
            ("new Thing {A = a}", new ObjectInitializerExpression(new ObjectInitializer(
                    NamedTypeIdentifier("Thing"),
                    [new FieldInitializer(Identifier("A"), VariableAccessor("a"))]),
                SourceRange.Default)),
            ("myFn(a,)", new MethodCallExpression(new MethodCall(
                new ValueAccessorExpression(new ValueAccessor(
                    ValueAccessType.Variable,
                    Identifier("myFn"), null)),
                [
                    new ValueAccessorExpression(new ValueAccessor(
                        ValueAccessType.Variable,
                        Identifier("a"), null))
                ]), SourceRange.Default)),
            ("new SomeType::<string,>{}", new ObjectInitializerExpression(new ObjectInitializer(
                NamedTypeIdentifier("SomeType", [
                    NamedTypeIdentifier("string")
                ]),
                []), SourceRange.Default)),
            ("SomeFn::<string,>()", new MethodCallExpression(new MethodCall(
                        new ValueAccessorExpression(new ValueAccessor(
                            ValueAccessType.Variable,
                            Identifier("SomeFn"), [StringType()])),
                []
            ), SourceRange.Default)),
            ("new Thing {A = a,}", new ObjectInitializerExpression(new ObjectInitializer(
                    NamedTypeIdentifier("Thing"),
                    [new FieldInitializer(Identifier("A"), VariableAccessor("a"))]),
                SourceRange.Default)),
            ("new Thing {A = a, B = b}", new ObjectInitializerExpression(new ObjectInitializer(
                NamedTypeIdentifier("Thing"),
                [
                    new FieldInitializer(Identifier("A"), VariableAccessor("a")),
                    new FieldInitializer(Identifier("B"), VariableAccessor("b"))
                ]), SourceRange.Default)),
            ("MyType::CallMethod",
                new StaticMemberAccessExpression(new StaticMemberAccess(
                    NamedTypeIdentifier("MyType"),
                    Identifier("CallMethod"), null))),
            ("MyType::StaticField.InstanceField", new MemberAccessExpression(
                new MemberAccess(
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("MyType"),
                        Identifier("StaticField"), null)),
                    Identifier("InstanceField"), null))),
            ("string::CallMethod",
                new StaticMemberAccessExpression(new StaticMemberAccess(
                    NamedTypeIdentifier("string"),
                    Identifier("CallMethod"), null))),
            ("result::<string>::CallMethod", new StaticMemberAccessExpression(new StaticMemberAccess(
                NamedTypeIdentifier("result",
                    [NamedTypeIdentifier("string")]),
                Identifier("CallMethod"), null))),
            (
                "a = b matches _",
                BinaryOperatorExpression(BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"),
                    Matches(VariableAccessor("b"), DiscardPattern()))
            ),
            (
                "a && b matches _",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    VariableAccessor("a"),
                    Matches(VariableAccessor("b"), DiscardPattern()))
            ),
            (
                "a matches _ && b",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    Matches(VariableAccessor("a"), DiscardPattern()),
                    VariableAccessor("b"))
            ),
            // ____binding strength tests
            // __greater than
            ( // greater than
                "a > b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.RightAngleBracket(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a > b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.RightAngleBracket(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a > b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Star(SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // divide
                "a > b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.ForwardSlash(SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // plus
                "a > b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Plus(SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // minus
                "a > b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Dash(SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // fallOut
                "a > b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
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
            ( // negative equality check
                "a > b != c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.RightAngleBracket(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a > b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Identifier("c"), null)),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // static member access
                "a > b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("b"),
                        Identifier("c"), null)),
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
            ( // negate
                "a > -b",
                BinaryOperatorExpression(
                    BinaryOperatorType.GreaterThan,
                    VariableAccessor("a"),
                    UnaryOperatorExpression(UnaryOperatorType.Negate, VariableAccessor("b")))
            ),
            ( // and 
                "a > b && c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    BinaryOperatorExpression(BinaryOperatorType.GreaterThan,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // or 
                "a > b || c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    BinaryOperatorExpression(BinaryOperatorType.GreaterThan,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            // __Less than
            ( // greater than
                "a < b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.LeftAngleBracket(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a < b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.LeftAngleBracket(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a < b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Star(SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // divide
                "a < b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.ForwardSlash(SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // plus
                "a < b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Plus(SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // minus
                "a < b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Dash(SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // fallOut
                "a < b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
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
            ( // negative equality check
                "a < b != c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.LeftAngleBracket(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a < b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Identifier("c"), null)),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // static member access
                "a < b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("b"),
                        Identifier("c"), null)),
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
            ( // and 
                "a < b && c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    BinaryOperatorExpression(BinaryOperatorType.LessThan,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // or 
                "a < b || c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    BinaryOperatorExpression(BinaryOperatorType.LessThan,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // negate
                "a < -b",
                BinaryOperatorExpression(
                    BinaryOperatorType.LessThan,
                    VariableAccessor("a"),
                    UnaryOperatorExpression(UnaryOperatorType.Negate, VariableAccessor("b")))
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
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.Star(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "a * b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.Star(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // plus
                "a * b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.Star(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a * b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.Star(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
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
            ( // negative equality check
                "a * b != c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.Star(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a * b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Identifier("c"), null)),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // static member access
                "a * b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("b"),
                        Identifier("c"), null)),
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
            ( // and 
                "a * b && c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    BinaryOperatorExpression(BinaryOperatorType.Multiply,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // or 
                "a * b || c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    BinaryOperatorExpression(BinaryOperatorType.Multiply,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // negate
                "a * -b",
                BinaryOperatorExpression(
                    BinaryOperatorType.Multiply,
                    VariableAccessor("a"),
                    UnaryOperatorExpression(UnaryOperatorType.Negate, VariableAccessor("b")))
            ),
            // __divide
            ( // greater than
                "a / b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a / b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a / b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "a / b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // plus
                "a / b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a / b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a / b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
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
            ( // equality check
                "a / b != c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.ForwardSlash(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a / b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Identifier("c"), null)),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // static member access
                "a / b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("b"),
                        Identifier("c"), null)),
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
            ( // and 
                "a / b && c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    BinaryOperatorExpression(BinaryOperatorType.Divide,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // or 
                "a / b || c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    BinaryOperatorExpression(BinaryOperatorType.Divide,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // negate
                "a / -b",
                BinaryOperatorExpression(
                    BinaryOperatorType.Divide,
                    VariableAccessor("a"),
                    UnaryOperatorExpression(UnaryOperatorType.Negate, VariableAccessor("b")))
            ),
            // __plus
            ( // greater than
                "a + b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.Plus(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a + b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.Plus(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a + b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Star(SourceSpan.Default))),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // divide
                "a + b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
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
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.Plus(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a + b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.Plus(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a + b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
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
            ( // equality check
                "a + b != c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.Plus(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a + b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Identifier("c"), null)),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // static member access
                "a + b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("b"),
                        Identifier("c"), null)),
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
            ( // and 
                "a + b && c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    BinaryOperatorExpression(BinaryOperatorType.Plus,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // or 
                "a + b || c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    BinaryOperatorExpression(BinaryOperatorType.Plus,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // negate
                "a + -b",
                BinaryOperatorExpression(
                    BinaryOperatorType.Plus,
                    VariableAccessor("a"),
                    UnaryOperatorExpression(UnaryOperatorType.Negate, VariableAccessor("b")))
            ),
            // __minus
            ( // greater than
                "a - b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.Dash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a - b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.Dash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a - b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Star(SourceSpan.Default))),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // divide
                "a - b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
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
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.Dash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a - b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.Dash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"), null)),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a - b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
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
            ( // negative equality check
                "a - b != c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.Dash(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a - b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Identifier("c"), null)),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // static member access
                "a - b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("b"),
                        Identifier("c"), null)),
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
            ( // and 
                "a - b && c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    BinaryOperatorExpression(BinaryOperatorType.Minus,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // or 
                "a - b || c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    BinaryOperatorExpression(BinaryOperatorType.Minus,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // negate
                "a - -b",
                BinaryOperatorExpression(
                    BinaryOperatorType.Minus,
                    VariableAccessor("a"),
                    UnaryOperatorExpression(UnaryOperatorType.Negate, VariableAccessor("b")))
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
            ( // negative equality check
                "a? != c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a?.c",
                new MemberAccessExpression(new MemberAccess(
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(SourceSpan.Default))),
                    Identifier("c"), null))
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
            ( // and 
                "a? && b",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    FallOut(VariableAccessor("a")),
                    VariableAccessor("b"))
            ),
            ( // or 
                "a? || b",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    FallOut(VariableAccessor("a")),
                    VariableAccessor("b"))
            ),
            ( // negate
                "-a?",
                Negate(FallOut(VariableAccessor("a")))
            ),
            // __ value assignment
            ( // greater than
                "a = b > c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.RightAngleBracket(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // less than
                "a = b < c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.LeftAngleBracket(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // multiply
                "a = b * c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Star(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // divide
                "a = b / c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.ForwardSlash(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // plus
                "a = b + c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Plus(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // minus
                "a = b - c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Dash(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // fallOut
                "a = b?",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
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
            ( // negative equality check
                "a = b != c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.NegativeEqualityCheck,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.NotEquals(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // member access
                "a = b.c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Identifier("c"), null)),
                    Token.Equals(SourceSpan.Default)))
            ),
            ( // static member access
                "a = b::c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("b"),
                        Identifier("c"), null)),
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
            ( // and 
                "a = b && c",
                BinaryOperatorExpression(BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // or 
                "a = b || c ",
                BinaryOperatorExpression(BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // negate
                "a = -b",
                BinaryOperatorExpression(
                    BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"),
                    UnaryOperatorExpression(UnaryOperatorType.Negate, VariableAccessor("b")))
            ),
            // __ equality check
            ( // greater than
                "a == b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.RightAngleBracket(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // less than
                "a == b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.LeftAngleBracket(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // multiply
                "a == b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Star(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // divide
                "a == b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.ForwardSlash(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // plus
                "a == b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Plus(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // minus
                "a == b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Dash(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // fallOut
                "a == b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
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
            ( // negative equality check
                "a == b != c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.DoubleEquals(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a == b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Identifier("c"), null)),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // static member access
                "a == b::c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.EqualityCheck,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("b"),
                        Identifier("c"), null)),
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
            ( // and 
                "a == b && c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    BinaryOperatorExpression(BinaryOperatorType.EqualityCheck,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // or 
                "a == b || c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    BinaryOperatorExpression(BinaryOperatorType.EqualityCheck,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // negate
                "a == -b",
                BinaryOperatorExpression(
                    BinaryOperatorType.EqualityCheck,
                    VariableAccessor("a"),
                    UnaryOperatorExpression(UnaryOperatorType.Negate, VariableAccessor("b")))
            ),
            // __ negative equality check
            ( // greater than
                "a != b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.RightAngleBracket(SourceSpan.Default))),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // less than
                "a != b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.LeftAngleBracket(SourceSpan.Default))),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // multiply
                "a != b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Star(SourceSpan.Default))),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // divide
                "a != b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.ForwardSlash(SourceSpan.Default))),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // plus
                "a != b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Plus(SourceSpan.Default))),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // minus
                "a != b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"), null)),
                        Token.Dash(SourceSpan.Default))),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // fallOut
                "a != b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"), null)),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"), null)),
                        Token.QuestionMark(SourceSpan.Default))),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // value assignment
                "a != b = c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.NegativeEqualityCheck,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.NotEquals(SourceSpan.Default))),
                    VariableAccessor("c"), Token.Equals(SourceSpan.Default)))
            ),
            ( // equality check
                "a != b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.NegativeEqualityCheck,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.NotEquals(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // negative equality check
                "a != b != c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.NegativeEqualityCheck,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.NotEquals(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a != b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    VariableAccessor("a"),
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("b"),
                        Identifier("c"), null)),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // static member access
                "a != b::c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.NegativeEqualityCheck,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("b"),
                        Identifier("c"), null)),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // not
                "a != !b",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    VariableAccessor("a"),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("b"),
                        Token.Bang(SourceSpan.Default))),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // and 
                "a != b && c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    BinaryOperatorExpression(BinaryOperatorType.NegativeEqualityCheck,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // or 
                "a != b || c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    BinaryOperatorExpression(BinaryOperatorType.NegativeEqualityCheck,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // negate
                "a != -b",
                BinaryOperatorExpression(
                    BinaryOperatorType.NegativeEqualityCheck,
                    VariableAccessor("a"),
                    UnaryOperatorExpression(UnaryOperatorType.Negate, VariableAccessor("b")))
            ),
            // __Member Access
            ( // greater than
                "a.b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a.b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a.b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "a.b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // plus
                "a.b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a.b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a.b?",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"), null)),
                    Token.QuestionMark(SourceSpan.Default)))
            ),
            ( // value assignment
                "a.b = c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.ValueAssignment,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.Equals(SourceSpan.Default)))
            ),
            ( // equality check
                "a.b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // negative equality check
                "a.b != c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a.b.c",
                new MemberAccessExpression(new MemberAccess(
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"), null)),
                    Identifier("c"), null))
            ),
            ( // and 
                "a.b && c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    MemberAccess(VariableAccessor("a"), "b"),
                    VariableAccessor("c"))
            ),
            ( // or 
                "a.b || c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    MemberAccess(VariableAccessor("a"), "b"),
                    VariableAccessor("c"))
            ),
            ( // negate
                "-a.b",
                UnaryOperatorExpression(
                    UnaryOperatorType.Negate,
                    MemberAccess(
                        VariableAccessor("a"), "b"))
            ),
            // __Static Member Access
            ( // greater than
                "a::b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a::b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a::b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "a::b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // plus
                "a::b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a::b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a::b?",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("a"),
                        Identifier("b"), null)),
                    Token.QuestionMark(SourceSpan.Default)))
            ),
            ( // value assignment
                "a::b = c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.ValueAssignment,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.Equals(SourceSpan.Default)))
            ),
            ( // equality check
                "a::b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // negative equality check
                "a::b != c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("a"),
                        Identifier("b"), null)),
                    VariableAccessor("c"),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a::b.c",
                new MemberAccessExpression(new MemberAccess(
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        NamedTypeIdentifier("a"),
                        Identifier("b"), null)),
                    Identifier("c"), null))
            ),
            ( // and 
                "a::b && c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    StaticMemberAccess(NamedTypeIdentifier("a"), "b"),
                    VariableAccessor("c"))
            ),
            ( // or 
                "a::b || c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    StaticMemberAccess(NamedTypeIdentifier("a"), "b"),
                    VariableAccessor("c"))
            ),
            ( // negate
                "-a::b",
                UnaryOperatorExpression(UnaryOperatorType.Negate,
                    StaticMemberAccess(NamedTypeIdentifier("a"), "b"))
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
            ( // negative equality check
                "!a != c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.NegativeEqualityCheck,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.Not,
                        VariableAccessor("a"),
                        Token.Bang(SourceSpan.Default))),
                    VariableAccessor("c"),
                    Token.NotEquals(SourceSpan.Default)))
            ),
            ( // member access
                "!a.c",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.Not,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("c"), null)),
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
            ),
            ( // and 
                "!a && b",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    Not(VariableAccessor("a")),
                    VariableAccessor("b"))
            ),
            ( // or 
                "!a || b",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    Not(VariableAccessor("a")),
                    VariableAccessor("b"))
            ),
            ( // negate
                "!-a",
                Not(Negate(VariableAccessor("a")))
            ),
            // __And
            ( // fallout
                "a && !b",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    VariableAccessor("a"),
                    Not(VariableAccessor("b")))
            ),
            ( // less than
                "a && b < c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.LessThan,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // greater than
                "a && b > c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.GreaterThan,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // plus
                "a && b + c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.Plus,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // minus
                "a && b - c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.Minus,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // multiply
                "a && b * c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.Multiply,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // divide
                "a && b / c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.Divide,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // assignment
                "a && b = c",
                BinaryOperatorExpression(BinaryOperatorType.ValueAssignment,
                    BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // equality check
                "a && b == c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.EqualityCheck,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // negative equality check
                "a && b != c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.NegativeEqualityCheck,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // member access
                "a && b.c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    VariableAccessor("a"),
                    MemberAccess(
                        VariableAccessor("b"),
                        "c"))
            ),
            ( // not
                "a && !b",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    VariableAccessor("a"),
                    Not(VariableAccessor("b")))
            ),
            ( // fallout
                "a && b?",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    VariableAccessor("a"),
                    FallOut(VariableAccessor("b")))
            ),
            ( // and 
                "a && b && c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // or 
                "a && b || c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // negate
                "a && -b",
                BinaryOperatorExpression(
                    BinaryOperatorType.BooleanAnd,
                    VariableAccessor("a"),
                    Negate(VariableAccessor("b")))
            ),
            // __Or
            ( // fallout
                "a || !b",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    VariableAccessor("a"),
                    Not(VariableAccessor("b")))
            ),
            ( // less than
                "a || b < c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.LessThan,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // greater than
                "a || b > c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.GreaterThan,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // plus
                "a || b + c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.Plus,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // minus
                "a || b - c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.Minus,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // multiply
                "a || b * c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.Multiply,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // divide
                "a || b / c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.Divide,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // assignment
                "a || b = c",
                BinaryOperatorExpression(BinaryOperatorType.ValueAssignment,
                    BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // equality check
                "a || b == c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.EqualityCheck,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // negative equality check
                "a || b != c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    VariableAccessor("a"),
                    BinaryOperatorExpression(BinaryOperatorType.NegativeEqualityCheck,
                        VariableAccessor("b"),
                        VariableAccessor("c")))
            ),
            ( // member access
                "a || b.c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    VariableAccessor("a"),
                    MemberAccess(
                        VariableAccessor("b"),
                        "c"))
            ),
            ( // not
                "a || !b",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    VariableAccessor("a"),
                    Not(VariableAccessor("b")))
            ),
            ( // fallout
                "a || b?",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    VariableAccessor("a"),
                    FallOut(VariableAccessor("b")))
            ),
            ( // and 
                "a || b && c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // or 
                "a || b || c",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                        VariableAccessor("a"),
                        VariableAccessor("b")),
                    VariableAccessor("c"))
            ),
            ( // negate
                "a || -b",
                BinaryOperatorExpression(
                    BinaryOperatorType.BooleanOr,
                    VariableAccessor("a"),
                    Negate(VariableAccessor("b")))
            ),
            // __Negate
            ( // fallout
                "-a?",
                Negate(FallOut(VariableAccessor("a")))
            ),
            ( // less than
                "-a < b",
                BinaryOperatorExpression(BinaryOperatorType.LessThan,
                    Negate(VariableAccessor("a")),
                    VariableAccessor("b"))
            ),
            ( // greater than
                "-a > b",
                BinaryOperatorExpression(BinaryOperatorType.GreaterThan,
                    Negate(VariableAccessor("a")),
                    VariableAccessor("b"))

            ),
            ( // plus
                "-a + b",
                BinaryOperatorExpression(BinaryOperatorType.Plus,
                    Negate(VariableAccessor("a")),
                    VariableAccessor("b"))

            ),
            ( // minus
                "-a - b",
                BinaryOperatorExpression(BinaryOperatorType.Minus,
                    Negate(VariableAccessor("a")),
                    VariableAccessor("b"))

            ),
            ( // multiply
                "-a * b",
                BinaryOperatorExpression(BinaryOperatorType.Multiply,
                    Negate(VariableAccessor("a")),
                    VariableAccessor("b"))

            ),
            ( // divide
                "-a / b",
                BinaryOperatorExpression(BinaryOperatorType.Divide,
                    Negate(VariableAccessor("a")),
                    VariableAccessor("b"))

            ),
            ( // assignment
                "-a = b",
                BinaryOperatorExpression(BinaryOperatorType.ValueAssignment,
                    Negate(VariableAccessor("a")),
                    VariableAccessor("b"))
),
            ( // equality check
                "-a == b",
                BinaryOperatorExpression(BinaryOperatorType.EqualityCheck,
                    Negate(VariableAccessor("a")),
                    VariableAccessor("b"))

            ),
            ( // negative equality check
                "-a != b",
                BinaryOperatorExpression(BinaryOperatorType.NegativeEqualityCheck,
                    Negate(VariableAccessor("a")),
                    VariableAccessor("b"))

            ),
            ( // member access
                "-a.c",
                Negate(MemberAccess(VariableAccessor("a"), "c"))
            ),
            ( // not
                "-!b",
                Negate(Not(VariableAccessor("b")))
            ),
            ( // and 
                "-a && b",
                BinaryOperatorExpression(BinaryOperatorType.BooleanAnd,
                    Negate(VariableAccessor("a")),
                    VariableAccessor("b"))
            ),
            ( // or 
                "-a || b",
                BinaryOperatorExpression(BinaryOperatorType.BooleanOr,
                    Negate(VariableAccessor("a")),
                    VariableAccessor("b"))
            ),
            ( // negate
                "--a",
                Negate(Negate(VariableAccessor("a")))
            ),
        }.Select(x => new object[] { x.Source, Tokenizer.Tokenize(x.Source), x.ExpectedExpression });
    }
}
