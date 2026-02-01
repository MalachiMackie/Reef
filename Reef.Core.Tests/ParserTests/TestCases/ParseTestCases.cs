namespace Reef.Core.Tests.ParserTests.TestCases;

using Expressions;
using static ExpressionHelpers;

public static class ParseTestCases
{
    public static IEnumerable<object[]> TestCases()
    {
        return new (string Source, LangProgram ExpectedProgram)[]
        {
            (
                "var a = someModule:::subModule:::SomeClass::<string>::B",
                Program("ParseTestCases",
                    [
                        VariableDeclaration(
                            "a",
                            StaticMemberAccess(
                                NamedTypeIdentifier("SomeClass", [StringType()], modulePath: ["someModule", "subModule"]),
                                "B"))
                    ])
            ),
            (
                "use :::someModule:::subModule:::MyClass;",
                Program("ParseTestCases",
                    moduleImports: [ModuleImport(["someModule", "subModule", "MyClass"], true)])
            ),
            (
                "use subModule:::MyClass;",
                Program(
                    "ParseTestCases",
                    moduleImports: [ModuleImport(["subModule", "MyClass"])])
            ),
            (
                "use :::someModule:::*;",
                Program("ParseTestCases",
                    moduleImports: [ModuleImport(["someModule"], true, true)])
            ),
            (
                "var a = new someModule:::MyClass{}",
                Program("ParseTestCases",
                    [
                        VariableDeclaration(
                            "a",
                            ObjectInitializer(
                                NamedTypeIdentifier("MyClass", modulePath: ["someModule"], modulePathIsGlobal: false)))
                    ])
            ),
            (
                "var a = :::someModule:::SomeClass::StaticField",
                Program("ParseTestCases",
                    [
                        VariableDeclaration("a",
                            StaticMemberAccess(
                                NamedTypeIdentifier("SomeClass", modulePath: ["someModule"], modulePathIsGlobal: true),
                                "StaticField"))
                    ])
            ),
            (
                "var a = someModule:::SomeClass::StaticField",
                Program("ParseTestCases",
                    [
                        VariableDeclaration("a",
                            StaticMemberAccess(
                                NamedTypeIdentifier("SomeClass", modulePath: ["someModule"]),
                                "StaticField"))
                    ])
            ),
            (
                """
                {
                    use someModule:::SomeClass;
                }
                """,
                Program("ParseTestCases",
                    [
                        Block([], [ModuleImport(["someModule", "SomeClass"])])
                    ])
            ),
            (
                "var a = b[0]; hi()",
                Program("ParseTestCases",
                    [
                        VariableDeclaration(
                            "a",
                            IndexExpression(VariableAccessor("b"), Literal(0))),
                        MethodCall(VariableAccessor("hi"))
                    ])
            ),
            (
                "var a = b[0]",
                Program("ParseTestCases",
                    [
                        VariableDeclaration(
                            "a",
                            IndexExpression(VariableAccessor("b"), Literal(0)))
                    ])
            ),
            (
                "var a: [string;5];",
                Program("ParseTestCases",
                    [
                        VariableDeclaration(
                            "a",
                            type: ArrayTypeIdentifier(
                                StringType(),
                                5,
                                null))
                    ])
            ),
            (
                "var a: unboxed [i32; 5];",
                Program("ParseTestCases",
                    [
                        VariableDeclaration(
                            "a",
                            type: ArrayTypeIdentifier(
                                NamedTypeIdentifier("i32"),
                                5,
                                Token.Unboxed(SourceSpan.Default)))
                    ])
            ),
            (
                "var a = []",
                Program("ParseTestCases",
                    [
                        VariableDeclaration(
                            "a",
                            value: CollectionExpression())
                    ])
            ),
            (
                "var a = [unboxed; 1]",
                Program("ParseTestCases",
                    [
                        VariableDeclaration(
                            "a",
                            value: CollectionExpression([Literal(1)], Token.Unboxed(SourceSpan.Default)))
                    ])
            ),
            (
                "var a = [1, 2]",
                Program("ParseTestCases",
                    [
                        VariableDeclaration(
                            "a",
                            value: CollectionExpression([Literal(1), Literal(2)]))
                    ])
            ),
            (
                "var a = [1, 2,]",
                Program("ParseTestCases",
                    [
                        VariableDeclaration(
                            "a",
                            value: CollectionExpression([Literal(1), Literal(2)]))
                    ])
            ),
            (
                "var a = [1; 15]",
                Program("ParseTestCases",
                    [
                        VariableDeclaration(
                            "a",
                            value: FillCollectionExpression(Literal(1), Token.IntLiteral(15, SourceSpan.Default)))
                    ])
            ),
            (
                "break",
                Program("ParseTestCases",
                    [
                        Break(),
                    ])
            ),
            (
                "continue",
                Program("ParseTestCases",
                    [
                        Continue(),
                    ])
            ),
            (
                "while (true) true",
                Program("ParseTestCases",
                    [
                        While(True(), True())
                    ])
            ),
            (
                "-1",
                Program("ParseTestCases",
                    [
                        Literal(-1)
                    ])
            ),
            (
                """
                var a = 1;
                var b = -a;
                """,
                Program("ParseTestCases",
                    [
                        VariableDeclaration("a", Literal(1)),
                        VariableDeclaration("b", new UnaryOperatorExpression(
                            new UnaryOperator(UnaryOperatorType.Negate, VariableAccessor("a"), Token.Dash(SourceSpan.Default))))
                    ])
            ),
            (
                """
                var a: u8 = 3;
                """,
                Program("ParseTestCases",
                [
                    VariableDeclaration("a",
                        new ValueAccessorExpression(new ValueAccessor(
                            ValueAccessType.Literal, Token.IntLiteral(3, SourceSpan.Default), null)),
                        NamedTypeIdentifier("u8"))
                ])
            ),
            (
                """
                var a: unboxed SomeType;
                """,
                Program("ParseTestCases",
                    [
                        VariableDeclaration("a", value: null, type: NamedTypeIdentifier("SomeType", boxedSpecifier: Token.Unboxed(SourceSpan.Default)))
                    ])
            ),
            (
                """
                var a: boxed SomeType;
                """,
                Program("ParseTestCases",
                    [
                        VariableDeclaration("a", value: null, type: NamedTypeIdentifier("SomeType", boxedSpecifier: Token.Boxed(SourceSpan.Default)))
                    ])
            ),
            (
                """
                var a: boxed (string, int);
                """,
                Program("ParseTestCases",
                    [
                        VariableDeclaration(
                            "a",
                            value: null,
                            type: TupleTypeIdentifier(Token.Boxed(SourceSpan.Default), [StringType(), IntType()]))
                    ])
            ),
            (
             """
             var a = if (true) {} else {};
             """,
             Program("ParseTestCases",
                 [
                    VariableDeclaration(
                        "a",
                        IfExpression(
                            Literal(true),
                            Block(),
                            Block()))
                 ])
            ),
            ("var a: int = (1 + 2) * 3;", Program("ParseTestCases", [
                new VariableDeclarationExpression(new VariableDeclaration(
                    Identifier("a"),
                    null,
                    NamedTypeIdentifier("int"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new TupleExpression([
                            new BinaryOperatorExpression(new BinaryOperator(
                                BinaryOperatorType.Plus,
                                new ValueAccessorExpression(new ValueAccessor(
                                    ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default), null)),
                                new ValueAccessorExpression(new ValueAccessor(
                                    ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default), null)),
                                Token.Plus(SourceSpan.Default)))
                        ], SourceRange.Default),
                        new ValueAccessorExpression(new ValueAccessor(
                            ValueAccessType.Literal,
                            Token.IntLiteral(3, SourceSpan.Default), null)),
                        Token.Star(SourceSpan.Default)))), SourceRange.Default)
            ])),
            (
                """
                union MyUnion<T> {
                    A { }
                }

                var a = new MyUnion::<string>::A {};
                """,
                Program("ParseTestCases", 
                    [
                        new VariableDeclarationExpression(new VariableDeclaration(
                            Identifier("a"),
                            null,
                            null,
                            new UnionClassVariantInitializerExpression(new UnionClassVariantInitializer(
                                NamedTypeIdentifier("MyUnion",
                                [
                                    NamedTypeIdentifier("string")
                                ]),
                                Identifier("A"),
                                []), SourceRange.Default)
                        ), SourceRange.Default)
                    ],
                    unions: [
                        new ProgramUnion(
                            null,
                            Identifier("MyUnion"),
                            [Identifier("T")],
                            [],
                            [
                                new ClassUnionVariant
                                {
                                    Name = Identifier("A"),
                                    Fields =
                                    []
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
                Program("ParseTestCases", 
                    [
                        new VariableDeclarationExpression(new VariableDeclaration(
                            Identifier("a"),
                            null,
                            null,
                            new UnionClassVariantInitializerExpression(new UnionClassVariantInitializer(
                                NamedTypeIdentifier("MyUnion"),
                                Identifier("A"),
                                [
                                    new FieldInitializer(
                                        Identifier("MyField"),
                                        new ValueAccessorExpression(new ValueAccessor(
                                            ValueAccessType.Literal, Token.StringLiteral("value", SourceSpan.Default), null))
                                    ),
                                    new FieldInitializer(
                                        Identifier("Field2"),
                                        new ValueAccessorExpression(new ValueAccessor(
                                            ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default), null))
                                    )
                                ]), SourceRange.Default)
                        ), SourceRange.Default)
                    ],
                    unions: [
                        new ProgramUnion(
                            null,
                            Identifier("MyUnion"),
                            [],
                            [],
                            [
                                new ClassUnionVariant
                                {
                                    Name = Identifier("A"),
                                    Fields =
                                    [
                                        new ClassField(null,
                                            null,
                                            null,
                                            Identifier("MyField"),
                                            NamedTypeIdentifier("string"),
                                            null
                                        ),
                                        new ClassField(null,
                                            null,
                                            null,
                                            Identifier("Field2"),
                                            IntType(),
                                            null)
                                    ]
                                }
                            ])
                    ])
            ),
            ("class MyClass {field myFieldWithoutComma: string}",
                Program("ParseTestCases",
                classes: [
                    new ProgramClass(null, Identifier("MyClass"), [], [], [
                        new ClassField(
                            null,
                            null,
                            null,
                            Identifier("myFieldWithoutComma"),
                            NamedTypeIdentifier("string"),
                            null)
                    ])
                ])),
            (
                """
                union MyUnion {
                }
                """,
                Program("ParseTestCases", 
                    unions: [
                        new ProgramUnion(
                            null,
                            Identifier("MyUnion"),
                            [],
                            [],
                            [])
                    ])
            ),
            (
                "pub union MyUnion {}",
                Program("ParseTestCases", 
                    unions: [
                        new ProgramUnion(
                            new AccessModifier(Token.Pub(SourceSpan.Default)),
                            Identifier("MyUnion"),
                            [],
                            [],
                            [])
                    ])
            ),
            (
                "union MyUnion<T1, T2,> {} ",
                Program("ParseTestCases",
                    unions: [
                        new ProgramUnion(
                            null,
                            Identifier("MyUnion"),
                            [Identifier("T1"), Identifier("T2")],
                            [],
                            []
                        )
                    ])
            ),
            (
                "union MyUnion {fn SomeFn(){}}",
                Program("ParseTestCases",
                    unions: [
                        new ProgramUnion(
                            null,
                            Identifier("MyUnion"),
                            [],
                            [
                                new LangFunction(null, null, null, Identifier("SomeFn"),
                                    [], [], null, null, new Block([], [], []))
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
                Program("ParseTestCases", 
                    unions: [
                        new ProgramUnion(
                            null,
                            Identifier("MyUnion"),
                            [],
                            [],
                            [new UnitUnionVariant(Identifier("A"))])
                    ])
            ),
            (
                """
                union MyUnion {
                    A,
                }
                """,
                Program("ParseTestCases", 
                    unions: [
                        new ProgramUnion(
                            null,
                            Identifier("MyUnion"),
                            [],
                            [],
                            [new UnitUnionVariant(Identifier("A"))])
                    ])
            ),
            (
                """
                union MyUnion {
                    A,
                    B(string, int, MyClass::<string>)
                }
                """,
                Program("ParseTestCases", 
                    unions: [
                        new ProgramUnion(
                            null,
                            Identifier("MyUnion"),
                            [],
                            [],
                            [
                                new UnitUnionVariant(Identifier("A")),
                                new TupleUnionVariant(
                                    Identifier("B"),
                                    [
                                        NamedTypeIdentifier("string"),
                                        NamedTypeIdentifier("int"),
                                        NamedTypeIdentifier(
                                            "MyClass",
                                            [
                                                NamedTypeIdentifier("string")
                                            ])
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
                Program("ParseTestCases", 
                    unions: [
                        new ProgramUnion(
                            null,
                            Identifier("MyUnion"),
                            [],
                            [],
                            [
                                new ClassUnionVariant
                                {
                                    Name = Identifier("A"),
                                    Fields =
                                    [
                                        new ClassField(null, null, null,
                                            Identifier("MyField"),
                                            NamedTypeIdentifier("string"), null)
                                    ]
                                }
                            ])
                    ])
            ),
            (
                "fn MyFn(): mut string{}",
                Program("ParseTestCases",
                    functions: [
                        new LangFunction(
                        null,
                        null,
                        null,
                        Identifier("MyFn"),
                        [],
                        [],
                        StringType(),
                        Token.Mut(SourceSpan.Default),
                        new Block([], [], []))
                    ])
            ),
            ("fn MyFn(mut a: int,){}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [
                        new FunctionParameter(
                            NamedTypeIdentifier("int"),
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Identifier("a")
                        )
                    ],
                    null,
                    null,
                    new Block([], [], []))
            ])),
            ("fn MyFn(mut a: int, b: int){}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [
                        new FunctionParameter(
                            NamedTypeIdentifier("int"),
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Identifier("a")
                        ),
                        new FunctionParameter(
                            NamedTypeIdentifier("int"),
                            null,
                            Identifier("b")
                        )
                    ],
                    null,
                    null,
                    new Block([], [], []))
            ])),
            ("fn MyFn(mut a: int, mut b: int){}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [
                        new FunctionParameter(
                            NamedTypeIdentifier("int"),
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Identifier("a")
                        ),
                        new FunctionParameter(
                            NamedTypeIdentifier("int"),
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Identifier("b")
                        )
                    ],
                    null,
                    null,
                    new Block([], [], []))
            ])),
            ("fn MyFn(a: int,){}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [
                        new FunctionParameter(
                            NamedTypeIdentifier("int"), null,
                            Identifier("a"))
                    ],
                    null,
                    null,
                    new Block([], [], []))
            ])),
            ("fn /* some comment */ MyFn(/*some comment*/a: int,)/**/{//}\r\n}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [
                        new FunctionParameter(
                            NamedTypeIdentifier("int"), null,
                            Identifier("a"))
                    ],
                    null,
                    null,
                    new Block([], [], []))
            ])),
            ("class MyClass<T,> {}", Program("ParseTestCases", classes: [
                new ProgramClass(
                    null,
                    Identifier("MyClass"),
                    [Identifier("T")],
                    [], [])
            ])),
            ("fn MyFn<T,>(){}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [Identifier("T")],
                    [],
                    null,
                    null,
                    new Block([], [], [])
                )
            ])),
            ("var a = 1;var b = 2;", Program("ParseTestCases", [
                new VariableDeclarationExpression(new VariableDeclaration(Identifier("a"),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(1, SourceSpan.Default), null))), SourceRange.Default),
                new VariableDeclarationExpression(new VariableDeclaration(Identifier("b"),
                    null, null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                        Token.IntLiteral(2, SourceSpan.Default), null))), SourceRange.Default)
            ])),
            ("a = b;", Program("ParseTestCases", [
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                    VariableAccessor("a"), VariableAccessor("b"), Token.Equals(SourceSpan.Default)))
            ])),
            ("error();", Program("ParseTestCases", [
                new MethodCallExpression(new MethodCall(
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("error"), null)), []), SourceRange.Default)
            ])),
            ("something(a,);", Program("ParseTestCases", [
                new MethodCallExpression(new MethodCall(
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("something"), null)),
                    [
                        VariableAccessor("a")
                    ]), SourceRange.Default)
            ])),
            ("ok();", Program("ParseTestCases", [
                new MethodCallExpression(new MethodCall(
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                        Identifier("ok"), null)), []), SourceRange.Default)
            ])),
            ("ok().b()", Program("ParseTestCases", [
                new MethodCallExpression(new MethodCall(
                    new MemberAccessExpression(new MemberAccess(
                        new MethodCallExpression(new MethodCall(
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                                Identifier("ok"), null)), []), SourceRange.Default),
                        Identifier("b"), null)), []), SourceRange.Default)
            ])),
            ("if (a) {} b = c;", Program("ParseTestCases", 
                [
                    new IfExpressionExpression(new IfExpression(VariableAccessor("a"),
                        new BlockExpression(new Block([], [], []), SourceRange.Default), [], null), SourceRange.Default),
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                        VariableAccessor("b"), VariableAccessor("c"), Token.Equals(SourceSpan.Default)))
                ])),
            ("{} b = c;", Program("ParseTestCases", 
                [
                    new BlockExpression(new Block([], [], []), SourceRange.Default),
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                        VariableAccessor("b"), VariableAccessor("c"), Token.Equals(SourceSpan.Default)))
                ])),
            ("fn MyFn() {}", Program("ParseTestCases", functions: [
                new LangFunction(null, null, null, Identifier("MyFn"), [], [], null,
                    null, new Block([], [], []))
            ])),
            ("if (a) {return b;}", Program("ParseTestCases", [
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("a"),
                    new BlockExpression(new Block(
                        [new MethodReturnExpression(new MethodReturn(VariableAccessor("b")), SourceRange.Default)],
                        [], []), SourceRange.Default),
                    [],
                    null), SourceRange.Default)
            ])),
            ("fn MyFn() {if (a) {return b;}}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [],
                    null,
                    null,
                    new Block([
                        new IfExpressionExpression(new IfExpression(
                            VariableAccessor("a"),
                            new BlockExpression(
                                new Block(
                                [
                                    new MethodReturnExpression(new MethodReturn(VariableAccessor("b")),
                                        SourceRange.Default)
                                ], [], []), SourceRange.Default),
                            [],
                            null), SourceRange.Default)
                    ], [], []))
            ])),
            ("fn MyFn() {if (a) {return b();}}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [],
                    null,
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
                            ], [], []), SourceRange.Default),
                            [],
                            null), SourceRange.Default)
                    ], [], []))
            ])),
            ("fn MyFn(): string {}", Program("ParseTestCases", functions: [
                new LangFunction(null, null, null, Identifier("MyFn"), [], [],
                    NamedTypeIdentifier("string"),
                    null,
                    new Block([], [], []))
            ])),
            ("fn MyFn(): result::<int, MyErrorType> {}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [],
                    NamedTypeIdentifier(
                        "result",
                        [
                            NamedTypeIdentifier("int"),
                            NamedTypeIdentifier("MyErrorType")
                        ]),
                    null,
                    new Block([], [], []))
            ])),
            ("fn MyFn(): Outer::<Inner::<int>> {}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [],
                    NamedTypeIdentifier(
                        "Outer",
                        [
                            NamedTypeIdentifier("Inner", [
                                NamedTypeIdentifier("int")
                            ])
                        ]),
                    null,
                    new Block([], [], []))
            ])),
            ("fn MyFn(): Outer::<Inner::<int>, Inner::<int>> {}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [],
                    NamedTypeIdentifier(
                        "Outer",
                        [
                            NamedTypeIdentifier("Inner",
                                [NamedTypeIdentifier("int")]),
                            NamedTypeIdentifier("Inner",
                                [NamedTypeIdentifier("int")])
                        ]),
                    null,
                    new Block([], [], []))
            ])),
            ("fn MyFn(): result::<int, MyErrorType, ThirdTypeArgument> {}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [],
                    NamedTypeIdentifier(
                        "result",
                        [
                            NamedTypeIdentifier("int"),
                            NamedTypeIdentifier("MyErrorType"),
                            NamedTypeIdentifier("ThirdTypeArgument")
                        ]),
                    null,
                    new Block([], [], []))
            ])),
            ("fn MyFn() { var a = 2; }", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [],
                    null,
                    null,
                    new Block([
                        new VariableDeclarationExpression(new VariableDeclaration(
                            Identifier("a"),
                            null,
                            null,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                Token.IntLiteral(2, SourceSpan.Default), null))), SourceRange.Default)
                    ], [], [])
                )
            ])),
            ("fn MyFn(a: int) {}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [
                        new FunctionParameter(
                            NamedTypeIdentifier("int"), null,
                            Identifier("a"))
                    ],
                    null,
                    null,
                    new Block([], [], [])
                )
            ])),
            ("static fn MyFn() {}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    new StaticModifier(Token.Static(SourceSpan.Default)),
                    null,
                    Identifier("MyFn"),
                    [],
                    [],
                    null,
                    null,
                    new Block([], [], [])
                )
            ])),
            ("fn MyFn<T1>() {}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [Identifier("T1")],
                    [],
                    null,
                    null,
                    new Block([], [], [])
                )
            ])),
            ("fn MyFn<T1, T2>() {}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [Identifier("T1"), Identifier("T2")],
                    [],
                    null,
                    null,
                    new Block([], [], [])
                )
            ])),
            ("fn MyFn<T1, T2, T3>() {}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [
                        Identifier("T1"), Identifier("T2"),
                        Identifier("T3")
                    ],
                    [],
                    null,
                    null,
                    new Block([], [], [])
                )
            ])),
            ("fn MyFn(a: result::<int, MyType>) {}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [
                        new FunctionParameter(NamedTypeIdentifier(
                            "result", [
                                NamedTypeIdentifier("int"),
                                NamedTypeIdentifier("MyType")
                            ]), null, Identifier("a"))
                    ],
                    null,
                    null,
                    new Block([], [], [])
                )
            ])),
            ("fn MyFn(a: int, b: MyType) {}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [
                        new FunctionParameter(
                            NamedTypeIdentifier("int"), null,
                            Identifier("a")),
                        new FunctionParameter(
                            NamedTypeIdentifier("MyType"),
                            null, Identifier("b"))
                    ],
                    null,
                    null,
                    new Block([], [], [])
                )
            ])),
            ("fn MyFn(): int {return 1;}", Program("ParseTestCases", functions: [
                new LangFunction(
                    null,
                    null,
                    null,
                    Identifier("MyFn"),
                    [],
                    [],
                    NamedTypeIdentifier("int"),
                    null,
                    new Block(
                    [
                        new MethodReturnExpression(new MethodReturn(new ValueAccessorExpression(
                                new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default), null))),
                            SourceRange.Default)
                    ], [], [])
                )
            ])),
            ("class MyClass {}", Program("ParseTestCases", classes: [
                new ProgramClass(
                    null,
                    Identifier("MyClass"),
                    [],
                    [],
                    [])
            ])),
            ("class MyClass<T> {}", Program("ParseTestCases", classes: [
                new ProgramClass(
                    null,
                    Identifier("MyClass"),
                    [Identifier("T")],
                    [],
                    [])
            ])),
            ("class MyClass<T, T2, T3> {}", Program("ParseTestCases", classes: [
                new ProgramClass(
                    null,
                    Identifier("MyClass"),
                    [
                        Identifier("T"), Identifier("T2"),
                        Identifier("T3")
                    ],
                    [],
                    [])
            ])),
            ("pub class MyClass {}", Program("ParseTestCases", classes: [
                new ProgramClass(
                    new AccessModifier(Token.Pub(SourceSpan.Default)),
                    Identifier("MyClass"),
                    [],
                    [],
                    [])
            ])),
            ("class MyClass {pub mut field MyField: string,}", Program("ParseTestCases", classes: [
                new ProgramClass(
                    null,
                    Identifier("MyClass"),
                    [],
                    [],
                    [
                        new ClassField(
                            new AccessModifier(Token.Pub(SourceSpan.Default)),
                            null,
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Identifier("MyField"),
                            NamedTypeIdentifier("string"), null)
                    ])
            ])),
            ("class MyClass {pub static mut field MyField: string,}", Program("ParseTestCases", classes: [
                new ProgramClass(
                    null,
                    Identifier("MyClass"),
                    [],
                    [],
                    [
                        new ClassField(
                            new AccessModifier(Token.Pub(SourceSpan.Default)),
                            new StaticModifier(Token.Static(SourceSpan.Default)),
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Identifier("MyField"),
                            NamedTypeIdentifier("string"), null)
                    ])
            ])),
            ("class MyClass {mut field MyField: string,}", Program("ParseTestCases", classes: [
                new ProgramClass(
                    null,
                    Identifier("MyClass"),
                    [],
                    [],
                    [
                        new ClassField(
                            null,
                            null,
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Identifier("MyField"),
                            NamedTypeIdentifier("string"), null)
                    ])
            ])),
            ("class MyClass {field MyField: string,}", Program("ParseTestCases", classes: [
                new ProgramClass(
                    null,
                    Identifier("MyClass"),
                    [],
                    [],
                    [
                        new ClassField(
                            null,
                            null,
                            null,
                            Identifier("MyField"),
                            NamedTypeIdentifier("string"), null)
                    ])
            ])),
            ("class MyClass {pub field MyField: string,}", Program("ParseTestCases", classes: [
                new ProgramClass(
                    null,
                    Identifier("MyClass"),
                    [],
                    [],
                    [
                        new ClassField(
                            new AccessModifier(Token.Pub(SourceSpan.Default)),
                            null,
                            null,
                            Identifier("MyField"),
                            NamedTypeIdentifier("string"), null)
                    ])
            ])),
            ("class MyClass {pub mut field MyField: string, pub fn MyFn() {},}", Program("ParseTestCases", classes: [
                new ProgramClass(
                    null,
                    Identifier("MyClass"),
                    [],
                    [
                        new LangFunction(new AccessModifier(Token.Pub(SourceSpan.Default)), null, null,
                            Identifier("MyFn"), [], [], null, null, new Block([], [], []))
                    ],
                    [
                        new ClassField(
                            new AccessModifier(Token.Pub(SourceSpan.Default)),
                            null,
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Identifier("MyField"),
                            NamedTypeIdentifier("string"), null)
                    ])
            ])),
            ("class MyClass {field MyField: string, fn MyFn() {}}", Program("ParseTestCases", classes: [
                new ProgramClass(
                    null,
                    Identifier("MyClass"),
                    [],
                    [
                        new LangFunction(null, null, null, Identifier("MyFn"), [], [], null,
                            null, new Block([], [], []))
                    ],
                    [
                        new ClassField(
                            null,
                            null,
                            null,
                            Identifier("MyField"),
                            NamedTypeIdentifier("string"), null)
                    ])
            ])),
            ("pub fn DoSomething(a: int): result::<int, string> {}", Program("ParseTestCases", 
                functions: [
                    new LangFunction(
                        new AccessModifier(Token.Pub(SourceSpan.Default)),
                        null,
                        null,
                        Identifier("DoSomething"),
                        [],
                        [
                            new FunctionParameter(
                                NamedTypeIdentifier("int"), null,
                                Identifier("a"))
                        ],
                        NamedTypeIdentifier("result",
                        [
                            NamedTypeIdentifier("int"),
                            NamedTypeIdentifier("string")
                        ]),
                        null,
                        new Block([], [], []))
                ])),
            (
                "class MyClass { static field someField: int = 3, }",
                Program("ParseTestCases", 
                    classes: [
                        new ProgramClass(
                            null,
                            Identifier("MyClass"),
                            [],
                            [],
                            [
                                new ClassField(
                                    null,
                                    new StaticModifier(Token.Static(SourceSpan.Default)),
                                    null,
                                    Identifier("someField"),
                                    NamedTypeIdentifier("int"),
                                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                        Token.IntLiteral(3, SourceSpan.Default), null)))
                            ]
                        )
                    ])
            ),
            (
                """
                boxed int::something 
                """,
                Program("ParseTestCases",
                    [new StaticMemberAccessExpression(
                        new StaticMemberAccess(
                            new NamedTypeIdentifier(
                                Identifier("int"),
                                [],
                                Token.Boxed(SourceSpan.Default),
                                [],
                                false,
                                SourceRange.Default),
                            Identifier("something"),
                            null))])
            ),
            (
                """
                unboxed int::something 
                """,
                Program("ParseTestCases",
                    [new StaticMemberAccessExpression(
                        new StaticMemberAccess(
                            new NamedTypeIdentifier(
                                Identifier("int"),
                                [],
                                Token.Unboxed(SourceSpan.Default),
                                [],
                                false,
                                SourceRange.Default),
                            Identifier("something"),
                            null))])
            ),
            (
                """
                var b = a matches unboxed MyType;
                """,
                Program("ParseTestCases",
                    [
                        new VariableDeclarationExpression(
                            new VariableDeclaration(
                                Identifier("b"),
                                null,
                                null,
                                new MatchesExpression(
                                    VariableAccessor("a"),
                                    new TypePattern(
                                        NamedTypeIdentifier("MyType", boxedSpecifier: Token.Unboxed(SourceSpan.Default)),
                                        null,
                                        false,
                                        SourceRange.Default),
                                    SourceRange.Default)),
                            SourceRange.Default)
                    ])
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
             """, Program("ParseTestCases", 
                [
                    new MethodCallExpression(new MethodCall(VariableAccessor("Println"),
                    [
                        new MethodCallExpression(new MethodCall(VariableAccessor("DoSomething"),
                        [
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                Token.IntLiteral(5, SourceSpan.Default), null))
                        ]), SourceRange.Default)
                    ]), SourceRange.Default),
                    new MethodCallExpression(new MethodCall(VariableAccessor("Println"),
                    [
                        new MethodCallExpression(new MethodCall(VariableAccessor("DoSomething"),
                        [
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                Token.IntLiteral(1, SourceSpan.Default), null))
                        ]), SourceRange.Default)
                    ]), SourceRange.Default),
                    new MethodCallExpression(new MethodCall(VariableAccessor("Println"),
                    [
                        new MethodCallExpression(new MethodCall(VariableAccessor("SomethingElse"),
                        [
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                Token.IntLiteral(1, SourceSpan.Default), null))
                        ]), SourceRange.Default)
                    ]), SourceRange.Default)
                ],
                [
                    new LangFunction(
                        new AccessModifier(Token.Pub(SourceSpan.Default)),
                        null,
                        null,
                        Identifier("DoSomething"),
                        [],
                        [
                            new FunctionParameter(
                                NamedTypeIdentifier("int"), null,
                                Identifier("a"))
                        ],
                        NamedTypeIdentifier("result",
                        [
                            NamedTypeIdentifier("int"),
                            NamedTypeIdentifier("string")
                        ]),
                        null,
                        new Block(
                            [
                                new VariableDeclarationExpression(new VariableDeclaration(
                                    Identifier("b"),
                                    null,
                                    NamedTypeIdentifier("int"),
                                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                        Token.IntLiteral(2, SourceSpan.Default), null))), SourceRange.Default),
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
                                                                Identifier("ok"), null)),
                                                        [VariableAccessor("a")]), SourceRange.Default)
                                                )
                                                , SourceRange.Default)
                                        ],
                                        [], []), SourceRange.Default),
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
                                                                    Identifier("ok"), null)),
                                                            [VariableAccessor("b")]), SourceRange.Default)
                                                    )
                                                    , SourceRange.Default)
                                            ], [], []), SourceRange.Default)
                                        )
                                    ],
                                    new BlockExpression(new Block([], [], []), SourceRange.Default)
                                ), SourceRange.Default),
                                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment,
                                    VariableAccessor("b"),
                                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                        Token.IntLiteral(3, SourceSpan.Default), null)), Token.Equals(SourceSpan.Default))),
                                new VariableDeclarationExpression(new VariableDeclaration(
                                    Identifier("thing"),
                                    null,
                                    null,
                                    new ObjectInitializerExpression(new ObjectInitializer(
                                        NamedTypeIdentifier("Class2"),
                                        [
                                            new FieldInitializer(Identifier("A"),
                                                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                                    Token.IntLiteral(3, SourceSpan.Default), null)))
                                        ]), SourceRange.Default)), SourceRange.Default),
                                new MethodCallExpression(new MethodCall(
                                    new StaticMemberAccessExpression(new StaticMemberAccess(
                                        NamedTypeIdentifier("MyClass"),
                                        Identifier("StaticMethod"),
                                       null
                                    )),
                                    []), SourceRange.Default),
                                new MethodCallExpression(new MethodCall(
                                        new ValueAccessorExpression(
                                            new ValueAccessor(ValueAccessType.Variable,
                                                Identifier("PrivateFn"), [NamedTypeIdentifier("string")])),
                                    []
                                ), SourceRange.Default),
                                new MethodReturnExpression(new MethodReturn(
                                    new MethodCallExpression(new MethodCall(
                                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable,
                                            Identifier("error"), null)),
                                        [
                                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                                Token.StringLiteral("something wrong", SourceSpan.Default), null))
                                        ]
                                    ), SourceRange.Default)), SourceRange.Default)
                            ],
                            [], [])),
                    new LangFunction(
                        null,
                        null,
                        null,
                        Identifier("PrivateFn"),
                        [Identifier("T")],
                        [],
                        null,
                        null,
                        new Block(
                            [
                                new MethodCallExpression(
                                    new MethodCall(VariableAccessor("Println"),
                                    [
                                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                            Token.StringLiteral("Message", SourceSpan.Default), null))
                                    ]), SourceRange.Default)
                            ],
                            [
                                new LangFunction(
                                    null,
                                    null,
                                    null,
                                    Identifier("InnerFn"),
                                    [],
                                    [],
                                    null,
                                    null,
                                    new Block(
                                        [
                                            new MethodCallExpression(
                                                new MethodCall(VariableAccessor("Println"),
                                                [
                                                    new ValueAccessorExpression(
                                                        new ValueAccessor(ValueAccessType.Literal,
                                                            Token.StringLiteral("Something", SourceSpan.Default), null))
                                                ]), SourceRange.Default)
                                        ],
                                        [
                                        ], []))
                            ], [])),
                    new LangFunction(
                        new AccessModifier(Token.Pub(SourceSpan.Default)),
                        null,
                        null,
                        Identifier("SomethingElse"),
                        [],
                        [
                            new FunctionParameter(
                                NamedTypeIdentifier("int"), null,
                                Identifier("a"))
                        ],
                        NamedTypeIdentifier("result",
                        [
                            NamedTypeIdentifier("int"),
                            NamedTypeIdentifier("string")
                        ]),
                        null,
                        new Block(
                            [
                                new VariableDeclarationExpression(new VariableDeclaration(
                                    Identifier("b"),
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
                                    Identifier("c"),
                                    new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                                    null,
                                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal,
                                        Token.IntLiteral(2, SourceSpan.Default), null))), SourceRange.Default),
                                new MethodReturnExpression(new MethodReturn(VariableAccessor("b")), SourceRange.Default)
                            ],
                            [], [])
                    )
                ],
                [
                    new ProgramClass(
                        new AccessModifier(Token.Pub(SourceSpan.Default)),
                        Identifier("MyClass"),
                        [],
                        [
                            new LangFunction(
                                new AccessModifier(Token.Pub(SourceSpan.Default)),
                                null,
                                null,
                                Identifier("PublicMethod"),
                                [],
                                [],
                                null,
                                null,
                                new Block([], [], [])),
                            new LangFunction(
                                new AccessModifier(Token.Pub(SourceSpan.Default)),
                                new StaticModifier(Token.Static(SourceSpan.Default)),
                                null,
                                Identifier("StaticMethod"),
                                [],
                                [],
                                null,
                                null,
                                new Block([], [], []))
                        ],
                        [
                            new ClassField(null,
                                null,
                                null,
                                Identifier("FieldA"),
                                NamedTypeIdentifier("string"),
                                null),
                            new ClassField(null,
                                null,
                                new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                                Identifier("FieldB"),
                                NamedTypeIdentifier("string"),
                                null),
                            new ClassField(new AccessModifier(Token.Pub(SourceSpan.Default)),
                                null,
                                new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                                Identifier("FieldC"),
                                NamedTypeIdentifier("string"),
                                null),
                            new ClassField(new AccessModifier(Token.Pub(SourceSpan.Default)),
                                null,
                                null,
                                Identifier("FieldD"),
                                NamedTypeIdentifier("string"),
                                null)
                        ]),
                    new ProgramClass(
                        new AccessModifier(Token.Pub(SourceSpan.Default)),
                        Identifier("GenericClass"),
                        [Identifier("T")],
                        [
                            new LangFunction(
                                new AccessModifier(Token.Pub(SourceSpan.Default)),
                                null,
                                null,
                                Identifier("PublicMethod"),
                                [Identifier("T1")],
                                [],
                                null,
                                null,
                                new Block([], [], []))
                        ],
                        []
                    ),
                    new ProgramClass(
                        new AccessModifier(Token.Pub(SourceSpan.Default)),
                        Identifier("Class2"),
                        [],
                        [],
                        [
                            new ClassField(new AccessModifier(Token.Pub(SourceSpan.Default)),
                                null,
                                null,
                                Identifier("A"),
                                NamedTypeIdentifier("string"),
                                null)
                        ]
                    )
                ]))
        }.Select(x => new object[] { x.Source, Tokenizer.Tokenize(x.Source), x.ExpectedProgram });
    }
}
