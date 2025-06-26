namespace NewLang.Core.Tests.ParserTests.TestCases;

public static class FailTestCases
{
    public static IEnumerable<object[]> TestCases()
    {
        IEnumerable<string> strings =
        [
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
            // body has tail expression but else doesn't
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
            "class MyClass { union MyUnion {}}",
            "{union MyUnion{}}",
            "fn MyFn(){union MyUnion{}}",
            "fn SomeFn() { class NoClassesInFunctions {}}",
            "new",
            "new Thing",
            "new Thing {",
            "new Thing{ a }",
            "new Thing{ a = }",
            "new Thing{ a = 1 b = 2 }",
            "new Thing { , }",
            "new Thing { , a = 1 }",
            "union MyUnion",
            "union MyUnion {",
            "union MyUnion { A(}",
            "union MyUnion { A {}",
            "union MyUnion { A { field}}",
            "union MyUnion { A B }",
            "union MyUnion< {}",
            "a matches",
            "a matches B {",
            "a matches B { field }",
            "a matches B { SomeField: }",
            "a matches B { SomeField OtherField }",
            "a matches B { SomeField: var field }",
            "a matches B(",
            "a matches B(,)",
            "a matches B{_, _}",
            "(",
            "(a",
            "(a b)",
        ];
        return strings.Select(x => new object[] { x, Tokenizer.Tokenize(x) });
    }
}