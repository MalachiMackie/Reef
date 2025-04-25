using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
        Expression expectedExpression)
    {
        var result = Parser.PopExpression(tokens);
        result.Should().NotBeNull();
        
        // clear out the source spans, we don't actually care about them
        var expression = RemoveSourceSpan(result.Value);

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
        Expression expectedProgram)
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
                new Expression(new VariableDeclaration(Token.Identifier("a", default), null, null, new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))))),
                new Expression(new VariableDeclaration(Token.Identifier("b", default), null, null, new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default))))),
                ], [], [])),
            ("fn MyFn() {}", new LangProgram([], [
                new LangFunction(null, Token.Identifier("MyFn", default), [], [], null, new Block([], []))
            ], [])),
            ("fn MyFn(): string {}", new LangProgram([], [
                new LangFunction(null, Token.Identifier("MyFn", default), [], [], new TypeIdentifier(Token.StringKeyword(default), []), new Block([], []))
            ], [])),
            ("fn MyFn(): result::<int, MyErrorType> {}", new LangProgram([], [
                new LangFunction(
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
                    Token.Identifier("MyFn", default),
                    [],
                    [],
                    null,
                    new Block([new Expression(new VariableDeclaration(
                        Token.Identifier("a", default),
                        null,
                        null,
                        new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))))], [])
                )
            ], [])),
            ("fn MyFn(int a) {}", new LangProgram([], [
                new LangFunction(
                    null,
                    Token.Identifier("MyFn", default),
                    [],
                    [new FunctionParameter(new TypeIdentifier(Token.IntKeyword(default), []), Token.Identifier("a", default))],
                    null,
                    new Block([], [])
                )
            ], [])),
            ("fn MyFn<T1>() {}", new LangProgram([], [new LangFunction(
                    null,
                    Token.Identifier("MyFn", default),
                    [Token.Identifier("T1", default)],
                    [],
                    null,
                    new Block([], [])
                )], [])),
            ("fn MyFn<T1, T2>() {}", new LangProgram([], [new LangFunction(
                    null,
                    Token.Identifier("MyFn", default),
                    [Token.Identifier("T1", default), Token.Identifier("T2", default)],
                    [],
                    null,
                    new Block([], [])
                )], [])),
            ("fn MyFn<T1, T2, T3>() {}", new LangProgram([], [new LangFunction(
                    null,
                    Token.Identifier("MyFn", default),
                    [Token.Identifier("T1", default), Token.Identifier("T2", default), Token.Identifier("T3", default)],
                    [],
                    null,
                    new Block([], [])
                )], [])),
            ("fn MyFn(result::<int, MyType> a) {}", new LangProgram([], [
                new LangFunction(
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
            ("fn MyFn(int a, MyType b) {}", new LangProgram([], [
                new LangFunction(
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
                    Token.Identifier("MyFn", default),
                    [],
                    [],
                    new TypeIdentifier(Token.IntKeyword(default), []),
                    new Block([new Expression(new MethodReturn(new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))))], [])
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
                    Token.Identifier("MyField", default),
                    new TypeIdentifier(Token.StringKeyword(default), []))])])),
            ("class MyClass {pub mut field MyField: string; pub fn MyFn() {}}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", default),
                [],
                [new LangFunction(new AccessModifier(Token.Pub(default)), Token.Identifier("MyFn", default), [], [], null, new Block([], []))],
                [new ClassField(
                    new AccessModifier(Token.Pub(default)),
                    new MutabilityModifier(Token.Mut(default)),
                    Token.Identifier("MyField", default),
                    new TypeIdentifier(Token.StringKeyword(default), []))])])),
            ("class MyClass {field MyField: string; fn MyFn() {}}", new LangProgram([], [], [new ProgramClass(
                null,
                Token.Identifier("MyClass", default),
                [],
                [new LangFunction(null, Token.Identifier("MyFn", default), [], [], null, new Block([], []))],
                [new ClassField(
                    null,
                    null,
                    Token.Identifier("MyField", default),
                    new TypeIdentifier(Token.StringKeyword(default), []))])])),
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
            // expression after if else expression
            "if (a) {} else {} var a = 1",
            "{} var a = 1",
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
            "var a = 2",
            "var a = 2; var b = 2",
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
            "fn MyFunction(result<int> a) {}",
            "fn MyFunction(result::<,> a) {}",
            "fn MyFunction(result::<int,> a) {}",
            "fn MyFunction(result::<> a) {}",
            "fn MyFunction(result::<int int> a) {}",
            "fn MyFunction(int a, ) {}",
            "fn MyFunction(,) {}",
            "fn MyFunction(int a int b) {}",
            // no semicolon
            "return 1",
            "pub MyClass {}",
            "class MyClass<> {}",
            "class MyClass<,> {}",
            "class MyClass<T1,> {}",
            "class MyClass<T1 T2> {}",
            "pub mut class MyClass {}",
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
        return new (string Source, Expression ExpectedProgram)[]
        {
            ("a::<string>()", new Expression(new MethodCall(VariableAccessor("a"), [new TypeIdentifier(Token.StringKeyword(default), [])], []))),
        }.Select(x => new object[] { x.Source, new Tokenizer().Tokenize(x.Source), x.ExpectedProgram });
    }

    public static IEnumerable<object[]> PopExpressionTestCases()
    {
        return new (string Source, Expression ExpectedExpression)[]
        {
            // value access expressions
            ("a", new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default)))),
            ("1", new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))),
            ("\"my string\"", new Expression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("my string", default)))),
            ("true", new Expression(new ValueAccessor(ValueAccessType.Literal, Token.True(default)))),
            ("false", new Expression(new ValueAccessor(ValueAccessType.Literal, Token.False(default)))),
            // postfix unary operator
            ("a?", new Expression(new UnaryOperator(
                UnaryOperatorType.FallOut,
                new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                Token.QuestionMark(default)))),
            ("a??",
                new Expression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new Expression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        Token.QuestionMark(default))),
                    Token.QuestionMark(default)))
            ),
            ("return 1", new Expression(
                new MethodReturn(new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))))),
            // binary operator expressions
            ("a < 5", new Expression(new BinaryOperator(
                BinaryOperatorType.LessThan,
                new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(5, default))),
                Token.LeftAngleBracket(default)))),
            ("\"thing\" > true", new Expression(new BinaryOperator(
                BinaryOperatorType.GreaterThan,
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("thing", default))),
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.True(default))),
                Token.RightAngleBracket(default)))),
            ("a + b", new Expression(new BinaryOperator(
                BinaryOperatorType.Plus,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.Plus(default)))),
            ("a - b", new Expression(new BinaryOperator(
                BinaryOperatorType.Minus,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.Dash(default)))),
            ("a * b", new Expression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.Star(default)))),
            ("a / b", new Expression(new BinaryOperator(
                BinaryOperatorType.Divide,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.ForwardSlash(default)))),
            ("var a: int = b", new Expression(
                new VariableDeclaration(
                    Token.Identifier("a", default),
                    null,
                    new TypeIdentifier(Token.IntKeyword(default), []),
                    VariableAccessor("b")))),
            ("var a: int", new Expression(
                new VariableDeclaration(
                    Token.Identifier("a", default),
                    null,
                    new TypeIdentifier(Token.IntKeyword(default), []),
                    null))),
            ("var mut a = 2", new Expression(new VariableDeclaration(
                Token.Identifier("a", default),
                new MutabilityModifier(Token.Mut(default)),
                null,
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))))),
            ("a = b", new Expression(new ValueAssignment(VariableAccessor("a"), VariableAccessor("b")))),
            ("var mut a: int = 2", new Expression(new VariableDeclaration(
                Token.Identifier("a", default),
                new MutabilityModifier(Token.Mut(default)),
                new TypeIdentifier(Token.IntKeyword(default), []),
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))))),
            ("var a = b", new Expression(new VariableDeclaration(Token.Identifier("a", default), null, null, VariableAccessor("b")))),
            ("var a = 1", new Expression(new VariableDeclaration(
                Token.Identifier("a", default),
                null,
                null,
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))))),
            ("var a = true", new Expression(new VariableDeclaration(
                Token.Identifier("a", default),
                null,
                null,
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.True(default)))))),
            ("var a = \"thing\"", new Expression(new VariableDeclaration(
                Token.Identifier("a", default),
                null,
                null,
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("thing", default)))))),
            ("{}", new Expression(Block.Empty)),
            ("{var a = 1;}", new Expression(new Block([
                new Expression(new VariableDeclaration(Token.Identifier("a", default), null, null, new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))))),
                ], []))),
            // tail expression
            ("{var a = 1}", new Expression(new Block(
                [
                new Expression(new VariableDeclaration(Token.Identifier("a", default), null, null, new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))))], []))),
            // tail expression
            ("{var a = 1;var b = 2}", new Expression(new Block(
                [new Expression(new VariableDeclaration(Token.Identifier("a", default), null, null, new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))))),
                new Expression(new VariableDeclaration(Token.Identifier("b", default), null, null, new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))))], []))),
            ("{var a = 1; var b = 2;}", new Expression(new Block([
                new Expression(new VariableDeclaration(Token.Identifier("a", default), null, null, new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))))),
                new Expression(new VariableDeclaration(Token.Identifier("b", default), null, null, new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default))))),
                ], []))),
            ("if (a) var c = 2;", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(new VariableDeclaration(
                    Token.Identifier("c", default),
                    null,
                    null,
                    new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default))))), [], null))),
            ("if (a > b) {var c = \"value\";}", new Expression(new IfExpression(
                new Expression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    VariableAccessor("a"),
                    VariableAccessor("b"),
                    Token.RightAngleBracket(default))),
                new Expression(new Block([
                    new Expression(new VariableDeclaration(
                        Token.Identifier("c", default),
                        null,
                        null,
                        new Expression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("value", default)))))
                ], [])), [], null))),
            ("if (a) {} else {var b = 2;}", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(Block.Empty),
                [],
                new Expression(new Block([
                    new Expression(new VariableDeclaration(
                        Token.Identifier("b", default),
                        null,
                        null,
                        new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))))
                ], []))))),
            ("if (a) {} else if (b) {}", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(Block.Empty),
                [new ElseIf(VariableAccessor("b"), new Expression(Block.Empty))],
                null))),
            ("if (a) {} else if (b) {} else {}", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(Block.Empty),
                [
                    new ElseIf(VariableAccessor("b"), new Expression(Block.Empty)),
                ],
                new Expression(Block.Empty)))),
            ("if (a) {} else if (b) {} else if (c) {} else {}", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(Block.Empty),
                [
                    new ElseIf(VariableAccessor("b"), new Expression(Block.Empty)),
                    new ElseIf(VariableAccessor("c"), new Expression(Block.Empty)),
                ],
                new Expression(Block.Empty)))),
            ("if (a) {b} else {c}", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(new Block([VariableAccessor("b")], [])),
                [],
                new Expression(new Block([VariableAccessor("c")], []))))),
            ("if (a) b else c", new Expression(new IfExpression(
                VariableAccessor("a"),
                VariableAccessor("b"),
                [],
                VariableAccessor("c")))),
            ("if (a) {if (b) {1} else {2}} else {3}", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(new Block([new Expression(new IfExpression(
                    VariableAccessor("b"),
                    new Expression(new Block([new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))], [])),
                    [],
                    new Expression(new Block([new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))], []))))], [])),
                [],
                new Expression(new Block([new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(3, default)))], []))))),
            ("if (a) if (b) 1 else 2 else 3", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(new IfExpression(
                    VariableAccessor("b"),
                    new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))),
                    [],
                    new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default))))),
                [],
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(3, default)))))),
            ("var a = if (b) 1 else 2;", new Expression(new VariableDeclaration(
                Token.Identifier("a", default),
                null,
                null,
                new Expression(new IfExpression(
                    VariableAccessor("b"),
                    new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))),
                    [],
                    new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))))))),
            ("var a = if (b) {1} else {2};", new Expression(new VariableDeclaration(
                Token.Identifier("a", default),
                null,
                null,
                new Expression(new IfExpression(
                    VariableAccessor("b"),
                    new Expression(new Block([new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))], [])),
                    [],
                    new Expression(new Block([new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))], []))))))),
            ("a()", new Expression(new MethodCall(VariableAccessor("a"), [], []))),
            ("a::<string>()", new Expression(new MethodCall(VariableAccessor("a"), [new TypeIdentifier(Token.StringKeyword(default), [])], []))),
            ("a::<string, int>()", new Expression(new MethodCall(
                VariableAccessor("a"),
                [new TypeIdentifier(Token.StringKeyword(default), []), new TypeIdentifier(Token.IntKeyword(default), [])],
                []))),
            ("a::<string, int, result::<int>>()", new Expression(new MethodCall(
                VariableAccessor("a"),
                [
                    new TypeIdentifier(Token.StringKeyword(default), []),
                    new TypeIdentifier(Token.IntKeyword(default), []),
                    new TypeIdentifier(Token.Result(default), [new TypeIdentifier(Token.IntKeyword(default), [])]),
                ],
                []))),
            ("a(b)", new Expression(new MethodCall(VariableAccessor("a"), [], [
            VariableAccessor("b")]))),
            ("a(b, c)", new Expression(new MethodCall(VariableAccessor("a"), [], [
                VariableAccessor("b"), VariableAccessor("c")
            ]))),
            ("a(b, c > d, e)", new Expression(new MethodCall(VariableAccessor("a"), [], [
                VariableAccessor("b"),
                new Expression(new BinaryOperator(BinaryOperatorType.GreaterThan, VariableAccessor("c"), VariableAccessor("d"), Token.RightAngleBracket(default))),
                VariableAccessor("e")
            ]))),
            ("a.b", new Expression(new MemberAccess(VariableAccessor("a"), Token.Identifier("b", default)))),
            ("a.b()", new Expression(new MethodCall(new Expression(new MemberAccess(VariableAccessor("a"), Token.Identifier("b", default))), [], []))),
            ("a?.b", new Expression(new MemberAccess(
                new Expression(new UnaryOperator(UnaryOperatorType.FallOut, VariableAccessor("a"), Token.QuestionMark(default))),
                Token.Identifier("b", default)))),
            ("a.b?", new Expression(new UnaryOperator(
                UnaryOperatorType.FallOut,
                new Expression(new MemberAccess(VariableAccessor("a"), Token.Identifier("b", default))),
                Token.QuestionMark(default)))),
            ("a * b.c", new Expression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                VariableAccessor("a"),
                new Expression(new MemberAccess(VariableAccessor("b"), Token.Identifier("c", default))),
                Token.Star(default)))),
            ("b.c * a", new Expression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                new Expression(new MemberAccess(VariableAccessor("b"), Token.Identifier("c", default))),
                VariableAccessor("a"),
                Token.Star(default)))),
            ("new Thing {}", new Expression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("Thing", default), []),
                []))),
            ("new Thing {A = a}", new Expression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("Thing", default), []),
                [new FieldInitializer(Token.Identifier("A", default), VariableAccessor("a"))]))),
            // todo: trailing commas everwhere
            //("new Thing {A = a,}", new Expression(new ObjectInitializer(
            //    new TypeIdentifier(Token.Identifier("Thing", default), []),
            //    [new FieldInitializer(Token.Identifier("A", default), VariableAccessor("a"))]))),
            ("new Thing {A = a, B = b}", new Expression(new ObjectInitializer(
                new TypeIdentifier(Token.Identifier("Thing", default), []),
                [
                    new FieldInitializer(Token.Identifier("A", default), VariableAccessor("a")),
                    new FieldInitializer(Token.Identifier("B", default), VariableAccessor("b")),
                ]))),
            ("MyType::CallMethod", new Expression(new StaticMemberAccess(new TypeIdentifier(Token.Identifier("MyType", default), []), Token.Identifier("CallMethod", default)))),
            ("MyType::StaticField.InstanceField", new Expression(
                new MemberAccess(
                    new Expression(new StaticMemberAccess(new TypeIdentifier(Token.Identifier("MyType", default), []), Token.Identifier("CallMethod", default))),
                    Token.Identifier("InstanceField", default)))),
            ("string::CallMethod", new Expression(new StaticMemberAccess(new TypeIdentifier(Token.StringKeyword(default), []), Token.Identifier("CallMethod", default)))),
            ("result<string>::CallMethod", new Expression(new StaticMemberAccess(
                new TypeIdentifier(Token.Result(default), [new TypeIdentifier(Token.StringKeyword(default), [])]), Token.Identifier("CallMethod", default)))),
            // ____binding strength tests
            // __greater than
            ( // greater than
                "a > b > c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.GreaterThan,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.RightAngleBracket(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a > b < c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.GreaterThan,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.RightAngleBracket(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a > b * c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // divide
                "a > b / c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // plus
                "a > b + c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Plus(default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // minus
                "a > b - c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Dash(default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // fallOut
                "a > b?",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.RightAngleBracket(default)))
            ),
            // __Less than
            ( // greater than
                "a < b > c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.LeftAngleBracket(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a < b < c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.LeftAngleBracket(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a < b * c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // divide
                "a < b / c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // plus
                "a < b + c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Plus(default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // minus
                "a < b - c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Dash(default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // fallOut
                "a < b?",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.LeftAngleBracket(default)))
            ),
            ("a < b = c",
                new Expression(new ValueAssignment(
                    new Expression(
                        new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.LeftAngleBracket(default))),
                        VariableAccessor("c")))),
            // __multiply
            ( // greater than
                "a * b > c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Star(default))),
                        VariableAccessor("c"),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a * b < c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Star(default))),
                        VariableAccessor("c"),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a * b * c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Star(default)))
            ),
            ( // divide
                "a * b / c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.ForwardSlash(default)))
            ),
            ( // plus
                "a * b + c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
            ),
            ( // minus
                "a * b - c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
            ),
            ( // fallOut
                "a * b?",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        VariableAccessor("a"),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("b"),
                            Token.QuestionMark(default))),
                        Token.Star(default)))
            ),
            ("a * b = c",
                new Expression(new ValueAssignment(
                    new Expression(
                        new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Star(default))),
                        VariableAccessor("c")))),
            // __divide
            ( // greater than
                "a / b > c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a / b < c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a / b * c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Star(default)))
            ),
            ( // divide
                "a / b / c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.ForwardSlash(default)))
            ),
            ( // plus
                "a / b + c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
            ),
            ( // minus
                "a / b - c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
            ),
            ( // fallOut
                "a / b?",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.ForwardSlash(default)))
            ),
            ("a / b = c",
                new Expression(new ValueAssignment(
                    new Expression(
                        new BinaryOperator(
                            BinaryOperatorType.Divide,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.ForwardSlash(default))),
                        VariableAccessor("c")))),
            // __plus
            ( // greater than
                "a + b > c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a + b < c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a + b * c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.Plus(default)))
            ),
            ( // divide
                "a + b / c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.Plus(default)))
            ),
            ( // plus
                "a + b + c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
            ),
            ( // minus
                "a + b - c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
            ),
            ( // fallOut
                "a + b?",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.Plus(default)))
            ),
            ("a + b = c",
                new Expression(new ValueAssignment(
                    new Expression(
                        new BinaryOperator(
                            BinaryOperatorType.Plus,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Plus(default))),
                        VariableAccessor("c")))),
            // __minus
            ( // greater than
                "a - b > c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a - b < c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a - b * c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.Dash(default)))
            ),
            ( // divide
                "a - b / c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.Dash(default)))
            ),
            ( // plus
                "a - b + c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
            ),
            ( // minus
                "a - b - c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
            ),
            ( // fallOut
                "a - b?",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.Dash(default)))
            ),
            ("a - b = c",
                new Expression(new ValueAssignment(
                    new Expression(
                        new BinaryOperator(
                            BinaryOperatorType.Minus,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Dash(default))),
                        VariableAccessor("c")))),
            // __FallOut
            ( // fallout
                "a??",
                    new Expression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new Expression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(default))),
                    Token.QuestionMark(default)))
                ),
            ( // less than
                "a? < b",
                    new Expression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new Expression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(default))),
                    VariableAccessor("b"),
                    Token.LeftAngleBracket(default)))
            ),
            ( // greater than
                "a? > b",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.RightAngleBracket(default)))
            ),
            (// plus
                "a? + b",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.Plus(default)))
            ),
            ( // minus
                "a? - b",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.Dash(default)))
            ),
            ( // multiply
                "a? * b",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.Star(default)))
            ),
            ( // divide
                "a? / b",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.ForwardSlash(default)))
            ),
            ( // assignment
                "a? = c",
                new Expression(new ValueAssignment(
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("c")))),
            // __ value assignment
            ( // greater than
                "a = b > c",
                    new Expression(new ValueAssignment(
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.GreaterThan,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.RightAngleBracket(default)))))
            ),
            ( // less than
                "a = b < c",
                    new Expression(new ValueAssignment(
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.LeftAngleBracket(default)))
                        ))
            ),
            ( // multiply
                "a = b * c",
                    new Expression(new ValueAssignment(
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default)))))
            ),
            ( // divide
                "a = b / c",
                    new Expression(new ValueAssignment(
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default)))))
            ),
            ( // plus
                "a = b + c",
                    new Expression(new ValueAssignment(
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Plus(default)))))
            ),
            ( // minus
                "a = b - c",
                    new Expression(new ValueAssignment(
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Dash(default)))))
            ),
            ( // fallOut
                "a = b?",
                    new Expression(new ValueAssignment(
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default)))))
            ),
            ("a = b = c",
                new Expression(new ValueAssignment(
                    new Expression(
                        new ValueAssignment(
                            VariableAccessor("a"),
                            VariableAccessor("b"))),
                        VariableAccessor("c")))),
        }.Select(x => new object[] { x.Source, new Tokenizer().Tokenize(x.Source), x.ExpectedExpression });
    }
    
    private static Expression VariableAccessor(string name) =>
        new (new ValueAccessor(ValueAccessType.Variable, Token.Identifier(name, default)));

    private static LangFunction RemoveSourceSpan(LangFunction function)
    {
        return new LangFunction(
            RemoveSourceSpan(function.AccessModifier),
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
            [..block.Expressions.Select(RemoveSourceSpan)],
            [..block.Functions.Select(RemoveSourceSpan)]);
    }

    private static AccessModifier? RemoveSourceSpan(AccessModifier? accessModifier)
    {
        return accessModifier is null
            ? null
            : new AccessModifier(RemoveSourceSpan(accessModifier.Value.Token));
    }

    private static TypeIdentifier? RemoveSourceSpan(TypeIdentifier? typeIdentifier)
    {
        if (typeIdentifier is null)
        {
            return null;
        }

        return RemoveSourceSpan(typeIdentifier.Value);
    }

    private static TypeIdentifier RemoveSourceSpan(TypeIdentifier typeIdentifier)
    {
        return new TypeIdentifier(RemoveSourceSpan(typeIdentifier.Identifier), [..typeIdentifier.TypeArguments.Select(RemoveSourceSpan)]);
    }

    private static FunctionParameter RemoveSourceSpan(FunctionParameter parameter)
    {
        return new FunctionParameter(
            RemoveSourceSpan(parameter.Type),
            RemoveSourceSpan(parameter.Identifier));
    }

    private static Expression? RemoveSourceSpan(Expression? expression)
    {
        if (expression is null)
        {
            return null;
        }

        return RemoveSourceSpan(expression.Value);
    }
    
    private static Expression RemoveSourceSpan(Expression expression)
    {
        return new Expression(
            expression.ExpressionType,
            RemoveSourceSpan(expression.ValueAccessor),
            RemoveSourceSpan(expression.UnaryOperator),
            RemoveSourceSpan(expression.BinaryOperator),
            RemoveSourceSpan(expression.VariableDeclaration),
            RemoveSourceSpan(expression.IfExpression),
            RemoveSourceSpan(expression.Block),
            RemoveSourceSpan(expression.MethodCall),
            RemoveSourceSpan(expression.MemberAccess),
            RemoveSourceSpan(expression.MethodReturn),
            RemoveSourceSpan(expression.ObjectInitializer),
            RemoveSourceSpan(expression.ValueAssignment),
            RemoveSourceSpan(expression.StaticMemberAccess));
    }

    private static StrongBox<StaticMemberAccess>? RemoveSourceSpan(StrongBox<StaticMemberAccess>? staticMemberAccess)
    {
        return staticMemberAccess is null
            ? null
            : new StrongBox<StaticMemberAccess>(new StaticMemberAccess(
                RemoveSourceSpan(staticMemberAccess.Value.Type), RemoveSourceSpan(staticMemberAccess.Value.Identifier)));
    }

    private static StrongBox<ValueAssignment>? RemoveSourceSpan(StrongBox<ValueAssignment>? valueAssignment)
    {
        return valueAssignment is null
            ? null
            : new StrongBox<ValueAssignment>(new ValueAssignment(
                RemoveSourceSpan(valueAssignment.Value.Left), RemoveSourceSpan(valueAssignment.Value.Right)));
    }

    private static StrongBox<ObjectInitializer>? RemoveSourceSpan(StrongBox<ObjectInitializer>? objectInitializer)
    {
        return objectInitializer is null
            ? null
            : new StrongBox<ObjectInitializer>(new ObjectInitializer(
                RemoveSourceSpan(objectInitializer.Value.Type),
                [..objectInitializer.Value.FieldInitializers.Select(RemoveSourceSpan)]));
    }

    private static FieldInitializer RemoveSourceSpan(FieldInitializer fieldInitializer)
    {
        return new FieldInitializer(
            RemoveSourceSpan(fieldInitializer.FieldName),
            RemoveSourceSpan(fieldInitializer.Value));
    }

    private static StrongBox<MemberAccess>? RemoveSourceSpan(StrongBox<MemberAccess>? memberAccess)
    {
        return memberAccess is null
            ? null
            : new StrongBox<MemberAccess>(new MemberAccess(RemoveSourceSpan(memberAccess.Value.MemberOwner), RemoveSourceSpan(memberAccess.Value.Identifier)));
    }

    private static StrongBox<Block>? RemoveSourceSpan(StrongBox<Block>? block)
    {
        return block is null ? null : new StrongBox<Block>(new Block(
            [..block.Value.Expressions.Select(RemoveSourceSpan)],
            [..block.Value.Functions.Select(RemoveSourceSpan)]));
    }

    private static StrongBox<MethodCall>? RemoveSourceSpan(StrongBox<MethodCall>? methodCall)
    {
        return methodCall is null
            ? null
            : new StrongBox<MethodCall>(new MethodCall(
                RemoveSourceSpan(methodCall.Value.Method),
                [..methodCall.Value.TypeArguments.Select(RemoveSourceSpan)],
                [..methodCall.Value.ParameterList.Select(RemoveSourceSpan)]));
    }

    private static StrongBox<IfExpression>? RemoveSourceSpan(StrongBox<IfExpression>? ifExpression)
    {
        return ifExpression is null ? null : new StrongBox<IfExpression>(new IfExpression(
            CheckExpression: RemoveSourceSpan(ifExpression.Value.CheckExpression),
            Body: RemoveSourceSpan(ifExpression.Value.Body),
            ElseIfs: [..ifExpression.Value.ElseIfs.Select(RemoveSourceSpan)],
            ElseBody: RemoveSourceSpan(ifExpression.Value.ElseBody)));
    }

    private static ElseIf RemoveSourceSpan(ElseIf elseIf)
    {
        return new ElseIf(RemoveSourceSpan(elseIf.CheckExpression), RemoveSourceSpan(elseIf.Body));
    }
    
    private static StrongBox<VariableDeclaration>? RemoveSourceSpan(StrongBox<VariableDeclaration>? variableDeclaration)
    {
        return variableDeclaration is null ? null
            : new StrongBox<VariableDeclaration>(
                new VariableDeclaration(
                    VariableNameToken: RemoveSourceSpan(variableDeclaration.Value.VariableNameToken),
                    MutabilityModifier: RemoveSourceSpan(variableDeclaration.Value.MutabilityModifier),
                    Type: RemoveSourceSpan(variableDeclaration.Value.Type),
                    Value: RemoveSourceSpan(variableDeclaration.Value.Value)));
    }

    private static MutabilityModifier? RemoveSourceSpan(MutabilityModifier? MutabilityModifier)
    {
        return MutabilityModifier is null
            ? null
            : new MutabilityModifier(RemoveSourceSpan(MutabilityModifier.Value.Modifier));
    }

    private static StrongBox<BinaryOperator>? RemoveSourceSpan(StrongBox<BinaryOperator>? binaryOperator)
    {
        return binaryOperator is not null
            ? new StrongBox<BinaryOperator>(binaryOperator.Value with
            {
                Left = RemoveSourceSpan(binaryOperator.Value.Left),
                Right = RemoveSourceSpan(binaryOperator.Value.Right),
                OperatorToken = RemoveSourceSpan(binaryOperator.Value.OperatorToken)
            })
            : null;
    }

    private static StrongBox<UnaryOperator>? RemoveSourceSpan(StrongBox<UnaryOperator>? unaryOperator)
    {
        return unaryOperator is not null
            ? new StrongBox<UnaryOperator>(unaryOperator.Value with
            {
                OperatorToken = RemoveSourceSpan(unaryOperator.Value.OperatorToken),
                Operand = RemoveSourceSpan(unaryOperator.Value.Operand)
            })
            : null;
    }

    private static ValueAccessor? RemoveSourceSpan(ValueAccessor? valueAccessor)
    {
        return valueAccessor.HasValue
            ? valueAccessor.Value with { Token = RemoveSourceSpan(valueAccessor.Value.Token) }
            : null;
    }

    private static Token RemoveSourceSpan(Token token)
    {
        return token with { SourceSpan = default };
    }

    private static LangProgram RemoveSourceSpan(LangProgram program)
    {
        return new LangProgram(
            [..program.Expressions.Select(RemoveSourceSpan)],
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
            RemoveSourceSpan(field.MutabilityModifier),
            RemoveSourceSpan(field.Name),
            RemoveSourceSpan(field.Type));
    }

    private static StrongBox<MethodReturn>? RemoveSourceSpan(StrongBox<MethodReturn>? methodReturn)
    {
        return methodReturn is null
            ? null
            : new StrongBox<MethodReturn>(new MethodReturn(RemoveSourceSpan(methodReturn.Value.Expression)));
    }
}