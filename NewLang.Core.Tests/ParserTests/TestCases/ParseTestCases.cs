namespace NewLang.Core.Tests.ParserTests.TestCases;

using static ParserHelpers;

public static class ParseTestCases
{
    public static IEnumerable<object[]> TestCases()
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
                                [
                                    new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default)
                                ], SourceRange.Default),
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
                                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [],
                                                SourceRange.Default),
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
                                new TypeIdentifier(Token.Identifier("MyUnion", SourceSpan.Default), [],
                                    SourceRange.Default),
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
                                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [],
                                                SourceRange.Default),
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
                                        new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [],
                                            SourceRange.Default),
                                        new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [],
                                            SourceRange.Default),
                                        new TypeIdentifier(
                                            Token.Identifier("MyClass", SourceSpan.Default),
                                            [
                                                new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [],
                                                    SourceRange.Default)
                                            ]
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
                                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [],
                                                SourceRange.Default), null)
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
                        new FunctionParameter(
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default), null,
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
                        new FunctionParameter(
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default), null,
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
                    new BlockExpression(new Block(
                        [new MethodReturnExpression(new MethodReturn(VariableAccessor("b")), SourceRange.Default)],
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
                                new Block(
                                [
                                    new MethodReturnExpression(new MethodReturn(VariableAccessor("b")),
                                        SourceRange.Default)
                                ], []), SourceRange.Default),
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
                                        new MethodCallExpression(new MethodCall(VariableAccessor("b"), []),
                                            SourceRange.Default)), SourceRange.Default)
                            ], []), SourceRange.Default),
                            [],
                            null), SourceRange.Default)
                    ], []))
            ], [], [])),
            ("fn MyFn(): string {}", new LangProgram([], [
                new LangFunction(null, null, Token.Identifier("MyFn", SourceSpan.Default), [], [],
                    new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                    new Block([], []))
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
                            new TypeIdentifier(Token.Identifier("MyErrorType", SourceSpan.Default), [],
                                SourceRange.Default)
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
                                [new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default)],
                                SourceRange.Default),
                            new TypeIdentifier(Token.Identifier("Inner", SourceSpan.Default),
                                [new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default)],
                                SourceRange.Default)
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
                            new TypeIdentifier(Token.Identifier("MyErrorType", SourceSpan.Default), [],
                                SourceRange.Default),
                            new TypeIdentifier(Token.Identifier("ThirdTypeArgument", SourceSpan.Default), [],
                                SourceRange.Default)
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
                        new FunctionParameter(
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default), null,
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
                                new TypeIdentifier(Token.Identifier("MyType", SourceSpan.Default), [],
                                    SourceRange.Default)
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
                        new FunctionParameter(
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default), null,
                            Token.Identifier("a", SourceSpan.Default)),
                        new FunctionParameter(
                            new TypeIdentifier(Token.Identifier("MyType", SourceSpan.Default), [], SourceRange.Default),
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
                                new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default)))),
                            SourceRange.Default)
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
                            new FunctionParameter(
                                new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default), null,
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
                            new FunctionParameter(
                                new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default), null,
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
                                        new TypeIdentifier(Token.Identifier("Class2", SourceSpan.Default), [],
                                            SourceRange.Default),
                                        [
                                            new FieldInitializer(Token.Identifier("A", SourceSpan.Default),
                                                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                                    Token.IntLiteral(3, SourceSpan.Default))))
                                        ]), SourceRange.Default)), SourceRange.Default),
                                new MethodCallExpression(new MethodCall(
                                    new StaticMemberAccessExpression(new StaticMemberAccess(
                                        new TypeIdentifier(Token.Identifier("MyClass", SourceSpan.Default), [],
                                            SourceRange.Default),
                                        Token.Identifier("StaticMethod", SourceSpan.Default)
                                    )),
                                    []), SourceRange.Default),
                                new MethodCallExpression(new MethodCall(
                                    new GenericInstantiationExpression(new GenericInstantiation(
                                        new ValueAccessorExpression(
                                            new ValueAccessor(ValueAccessType.Variable,
                                                Token.Identifier("PrivateFn", SourceSpan.Default))),
                                        [
                                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [],
                                                SourceRange.Default)
                                        ]), SourceRange.Default),
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
                            new FunctionParameter(
                                new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [], SourceRange.Default), null,
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
                                new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                                null),
                            new ClassField(null,
                                null,
                                new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                                Token.Identifier("FieldB", SourceSpan.Default),
                                new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                                null),
                            new ClassField(new AccessModifier(Token.Pub(SourceSpan.Default)),
                                null,
                                new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                                Token.Identifier("FieldC", SourceSpan.Default),
                                new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                                null),
                            new ClassField(new AccessModifier(Token.Pub(SourceSpan.Default)),
                                null,
                                null,
                                Token.Identifier("FieldD", SourceSpan.Default),
                                new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                                null)
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
                                new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [], SourceRange.Default),
                                null)
                        ]
                    )
                ], []))
        }.Select(x => new object[] { x.Source, Tokenizer.Tokenize(x.Source), x.ExpectedProgram });
    }
}