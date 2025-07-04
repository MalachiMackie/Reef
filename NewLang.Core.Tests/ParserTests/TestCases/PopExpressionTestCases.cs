namespace NewLang.Core.Tests.ParserTests.TestCases;

using static ExpressionHelpers;

public static class PopExpressionTestCases
{
    public static IEnumerable<object[]> TestCases()
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
                        new ClassPattern(
                            new TypeIdentifier(Identifier("SomeClass"), [],
                                SourceRange.Default),
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
                        new ClassPattern(
                            new TypeIdentifier(Identifier("SomeClass"), [],
                                SourceRange.Default),
                            [new FieldPattern(Identifier("SomeField"), null)],
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
                            new TypeIdentifier(Identifier("SomeUnion"), [],
                                SourceRange.Default),
                            Identifier("B"),
                            [
                                new FieldPattern(
                                    Identifier("SomeField"),
                                    new UnionStructVariantPattern(
                                        new TypeIdentifier(Identifier("OtherUnion"), [],
                                            SourceRange.Default),
                                        Identifier("C"),
                                        [
                                            new FieldPattern(
                                                Identifier("OtherField"),
                                                new VariableDeclarationPattern(
                                                    Identifier("d"), SourceRange.Default)
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
                            new TypeIdentifier(Identifier("OtherUnion"), [],
                                SourceRange.Default),
                            Identifier("B"),
                            [
                                new UnionVariantPattern(
                                    new TypeIdentifier(Identifier("MyUnion"), [],
                                        SourceRange.Default),
                                    Identifier("A"),
                                    Identifier("c"), SourceRange.Default)
                            ],
                            Identifier("b"), SourceRange.Default), SourceRange.Default),
                    new BlockExpression(new Block([], []), SourceRange.Default),
                    [],
                    null), SourceRange.Default)
            ),
            (
                "var b: bool = a matches int;",
                new VariableDeclarationExpression(new VariableDeclaration(
                        Identifier("b"),
                        null,
                        new TypeIdentifier(Identifier("bool"), [], SourceRange.Default),
                        new MatchesExpression(
                            VariableAccessor("a"),
                            new ClassPattern(
                                new TypeIdentifier(Identifier("string"), [], SourceRange.Default),
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
                        new TypeIdentifier(Identifier("string"), [], SourceRange.Default),
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
                        new TypeIdentifier(Identifier("MyUnion"), [], SourceRange.Default),
                        Identifier("A"),
                        null
                        , SourceRange.Default)
                    , SourceRange.Default)
            ),
            (
                "a matches var a",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new VariableDeclarationPattern(Identifier("a"), SourceRange.Default)
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
                        new TypeIdentifier(Identifier("MyUnion"), [], SourceRange.Default),
                        Identifier("A"),
                        [
                            new VariableDeclarationPattern(Identifier("b"),
                                SourceRange.Default)
                        ],
                        null, SourceRange.Default)
                    , SourceRange.Default)
            ),
            (
                "a matches MyUnion::A(var b) var c",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionTupleVariantPattern(
                        new TypeIdentifier(Identifier("MyUnion"), [], SourceRange.Default),
                        Identifier("A"),
                        [
                            new VariableDeclarationPattern(Identifier("b"),
                                SourceRange.Default)
                        ],
                        Identifier("c"), SourceRange.Default)
                    , SourceRange.Default)
            ),
            (
                "a matches MyUnion::A(var b, var c, _)",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionTupleVariantPattern(
                        new TypeIdentifier(Identifier("MyUnion"), [], SourceRange.Default),
                        Identifier("A"),
                        [
                            new VariableDeclarationPattern(Identifier("b"),
                                SourceRange.Default),
                            new VariableDeclarationPattern(Identifier("c"),
                                SourceRange.Default),
                            new DiscardPattern(SourceRange.Default)
                        ], null, SourceRange.Default)
                    , SourceRange.Default)
            ),
            (
                "a matches MyUnion::A(OtherUnion::C)",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionTupleVariantPattern(
                        new TypeIdentifier(Identifier("MyUnion"), [], SourceRange.Default),
                        Identifier("A"),
                        [
                            new UnionVariantPattern(
                                new TypeIdentifier(Identifier("OtherUnion"), [],
                                    SourceRange.Default),
                                Identifier("C"),
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
                        new TypeIdentifier(Identifier("MyUnion"), [], SourceRange.Default),
                        Identifier("A"),
                        [
                            new UnionVariantPattern(
                                new TypeIdentifier(Identifier("OtherUnion"), [],
                                    SourceRange.Default),
                                Identifier("C"),
                                Identifier("c")
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
                        new TypeIdentifier(Identifier("MyUnion"), [], SourceRange.Default),
                        Identifier("A"),
                        [
                            new UnionTupleVariantPattern(
                                new TypeIdentifier(Identifier("OtherUnion"), [],
                                    SourceRange.Default),
                                Identifier("C"),
                                [
                                    new VariableDeclarationPattern(Identifier("d"),
                                        SourceRange.Default)
                                ],
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
                        new TypeIdentifier(Identifier("MyUnion"), [], SourceRange.Default),
                        Identifier("A"),
                        [
                            new FieldPattern(Identifier("MyField"), null)
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
                        new TypeIdentifier(Identifier("MyUnion"), [], SourceRange.Default),
                        Identifier("A"),
                        [
                            new FieldPattern(Identifier("MyField"), null)
                        ],
                        false,
                        Identifier("a")
                        , SourceRange.Default), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A { MyField, OtherField: var f }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionStructVariantPattern(
                        new TypeIdentifier(Identifier("MyUnion"), [], SourceRange.Default),
                        Identifier("A"),
                        [
                            new FieldPattern(Identifier("MyField"), null),
                            new FieldPattern(
                                Identifier("OtherField"),
                                new VariableDeclarationPattern(Identifier("f"),
                                    SourceRange.Default)
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
                        new TypeIdentifier(Identifier("MyUnion"), [], SourceRange.Default),
                        [
                            new FieldPattern(Identifier("MyField"), null)
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
                        new TypeIdentifier(Identifier("MyUnion"), [], SourceRange.Default),
                        [
                            new FieldPattern(Identifier("MyField"), null)
                        ],
                        false,
                        Identifier("a")
                        , SourceRange.Default), SourceRange.Default)
            ),
            (
                "a matches MyUnion::A { MyField, _ }",
                new MatchesExpression(
                    VariableAccessor("a"),
                    new UnionStructVariantPattern(
                        new TypeIdentifier(Identifier("MyUnion"), [], SourceRange.Default),
                        Identifier("A"),
                        [
                            new FieldPattern(Identifier("MyField"), null)
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
                        new TypeIdentifier(Identifier("MyUnion"), [], SourceRange.Default),
                        Identifier("A"),
                        [
                            new FieldPattern(
                                Identifier("MyField"),
                                new UnionVariantPattern(
                                    new TypeIdentifier(Identifier("MyUnion"), [],
                                        SourceRange.Default),
                                    Identifier("B"),
                                    Identifier("f"), SourceRange.Default))
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
                        new TypeIdentifier(Identifier("MyUnion"), [], SourceRange.Default),
                        Identifier("A"),
                        [
                            new FieldPattern(
                                Identifier("MyField"),
                                new UnionTupleVariantPattern(
                                    new TypeIdentifier(Identifier("MyUnion"), [],
                                        SourceRange.Default),
                                    Identifier("B"),
                                    [
                                        new VariableDeclarationPattern(Identifier("c"),
                                            SourceRange.Default)
                                    ],
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
                        new TypeIdentifier(Identifier("MyClass"), [], SourceRange.Default),
                        [
                            new FieldPattern(Identifier("MyField"), null)
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
                        new TypeIdentifier(Identifier("MyClass"), [], SourceRange.Default),
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
                        new TypeIdentifier(Identifier("MyClass"), [], SourceRange.Default),
                        [],
                        false,
                        Identifier("b")
                        , SourceRange.Default)
                    , SourceRange.Default)
            ),


            // value access expressions
            ("a",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                    Identifier("a")))),
            ("this",
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                    Identifier("this")))),
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
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Identifier("ok")))),
            ("a == b",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.EqualityCheck, VariableAccessor("a"),
                    VariableAccessor("b"), Token.DoubleEquals(SourceSpan.Default)))),
            ("ok()",
                new MethodCallExpression(new MethodCall(
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("ok"))), []), SourceRange.Default)),
            ("(a)", new TupleExpression([VariableAccessor("a")], SourceRange.Default)),
            ("(a, b)", new TupleExpression([VariableAccessor("a"), VariableAccessor("b")], SourceRange.Default)),
            ("!a", new UnaryOperatorExpression(new UnaryOperator(
                UnaryOperatorType.Not,
                VariableAccessor("a"),
                Token.Bang(SourceSpan.Default)))),
            ("a?", new UnaryOperatorExpression(new UnaryOperator(
                UnaryOperatorType.FallOut,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                    Identifier("a"))),
                Token.QuestionMark(SourceSpan.Default)))),
            ("a??",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
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
                    Identifier("a"))),
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
                    Identifier("a"),
                    null,
                    new TypeIdentifier(Identifier("int"), [], SourceRange.Default),
                    VariableAccessor("b")), SourceRange.Default)),
            ("var a: int", new VariableDeclarationExpression(
                new VariableDeclaration(
                    Identifier("a"),
                    null,
                    new TypeIdentifier(Identifier("int"), [], SourceRange.Default),
                    null), SourceRange.Default)),
            ("var mut a = 2", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default)),
            ("a = b",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"), VariableAccessor("b"), Token.Equals(SourceSpan.Default)))),
            ("var mut a: int = 2", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                new TypeIdentifier(Identifier("int"), [], SourceRange.Default),
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default)),
            ("var a: bool = b", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                new TypeIdentifier(Identifier("bool"), [], SourceRange.Default),
                VariableAccessor("b")), SourceRange.Default)),
            ("var a: int = b", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                new TypeIdentifier(Identifier("int"), [], SourceRange.Default),
                VariableAccessor("b")), SourceRange.Default)),
            ("var a: string = b", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                new TypeIdentifier(Identifier("string"), [], SourceRange.Default),
                VariableAccessor("b")), SourceRange.Default)),
            ("var a: result = b", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                new TypeIdentifier(Identifier("result"), [], SourceRange.Default),
                VariableAccessor("b")), SourceRange.Default)),
            ("var a: MyType = b", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                new TypeIdentifier(Identifier("MyType"), [], SourceRange.Default),
                VariableAccessor("b")), SourceRange.Default)),
            ("var a = 1", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.IntLiteral(1, SourceSpan.Default)))), SourceRange.Default)),
            ("var a = true", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.True(SourceSpan.Default)))), SourceRange.Default)),
            ("var a = \"thing\"", new VariableDeclarationExpression(new VariableDeclaration(
                Identifier("a"),
                null,
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                    Token.StringLiteral("thing", SourceSpan.Default)))), SourceRange.Default)),
            ("{}", new BlockExpression(new Block([], []), SourceRange.Default)),
            ("{var a = 1;}", new BlockExpression(new Block([
                new VariableDeclarationExpression(new VariableDeclaration(Identifier("a"),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default)))), SourceRange.Default)
            ], []), SourceRange.Default)),
            // tail expression
            ("{var a = 1}", new BlockExpression(new Block(
            [
                new VariableDeclarationExpression(new VariableDeclaration(Identifier("a"),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default)))), SourceRange.Default)
            ], []), SourceRange.Default)),
            // tail expression
            ("{var a = 1;var b = 2}", new BlockExpression(new Block(
            [
                new VariableDeclarationExpression(new VariableDeclaration(Identifier("a"),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default)))), SourceRange.Default),
                new VariableDeclarationExpression(new VariableDeclaration(Identifier("b"),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default)
            ], []), SourceRange.Default)),
            ("{var a = 1; var b = 2;}", new BlockExpression(new Block([
                new VariableDeclarationExpression(new VariableDeclaration(Identifier("a"),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default)))), SourceRange.Default),
                new VariableDeclarationExpression(new VariableDeclaration(Identifier("b"),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default)
            ], []), SourceRange.Default)),
            ("if (a) var c = 2;", new IfExpressionExpression(new IfExpression(
                    VariableAccessor("a"),
                    new VariableDeclarationExpression(new VariableDeclaration(
                        Identifier("c"),
                        null,
                        null,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                            Token.IntLiteral(2, SourceSpan.Default)))), SourceRange.Default), [], null),
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
                            Token.StringLiteral("value", SourceSpan.Default)))), SourceRange.Default)
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
                Identifier("a"),
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
                Identifier("a"),
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
                            Identifier("a"))),
                        Identifier("b")
                    )),
                    []
                ), SourceRange.Default),
                []
            ), SourceRange.Default)),
            ("a::<string>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(new GenericInstantiation(
                        new ValueAccessorExpression(new ValueAccessor(
                            ValueAccessType.Variable,
                            Identifier("a"))),
                        [new TypeIdentifier(Identifier("string"), [], SourceRange.Default)]),
                    SourceRange.Default),
                []), SourceRange.Default)),
            ("a::<string, int>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(new GenericInstantiation(new ValueAccessorExpression(
                    new ValueAccessor(
                        ValueAccessType.Variable,
                        Identifier("a")
                    )), [
                    new TypeIdentifier(Identifier("string"), [], SourceRange.Default),
                    new TypeIdentifier(Identifier("int"), [], SourceRange.Default)
                ]), SourceRange.Default), []), SourceRange.Default)),
            ("a::<string, int, result::<int>>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(new GenericInstantiation(new ValueAccessorExpression(
                    new ValueAccessor(
                        ValueAccessType.Variable,
                        Identifier("a")
                    )), [
                    new TypeIdentifier(Identifier("string"), [], SourceRange.Default),
                    new TypeIdentifier(Identifier("int"), [], SourceRange.Default),
                    new TypeIdentifier(Identifier("result"),
                        [new TypeIdentifier(Identifier("int"), [], SourceRange.Default)],
                        SourceRange.Default)
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
                    Identifier("b")))),
            ("a.b()",
                new MethodCallExpression(new MethodCall(
                    new MemberAccessExpression(new MemberAccess(VariableAccessor("a"),
                        Identifier("b"))), []), SourceRange.Default)),
            ("a?.b", new MemberAccessExpression(new MemberAccess(
                new UnaryOperatorExpression(new UnaryOperator(UnaryOperatorType.FallOut, VariableAccessor("a"),
                    Token.QuestionMark(SourceSpan.Default))),
                Identifier("b")))),
            ("a.b?", new UnaryOperatorExpression(new UnaryOperator(
                UnaryOperatorType.FallOut,
                new MemberAccessExpression(new MemberAccess(VariableAccessor("a"),
                    Identifier("b"))),
                Token.QuestionMark(SourceSpan.Default)))),
            ("a * b.c", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                VariableAccessor("a"),
                new MemberAccessExpression(new MemberAccess(VariableAccessor("b"),
                    Identifier("c"))),
                Token.Star(SourceSpan.Default)))),
            ("b.c * a", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                new MemberAccessExpression(new MemberAccess(VariableAccessor("b"),
                    Identifier("c"))),
                VariableAccessor("a"),
                Token.Star(SourceSpan.Default)))),
            ("new Thing {}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Identifier("Thing"), [], SourceRange.Default),
                []), SourceRange.Default)),
            ("new Thing {A = a}", new ObjectInitializerExpression(new ObjectInitializer(
                    new TypeIdentifier(Identifier("Thing"), [], SourceRange.Default),
                    [new FieldInitializer(Identifier("A"), VariableAccessor("a"))]),
                SourceRange.Default)),
            ("myFn(a,)", new MethodCallExpression(new MethodCall(
                new ValueAccessorExpression(new ValueAccessor(
                    ValueAccessType.Variable,
                    Identifier("myFn"))),
                [
                    new ValueAccessorExpression(new ValueAccessor(
                        ValueAccessType.Variable,
                        Identifier("a")))
                ]), SourceRange.Default)),
            ("new SomeType::<string,>{}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Identifier("SomeType"), [
                    new TypeIdentifier(Identifier("string"), [], SourceRange.Default)
                ], SourceRange.Default),
                []), SourceRange.Default)),
            ("SomeFn::<string,>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(
                    new GenericInstantiation(
                        new ValueAccessorExpression(new ValueAccessor(
                            ValueAccessType.Variable,
                            Identifier("SomeFn"))),
                        [new TypeIdentifier(Identifier("string"), [], SourceRange.Default)]),
                    SourceRange.Default),
                []
            ), SourceRange.Default)),
            ("new Thing {A = a,}", new ObjectInitializerExpression(new ObjectInitializer(
                    new TypeIdentifier(Identifier("Thing"), [], SourceRange.Default),
                    [new FieldInitializer(Identifier("A"), VariableAccessor("a"))]),
                SourceRange.Default)),
            ("new Thing {A = a, B = b}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Identifier("Thing"), [], SourceRange.Default),
                [
                    new FieldInitializer(Identifier("A"), VariableAccessor("a")),
                    new FieldInitializer(Identifier("B"), VariableAccessor("b"))
                ]), SourceRange.Default)),
            ("MyType::CallMethod",
                new StaticMemberAccessExpression(new StaticMemberAccess(
                    new TypeIdentifier(Identifier("MyType"), [], SourceRange.Default),
                    Identifier("CallMethod")))),
            ("MyType::StaticField.InstanceField", new MemberAccessExpression(
                new MemberAccess(
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("MyType"), [], SourceRange.Default),
                        Identifier("StaticField"))),
                    Identifier("InstanceField")
                ))),
            ("string::CallMethod",
                new StaticMemberAccessExpression(new StaticMemberAccess(
                    new TypeIdentifier(Identifier("string"), [], SourceRange.Default),
                    Identifier("CallMethod")))),
            ("result::<string>::CallMethod", new StaticMemberAccessExpression(new StaticMemberAccess(
                new TypeIdentifier(Identifier("result"),
                    [new TypeIdentifier(Identifier("string"), [], SourceRange.Default)],
                    SourceRange.Default),
                Identifier("CallMethod")))),
            // ____binding strength tests
            // __greater than
            ( // greater than
                "a > b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.RightAngleBracket(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a > b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.RightAngleBracket(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a > b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.Star(SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // divide
                "a > b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // plus
                "a > b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.Plus(SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // minus
                "a > b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.Dash(SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // fallOut
                "a > b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
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
                        Identifier("c")
                    )),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // static member access
                "a > b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("b"), [], SourceRange.Default),
                        Identifier("c"))),
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
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.LeftAngleBracket(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a < b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.LeftAngleBracket(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a < b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.Star(SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // divide
                "a < b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // plus
                "a < b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.Plus(SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // minus
                "a < b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.Dash(SourceSpan.Default))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // fallOut
                "a < b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
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
                        Identifier("c"))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // static member access
                "a < b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("b"), [], SourceRange.Default),
                        Identifier("c")
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
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.Star(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "a * b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.Star(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // plus
                "a * b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.Star(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a * b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.Star(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
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
                        Identifier("c"))),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // static member access
                "a * b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("b"), [], SourceRange.Default),
                        Identifier("c")
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
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a / b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a / b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "a / b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // plus
                "a / b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a / b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a / b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
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
                        Identifier("c"))),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // static member access
                "a / b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("b"), [], SourceRange.Default),
                        Identifier("c")
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
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.Plus(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a + b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.Plus(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a + b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.Star(SourceSpan.Default))),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // divide
                "a + b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
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
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.Plus(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a + b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.Plus(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a + b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
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
                        Identifier("c"))),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // static member access
                "a + b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("b"), [], SourceRange.Default),
                        Identifier("c")
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
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.Dash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a - b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.Dash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a - b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.Star(SourceSpan.Default))),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // divide
                "a - b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
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
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.Dash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a - b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("a"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        Token.Dash(SourceSpan.Default))),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("c"))),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a - b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
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
                        Identifier("c"))),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // static member access
                "a - b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("b"), [], SourceRange.Default),
                        Identifier("c")
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
                    Identifier("c")))
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
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.RightAngleBracket(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // less than
                "a = b < c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.LeftAngleBracket(SourceSpan.Default)))
                    , Token.Equals(SourceSpan.Default)))
            ),
            ( // multiply
                "a = b * c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.Star(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // divide
                "a = b / c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.ForwardSlash(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // plus
                "a = b + c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.Plus(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // minus
                "a = b - c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.Dash(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // fallOut
                "a = b?",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
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
                        Identifier("c"))),
                    Token.Equals(SourceSpan.Default)))
            ),
            ( // static member access
                "a = b::c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("b"), [], SourceRange.Default),
                        Identifier("c")
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
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.RightAngleBracket(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // less than
                "a == b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.LeftAngleBracket(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // multiply
                "a == b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.Star(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // divide
                "a == b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.ForwardSlash(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // plus
                "a == b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.Plus(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // minus
                "a == b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("c"))),
                        Token.Dash(SourceSpan.Default))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // fallOut
                "a == b?",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("a"))),
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                            Identifier("b"))),
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
                        Identifier("c"))),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // static member access
                "a == b::c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.EqualityCheck,
                    VariableAccessor("a"),
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("b"), [], SourceRange.Default),
                        Identifier("c")
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
                        Identifier("b"))),
                    VariableAccessor("c"),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a.b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"))),
                    VariableAccessor("c"),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a.b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"))),
                    VariableAccessor("c"),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "a.b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"))),
                    VariableAccessor("c"),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // plus
                "a.b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"))),
                    VariableAccessor("c"),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a.b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"))),
                    VariableAccessor("c"),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a.b?",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"))),
                    Token.QuestionMark(SourceSpan.Default)))
            ),
            ( // value assignment
                "a.b = c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.ValueAssignment,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"))),
                    VariableAccessor("c"),
                    Token.Equals(SourceSpan.Default)))
            ),
            ( // equality check
                "a.b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a.b.c",
                new MemberAccessExpression(new MemberAccess(
                    new MemberAccessExpression(new MemberAccess(
                        VariableAccessor("a"),
                        Identifier("b"))),
                    Identifier("c")))
            ),
            // __Static Member Access
            ( // greater than
                "a::b > c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("a"), [], SourceRange.Default),
                        Identifier("b")
                    )),
                    VariableAccessor("c"),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a::b < c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("a"), [], SourceRange.Default),
                        Identifier("b")
                    )),
                    VariableAccessor("c"),
                    Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a::b * c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("a"), [], SourceRange.Default),
                        Identifier("b")
                    )),
                    VariableAccessor("c"),
                    Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "a::b / c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("a"), [], SourceRange.Default),
                        Identifier("b")
                    )),
                    VariableAccessor("c"),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // plus
                "a::b + c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("a"), [], SourceRange.Default),
                        Identifier("b")
                    )),
                    VariableAccessor("c"),
                    Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a::b - c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("a"), [], SourceRange.Default),
                        Identifier("b")
                    )),
                    VariableAccessor("c"),
                    Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a::b?",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("a"), [], SourceRange.Default),
                        Identifier("b")
                    )),
                    Token.QuestionMark(SourceSpan.Default)))
            ),
            ( // value assignment
                "a::b = c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.ValueAssignment,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("a"), [], SourceRange.Default),
                        Identifier("b")
                    )),
                    VariableAccessor("c"),
                    Token.Equals(SourceSpan.Default)))
            ),
            ( // equality check
                "a::b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("a"), [], SourceRange.Default),
                        Identifier("b")
                    )),
                    VariableAccessor("c"),
                    Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // member access
                "a::b.c",
                new MemberAccessExpression(new MemberAccess(
                    new StaticMemberAccessExpression(new StaticMemberAccess(
                        new TypeIdentifier(Identifier("a"), [], SourceRange.Default),
                        Identifier("b")
                    )),
                    Identifier("c")))
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
                        Identifier("c"))),
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
}