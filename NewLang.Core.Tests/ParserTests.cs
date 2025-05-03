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
        var act = () => Parser.Parse(tokens);

        act.Should().Throw<InvalidOperationException>();
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
    public void ParseTest([SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        string source,
        IEnumerable<Token> tokens,
        LangProgram expectedProgram)
    {
        var program = RemoveSourceSpan(Parser.Parse(tokens));
 
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

    public static IEnumerable<object[]> ParseTestCases()
    {
        return new (string Source, LangProgram ExpectedProgram)[]
        {
            ("var a = 1;var b = 2;", new LangProgram([
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))))),
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("b", default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default))))),
                ], [], [])),
            ("a = b;", new LangProgram([
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, VariableAccessor("a"), VariableAccessor("b"), Token.Equals(default))),
                ], [], [])),
            ("error();", new LangProgram([
                new MethodCallExpression(new MethodCall(new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Error(default))), [])),
                ], [], [])),
            ("ok();", new LangProgram([
                new MethodCallExpression(new MethodCall(new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Ok(default))), [])),
                ], [], [])),
            ("ok().b()", new LangProgram([
                new MethodCallExpression(new MethodCall(
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.MemberAccess, 
                        new MethodCallExpression(new MethodCall(
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Ok(default))), [])),
                        VariableAccessor("b"), Token.Dot(default))), [])),
                ], [], [])),
            ("if (a) {} b = c;", new LangProgram(
                [
                    new IfExpressionExpression(new IfExpression(VariableAccessor("a"), new BlockExpression(new Block([], [])), [], null)),
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, VariableAccessor("b"), VariableAccessor("c"), Token.Equals(default)))
                ],
                [],
                [])),
            ("{} b = c;", new LangProgram(
                [
                    new BlockExpression(new Block([], [])),
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, VariableAccessor("b"), VariableAccessor("c"), Token.Equals(default)))
                ],
                [],
                [])),
            ("fn MyFn() {}", new LangProgram([], [
                new LangFunction(null, null, Token.Identifier("MyFn", default), [], [], null, new Block([], []))
            ], [])),
            ("if (a) {return b;}", new LangProgram([
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("a"),
                    new BlockExpression(new Block([new MethodReturnExpression(new MethodReturn(VariableAccessor("b")))], [])), 
                    [],
                    null))], [], [])),
            ("fn MyFn() {if (a) {return b;}}", new LangProgram([], [new LangFunction(
                null,
                null,
                Token.Identifier("MyFn", default),
                [],
                [],
                null,
                new Block([
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("a"),
                    new BlockExpression(new Block([new MethodReturnExpression(new MethodReturn(VariableAccessor("b")))], [])), 
                    [],
                    null))], []))], [])),
            ("fn MyFn() {if (a) {return b();}}", new LangProgram([], [new LangFunction(
                            null,
                            null,
                            Token.Identifier("MyFn", default),
                            [],
                            [],
                            null,
                            new Block([
                            new IfExpressionExpression(new IfExpression(
                                VariableAccessor("a"),
                                new BlockExpression(new Block([new MethodReturnExpression(new MethodReturn(new MethodCallExpression(new MethodCall(VariableAccessor("b"), []))))], [])), 
                                [],
                                null))], []))], [])),
            ("fn MyFn(): string {}", new LangProgram([], [
                new LangFunction(null, null, Token.Identifier("MyFn", default), [], [], new TypeIdentifier(Token.StringKeyword(default), []), new Block([], []))
            ], [])),
            ("fn MyFn(): result::<int, MyErrorType> {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", default),
                    [],
                    [],
                    new TypeIdentifier(
                        Token.Result(default),
                        [
                            new TypeIdentifier(Token.IntKeyword(default), []),
                            new TypeIdentifier(Token.Identifier("MyErrorType", default), []),
                        ]),
                    new Block([], []))
            ], [])),
            ("fn MyFn(): Outer::<Inner::<int>> {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", default),
                    [],
                    [],
                    new TypeIdentifier(
                        Token.Identifier("Outer", default),
                        [
                            new TypeIdentifier(Token.Identifier("Inner", default), [
                                new TypeIdentifier(Token.IntKeyword(default), [])]),
                        ]),
                    new Block([], []))
            ], [])),
            ("fn MyFn(): Outer::<Inner::<int>, Inner::<int>> {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", default),
                    [],
                    [],
                    new TypeIdentifier(
                        Token.Identifier("Outer", default),
                        [
                            new TypeIdentifier(Token.Identifier("Inner", default), [new TypeIdentifier(Token.IntKeyword(default), [])]),
                            new TypeIdentifier(Token.Identifier("Inner", default), [new TypeIdentifier(Token.IntKeyword(default), [])]),
                        ]),
                    new Block([], []))
            ], [])),
            ("fn MyFn(): result::<int, MyErrorType, ThirdTypeArgument> {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", default),
                    [],
                    [],
                    new TypeIdentifier(
                        Token.Result(default),
                        [
                            new TypeIdentifier(Token.IntKeyword(default), []),
                            new TypeIdentifier(Token.Identifier("MyErrorType", default), []),
                            new TypeIdentifier(Token.Identifier("ThirdTypeArgument", default), []),
                        ]),
                    new Block([], []))
            ], [])),
            ("fn MyFn() { var a = 2; }", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", default),
                    [],
                    [],
                    null,
                    new Block([new VariableDeclarationExpression(new VariableDeclaration(
                        Token.Identifier("a", default),
                        null,
                        null,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))))], [])
                )
            ], [])),
            ("fn MyFn(a: int) {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", default),
                    [],
                    [new FunctionParameter(new TypeIdentifier(Token.IntKeyword(default), []), Token.Identifier("a", default))],
                    null,
                    new Block([], [])
                )
            ], [])),
            ("static fn MyFn() {}", new LangProgram([], [
                new LangFunction(
                    null,
                    new StaticModifier(Token.Static(default)),
                    Token.Identifier("MyFn", default),
                    [],
                    [],
                    null,
                    new Block([], [])
                )
            ], [])),
            ("fn MyFn<T1>() {}", new LangProgram([], [new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", default),
                    [Token.Identifier("T1", default)],
                    [],
                    null,
                    new Block([], [])
                )], [])),
            ("fn MyFn<T1, T2>() {}", new LangProgram([], [new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", default),
                    [Token.Identifier("T1", default), Token.Identifier("T2", default)],
                    [],
                    null,
                    new Block([], [])
                )], [])),
            ("fn MyFn<T1, T2, T3>() {}", new LangProgram([], [new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", default),
                    [Token.Identifier("T1", default), Token.Identifier("T2", default), Token.Identifier("T3", default)],
                    [],
                    null,
                    new Block([], [])
                )], [])),
            ("fn MyFn(a: result::<int, MyType>) {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", default),
                    [],
                    [new FunctionParameter(new TypeIdentifier(
                        Token.Result(default), [
                            new TypeIdentifier(Token.IntKeyword(default), []),
                            new TypeIdentifier(Token.Identifier("MyType", default), []),
                        ]), Token.Identifier("a", default))],
                    null,
                    new Block([], [])
                )
            ], [])),
            ("fn MyFn(a: int, b: MyType) {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", default),
                    [],
                    [
                        new FunctionParameter(new TypeIdentifier(Token.IntKeyword(default), []), Token.Identifier("a", default)),
                        new FunctionParameter(new TypeIdentifier(Token.Identifier("MyType", default), []), Token.Identifier("b", default)),
                    ],
                    null,
                    new Block([], [])
                )
            ], [])),
            ("fn MyFn(): int {return 1;}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", default),
                    [],
                    [],
                    new TypeIdentifier(Token.IntKeyword(default), []),
                    new Block([new MethodReturnExpression(new MethodReturn(new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))))], [])
                )
            ], [])),
            ("class MyClass {}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", default),
                [],
                [],
                [])])),
            ("class MyClass<T> {}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", default),
                [Token.Identifier("T", default)],
                [],
                [])])),
            ("class MyClass<T, T2, T3> {}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", default),
                [Token.Identifier("T", default), Token.Identifier("T2", default), Token.Identifier("T3", default)],
                [],
                [])])),
            ("pub class MyClass {}", new LangProgram([], [], [new ProgramClass(
                new AccessModifier(Token.Pub(default)),
                Token.Identifier("MyClass", default),
                [],
                [],
                [])])),
            ("class MyClass {pub mut field MyField: string;}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", default),
                [],
                [],
                [new ClassField(
                    new AccessModifier(Token.Pub(default)),
                    null,
                    new MutabilityModifier(Token.Mut(default)),
                    Token.Identifier("MyField", default),
                    new TypeIdentifier(Token.StringKeyword(default), []))])])),
            ("class MyClass {pub static mut field MyField: string;}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", default),
                [],
                [],
                [new ClassField(
                    new AccessModifier(Token.Pub(default)),
                    new StaticModifier(Token.Static(default)),
                    new MutabilityModifier(Token.Mut(default)),
                    Token.Identifier("MyField", default),
                    new TypeIdentifier(Token.StringKeyword(default), []))])])),
            ("class MyClass {mut field MyField: string;}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", default),
                [],
                [],
                [new ClassField(
                    null,
                    null,
                    new MutabilityModifier(Token.Mut(default)),
                    Token.Identifier("MyField", default),
                    new TypeIdentifier(Token.StringKeyword(default), []))])])),
            ("class MyClass {field MyField: string;}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", default),
                [],
                [],
                [new ClassField(
                    null,
                    null,
                    null,
                    Token.Identifier("MyField", default),
                    new TypeIdentifier(Token.StringKeyword(default), []))])])),
            ("class MyClass {pub field MyField: string;}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", default),
                [],
                [],
                [new ClassField(
                    new AccessModifier(Token.Pub(default)),
                    null,
                    null,
                    Token.Identifier("MyField", default),
                    new TypeIdentifier(Token.StringKeyword(default), []))])])),
            ("class MyClass {pub mut field MyField: string; pub fn MyFn() {}}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", default),
                [],
                [new LangFunction(new AccessModifier(Token.Pub(default)), null, Token.Identifier("MyFn", default), [], [], null, new Block([], []))],
                [new ClassField(
                    new AccessModifier(Token.Pub(default)),
                    null,
                    new MutabilityModifier(Token.Mut(default)),
                    Token.Identifier("MyField", default),
                    new TypeIdentifier(Token.StringKeyword(default), []))])])),
            ("class MyClass {field MyField: string; fn MyFn() {}}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", default),
                [],
                [new LangFunction(null, null, Token.Identifier("MyFn", default), [], [], null, new Block([], []))],
                [new ClassField(
                    null,
                    null,
                    null,
                    Token.Identifier("MyField", default),
                    new TypeIdentifier(Token.StringKeyword(default), []))])])),
            ("pub fn DoSomething(a: int): result::<int, string> {}", new LangProgram(
                [],
                [
                    new LangFunction(
                        new AccessModifier(Token.Pub(default)),
                        null,
                        Token.Identifier("DoSomething", default),
                        [],
                        [new FunctionParameter(new TypeIdentifier(Token.IntKeyword(default), []), Token.Identifier("a", default))],
                        new TypeIdentifier(Token.Result(default), [new TypeIdentifier(Token.IntKeyword(default), []), new TypeIdentifier(Token.StringKeyword(default), [])]),
                        new Block([], []))
                ],
                [])),
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
                 
                 field FieldA: string;
                 mut field FieldB: string;
                 pub mut field FieldC: string;
                 pub field FieldD: string;
             }
             
             pub class GenericClass<T> {
                 pub fn PublicMethod<T1>() {
                 }
             }
             
             pub class Class2 {
                 pub field A: string;
             }
             """, new LangProgram(
            [
                new MethodCallExpression(new MethodCall(VariableAccessor("Println"), [new MethodCallExpression(new MethodCall(VariableAccessor("DoSomething"), [new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(5, default)))]))])),
                new MethodCallExpression(new MethodCall(VariableAccessor("Println"), [new MethodCallExpression(new MethodCall(VariableAccessor("DoSomething"), [new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))]))])),
                new MethodCallExpression(new MethodCall(VariableAccessor("Println"), [new MethodCallExpression(new MethodCall(VariableAccessor("SomethingElse"), [new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))]))])),
            ], 
            [
                new LangFunction(
                    new AccessModifier(Token.Pub(default)),
                    null,
                    Token.Identifier("DoSomething", default),
                    [],
                    [new FunctionParameter(new TypeIdentifier(Token.IntKeyword(default), []), Token.Identifier("a", default))],
                    new TypeIdentifier(Token.Result(default), [new TypeIdentifier(Token.IntKeyword(default), []), new TypeIdentifier(Token.StringKeyword(default), [])]),
                    new Block(
                        [
                            new VariableDeclarationExpression(new VariableDeclaration(
                                Token.Identifier("b", default),
                                null,
                                new TypeIdentifier(Token.IntKeyword(default), []),
                                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default))))),
                            new IfExpressionExpression(new IfExpression(
                                new BinaryOperatorExpression(new BinaryOperator(
                                    BinaryOperatorType.GreaterThan,
                                    VariableAccessor("a"),
                                    VariableAccessor("b"),
                                    Token.RightAngleBracket(default))),
                                new BlockExpression(new Block(
                                    [new MethodReturnExpression(new MethodReturn(
                                            new MethodCallExpression(new MethodCall(
                                                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Ok(default))),
                                                [VariableAccessor("a")]))
                                            )
                                        )],
                                    [])),
                                [new ElseIf(
                                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.EqualityCheck, VariableAccessor("a"), VariableAccessor("b"), Token.DoubleEquals(default))),
                                    new BlockExpression(new Block([new MethodReturnExpression(new MethodReturn(
                                            new MethodCallExpression(new MethodCall(
                                                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Ok(default))),
                                                [VariableAccessor("b")]))
                                            )
                                        )], []))
                                    )],
                                    new BlockExpression(new Block([], []))
                                )),
                            new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                                VariableAccessor("b"),
                                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(3, default))), Token.Equals(default))),
                            new VariableDeclarationExpression(new VariableDeclaration(
                                Token.Identifier("thing", default),
                                null,
                                null,
                                new ObjectInitializerExpression(new ObjectInitializer(
                                    new TypeIdentifier(Token.Identifier("Class2", default), []),
                                    [
                                        new FieldInitializer(Token.Identifier("A", default), new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(3, default))))
                                    ])))),
                            new MethodCallExpression(new MethodCall(
                                new BinaryOperatorExpression(new BinaryOperator(
                                    BinaryOperatorType.StaticMemberAccess,
                                    VariableAccessor("MyClass"),
                                    VariableAccessor("StaticMethod"),
                                    Token.DoubleColon(default))),
                                [])),
                            new MethodCallExpression(new MethodCall(
                                new GenericInstantiationExpression(new GenericInstantiation(
                                    VariableAccessor("PrivateFn"),
                                    [new TypeIdentifier(Token.StringKeyword(default), [])])),
                                []
                                )),
                            new MethodReturnExpression(new MethodReturn(
                                new MethodCallExpression(new MethodCall(
                                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Error(default))),
                                    [new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("something wrong", default)))]
                                    ))))],
                        [])),
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("PrivateFn", default),
                    [Token.Identifier("T", default)],
                    [],
                    null,
                    new Block(
                        [new MethodCallExpression(
                            new MethodCall(VariableAccessor("Println"),
                                [new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("Message", default)))]))],
                        [
                            new LangFunction(
                                null,
                                null,
                                Token.Identifier("InnerFn", default),
                                [],
                                [],
                                null,
                                new Block(
                                    [new MethodCallExpression(
                                        new MethodCall(VariableAccessor("Println"),
                                            [new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("Something", default)))]))],
                                    [
                                    ]))
                        ])),
                new LangFunction(
                    new AccessModifier(Token.Pub(default)),
                    null,
                    Token.Identifier("SomethingElse", default),
                    [],
                    [new FunctionParameter(new TypeIdentifier(Token.IntKeyword(default), []), Token.Identifier("a", default))],
                    new TypeIdentifier(Token.Result(default), [new TypeIdentifier(Token.IntKeyword(default), []), new TypeIdentifier(Token.StringKeyword(default), [])]),
                    new Block(
                        [
                            new VariableDeclarationExpression(new VariableDeclaration(
                                Token.Identifier("b", default),
                                null,
                                null,
                                new UnaryOperatorExpression(new UnaryOperator(
                                    UnaryOperatorType.FallOut,
                                    new MethodCallExpression(new MethodCall(
                                        VariableAccessor("DoSomething"),
                                        [
                                            VariableAccessor("a")
                                        ])),
                                    Token.QuestionMark(default))))),
                            new VariableDeclarationExpression(new VariableDeclaration(
                                Token.Identifier("c", default),
                                new MutabilityModifier(Token.Mut(default)),
                                null,
                                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default))))),
                            new MethodReturnExpression(new MethodReturn(VariableAccessor("b")))
                        ],
                        [])
                    ),
            ],
            [
                new ProgramClass(
                    new AccessModifier(Token.Pub(default)),
                    Token.Identifier("MyClass", default),
                    [],
                    [
                        new LangFunction(
                            new AccessModifier(Token.Pub(default)),
                            null,
                            Token.Identifier("PublicMethod", default),
                            [],
                            [],
                            null,
                            new Block([], [])),
                        new LangFunction(
                            new AccessModifier(Token.Pub(default)),
                            new StaticModifier(Token.Static(default)),
                            Token.Identifier("StaticMethod", default),
                            [],
                            [],
                            null,
                            new Block([], []))
                    ],
                    
                    [
                        new ClassField(null,
                            null,
                            null,
                            Token.Identifier("FieldA", default),
                            new TypeIdentifier(Token.StringKeyword(default), [])),
                        new ClassField(null,
                            null,
                            new MutabilityModifier(Token.Mut(default)),
                            Token.Identifier("FieldB", default),
                            new TypeIdentifier(Token.StringKeyword(default), [])),
                        new ClassField(new AccessModifier(Token.Pub(default)),
                            null,
                            new MutabilityModifier(Token.Mut(default)),
                            Token.Identifier("FieldC", default),
                            new TypeIdentifier(Token.StringKeyword(default), [])),
                        new ClassField(new AccessModifier(Token.Pub(default)),
                            null,
                            null,
                            Token.Identifier("FieldD", default),
                            new TypeIdentifier(Token.StringKeyword(default), [])),
                    ]),
                new ProgramClass(
                    new AccessModifier(Token.Pub(default)),
                    Token.Identifier("GenericClass", default),
                    [Token.Identifier("T", default)],
                    [
                        new LangFunction(
                            new AccessModifier(Token.Pub(default)),
                            null,
                            Token.Identifier("PublicMethod", default),
                            [Token.Identifier("T1", default)],
                            [],
                            null,
                            new Block([], []))
                    ],
                    []
                    ),
                new ProgramClass(
                    new AccessModifier(Token.Pub(default)),
                    Token.Identifier("Class2", default),
                    [],
                    [],
                    [
                        new ClassField(new AccessModifier(Token.Pub(default)),
                            null,
                            null,
                            Token.Identifier("A", default),
                            new TypeIdentifier(Token.StringKeyword(default), []))
                    ]
                    ),
            ]))
        }.Select(x => new object[] { x.Source, new Tokenizer().Tokenize(x.Source), x.ExpectedProgram });
    }

    public static IEnumerable<object[]> FailTestCases()
    {
        IEnumerable<string> strings = [
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
            // body has tail expression, but else doesn't
            
            /*
             todo: these are type checking errors, because {} is a valid expression that returns unit
            "if (a) {a} else {}",
            "if (a) {} else {a}",
            "if (a) {var b = 1;} else {a}",
            "if (a) {if (b) {var c = 1;}} else {1}",
            "if (a) {if (b) {1} else {2}} else {}",
            */
            "a(",
            "a<string>()",
            "a::<>()",
            "a::<string,>()",
            "a::<,>()",
            "a::<string string>()",
            "a::<string()",
            "a(a, )",
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
            "fn MyFn<A,>(){}",
            "fn MyFn<A B>(){}",
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
            "fn MyFunction(a: result::<int,>) {}",
            "fn MyFunction(a: result::<>) {}",
            "fn MyFunction(a: result::<int int>) {}",
            "fn MyFunction(a: int, ) {}",
            "fn MyFunction(,) {}",
            "fn MyFunction(a: int b: int) {}",
            // no semicolon
            "return 1",
            "pub MyClass {}",
            "class MyClass<> {}",
            "class MyClass<,> {}",
            "class MyClass<T1,> {}",
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
            "fn SomeFn() { class NoClassesInFunctions {}}",
            "new",
            "new Thing",
            "new Thing {",
            "new Thing{ a }",
            "new Thing{ a = }",
            "new Thing{ a = 1 b = 2 }",
            "new Thing { , }",
            "new Thing { , a = 1 }"
        ];
        return strings.Select(x => new object[] { x, new Tokenizer().Tokenize(x) });
    }

    public static IEnumerable<object[]> SingleTestCase()
    {
        return new (string Source, IExpression ExpectedProgram)[]
        {
            ("a == b", new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.EqualityCheck, VariableAccessor("a"), VariableAccessor("b"), Token.DoubleEquals(default))))
        }.Select(x => new object[] { x.Source, new Tokenizer().Tokenize(x.Source), x.ExpectedProgram });
    }

    public static IEnumerable<object[]> PopExpressionTestCases()
    {
        return new (string Source, IExpression ExpectedExpression)[]
        {
            // value access expressions
            ("a", new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default)))),
            ("1", new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))),
            ("\"my string\"", new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("my string", default)))),
            ("true", new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.True(default)))),
            ("false", new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.False(default)))),
            ("ok", new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Ok(default)))),
            ("a == b", new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.EqualityCheck, VariableAccessor("a"), VariableAccessor("b"), Token.DoubleEquals(default)))),
            ("ok()", new MethodCallExpression(new MethodCall(new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Ok(default))), []))),
            // postfix unary operator
            ("a?", new UnaryOperatorExpression(new UnaryOperator(
                UnaryOperatorType.FallOut,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                Token.QuestionMark(default)))),
            ("a??",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        Token.QuestionMark(default))),
                    Token.QuestionMark(default)))
            ),
            ("return 1", new MethodReturnExpression(
                new MethodReturn(new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))))),
            // binary operator expressions
            ("a < 5", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.LessThan,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(5, default))),
                Token.LeftAngleBracket(default)))),
            ("\"thing\" > true", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.GreaterThan,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("thing", default))),
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.True(default))),
                Token.RightAngleBracket(default)))),
            ("a + b", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Plus,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.Plus(default)))),
            ("a - b", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Minus,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.Dash(default)))),
            ("a * b", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.Star(default)))),
            ("a / b", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Divide,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.ForwardSlash(default)))),
            ("var a: int = b", new VariableDeclarationExpression(
                new VariableDeclaration(
                    Token.Identifier("a", default),
                    null,
                    new TypeIdentifier(Token.IntKeyword(default), []),
                    VariableAccessor("b")))),
            ("var a: int", new VariableDeclarationExpression(
                new VariableDeclaration(
                    Token.Identifier("a", default),
                    null,
                    new TypeIdentifier(Token.IntKeyword(default), []),
                    null))),
            ("var mut a = 2", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", default),
                new MutabilityModifier(Token.Mut(default)),
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))))),
            ("a = b", new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, VariableAccessor("a"), VariableAccessor("b"), Token.Equals(default)))),
            ("var mut a: int = 2", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", default),
                new MutabilityModifier(Token.Mut(default)),
                new TypeIdentifier(Token.IntKeyword(default), []),
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))))),
            ("var a = b", new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", default), null, null, VariableAccessor("b")))),
            ("var a = 1", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", default),
                null,
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))))),
            ("var a = true", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", default),
                null,
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.True(default)))))),
            ("var a = \"thing\"", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", default),
                null,
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("thing", default)))))),
            ("{}", new BlockExpression(new Block([], []))),
            ("{var a = 1;}", new BlockExpression(new Block([
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))))),
                ], []))),
            // tail expression
            ("{var a = 1}", new BlockExpression(new Block(
                [
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))))], []))),
            // tail expression
            ("{var a = 1;var b = 2}", new BlockExpression(new Block(
                [new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))))),
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("b", default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))))], []))),
            ("{var a = 1; var b = 2;}", new BlockExpression(new Block([
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))))),
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("b", default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default))))),
                ], []))),
            ("if (a) var c = 2;", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new VariableDeclarationExpression(new VariableDeclaration(
                    Token.Identifier("c", default),
                    null,
                    null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default))))), [], null))),
            ("if (a > b) {var c = \"value\";}", new IfExpressionExpression(new IfExpression(
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    VariableAccessor("a"),
                    VariableAccessor("b"),
                    Token.RightAngleBracket(default))),
                new BlockExpression(new Block([
                    new VariableDeclarationExpression(new VariableDeclaration(
                        Token.Identifier("c", default),
                        null,
                        null,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("value", default)))))
                ], [])), [], null))),
            ("if (a) {} else {var b = 2;}", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new BlockExpression(new Block([], [])),
                [],
                new BlockExpression(new Block([
                    new VariableDeclarationExpression(new VariableDeclaration(
                        Token.Identifier("b", default),
                        null,
                        null,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))))
                ], []))))),
            ("if (a) {} else if (b) {}", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new BlockExpression(new Block([], [])),
                [new ElseIf(VariableAccessor("b"), new BlockExpression(new Block([], [])))],
                null))),
            ("if (a) {} else if (b) {} else {}", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new BlockExpression(new Block([], [])),
                [
                    new ElseIf(VariableAccessor("b"), new BlockExpression(new Block([], []))),
                ],
                new BlockExpression(new Block([], []))))),
            ("if (a) {} else if (b) {} else if (c) {} else {}", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new BlockExpression(new Block([], [])),
                [
                    new ElseIf(VariableAccessor("b"), new BlockExpression(new Block([], []))),
                    new ElseIf(VariableAccessor("c"), new BlockExpression(new Block([], []))),
                ],
                new BlockExpression(new Block([], []))))),
            ("if (a) {b} else {c}", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new BlockExpression(new Block([VariableAccessor("b")], [])),
                [],
                new BlockExpression(new Block([VariableAccessor("c")], []))))),
            ("if (a) b else c", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                VariableAccessor("b"),
                [],
                VariableAccessor("c")))),
            ("if (a) {if (b) {1} else {2}} else {3}", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new BlockExpression(new Block([new IfExpressionExpression(new IfExpression(
                    VariableAccessor("b"),
                    new BlockExpression(new Block([new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))], [])),
                    [],
                    new BlockExpression(new Block([new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))], []))))], [])),
                [],
                new BlockExpression(new Block([new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(3, default)))], []))))),
            ("if (a) if (b) 1 else 2 else 3", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("b"),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))),
                    [],
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default))))),
                [],
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(3, default)))))),
            ("var a = if (b) 1 else 2;", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", default),
                null,
                null,
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("b"),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))),
                    [],
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))))))),
            ("var a = if (b) {1} else {2};", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", default),
                null,
                null,
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("b"),
                    new BlockExpression(new Block([new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))], [])),
                    [],
                    new BlockExpression(new Block([new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))], []))))))),
            ("a()", new MethodCallExpression(new MethodCall(VariableAccessor("a"), []))),
            ("a::<string>()", new MethodCallExpression(new MethodCall(new GenericInstantiationExpression(new GenericInstantiation(VariableAccessor("a"), [new TypeIdentifier(Token.StringKeyword(default), [])])), []))),
            ("a::<string, int>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(new GenericInstantiation(
                    VariableAccessor("a"),
                    [
                        new TypeIdentifier(Token.StringKeyword(default), []),
                        new TypeIdentifier(Token.IntKeyword(default), []),
                    ])), []))),
            ("a::<string, int, result::<int>>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(
                    new GenericInstantiation(
                VariableAccessor("a"),
                [
                    new TypeIdentifier(Token.StringKeyword(default), []),
                    new TypeIdentifier(Token.IntKeyword(default), []),
                    new TypeIdentifier(Token.Result(default), [new TypeIdentifier(Token.IntKeyword(default), [])]),
                ])),
                []))),
            ("a(b)", new MethodCallExpression(new MethodCall(VariableAccessor("a"), [
            VariableAccessor("b")]))),
            ("a(b, c)", new MethodCallExpression(new MethodCall(VariableAccessor("a"), [
                VariableAccessor("b"), VariableAccessor("c")
            ]))),
            ("a(b, c > d, e)", new MethodCallExpression(new MethodCall(VariableAccessor("a"), [
                VariableAccessor("b"),
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.GreaterThan, VariableAccessor("c"), VariableAccessor("d"), Token.RightAngleBracket(default))),
                VariableAccessor("e")
            ]))),
            ("a.b", new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.MemberAccess, VariableAccessor("a"), VariableAccessor("b"), Token.Dot(default)))),
            ("a.b()", new MethodCallExpression(new MethodCall(new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.MemberAccess, VariableAccessor("a"), VariableAccessor("b"), Token.Dot(default))), []))),
            ("a?.b", new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.MemberAccess, 
                new UnaryOperatorExpression(new UnaryOperator(UnaryOperatorType.FallOut, VariableAccessor("a"), Token.QuestionMark(default))),
                VariableAccessor("b"), Token.Dot(default)))),
            ("a.b?", new UnaryOperatorExpression(new UnaryOperator(
                UnaryOperatorType.FallOut,
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.MemberAccess, VariableAccessor("a"), VariableAccessor("b"), Token.Dot(default))),
                Token.QuestionMark(default)))),
            ("a * b.c", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                VariableAccessor("a"),
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.MemberAccess, VariableAccessor("b"), VariableAccessor("c"), Token.Dot(default))),
                Token.Star(default)))),
            ("b.c * a", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.MemberAccess, VariableAccessor("b"), VariableAccessor("c"), Token.Dot(default))),
                VariableAccessor("a"),
                Token.Star(default)))),
            ("new Thing {}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("Thing", default), []),
                []))),
            ("new Thing {A = a}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("Thing", default), []),
                [new FieldInitializer(Token.Identifier("A", default), VariableAccessor("a"))]))),
            // todo: trailing commas everwhere
            //("new Thing {A = a,}", new ObjectInitializerExpression(new ObjectInitializer(
            //    new TypeIdentifier(Token.Identifier("Thing", default), []),
            //    [new FieldInitializer(Token.Identifier("A", default), VariableAccessor("a"))]))),
            ("new Thing {A = a, B = b}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("Thing", default), []),
                [
                    new FieldInitializer(Token.Identifier("A", default), VariableAccessor("a")),
                    new FieldInitializer(Token.Identifier("B", default), VariableAccessor("b")),
                ]))),
            ("MyType::CallMethod", new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.StaticMemberAccess, VariableAccessor("MyType"), VariableAccessor("CallMethod"), Token.DoubleColon(default)))),
            ("MyType::StaticField.InstanceField", new BinaryOperatorExpression(
                new BinaryOperator(BinaryOperatorType.MemberAccess, 
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.StaticMemberAccess, VariableAccessor("MyType"), VariableAccessor("StaticField"), Token.DoubleColon(default))),
                    VariableAccessor("InstanceField"),
                Token.Dot(default)))),
            ("string::CallMethod", new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.StaticMemberAccess, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.StringKeyword(default))), VariableAccessor("CallMethod"), Token.DoubleColon(default)))),
            ("result::<string>::CallMethod", new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.StaticMemberAccess, 
                new GenericInstantiationExpression(new GenericInstantiation(
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Result(default))),
                    [new TypeIdentifier(Token.StringKeyword(default), [])])),
                VariableAccessor("CallMethod"), Token.DoubleColon(default)))),
            // ____binding strength tests
            // __greater than
            ( // greater than
                "a > b > c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.GreaterThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.RightAngleBracket(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a > b < c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.GreaterThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.RightAngleBracket(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a > b * c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // divide
                "a > b / c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // plus
                "a > b + c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Plus(default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // minus
                "a > b - c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Dash(default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // fallOut
                "a > b?",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // value assignment check
                "a > b = c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.ValueAssignment,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.RightAngleBracket(default))),
                    VariableAccessor("c"),
                    Token.Equals(default)))
             ),
            ( // equality check
                "a > b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.RightAngleBracket(default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(default)))
             ),
            ( // member access
                "a > b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.MemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.Dot(default))),
                    Token.RightAngleBracket(default)))
            ),
            ( // static member access
                "a > b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.StaticMemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.DoubleColon(default))),
                    Token.RightAngleBracket(default)))
            ),
            // __Less than
            ( // greater than
                "a < b > c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.LeftAngleBracket(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a < b < c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.LeftAngleBracket(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a < b * c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // divide
                "a < b / c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // plus
                "a < b + c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Plus(default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // minus
                "a < b - c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Dash(default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // fallOut
                "a < b?",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.LeftAngleBracket(default)))
            ),
            ("a < b = c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                    new BinaryOperatorExpression(
                        new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.LeftAngleBracket(default))),
                        VariableAccessor("c"), Token.Equals(default)))),
            ( // equality check
                "a < b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.LeftAngleBracket(default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(default)))
             ),
            ( // member access
                "a < b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.MemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.Dot(default))),
                    Token.LeftAngleBracket(default)))
            ),
            ( // static member access
                "a < b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.StaticMemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.DoubleColon(default))),
                    Token.LeftAngleBracket(default)))
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
                            Token.Star(default))),
                        VariableAccessor("c"),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a * b < c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Star(default))),
                        VariableAccessor("c"),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a * b * c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Star(default)))
            ),
            ( // divide
                "a * b / c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.ForwardSlash(default)))
            ),
            ( // plus
                "a * b + c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
            ),
            ( // minus
                "a * b - c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
            ),
            ( // fallOut
                "a * b?",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        VariableAccessor("a"),
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("b"),
                            Token.QuestionMark(default))),
                        Token.Star(default)))
            ),
            ("a * b = c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                    new BinaryOperatorExpression(
                        new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Star(default))),
                        VariableAccessor("c"), Token.Equals(default)))),
            ( // equality check
                "a * b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.Star(default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(default)))
             ),
            ( // member access
                "a * b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.MemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.Dot(default))),
                    Token.Star(default)))
            ),
            ( // static member access
                "a * b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Multiply,
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.StaticMemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.DoubleColon(default))),
                    Token.Star(default)))
            ),
            // __divide
            ( // greater than
                "a / b > c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a / b < c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a / b * c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Star(default)))
            ),
            ( // divide
                "a / b / c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.ForwardSlash(default)))
            ),
            ( // plus
                "a / b + c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
            ),
            ( // minus
                "a / b - c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
            ),
            ( // fallOut
                "a / b?",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.ForwardSlash(default)))
            ),
            ("a / b = c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                    new BinaryOperatorExpression(
                        new BinaryOperator(
                            BinaryOperatorType.Divide,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.ForwardSlash(default))),
                        VariableAccessor("c"), Token.Equals(default)))),
            ( // equality check
                "a / b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.ForwardSlash(default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(default)))
             ),
            ( // member access
                "a / b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.MemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.Dot(default))),
                    Token.ForwardSlash(default)))
            ),
            ( // static member access
                "a / b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Divide,
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.StaticMemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.DoubleColon(default))),
                    Token.ForwardSlash(default)))
            ),
            // __plus
            ( // greater than
                "a + b > c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a + b < c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a + b * c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.Plus(default)))
            ),
            ( // divide
                "a + b / c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.Plus(default)))
            ),
            ( // plus
                "a + b + c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
            ),
            ( // minus
                "a + b - c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
            ),
            ( // fallOut
                "a + b?",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.Plus(default)))
            ),
            ("a + b = c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                    new BinaryOperatorExpression(
                        new BinaryOperator(
                            BinaryOperatorType.Plus,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Plus(default))),
                        VariableAccessor("c"), Token.Equals(default)))),
            ( // equality check
                "a + b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.Plus(default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(default)))
             ),
            ( // member access
                "a + b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.MemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.Dot(default))),
                    Token.Plus(default)))
            ),
            ( // static member access
                "a + b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Plus,
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.StaticMemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.DoubleColon(default))),
                    Token.Plus(default)))
            ),
            // __minus
            ( // greater than
                "a - b > c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a - b < c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a - b * c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.Dash(default)))
            ),
            ( // divide
                "a - b / c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.Dash(default)))
            ),
            ( // plus
                "a - b + c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
            ),
            ( // minus
                "a - b - c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
            ),
            ( // fallOut
                "a - b?",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.Dash(default)))
            ),
            ("a - b = c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                    new BinaryOperatorExpression(
                        new BinaryOperator(
                            BinaryOperatorType.Minus,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Dash(default))),
                        VariableAccessor("c"), Token.Equals(default)))),
            ( // equality check
                "a - b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.Dash(default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(default)))
             ),
            ( // member access
                "a - b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.MemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.Dot(default))),
                    Token.Dash(default)))
            ),
            ( // static member access
                "a - b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.Minus,
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.StaticMemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.DoubleColon(default))),
                    Token.Dash(default)))
            ),
            // __FallOut
            ( // fallout
                "a??",
                    new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(default))),
                    Token.QuestionMark(default)))
                ),
            ( // less than
                "a? < b",
                    new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(default))),
                    VariableAccessor("b"),
                    Token.LeftAngleBracket(default)))
            ),
            ( // greater than
                "a? > b",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.RightAngleBracket(default)))
            ),
            (// plus
                "a? + b",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.Plus(default)))
            ),
            ( // minus
                "a? - b",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.Dash(default)))
            ),
            ( // multiply
                "a? * b",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.Star(default)))
            ),
            ( // divide
                "a? / b",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.ForwardSlash(default)))
            ),
            ( // assignment
                "a? = c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.ValueAssignment,
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("c"),
                    Token.Equals(default)))),
            ( // equality check
                "a? == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(default)))
             ),
            ( // member access
                "a?.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.MemberAccess,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(default))),
                    VariableAccessor("c"),
                    Token.Dot(default)))
            ),
            ( // static member access
                "a?::c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.StaticMemberAccess,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(default))),
                    VariableAccessor("c"),
                    Token.DoubleColon(default)))
            ),
            // __ value assignment
            ( // greater than
                "a = b > c",
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.GreaterThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.RightAngleBracket(default))), Token.Equals(default)))
            ),
            ( // less than
                "a = b < c",
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.LeftAngleBracket(default)))
                        , Token.Equals(default)))
            ),
            ( // multiply
                "a = b * c",
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))), Token.Equals(default)))
            ),
            ( // divide
                "a = b / c",
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))), Token.Equals(default)))
            ),
            ( // plus
                "a = b + c",
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Plus(default))), Token.Equals(default)))
            ),
            ( // minus
                "a = b - c",
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Dash(default))), Token.Equals(default)))
            ),
            ( // fallOut
                "a = b?",
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))), Token.Equals(default)))
            ),
            ( // value assignment
                "a = b = c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                    new BinaryOperatorExpression(
                        new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                            VariableAccessor("a"),
                            VariableAccessor("b"), Token.Equals(default))),
                        VariableAccessor("c"), Token.Equals(default)))),
            ( // equality check
                "a = b == c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.DoubleEquals(default))), Token.Equals(default)))
             ),
            ( // member access
                "a = b.c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.MemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.Dot(default))),
                    Token.Equals(default)))
             ),
            ( // static member access
                "a = b::c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.StaticMemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.DoubleColon(default))),
                    Token.Equals(default)))
             ),
            // __ equality check
            ( // greater than
                "a == b > c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.GreaterThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.RightAngleBracket(default))),
                        Token.DoubleEquals(default)))
            ),
            ( // less than
                "a == b < c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.LeftAngleBracket(default))),
                        Token.DoubleEquals(default)))
            ),
            ( // multiply
                "a == b * c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.DoubleEquals(default)))
            ),
            ( // divide
                "a == b / c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                            Token.DoubleEquals(default)))
            ),
            ( // plus
                "a == b + c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Plus(default))),
                        Token.DoubleEquals(default)))
            ),
            ( // minus
                "a == b - c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Dash(default))),
                        Token.DoubleEquals(default)))
            ),
            ( // fallOut
                "a == b?",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.DoubleEquals(default)))
            ),
            ( // value assignment
                "a == b = c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.DoubleEquals(default))),
                    VariableAccessor("c"), Token.Equals(default)))
            ),
            ( // equality check
                "a == b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        VariableAccessor("a"),
                        VariableAccessor("b"),
                        Token.DoubleEquals(default))),
                    VariableAccessor("c"),
                    Token.DoubleEquals(default)))
             ),
            ( // member access
                "a == b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                    BinaryOperatorType.EqualityCheck,
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.MemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.Dot(default))),
                    Token.DoubleEquals(default)))
            ),
            ( // static member access
                "a == b::c",
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.EqualityCheck, 
                    VariableAccessor("a"),
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.StaticMemberAccess,
                        VariableAccessor("b"),
                        VariableAccessor("c"),
                        Token.DoubleColon(default))),
                    Token.DoubleEquals(default)))
             ),
            // __Member Access
            ( // greater than
                "a.b > c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.MemberAccess,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Dot(default))),
                        VariableAccessor("c"),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a.b < c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.MemberAccess,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Dot(default))),
                        VariableAccessor("c"),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a.b * c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.MemberAccess,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Dot(default))),
                        VariableAccessor("c"),
                        Token.Star(default)))
            ),
            ( // divide
                "a.b / c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.MemberAccess,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Dot(default))),
                        VariableAccessor("c"),
                        Token.ForwardSlash(default)))
            ),
            ( // plus
                "a.b + c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.MemberAccess,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Dot(default))),
                        VariableAccessor("c"),
                        Token.Plus(default)))
            ),
            ( // minus
                "a.b - c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.MemberAccess,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Dot(default))),
                        VariableAccessor("c"),
                        Token.Dash(default)))
            ),
            ( // fallOut
                "a.b?",
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.MemberAccess,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Dot(default))),
                        Token.QuestionMark(default)))
            ),
            ( // value assignment
                "a.b = c",
                new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.ValueAssignment,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.MemberAccess,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Dot(default))),
                        VariableAccessor("c"),
                        Token.Equals(default)))
            ),
            ( // equality check
                "a.b == c",
                new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.MemberAccess,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Dot(default))),
                        VariableAccessor("c"),
                        Token.DoubleEquals(default)))
             ),
            ( // member access
                "a.b.c",
                new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.MemberAccess,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.MemberAccess,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Dot(default))),
                        VariableAccessor("c"),
                        Token.Dot(default)))
            ),
            ( // static member access
                "a.b::c",
                new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.MemberAccess,
                        VariableAccessor("a"),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.StaticMemberAccess,
                            VariableAccessor("b"),
                            VariableAccessor("c"),
                            Token.DoubleColon(default))),
                        Token.Dot(default)))
            ),
             // __Static Member Access
             ( // greater than
                 "a::b > c",
                     new BinaryOperatorExpression(new BinaryOperator(
                         BinaryOperatorType.GreaterThan,
                         new BinaryOperatorExpression(new BinaryOperator(
                             BinaryOperatorType.StaticMemberAccess,
                             VariableAccessor("a"),
                             VariableAccessor("b"),
                             Token.DoubleColon(default))),
                         VariableAccessor("c"),
                         Token.RightAngleBracket(default)))
             ),
             ( // less than
                 "a::b < c",
                     new BinaryOperatorExpression(new BinaryOperator(
                         BinaryOperatorType.LessThan,
                         new BinaryOperatorExpression(new BinaryOperator(
                             BinaryOperatorType.StaticMemberAccess,
                             VariableAccessor("a"),
                             VariableAccessor("b"),
                             Token.DoubleColon(default))),
                         VariableAccessor("c"),
                         Token.LeftAngleBracket(default)))
             ),
             ( // multiply
                 "a::b * c",
                     new BinaryOperatorExpression(new BinaryOperator(
                         BinaryOperatorType.Multiply,
                         new BinaryOperatorExpression(new BinaryOperator(
                             BinaryOperatorType.StaticMemberAccess,
                             VariableAccessor("a"),
                             VariableAccessor("b"),
                             Token.DoubleColon(default))),
                         VariableAccessor("c"),
                         Token.Star(default)))
             ),
             ( // divide
                 "a::b / c",
                     new BinaryOperatorExpression(new BinaryOperator(
                         BinaryOperatorType.Divide,
                         new BinaryOperatorExpression(new BinaryOperator(
                             BinaryOperatorType.StaticMemberAccess,
                             VariableAccessor("a"),
                             VariableAccessor("b"),
                             Token.DoubleColon(default))),
                         VariableAccessor("c"),
                         Token.ForwardSlash(default)))
             ),
             ( // plus
                 "a::b + c",
                     new BinaryOperatorExpression(new BinaryOperator(
                         BinaryOperatorType.Plus,
                         new BinaryOperatorExpression(new BinaryOperator(
                             BinaryOperatorType.StaticMemberAccess,
                             VariableAccessor("a"),
                             VariableAccessor("b"),
                             Token.DoubleColon(default))),
                         VariableAccessor("c"),
                         Token.Plus(default)))
             ),
             ( // minus
                 "a::b - c",
                     new BinaryOperatorExpression(new BinaryOperator(
                         BinaryOperatorType.Minus,
                         new BinaryOperatorExpression(new BinaryOperator(
                             BinaryOperatorType.StaticMemberAccess,
                             VariableAccessor("a"),
                             VariableAccessor("b"),
                             Token.DoubleColon(default))),
                         VariableAccessor("c"),
                         Token.Dash(default)))
             ),
             ( // fallOut
                 "a::b?",
                     new UnaryOperatorExpression(new UnaryOperator(
                         UnaryOperatorType.FallOut,
                         new BinaryOperatorExpression(new BinaryOperator(
                             BinaryOperatorType.StaticMemberAccess,
                             VariableAccessor("a"),
                             VariableAccessor("b"),
                             Token.DoubleColon(default))),
                         Token.QuestionMark(default)))
             ),
             ( // value assignment
                 "a::b = c",
                 new BinaryOperatorExpression(new BinaryOperator(
                         BinaryOperatorType.ValueAssignment,
                         new BinaryOperatorExpression(new BinaryOperator(
                             BinaryOperatorType.StaticMemberAccess,
                             VariableAccessor("a"),
                             VariableAccessor("b"),
                             Token.DoubleColon(default))),
                         VariableAccessor("c"),
                         Token.Equals(default)))
             ),
             ( // equality check
                 "a::b == c",
                 new BinaryOperatorExpression(new BinaryOperator(
                         BinaryOperatorType.EqualityCheck,
                         new BinaryOperatorExpression(new BinaryOperator(
                             BinaryOperatorType.StaticMemberAccess,
                             VariableAccessor("a"),
                             VariableAccessor("b"),
                             Token.DoubleColon(default))),
                         VariableAccessor("c"),
                         Token.DoubleEquals(default)))
              ),
             ( // member access
                 "a::b.c",
                 new BinaryOperatorExpression(new BinaryOperator(
                         BinaryOperatorType.MemberAccess,
                         new BinaryOperatorExpression(new BinaryOperator(
                             BinaryOperatorType.StaticMemberAccess,
                             VariableAccessor("a"),
                             VariableAccessor("b"),
                             Token.DoubleColon(default))),
                         VariableAccessor("c"),
                         Token.Dot(default)))
             ),
             ( // static member access
                 "a::b::c",
                 new BinaryOperatorExpression(new BinaryOperator(
                         BinaryOperatorType.StaticMemberAccess,
                         new BinaryOperatorExpression(new BinaryOperator(
                             BinaryOperatorType.StaticMemberAccess,
                             VariableAccessor("a"),
                             VariableAccessor("b"),
                             Token.DoubleColon(default))),
                         VariableAccessor("c"),
                         Token.DoubleColon(default)))
             ),           
        }.Select(x => new object[] { x.Source, new Tokenizer().Tokenize(x.Source), x.ExpectedExpression });
    }
    
    private static IExpression VariableAccessor(string name) =>
        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier(name, default)));

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

        return new TypeIdentifier(RemoveSourceSpan(typeIdentifier.Identifier), [..typeIdentifier.TypeArguments.Select(RemoveSourceSpan)!]);
    }

    private static FunctionParameter RemoveSourceSpan(FunctionParameter parameter)
    {
        return new FunctionParameter(
            RemoveSourceSpan(parameter.Type),
            RemoveSourceSpan(parameter.Identifier));
    }

    [return: NotNullIfNotNull(nameof(expression))]
    private static IExpression? RemoveSourceSpan(IExpression? expression)
    {
        return expression switch
        {
            null => null,
            ValueAccessorExpression valueAccessorExpression => new ValueAccessorExpression(RemoveSourceSpan(valueAccessorExpression.ValueAccessor)),
            UnaryOperatorExpression unaryOperatorExpression => new UnaryOperatorExpression(RemoveSourceSpan(unaryOperatorExpression.UnaryOperator)),
            BinaryOperatorExpression binaryOperatorExpression => new BinaryOperatorExpression(RemoveSourceSpan(binaryOperatorExpression.BinaryOperator)),
            VariableDeclarationExpression variableDeclarationExpression => new VariableDeclarationExpression(RemoveSourceSpan(variableDeclarationExpression.VariableDeclaration)),
            IfExpressionExpression ifExpressionExpression => new IfExpressionExpression(RemoveSourceSpan(ifExpressionExpression.IfExpression)),
            BlockExpression blockExpression => new BlockExpression(RemoveSourceSpan(blockExpression.Block)),
            MethodCallExpression methodCallExpression => new MethodCallExpression(RemoveSourceSpan(methodCallExpression.MethodCall)),
            MethodReturnExpression methodReturnExpression => new MethodReturnExpression(RemoveSourceSpan(methodReturnExpression.MethodReturn)),
            ObjectInitializerExpression objectInitializerExpression => new ObjectInitializerExpression(RemoveSourceSpan(objectInitializerExpression.ObjectInitializer)),
            GenericInstantiationExpression genericInstantiationExpression => new GenericInstantiationExpression(RemoveSourceSpan(genericInstantiationExpression.GenericInstantiation)),
            _ => throw new NotImplementedException()
        };
    }
    
    private static GenericInstantiation RemoveSourceSpan(
        GenericInstantiation genericInstantiation)
    {
            return new GenericInstantiation(
                RemoveSourceSpan(genericInstantiation.GenericInstance),
                [..genericInstantiation.TypeArguments.Select(RemoveSourceSpan)!]);
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
            CheckExpression: RemoveSourceSpan(ifExpression.CheckExpression),
            Body: RemoveSourceSpan(ifExpression.Body),
            ElseIfs: [..ifExpression.ElseIfs.Select(RemoveSourceSpan)],
            ElseBody: RemoveSourceSpan(ifExpression.ElseBody));
    }

    private static ElseIf RemoveSourceSpan(ElseIf elseIf)
    {
        return new ElseIf(RemoveSourceSpan(elseIf.CheckExpression), RemoveSourceSpan(elseIf.Body));
    }
    
    private static VariableDeclaration RemoveSourceSpan(VariableDeclaration variableDeclaration)
    {
                return new VariableDeclaration(
                    VariableNameToken: RemoveSourceSpan(variableDeclaration.VariableNameToken),
                    MutabilityModifier: RemoveSourceSpan(variableDeclaration.MutabilityModifier),
                    Type: RemoveSourceSpan(variableDeclaration.Type),
                    Value: RemoveSourceSpan(variableDeclaration.Value));
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
        return token with { SourceSpan = default };
    }

    private static LangProgram RemoveSourceSpan(LangProgram program)
    {
        return new LangProgram(
            [..program.Expressions.Select(RemoveSourceSpan)!],
            [..program.Functions.Select(RemoveSourceSpan)],
            [..program.Classes.Select(RemoveSourceSpan)]);
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
            RemoveSourceSpan(field.Type));
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