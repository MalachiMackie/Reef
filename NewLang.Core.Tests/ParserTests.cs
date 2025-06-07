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
        LangProgram expectedProgram)
    {
        var result = Parser.Parse(tokens);
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
            ("fn MyFn(a: int,){}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [new FunctionParameter(new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []), Token.Identifier("a", SourceSpan.Default))],
                    null,
                    new Block([], []))
            ], [])),
            ("fn /* some comment */ MyFn(/*some comment*/a: int,)/**/{//}\r\n}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [new FunctionParameter(new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []), Token.Identifier("a", SourceSpan.Default))],
                    null,
                    new Block([], []))
            ], [])),
            ("class MyClass<T,> {}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", SourceSpan.Default),
                [Token.Identifier("T", SourceSpan.Default)],
                [], [])])),
            ("fn MyFn<T,>(){}", new LangProgram([], [new LangFunction(
                null,
                null,
                Token.Identifier("MyFn", SourceSpan.Default),
                [Token.Identifier("T", SourceSpan.Default)],
                [],
                null,
                new Block([], [])
                )], [])),
            ("var a = 1;var b = 2;", new LangProgram([
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", SourceSpan.Default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default))))),
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("b", SourceSpan.Default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default))))),
                ], [], [])),
            ("a = b;", new LangProgram([
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, VariableAccessor("a"), VariableAccessor("b"), Token.Equals(SourceSpan.Default))),
                ], [], [])),
            ("error();", new LangProgram([
                new MethodCallExpression(new MethodCall(new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Error(SourceSpan.Default))), [])),
                ], [], [])),
            ("ok();", new LangProgram([
                new MethodCallExpression(new MethodCall(new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Ok(SourceSpan.Default))), [])),
                ], [], [])),
            ("ok().b()", new LangProgram([
                new MethodCallExpression(new MethodCall(
                    new MemberAccessExpression(new MemberAccess( 
                        new MethodCallExpression(new MethodCall(
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Ok(SourceSpan.Default))), [])),
                        Token.Identifier("b", SourceSpan.Default))), [])),
                ], [], [])),
            ("if (a) {} b = c;", new LangProgram(
                [
                    new IfExpressionExpression(new IfExpression(VariableAccessor("a"), new BlockExpression(new Block([], [])), [], null)),
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, VariableAccessor("b"), VariableAccessor("c"), Token.Equals(SourceSpan.Default)))
                ],
                [],
                [])),
            ("{} b = c;", new LangProgram(
                [
                    new BlockExpression(new Block([], [])),
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, VariableAccessor("b"), VariableAccessor("c"), Token.Equals(SourceSpan.Default)))
                ],
                [],
                [])),
            ("fn MyFn() {}", new LangProgram([], [
                new LangFunction(null, null, Token.Identifier("MyFn", SourceSpan.Default), [], [], null, new Block([], []))
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
                Token.Identifier("MyFn", SourceSpan.Default),
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
                            Token.Identifier("MyFn", SourceSpan.Default),
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
                new LangFunction(null, null, Token.Identifier("MyFn", SourceSpan.Default), [], [], new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []), new Block([], []))
            ], [])),
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
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []),
                            new TypeIdentifier(Token.Identifier("MyErrorType", SourceSpan.Default), []),
                        ]),
                    new Block([], []))
            ], [])),
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
                                new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [])]),
                        ]),
                    new Block([], []))
            ], [])),
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
                            new TypeIdentifier(Token.Identifier("Inner", SourceSpan.Default), [new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [])]),
                            new TypeIdentifier(Token.Identifier("Inner", SourceSpan.Default), [new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [])]),
                        ]),
                    new Block([], []))
            ], [])),
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
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []),
                            new TypeIdentifier(Token.Identifier("MyErrorType", SourceSpan.Default), []),
                            new TypeIdentifier(Token.Identifier("ThirdTypeArgument", SourceSpan.Default), []),
                        ]),
                    new Block([], []))
            ], [])),
            ("fn MyFn() { var a = 2; }", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [],
                    null,
                    new Block([new VariableDeclarationExpression(new VariableDeclaration(
                        Token.Identifier("a", SourceSpan.Default),
                        null,
                        null,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default)))))], [])
                )
            ], [])),
            ("fn MyFn(a: int) {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [new FunctionParameter(new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []), Token.Identifier("a", SourceSpan.Default))],
                    null,
                    new Block([], [])
                )
            ], [])),
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
            ], [])),
            ("fn MyFn<T1>() {}", new LangProgram([], [new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [Token.Identifier("T1", SourceSpan.Default)],
                    [],
                    null,
                    new Block([], [])
                )], [])),
            ("fn MyFn<T1, T2>() {}", new LangProgram([], [new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [Token.Identifier("T1", SourceSpan.Default), Token.Identifier("T2", SourceSpan.Default)],
                    [],
                    null,
                    new Block([], [])
                )], [])),
            ("fn MyFn<T1, T2, T3>() {}", new LangProgram([], [new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [Token.Identifier("T1", SourceSpan.Default), Token.Identifier("T2", SourceSpan.Default), Token.Identifier("T3", SourceSpan.Default)],
                    [],
                    null,
                    new Block([], [])
                )], [])),
            ("fn MyFn(a: result::<int, MyType>) {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [new FunctionParameter(new TypeIdentifier(
                        Token.Result(SourceSpan.Default), [
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []),
                            new TypeIdentifier(Token.Identifier("MyType", SourceSpan.Default), []),
                        ]), Token.Identifier("a", SourceSpan.Default))],
                    null,
                    new Block([], [])
                )
            ], [])),
            ("fn MyFn(a: int, b: MyType) {}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [
                        new FunctionParameter(new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []), Token.Identifier("a", SourceSpan.Default)),
                        new FunctionParameter(new TypeIdentifier(Token.Identifier("MyType", SourceSpan.Default), []), Token.Identifier("b", SourceSpan.Default)),
                    ],
                    null,
                    new Block([], [])
                )
            ], [])),
            ("fn MyFn(): int {return 1;}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [],
                    new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []),
                    new Block([new MethodReturnExpression(new MethodReturn(new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default)))))], [])
                )
            ], [])),
            ("class MyClass {}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", SourceSpan.Default),
                [],
                [],
                [])])),
            ("class MyClass<T> {}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", SourceSpan.Default),
                [Token.Identifier("T", SourceSpan.Default)],
                [],
                [])])),
            ("class MyClass<T, T2, T3> {}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", SourceSpan.Default),
                [Token.Identifier("T", SourceSpan.Default), Token.Identifier("T2", SourceSpan.Default), Token.Identifier("T3", SourceSpan.Default)],
                [],
                [])])),
            ("pub class MyClass {}", new LangProgram([], [], [new ProgramClass(
                new AccessModifier(Token.Pub(SourceSpan.Default)),
                Token.Identifier("MyClass", SourceSpan.Default),
                [],
                [],
                [])])),
            ("class MyClass {pub mut field MyField: string;}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", SourceSpan.Default),
                [],
                [],
                [new ClassField(
                    new AccessModifier(Token.Pub(SourceSpan.Default)),
                    null,
                    new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                    Token.Identifier("MyField", SourceSpan.Default),
                    new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []), null)])])),
            ("class MyClass {pub static mut field MyField: string;}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", SourceSpan.Default),
                [],
                [],
                [new ClassField(
                    new AccessModifier(Token.Pub(SourceSpan.Default)),
                    new StaticModifier(Token.Static(SourceSpan.Default)),
                    new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                    Token.Identifier("MyField", SourceSpan.Default),
                    new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []), null)])])),
            ("class MyClass {mut field MyField: string;}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", SourceSpan.Default),
                [],
                [],
                [new ClassField(
                    null,
                    null,
                    new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                    Token.Identifier("MyField", SourceSpan.Default),
                    new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []), null)])])),
            ("class MyClass {field MyField: string;}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", SourceSpan.Default),
                [],
                [],
                [new ClassField(
                    null,
                    null,
                    null,
                    Token.Identifier("MyField", SourceSpan.Default),
                    new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []), null)])])),
            ("class MyClass {pub field MyField: string;}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", SourceSpan.Default),
                [],
                [],
                [new ClassField(
                    new AccessModifier(Token.Pub(SourceSpan.Default)),
                    null,
                    null,
                    Token.Identifier("MyField", SourceSpan.Default),
                    new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []), null)])])),
            ("class MyClass {pub mut field MyField: string; pub fn MyFn() {}}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", SourceSpan.Default),
                [],
                [new LangFunction(new AccessModifier(Token.Pub(SourceSpan.Default)), null, Token.Identifier("MyFn", SourceSpan.Default), [], [], null, new Block([], []))],
                [new ClassField(
                    new AccessModifier(Token.Pub(SourceSpan.Default)),
                    null,
                    new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                    Token.Identifier("MyField", SourceSpan.Default),
                    new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []), null)])])),
            ("class MyClass {field MyField: string; fn MyFn() {}}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", SourceSpan.Default),
                [],
                [new LangFunction(null, null, Token.Identifier("MyFn", SourceSpan.Default), [], [], null, new Block([], []))],
                [new ClassField(
                    null,
                    null,
                    null,
                    Token.Identifier("MyField", SourceSpan.Default),
                    new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []), null)])])),
            ("pub fn DoSomething(a: int): result::<int, string> {}", new LangProgram(
                [],
                [
                    new LangFunction(
                        new AccessModifier(Token.Pub(SourceSpan.Default)),
                        null,
                        Token.Identifier("DoSomething", SourceSpan.Default),
                        [],
                        [new FunctionParameter(new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []), Token.Identifier("a", SourceSpan.Default))],
                        new TypeIdentifier(Token.Result(SourceSpan.Default), [new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []), new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [])]),
                        new Block([], []))
                ],
                [])),
            (
                "class MyClass { static field someField: int = 3; }",
                new LangProgram(
                    [],
                    [],
                    [new ProgramClass(
                        null,
                        Token.Identifier("MyClass", SourceSpan.Default),
                        [],
                        [],
                        [new ClassField(
                            null,
                            new StaticModifier(Token.Static(SourceSpan.Default)),
                            null,
                            Token.Identifier("someField", SourceSpan.Default),
                            new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(3, SourceSpan.Default))))]
                        )])
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
                new MethodCallExpression(new MethodCall(VariableAccessor("Println"), [new MethodCallExpression(new MethodCall(VariableAccessor("DoSomething"), [new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(5, SourceSpan.Default)))]))])),
                new MethodCallExpression(new MethodCall(VariableAccessor("Println"), [new MethodCallExpression(new MethodCall(VariableAccessor("DoSomething"), [new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default)))]))])),
                new MethodCallExpression(new MethodCall(VariableAccessor("Println"), [new MethodCallExpression(new MethodCall(VariableAccessor("SomethingElse"), [new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default)))]))])),
            ], 
            [
                new LangFunction(
                    new AccessModifier(Token.Pub(SourceSpan.Default)),
                    null,
                    Token.Identifier("DoSomething", SourceSpan.Default),
                    [],
                    [new FunctionParameter(new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []), Token.Identifier("a", SourceSpan.Default))],
                    new TypeIdentifier(Token.Result(SourceSpan.Default), [new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []), new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [])]),
                    new Block(
                        [
                            new VariableDeclarationExpression(new VariableDeclaration(
                                Token.Identifier("b", SourceSpan.Default),
                                null,
                                new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []),
                                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default))))),
                            new IfExpressionExpression(new IfExpression(
                                new BinaryOperatorExpression(new BinaryOperator(
                                    BinaryOperatorType.GreaterThan,
                                    VariableAccessor("a"),
                                    VariableAccessor("b"),
                                    Token.RightAngleBracket(SourceSpan.Default))),
                                new BlockExpression(new Block(
                                    [new MethodReturnExpression(new MethodReturn(
                                            new MethodCallExpression(new MethodCall(
                                                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Ok(SourceSpan.Default))),
                                                [VariableAccessor("a")]))
                                            )
                                        )],
                                    [])),
                                [new ElseIf(
                                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.EqualityCheck, VariableAccessor("a"), VariableAccessor("b"), Token.DoubleEquals(SourceSpan.Default))),
                                    new BlockExpression(new Block([new MethodReturnExpression(new MethodReturn(
                                            new MethodCallExpression(new MethodCall(
                                                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Ok(SourceSpan.Default))),
                                                [VariableAccessor("b")]))
                                            )
                                        )], []))
                                    )],
                                    new BlockExpression(new Block([], []))
                                )),
                            new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                                VariableAccessor("b"),
                                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(3, SourceSpan.Default))), Token.Equals(SourceSpan.Default))),
                            new VariableDeclarationExpression(new VariableDeclaration(
                                Token.Identifier("thing", SourceSpan.Default),
                                null,
                                null,
                                new ObjectInitializerExpression(new ObjectInitializer(
                                    new TypeIdentifier(Token.Identifier("Class2", SourceSpan.Default), []),
                                    [
                                        new FieldInitializer(Token.Identifier("A", SourceSpan.Default), new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(3, SourceSpan.Default))))
                                    ])))),
                            new MethodCallExpression(new MethodCall(
                                new StaticMemberAccessExpression(new StaticMemberAccess(
                                    new TypeIdentifier(Token.Identifier("MyClass", SourceSpan.Default), []),
                                    Token.Identifier("StaticMethod", SourceSpan.Default)
                                    )),
                                [])),
                            new MethodCallExpression(new MethodCall(
                                new GenericInstantiationExpression(new GenericInstantiation(new ValueAccessorExpression(
                                    new ValueAccessor(ValueAccessType.Variable, Token.Identifier("PrivateFn", SourceSpan.Default))),
                                        [new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [])])),
                                []
                                )),
                            new MethodReturnExpression(new MethodReturn(
                                new MethodCallExpression(new MethodCall(
                                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Error(SourceSpan.Default))),
                                    [new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("something wrong", SourceSpan.Default)))]
                                    ))))],
                        [])),
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("PrivateFn", SourceSpan.Default),
                    [Token.Identifier("T", SourceSpan.Default)],
                    [],
                    null,
                    new Block(
                        [new MethodCallExpression(
                            new MethodCall(VariableAccessor("Println"),
                                [new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("Message", SourceSpan.Default)))]))],
                        [
                            new LangFunction(
                                null,
                                null,
                                Token.Identifier("InnerFn", SourceSpan.Default),
                                [],
                                [],
                                null,
                                new Block(
                                    [new MethodCallExpression(
                                        new MethodCall(VariableAccessor("Println"),
                                            [new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("Something", SourceSpan.Default)))]))],
                                    [
                                    ]))
                        ])),
                new LangFunction(
                    new AccessModifier(Token.Pub(SourceSpan.Default)),
                    null,
                    Token.Identifier("SomethingElse", SourceSpan.Default),
                    [],
                    [new FunctionParameter(new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []), Token.Identifier("a", SourceSpan.Default))],
                    new TypeIdentifier(Token.Result(SourceSpan.Default), [new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []), new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [])]),
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
                                        ])),
                                    Token.QuestionMark(SourceSpan.Default))))),
                            new VariableDeclarationExpression(new VariableDeclaration(
                                Token.Identifier("c", SourceSpan.Default),
                                new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                                null,
                                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default))))),
                            new MethodReturnExpression(new MethodReturn(VariableAccessor("b")))
                        ],
                        [])
                    ),
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
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []), null),
                        new ClassField(null,
                            null,
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Token.Identifier("FieldB", SourceSpan.Default),
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []), null),
                        new ClassField(new AccessModifier(Token.Pub(SourceSpan.Default)),
                            null,
                            new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                            Token.Identifier("FieldC", SourceSpan.Default),
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []), null),
                        new ClassField(new AccessModifier(Token.Pub(SourceSpan.Default)),
                            null,
                            null,
                            Token.Identifier("FieldD", SourceSpan.Default),
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []), null),
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
                            new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []), null)
                    ]
                    ),
            ]))
        }.Select(x => new object[] { x.Source, Tokenizer.Tokenize(x.Source), x.ExpectedProgram });
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
        return strings.Select(x => new object[] { x, Tokenizer.Tokenize(x) });
    }

    public static IEnumerable<object[]> SingleTestCase()
    {
        return new (string Source, LangProgram ExpectedProgram)[]
        {
            ("fn /* some comment */ MyFn(/*some comment*/a: int,)/**/{//}\r\n}", new LangProgram([], [
                new LangFunction(
                    null,
                    null,
                    Token.Identifier("MyFn", SourceSpan.Default),
                    [],
                    [new FunctionParameter(new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []), Token.Identifier("a", SourceSpan.Default))],
                    null,
                    new Block([], []))
            ], [])),

        }.Select(x => new object[] { x.Source, Tokenizer.Tokenize(x.Source), x.ExpectedProgram });
    }

    public static IEnumerable<object[]> PopExpressionTestCases()
    {
        return new (string Source, IExpression ExpectedExpression)[]
        {
            // value access expressions
            ("a", new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default)))),
            ("1", new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default)))),
            ("\"my string\"", new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("my string", SourceSpan.Default)))),
            ("true", new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.True(SourceSpan.Default)))),
            ("false", new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.False(SourceSpan.Default)))),
            ("ok", new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Ok(SourceSpan.Default)))),
            ("a == b", new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.EqualityCheck, VariableAccessor("a"), VariableAccessor("b"), Token.DoubleEquals(SourceSpan.Default)))),
            ("ok()", new MethodCallExpression(new MethodCall(new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Ok(SourceSpan.Default))), []))),
            // postfix unary operator
            ("a?", new UnaryOperatorExpression(new UnaryOperator(
                UnaryOperatorType.FallOut,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                Token.QuestionMark(SourceSpan.Default)))),
            ("a??",
                new UnaryOperatorExpression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new UnaryOperatorExpression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        Token.QuestionMark(SourceSpan.Default))),
                    Token.QuestionMark(SourceSpan.Default)))
            ),
            ("return 1", new MethodReturnExpression(
                new MethodReturn(new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default)))))),
            ("return", new MethodReturnExpression(new MethodReturn(null))),
            // binary operator expressions
            ("a < 5", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.LessThan,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(5, SourceSpan.Default))),
                Token.LeftAngleBracket(SourceSpan.Default)))),
            ("\"thing\" > true", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.GreaterThan,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("thing", SourceSpan.Default))),
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
                    new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []),
                    VariableAccessor("b")))),
            ("var a: int", new VariableDeclarationExpression(
                new VariableDeclaration(
                    Token.Identifier("a", SourceSpan.Default),
                    null,
                    new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []),
                    null))),
            ("var mut a = 2", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default)))))),
            ("a = b", new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, VariableAccessor("a"), VariableAccessor("b"), Token.Equals(SourceSpan.Default)))),
            ("var mut a: int = 2", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                new MutabilityModifier(Token.Mut(SourceSpan.Default)),
                new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []),
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default)))))),
            ("var a: bool = b", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                new TypeIdentifier(Token.Bool(SourceSpan.Default), []),
                VariableAccessor("b")))),
            ("var a: int = b", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []),
                VariableAccessor("b")))),
            ("var a: string = b", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []),
                VariableAccessor("b")))),
            ("var a: result = b", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                new TypeIdentifier(Token.Result(SourceSpan.Default), []),
                VariableAccessor("b")))),
            ("var a: MyType = b", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                new TypeIdentifier(Token.Identifier("MyType", SourceSpan.Default), []),
                VariableAccessor("b")))),
            ("var a = 1", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default)))))),
            ("var a = true", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.True(SourceSpan.Default)))))),
            ("var a = \"thing\"", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                null,
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("thing", SourceSpan.Default)))))),
            ("{}", new BlockExpression(new Block([], []))),
            ("{var a = 1;}", new BlockExpression(new Block([
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", SourceSpan.Default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default))))),
                ], []))),
            // tail expression
            ("{var a = 1}", new BlockExpression(new Block(
                [
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", SourceSpan.Default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default)))))], []))),
            // tail expression
            ("{var a = 1;var b = 2}", new BlockExpression(new Block(
                [new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", SourceSpan.Default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default))))),
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("b", SourceSpan.Default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default)))))], []))),
            ("{var a = 1; var b = 2;}", new BlockExpression(new Block([
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("a", SourceSpan.Default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default))))),
                new VariableDeclarationExpression(new VariableDeclaration(Token.Identifier("b", SourceSpan.Default), null, null, new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default))))),
                ], []))),
            ("if (a) var c = 2;", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new VariableDeclarationExpression(new VariableDeclaration(
                    Token.Identifier("c", SourceSpan.Default),
                    null,
                    null,
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default))))), [], null))),
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
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("value", SourceSpan.Default)))))
                ], [])), [], null))),
            ("if (a) {} else {var b = 2;}", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new BlockExpression(new Block([], [])),
                [],
                new BlockExpression(new Block([
                    new VariableDeclarationExpression(new VariableDeclaration(
                        Token.Identifier("b", SourceSpan.Default),
                        null,
                        null,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default)))))
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
                    new BlockExpression(new Block([new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default)))], [])),
                    [],
                    new BlockExpression(new Block([new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default)))], []))))], [])),
                [],
                new BlockExpression(new Block([new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(3, SourceSpan.Default)))], []))))),
            ("if (a) if (b) 1 else 2 else 3", new IfExpressionExpression(new IfExpression(
                VariableAccessor("a"),
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("b"),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default))),
                    [],
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default))))),
                [],
                new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(3, SourceSpan.Default)))))),
            ("var a = if (b) 1 else 2;", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                null,
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("b"),
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default))),
                    [],
                    new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default)))))))),
            ("var a = if (b) {1} else {2};", new VariableDeclarationExpression(new VariableDeclaration(
                Token.Identifier("a", SourceSpan.Default),
                null,
                null,
                new IfExpressionExpression(new IfExpression(
                    VariableAccessor("b"),
                    new BlockExpression(new Block([new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, SourceSpan.Default)))], [])),
                    [],
                    new BlockExpression(new Block([new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, SourceSpan.Default)))], []))))))),
            ("a()", new MethodCallExpression(new MethodCall(VariableAccessor("a"), []))),
            ("a.b::<int>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(new GenericInstantiation(
                    new MemberAccessExpression(new MemberAccess(
                        new ValueAccessorExpression(new ValueAccessor(
                            ValueAccessType.Variable,
                            Token.Identifier("a", SourceSpan.Default))),
                        Token.Identifier("b", SourceSpan.Default)
                        )),
                    []
                )),
                []
            ))),
            ("a::<string>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(new GenericInstantiation(
                    new ValueAccessorExpression(new ValueAccessor(
                        ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default))), [new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [])])),
                []))),
            ("a::<string, int>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(new GenericInstantiation(new ValueAccessorExpression(new ValueAccessor(
                    ValueAccessType.Variable,
                    Token.Identifier("a", SourceSpan.Default)
                    )), [
                        new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []),
                        new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []),
                    ])), []))),
            ("a::<string, int, result::<int>>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(new GenericInstantiation(new ValueAccessorExpression(new ValueAccessor(
                    ValueAccessType.Variable,
                    Token.Identifier("a", SourceSpan.Default)
                    )), [
                                                new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []),
                                                new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), []),
                                                new TypeIdentifier(Token.Result(SourceSpan.Default), [new TypeIdentifier(Token.IntKeyword(SourceSpan.Default), [])]),
                                            ])),
                []))),
            ("a(b)", new MethodCallExpression(new MethodCall(VariableAccessor("a"), [
            VariableAccessor("b")]))),
            ("a(b, c)", new MethodCallExpression(new MethodCall(VariableAccessor("a"), [
                VariableAccessor("b"), VariableAccessor("c")
            ]))),
            ("a(b, c > d, e)", new MethodCallExpression(new MethodCall(VariableAccessor("a"), [
                VariableAccessor("b"),
                new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.GreaterThan, VariableAccessor("c"), VariableAccessor("d"), Token.RightAngleBracket(SourceSpan.Default))),
                VariableAccessor("e")
            ]))),
            ("a.b", new MemberAccessExpression(new MemberAccess(VariableAccessor("a"), Token.Identifier("b", SourceSpan.Default)))),
            ("a.b()", new MethodCallExpression(new MethodCall(new MemberAccessExpression(new MemberAccess(VariableAccessor("a"), Token.Identifier("b", SourceSpan.Default))), []))),
            ("a?.b", new MemberAccessExpression(new MemberAccess(new UnaryOperatorExpression(new UnaryOperator(UnaryOperatorType.FallOut, VariableAccessor("a"), Token.QuestionMark(SourceSpan.Default))),
                Token.Identifier("b", SourceSpan.Default)))),
            ("a.b?", new UnaryOperatorExpression(new UnaryOperator(
                UnaryOperatorType.FallOut,
                new MemberAccessExpression(new MemberAccess(VariableAccessor("a"), Token.Identifier("b", SourceSpan.Default))),
                Token.QuestionMark(SourceSpan.Default)))),
            ("a * b.c", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                VariableAccessor("a"),
                new MemberAccessExpression(new MemberAccess(VariableAccessor("b"), Token.Identifier("c", SourceSpan.Default))),
                Token.Star(SourceSpan.Default)))),
            ("b.c * a", new BinaryOperatorExpression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                new MemberAccessExpression(new MemberAccess(VariableAccessor("b"), Token.Identifier("c", SourceSpan.Default))),
                VariableAccessor("a"),
                Token.Star(SourceSpan.Default)))),
            ("new Thing {}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("Thing", SourceSpan.Default), []),
                []))),
            ("new Thing {A = a}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("Thing", SourceSpan.Default), []),
                [new FieldInitializer(Token.Identifier("A", SourceSpan.Default), VariableAccessor("a"))]))),
            ("myFn(a,)", new MethodCallExpression(new MethodCall(
                new ValueAccessorExpression(new ValueAccessor(
                    ValueAccessType.Variable,
                    Token.Identifier("myFn", SourceSpan.Default))),
                [
                    new ValueAccessorExpression(new ValueAccessor(
                        ValueAccessType.Variable,
                        Token.Identifier("a", SourceSpan.Default)))
                ]))),
            ("new SomeType::<string,>{}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("SomeType", SourceSpan.Default), [
                    new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [])
                ]),
                []))),
            ("SomeFn::<string,>()", new MethodCallExpression(new MethodCall(
                new GenericInstantiationExpression(
                    new GenericInstantiation(
                        new ValueAccessorExpression(new ValueAccessor(
                                            ValueAccessType.Variable,
                                            Token.Identifier("SomeFn", SourceSpan.Default))),
                        [new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [])])),
                []
                ))),
            ("new Thing {A = a,}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("Thing", SourceSpan.Default), []),
                [new FieldInitializer(Token.Identifier("A", SourceSpan.Default), VariableAccessor("a"))]))),
            ("new Thing {A = a, B = b}", new ObjectInitializerExpression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("Thing", SourceSpan.Default), []),
                [
                    new FieldInitializer(Token.Identifier("A", SourceSpan.Default), VariableAccessor("a")),
                    new FieldInitializer(Token.Identifier("B", SourceSpan.Default), VariableAccessor("b")),
                ]))),
            ("MyType::CallMethod", new StaticMemberAccessExpression(new StaticMemberAccess(new TypeIdentifier(Token.Identifier("MyType", SourceSpan.Default), []), Token.Identifier("CallMethod", SourceSpan.Default)))),
            ("MyType::StaticField.InstanceField", new MemberAccessExpression(
                new MemberAccess( 
                    new StaticMemberAccessExpression(new StaticMemberAccess(new TypeIdentifier(Token.Identifier("MyType", SourceSpan.Default), []), Token.Identifier("StaticField", SourceSpan.Default))),
                    Token.Identifier("InstanceField", SourceSpan.Default)
                ))),
            ("string::CallMethod", new StaticMemberAccessExpression(new StaticMemberAccess(new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), []), Token.Identifier("CallMethod", SourceSpan.Default)))),
            ("result::<string>::CallMethod", new StaticMemberAccessExpression(new StaticMemberAccess( 
                new TypeIdentifier(Token.Result(SourceSpan.Default), [new TypeIdentifier(Token.StringKeyword(SourceSpan.Default), [])]),
                Token.Identifier("CallMethod", SourceSpan.Default)))),
            // ____binding strength tests
            // __greater than
            ( // greater than
                "a > b > c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.GreaterThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.RightAngleBracket(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a > b < c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.GreaterThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.RightAngleBracket(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a > b * c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.Star(SourceSpan.Default))),
                        Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // divide
                "a > b / c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.ForwardSlash(SourceSpan.Default))),
                        Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // plus
                "a > b + c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.Plus(SourceSpan.Default))),
                        Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // minus
                "a > b - c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.Dash(SourceSpan.Default))),
                        Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // fallOut
                "a > b?",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
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
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), []),
                        Token.Identifier("c", SourceSpan.Default))),
                    Token.RightAngleBracket(SourceSpan.Default)))
            ),
            // __Less than
            ( // greater than
                "a < b > c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.LeftAngleBracket(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a < b < c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.LeftAngleBracket(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a < b * c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.Star(SourceSpan.Default))),
                        Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // divide
                "a < b / c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.ForwardSlash(SourceSpan.Default))),
                        Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // plus
                "a < b + c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.Plus(SourceSpan.Default))),
                        Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // minus
                "a < b - c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.Dash(SourceSpan.Default))),
                        Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // fallOut
                "a < b?",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
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
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), []),
                        Token.Identifier("c", SourceSpan.Default)
                        )),
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
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.Star(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "a * b / c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.Star(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // plus
                "a * b + c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.Star(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a * b - c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.Star(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
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
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), []),
                        Token.Identifier("c", SourceSpan.Default)
                        )),
                    Token.Star(SourceSpan.Default)))
            ),
            // __divide
            ( // greater than
                "a / b > c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.ForwardSlash(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a / b < c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.ForwardSlash(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a / b * c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.ForwardSlash(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.Star(SourceSpan.Default)))
            ),
            ( // divide
                "a / b / c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.ForwardSlash(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.ForwardSlash(SourceSpan.Default)))
            ),
            ( // plus
                "a / b + c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.ForwardSlash(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a / b - c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.ForwardSlash(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a / b?",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
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
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), []),
                        Token.Identifier("c", SourceSpan.Default)
                        )),
                    Token.ForwardSlash(SourceSpan.Default)))
            ),
            // __plus
            ( // greater than
                "a + b > c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.Plus(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a + b < c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.Plus(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a + b * c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.Star(SourceSpan.Default))),
                        Token.Plus(SourceSpan.Default)))
            ),
            ( // divide
                "a + b / c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.ForwardSlash(SourceSpan.Default))),
                        Token.Plus(SourceSpan.Default)))
            ),
            ( // plus
                "a + b + c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.Plus(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a + b - c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.Plus(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a + b?",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
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
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), []),
                        Token.Identifier("c", SourceSpan.Default)
                        )),
                    Token.Plus(SourceSpan.Default)))
            ),
            // __minus
            ( // greater than
                "a - b > c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.Dash(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.RightAngleBracket(SourceSpan.Default)))
            ),
            ( // less than
                "a - b < c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.Dash(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.LeftAngleBracket(SourceSpan.Default)))
            ),
            ( // multiply
                "a - b * c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.Star(SourceSpan.Default))),
                        Token.Dash(SourceSpan.Default)))
            ),
            ( // divide
                "a - b / c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.ForwardSlash(SourceSpan.Default))),
                        Token.Dash(SourceSpan.Default)))
            ),
            ( // plus
                "a - b + c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.Dash(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.Plus(SourceSpan.Default)))
            ),
            ( // minus
                "a - b - c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            Token.Dash(SourceSpan.Default))),
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                        Token.Dash(SourceSpan.Default)))
            ),
            ( // fallOut
                "a - b?",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
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
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), []),
                        Token.Identifier("c", SourceSpan.Default)
                        )),
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
            (// plus
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
            // __ value assignment
            ( // greater than
                "a = b > c",
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.GreaterThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.RightAngleBracket(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // less than
                "a = b < c",
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.LeftAngleBracket(SourceSpan.Default)))
                        , Token.Equals(SourceSpan.Default)))
            ),
            ( // multiply
                "a = b * c",
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.Star(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // divide
                "a = b / c",
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.ForwardSlash(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // plus
                "a = b + c",
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.Plus(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // minus
                "a = b - c",
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.Dash(SourceSpan.Default))), Token.Equals(SourceSpan.Default)))
            ),
            ( // fallOut
                "a = b?",
                    new BinaryOperatorExpression(new BinaryOperator(BinaryOperatorType.ValueAssignment, 
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
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
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), []),
                        Token.Identifier("c", SourceSpan.Default)
                        )),
                    Token.Equals(SourceSpan.Default)))
             ),
            // __ equality check
            ( // greater than
                "a == b > c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.GreaterThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.RightAngleBracket(SourceSpan.Default))),
                        Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // less than
                "a == b < c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.LeftAngleBracket(SourceSpan.Default))),
                        Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // multiply
                "a == b * c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.Star(SourceSpan.Default))),
                        Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // divide
                "a == b / c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.ForwardSlash(SourceSpan.Default))),
                            Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // plus
                "a == b + c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.Plus(SourceSpan.Default))),
                        Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // minus
                "a == b - c",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new BinaryOperatorExpression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", SourceSpan.Default))),
                            Token.Dash(SourceSpan.Default))),
                        Token.DoubleEquals(SourceSpan.Default)))
            ),
            ( // fallOut
                "a == b?",
                    new BinaryOperatorExpression(new BinaryOperator(
                        BinaryOperatorType.EqualityCheck,
                        new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", SourceSpan.Default))),
                        new UnaryOperatorExpression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", SourceSpan.Default))),
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
                        new TypeIdentifier(Token.Identifier("b", SourceSpan.Default), []),
                        Token.Identifier("c", SourceSpan.Default)
                        )),
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
                             new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), []),
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
                             new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), []),
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
                             new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), []),
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
                             new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), []),
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
                             new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), []),
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
                             new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), []),
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
                             new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), []),
                             Token.Identifier("b", SourceSpan.Default)
                             )),
                         Token.QuestionMark(SourceSpan.Default)))
             ),
             ( // value assignment
                 "a::b = c",
                 new BinaryOperatorExpression(new BinaryOperator(
                         BinaryOperatorType.ValueAssignment,
                         new StaticMemberAccessExpression(new StaticMemberAccess(
                             new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), []),
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
                             new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), []),
                             Token.Identifier("b", SourceSpan.Default)
                             )),
                         VariableAccessor("c"),
                         Token.DoubleEquals(SourceSpan.Default)))
              ),
             ( // member access
                 "a::b.c",
                 new MemberAccessExpression(new MemberAccess(
                         new StaticMemberAccessExpression(new StaticMemberAccess(
                             new TypeIdentifier(Token.Identifier("a", SourceSpan.Default), []),
                             Token.Identifier("b", SourceSpan.Default)
                             )),
                         Token.Identifier("c", SourceSpan.Default)))
             )
        }.Select(x => new object[] { x.Source, Tokenizer.Tokenize(x.Source), x.ExpectedExpression });
    }
    
    private static ValueAccessorExpression VariableAccessor(string name) =>
        new (new ValueAccessor(ValueAccessType.Variable, Token.Identifier(name, SourceSpan.Default)));

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
            MemberAccessExpression memberAccessExpression => new MemberAccessExpression(RemoveSourceSpan(memberAccessExpression.MemberAccess)),
            StaticMemberAccessExpression staticMemberAccessExpression => new StaticMemberAccessExpression(RemoveSourceSpan(staticMemberAccessExpression.StaticMemberAccess)),
            GenericInstantiationExpression genericInstantiationExpression => new GenericInstantiationExpression(RemoveSourceSpan(genericInstantiationExpression.GenericInstantiation)),
            _ => throw new NotImplementedException()
        };
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
        return valueAccessor with { Token = RemoveSourceSpan(valueAccessor.Token)};
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