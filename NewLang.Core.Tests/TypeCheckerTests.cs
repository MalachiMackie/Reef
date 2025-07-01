using FluentAssertions;
using NewLang.Core.Tests.ParserTests;

using static NewLang.Core.TypeChecker;
using static NewLang.Core.Tests.ParserTests.ExpressionHelpers;

namespace NewLang.Core.Tests;

public class TypeCheckerTests
{
    [Theory]
    [MemberData(nameof(SuccessfulExpressionTestCases))]
    public void Should_SuccessfullyTypeCheckExpressions(string source)
    {
        var program = Parser.Parse(Tokenizer.Tokenize(source));
        var errors = TypeChecker.TypeCheck(program.ParsedProgram);
        errors.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(FailedExpressionTestCases))]
    public void Should_FailTypeChecking_When_ExpressionsAreNotValid(string source,
        IReadOnlyList<TypeCheckerError> expectedErrors)
    {
        var program = Parser.Parse(Tokenizer.Tokenize(source));
        var errors = TypeChecker.TypeCheck(program.ParsedProgram).Select(RemoveSourceSpanHelpers.RemoveSourceSpan);

        errors.Should().NotBeEmpty().And.BeEquivalentTo(expectedErrors);
    }

    [Fact]
    public void SingleTest()
    {
        const string src =
                """
                class MyClass {
                    field MyField: string
                }

                // MyField is not accessible
                var a = new MyClass { MyField = "" };
                """;


        var program = Parser.Parse(Tokenizer.Tokenize(src));
        var act = () => TypeChecker.TypeCheck(program.ParsedProgram);

        act.Should().NotThrow<InvalidOperationException>();
    }

    public static TheoryData<string> SuccessfulExpressionTestCases()
    {
        return new TheoryData<string>
        {
            """
            var a = SomeFn;

            var b: string = a();
            var c: string = a();

            fn SomeFn<T>(): T {
                return todo!;
            }
            """,
            """
            fn OtherFn<T2>(param2: T2): T2 {
                return ThirdFn(param2);

                fn ThirdFn<T3>(param3: T3): T3 {
                    return param3;
                }
            }
            """,
            """
            fn SomeFn<T>(param: T) {
                fn OtherFn<T2>(param2: T2) {
                    fn ThirdFn<T3>(param3: T3) {
                        var a: T = param;
                        var b: T2 = param2;
                        var c: T3 = param3;
                    }
                }
            }
            """,
            """
            fn SomeFn<T>(): int {
                var a = OtherFn();
                return a;
            }

            fn OtherFn<T>(): T {
                return todo!;
            }
            """,
            """
            var a = SomeFn();
            var a2 = SomeFn();
            var b = a;
            var b2 = a2;

            var c = OtherFn(b);
            var c2 = OtherFn(b2);

            var d: string = c;
            var d2: int = c2;

            fn OtherFn<T>(param: T): T {
                return param;
            }

            fn SomeFn<T>(): T {
                return todo!;
            }
            """,
            "var a: string = todo!",
            """
            fn SomeFn() {
                return todo!;
            }
            """,
            """
            fn SomeFn(): string {
                return todo!;
            }
            """,
            """
            fn SomeFn<T>(param: T): T {
                return param;
            }

            var a = SomeFn("");
            var b = SomeFn(2);
            var c = b + b;
            """,
            """
            class MyClass<T> {
                fn SomeFn(param: T): result::<T, string> {
                    if (true) {
                        return ok(param);
                    }
                    return error("some error");
                }
            }
            """,
            "var a: result::<int, bool> = error(false);",
            "var a: result::<int, bool> = ok(1);",
            """
            var a = match ("") {
                var b => ok(1),
                _ => error("")
            };
            """,
            """
            fn SomeFn(): result::<int, bool> {
                if (true) {
                    return ok(1);
                }
                
                return error(false);
            }
            """,
            """
            fn SomeFn(param: result::<int, bool>) {
            }

            var a = ok(1);
            SomeFn(a);
            var b = error(false);
            SomeFn(b);
            """,
            """
            class MyClass {
                fn SomeFn() {
                    var a: MyClass = this;
                }
            }
            """,
            """
            union MyUnion {
                A,
                
                fn SomeFn() {
                    var a: MyUnion = this;
                }
            }
            """,
            """
            union MyUnion {
                A,
                B(int),
                C { field MyField: int }
            }
            var a = MyUnion::A;
            match (a) {
                MyUnion::A => 1,
                MyUnion::B(var b) => b,
                MyUnion::C { MyField } => MyField,
            }
            """,
            """
            union MyUnion {
                A,
                B(int),
                C { field MyField: int }
            }
            var a = MyUnion::A;
            match (a) {
                MyUnion::A => 1,
                MyUnion::B(var b) => b,
                _ => 2,
            }
            """,
            """
            union MyUnion {
                A,
                B(int),
                C { field MyField: int }
            }
            var a = MyUnion::A;
            var d: int = match (a) {
                MyUnion::A => 1,
                MyUnion::B(var b) => b,
                MyUnion::C { MyField } => MyField,
            };
            """,
            "var a: bool = !(true);",
            """
            var a = (1, true, "");
            var b: int = a.First;
            var c: bool = a.Second;
            var d: string = a.Third;
            """,
            "var a: int = (1 + 2) * 3;",
            "var a: bool = !true;",
            """
            var a = "";
            var b: bool = a matches string;
            """,
            """
            var a = 1;
            var b: bool = a matches int;
            """,
            """
            union MyUnion {A}
            var a = MyUnion::A;
            var b: bool = a matches MyUnion;
            """,
            """
            union MyUnion {A}
            var a = MyUnion::A;
            var b: bool = a matches _;
            """,
            """
            union MyUnion {A, B(string)}
            var a = MyUnion::B("");
            var b: bool = a matches MyUnion::B(_);
            """,
            """
            union MyUnion {A { field MyField: string }}
            var a = new MyUnion::A { MyField = "" };
            var b: bool = a matches MyUnion::A { MyField: _ };
            """,
            """
            union MyUnion {A { field MyField: string, field OtherField: bool }}
            var a = new MyUnion::A { MyField = "", OtherField = true };
            var b: bool = a matches MyUnion::A { MyField: _, _ };
            """,
            """
            class MyClass {pub field MyField: string}
            var a = new MyClass { MyField = "" };
            var b: bool = a matches MyClass { MyField: _ };
            """,
            """
            class MyClass {pub field MyField: string}
            var a = new MyClass { MyField = "" };
            var b: bool = a matches MyClass;
            """,
            ";;;;;;;;",
            """
            var a: string;
            a = "";
            """,
            """
            class MyClass {
                mut field MyField: string,
                
                fn MyFn() {
                    MyField = "";
                }
            }
            """,
            """
            class MyClass {
                static field MyField: string = "",
                
                // instance functions have access to static fields
                fn MyFn(): string {
                    return MyField;
                }
            }
            """,
            """
            class MyClass {
                field MyField: string,
                
                // instance functions have access to instance fields
                fn MyFn(): string {
                    return MyField;
                }
            }
            """,
            """
            class MyClass {
                pub mut field MyField: string
            }

            var mut a = new MyClass {
                MyField = ""
            };

            a.MyField = "";
            """,
            """
            class MyClass { pub mut field MyField: string }
            var mut a = new MyClass { MyField = "" };

            // a is not marked as mutable
            a.MyField = "";
            """,
            """
            var mut a = "";
            a = "";
            """,
            """
            class MyClass {
                pub static mut field MyField: string = ""
            };

            MyClass::MyField = "";
            """,
            """
            class MyClass {
                pub mut field MyField: string
            }
            fn MyFn(mut param: MyClass) { 
                param.MyField = "";
            }
            """,
            """
            fn MyFn(mut param: string) {
               param = ""; 
            }
            """,
            """
            fn MyFn(mut param: string) {
            }
            var mut a = "";

            MyFn(a);
            """,
            """
            union MyUnion {A}

            var a: MyUnion = MyUnion::A;
            """,
            """
            union MyUnion<T> {
                A {
                    field MyField: T,
                }
            }
            var a: MyUnion::<string> = new MyUnion::<string>::A {
                MyField = ""
            };
            var b: MyUnion::<int> = new MyUnion::<int>::A {
                MyField = 1
            };
            """,
            """
            union MyUnion {
                A {
                    field MyField: string,
                }
            }
            var a: MyUnion = new MyUnion::A {
                MyField = ""
            };
            """,
            "union MyUnion {}",
            """
            union MyUnion {
                A,
            }
            """,
            """
            union myUnion {
                A(string)
            }
            """,
            """
            class MyClass {}
            union MyUnion {
                A(MyClass, string)
            }
            """,
            """
            union MyUnion {
                A { 
                    field myField: string
                }
            }
            """,
            """
            union MyUnion {
                A
            }
            var a: MyUnion;
            """,
            """
            union MyUnion {
                A(string)
            }
            var a = MyUnion::A("");
            """,
            """
            fn MyFn(): result::<string, int> {
                var a: string = OtherFn()?;
            }

            fn OtherFn(): result::<string, int> {
                return result::<string, int>::Error(1);
            }
            """,
            """
            MyClass::StaticMethod();

            class MyClass {
                pub static fn StaticMethod() {}
            }
            """,
            """
            var a: string = MyClass::<string>::StaticMethod("");

            class MyClass<T> {
                pub static fn StaticMethod(param: T): T { return param; }
            }
            """,
            """
            var a: int = MyClass::<string>::StaticMethod::<int>("", 1);

            class MyClass<T> {
                pub static fn StaticMethod<T2>(param: T, param2: T2): T2 { return param2; }
            }
            """,
            "var a = 2",
            "var a: int = 2",
            "var b: string = \"somestring\"",
            "var a = 2; var b: int = a",
            "fn MyFn(): int { return 1; }",
            """
            fn MyFn<T>(param: T): T {return param;}
            var a: string = MyFn::<string>("");
            """,
            """
            class MyClass<T> {
                fn MyFn<T2>(param1: T, param2: T2) {
                }
            }

            var a = new MyClass::<int>{};
            a.MyFn::<string>(1, "");
            """,
            """
            fn MyFn(){}
            MyFn();
            """,
            "var a = 2;{var b = a;}",
            "fn Fn1(){Fn2();} fn Fn2(){}",
            "fn MyFn() {fn InnerFn() {OuterFn();}} fn OuterFn() {}",
            "fn MyFn() {fn InnerFn() {} InnerFn();}",
            "fn MyFn(param: int) {var a: int = param;}",
            "fn MyFn(param1: string, param2: int) {} MyFn(\"value\", 3);",
            "fn MyFn(param: result::<string, int>) {}",
            "fn Fn1<T1>(){} Fn1::<string>();",
            "fn Fn1<T1, T2>(){} Fn1::<string, int>();",
            """
            fn Fn1<T1>(param: T1): T1 { return param; }
            var a: string = Fn1::<string>("");
            var b: int = Fn1::<int>(1);
            """,
            "if (true) {}",
            "if (false) {}",
            "var a = true; if (a) {}",
            "if (true) {} else {}",
            "if (true) {var a = 2} else if (true) {var a = 3} else if (true) {var a = 4} else {var a = 5}",
            "if (true) var a = 2",
            "var a: result::<int, string>",
            """
            class Class1 { field someField: Class1,}
            class Class2 { }
            """,
            // binary operators
            // less than
            "var a: bool = 1 < 2;",
            // GreaterThan,
            "var a: bool = 2 > 2;",
            // Plus,
            "var a: int = 2 + 2;",
            // Minus,
            "var a: int = 2 - 2;",
            // Multiply,
            "var a: int = 2 * 2;",
            // Divide,
            "var a: int = 2 / 2;",
            // EqualityCheck,
            "var a: bool = 2 == 2;",
            // ValueAssignment,
            "var mut a = 2; a = 3;",
            // Object Initializers
            """
            class MyClass {pub field myField: int, pub field otherField: string,}
            var a = new MyClass { myField = 1, otherField = "" };
            """,
            "class MyClass {} var a: MyClass = new MyClass {};",
            """
            class MyClass {pub field someField: int,}
            var a = new MyClass { someField = 1 };
            var b: int = a.someField;
            """,
            """
            class MyClass { static field someField: int = 3, }
            var a: int = MyClass::someField;
            """,
            """
            class MyClass<T> {
                fn MyFn(param: T): T {
                    return param;
                }
            }

            var a = new MyClass::<string>{};

            var b = a.MyFn;

            var c = b("");
            """,
            """
            class MyClass<T> { static field someField: int = 1, }
            var a = MyClass::<string>::someField;
            """,
            """
            class MyClass<T> { pub field someField: T, }
            var a = new MyClass::<int> {someField = 1};
            """,
            """
            class MyClass<T> { pub field someField: T, }
            var a = new MyClass::<string> {someField = ""};
            var b: string = a.someField;
            """,
            """
            union MyUnion {
                A,
                
                fn SomeMethod() {
                }
            }
            """,
            """
            class MyClass { 
                field someField: string,
                
                pub static fn New(): MyClass {
                    return new MyClass {
                        someField = ""
                    };
                }
            }
            """,
            """
            class MyClass<T> {}
            class OtherClass<T> {}
            """,
            """
            class MyClass<T> {
                 fn MyFn<T2>() {
                 }
            }

            var a = new MyClass::<string>{};
            var b = a.MyFn::<int>();
            """,
            """
            fn OuterFn() {
               var a = new MyClass{}; 
               a.MyFn();
            }
            class MyClass {
                fn MyFn() {
                    OuterFn();
                }
            }
            """,
            """
            class MyClass {
                fn MyFn() {
                    var a = new MyClass{};
                }
            }
            """,
            """
            fn MyFn(): string {
                fn InnerFn(): int { return 1; }
                return "";
            }
            """,
            """
            fn MyFn(): result::<string, int> {
                if (true) {
                    return result::<string, int>::Ok("someValue");
                }
                
                return result::<string, int>::Error(1);
            }
            """,
            """
            union MyUnion { A }
            var a = MyUnion::A;
            if (a matches MyUnion::A var b) {
                var c: MyUnion = b;
            }
            """,
            """
            union MyUnion { A(string) }
            var a = MyUnion::A("hi");
            if (a matches MyUnion::A(_) var b) {
                var c: MyUnion = b;
            }
            """,
            """
            union MyUnion { A(string) }
            var a = MyUnion::A("hi");
            if (a matches MyUnion::A(string var b)) {
                var c: string = b;
            }
            """,
            """
            union MyUnion { A(string) }
            var a = MyUnion::A("hi");
            if (a matches MyUnion::A(string var b)) {
                var c: string = b;
            }
            """,
            """
            union MyUnion { A { field MyField: string } }
            var a = new MyUnion::A { MyField = "" };
            if (a matches MyUnion::A { MyField }) {
                var c: string = MyField;
            }
            """,
            """
            union MyUnion { A { field MyField: string } }
            var a = new MyUnion::A { MyField = "" };
            if (a matches MyUnion::A { MyField: var b }) {
                var c: string = b;
            }
            """,
            """
            union MyUnion { A }
            var a = MyUnion::A;
            if (a matches var b) {
                var c: MyUnion = b;
            }
            """,
            """
            union MyUnion { A }
            var a = MyUnion::A;
            if (a matches MyUnion var b) {
                var c: MyUnion = b;
            }
            """,
            """
            union MyUnion { A }
            union OtherUnion { B(MyUnion) }
            var a = OtherUnion::B(MyUnion::A);
            if (a matches OtherUnion::B(MyUnion::A var c) var b) {
                var d: OtherUnion = b;
                var e: MyUnion = c;
            }
            """,
            """
            union MyUnion { A }
            var a = MyUnion::A;
            if (false) {}
            else if (a matches MyUnion::A var b) {
                var c: MyUnion = b;
            }
            """,
            """
            union MyUnion { A(string) }
            var a = MyUnion::A("hi");
            if (false) {}
            else if (a matches MyUnion::A(_) var b) {
                var c: MyUnion = b;
            }
            """,
            """
            union MyUnion { A(string) }
            var a = MyUnion::A("hi");
            if (false) {}
            else if (a matches MyUnion::A(string var b)) {
                var c: string = b;
            }
            """,
            """
            union MyUnion { A(string) }
            var a = MyUnion::A("hi");
            if (false) {}
            else if (a matches MyUnion::A(string var b)) {
                var c: string = b;
            }
            """,
            """
            union MyUnion { A { field MyField: string } }
            var a = new MyUnion::A { MyField = "" };
            if (false) {}
            else if (a matches MyUnion::A { MyField }) {
                var c: string = MyField;
            }
            """,
            """
            union MyUnion { A { field MyField: string } }
            var a = new MyUnion::A { MyField = "" };
            if (false) {}
            else if (a matches MyUnion::A { MyField: var b }) {
                var c: string = b;
            }
            """,
            """
            union MyUnion { A }
            var a = MyUnion::A;
            if (false) {}
            else if (a matches var b) {
                var c: MyUnion = b;
            }
            """,
            """
            union MyUnion { A }
            var a = MyUnion::A;
            if (false) {}
            else if (a matches MyUnion var b) {
                var c: MyUnion = b;
            }
            """,
            """
            union MyUnion { A }
            union OtherUnion { B(MyUnion) }
            var a = OtherUnion::B(MyUnion::A);
            if (false) {}
            else if (a matches OtherUnion::B(MyUnion::A var c) var b) {
                var d: OtherUnion = b;
                var e: MyUnion = c;
            }
            """,
            Mvp
        };
    }

    public static TheoryData<string, IReadOnlyList<TypeCheckerError>> FailedExpressionTestCases()
    {
        return new TheoryData<string, IReadOnlyList<TypeCheckerError>>
        {
            {
                """
                var a = SomeFn;

                var b: string = a();
                var c: int = a();

                fn SomeFn<T>(): T {
                    return todo!;
                }
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                """
                fn SomeFn<T>(param: T) {
                    fn OtherFn<T2>(param2: T2) {
                        fn ThirdFn<T3>(param3: T3): T2 {
                            var a: T = param;
                            var b: T2 = param2;
                            var c: T3 = param3;
                            
                            return a;
                        }
                    }
                }
                """,
                [MismatchedTypes(new TestGenericTypeReference("T2"), new TestGenericTypeReference("T"))]
            },
            {
                """
                fn SomeFn<T>() {
                }

                SomeFn();
                """,
                []
            },
            {
                """
                var mut a = ok(1);
                a = ok(true);
                a = error("");
                """,
                []
            },
            {
                "var a: result::<int, string> = error(1);",
                [MismatchedTypes(Result(Int, String), Result(Int, Int))]
            },
            {
                "var a = ok(1);",
                []
            },
            {
                "var a = this;",
                []
            },
            {
                """
                class MyClass {
                    static fn SomeFn() {
                        var a = this;
                    }
                }
                """,
                []
            },
            {
                """
                union MyUnion {
                    static fn SomeFn() {
                        var a = this;
                    }
                }
                """,
                []
            },
            {
                """
                union MyUnion {
                    A,
                    B(int),
                    C { field MyField: int }
                }
                var a = MyUnion::A;
                match (a) {
                    MyUnion::A => 1,
                    MyUnion::B(var b) => b,
                    MyUnion::C { MyField } => b,// b not available in this arm
                }
                """,
                []
            },
            {
                """
                union MyUnion {
                    A,
                    B(int),
                    C { field MyField: int }
                }
                var a = MyUnion::A;
                match (a) {
                    MyUnion::A => 1,
                    MyUnion::B(var b) => b,
                    // non exhaustive
                }
                """,
                []
            },
            {
                """
                var a = "";
                match (a) {
                    int => 1 // incompatible pattern
                }
                """,
                []
            },
            {
                """
                var a = "";
                match (a) {
                    SomeType => 1 // missing type
                }
                """,
                []
            },
            {
                """
                union MyUnion {A, B}
                var a = MyUnion::A;
                var b = match (a) {
                    MyUnion::A => 1,
                    MyUnion::B => "" // mismatched arm expression types
                }
                """,
                [MismatchedTypes(Int, String)]
            },
            {
                """
                union MyUnion { A }
                var a = MyUnion::A;
                var z = a matches MyUnion::A var b;

                var c: MyUnion = b;
                """,
                []
            },
            {
                """
                union MyUnion { A(string) }
                var a = MyUnion::A("hi");
                var z = a matches MyUnion::A(_) var b;
                var c: MyUnion = b;
                """,
                []
            },
            {
                """
                union MyUnion { A(string) }
                var a = MyUnion::A("hi");
                var z = a matches MyUnion::A(string var b);
                var c: string = b;
                """,
                []
            },
            {
                """
                union MyUnion { A(string) }
                var a = MyUnion::A("hi");
                var z = a matches MyUnion::A(string var b);
                var c: string = b;
                """,
                []
            },
            {
                """
                union MyUnion { A { field MyField: string } }
                var a = new MyUnion::A { MyField = "" };
                var z = a matches MyUnion::A { MyField };
                var c: string = MyField;
                """,
                []
            },
            {
                """
                union MyUnion { A { field MyField: string } }
                var a = new MyUnion::A { MyField = "" };
                var z = a matches MyUnion::A { MyField: var b };
                var c: string = b;
                """,
                []
            },
            {
                """
                union MyUnion { A }
                var a = MyUnion::A;
                var z = a matches var b;
                var c: MyUnion = b;
                """,
                []
            },
            {
                """
                union MyUnion { A }
                var a = MyUnion::A;
                var z = a matches MyUnion var b;
                var c: MyUnion = b;
                """,
                []
            },
            {
                """
                union MyUnion { A }
                union OtherUnion { B(MyUnion) }
                var a = OtherUnion::B(MyUnion::A);
                var z = a matches OtherUnion::B(MyUnion::A var c) var b;
                var d: OtherUnion = b;
                var e: MyUnion = c;
                """,
                []
            },
            {
                """
                union MyUnion { A }
                var a = MyUnion::A;
                if (!(a matches MyUnion::A var b)) {
                    var c: MyUnion = b;
                }
                """,
                []
            },
            {
                """
                union MyUnion { A(string) }
                var a = MyUnion::A("hi");
                if (!(a matches MyUnion::A(_) var b)) {
                    var c: MyUnion = b;
                }
                """,
                []
            },
            {
                """
                union MyUnion { A(string) }
                var a = MyUnion::A("hi");
                if (!(a matches MyUnion::A(string var b))) {
                    var c: string = b;
                }
                """,
                []
            },
            {
                """
                union MyUnion { A(string) }
                var a = MyUnion::A("hi");
                if (!(a matches MyUnion::A(string var b))) {
                    var c: string = b;
                }
                """,
                []
            },
            {
                """
                union MyUnion { A { field MyField: string } }
                var a = new MyUnion::A { MyField = "" };
                if (!(a matches MyUnion::A { MyField })) {
                    var c: string = MyField;
                }
                """,
                []
            },
            {
                """
                union MyUnion { A { field MyField: string } }
                var a = new MyUnion::A { MyField = "" };
                if (!(a matches MyUnion::A { MyField: var b })) {
                    var c: string = b;
                }
                """,
                []
            },
            {
                """
                union MyUnion { A }
                var a = MyUnion::A;
                if (!(a matches var b)) {
                    var c: MyUnion = b;
                }
                """,
                []
            },
            {
                """
                union MyUnion { A }
                var a = MyUnion::A;
                if (!(a matches MyUnion var b)) {
                    var c: MyUnion = b;
                }
                """,
                []
            },
            {
                """
                union MyUnion { A }
                union OtherUnion { B(MyUnion) }
                var a = OtherUnion::B(MyUnion::A);
                if (!(a matches OtherUnion::B(MyUnion::A var c) var b)) {
                    var d: OtherUnion = b;
                    var e: MyUnion = c;
                }
                """,
                []
            },
            {
                """
                union MyUnion {
                    A,
                    
                    fn SomeMethod() {
                        var a: bool = 1;
                    }
                }
                """,
                [MismatchedTypes(Boolean, Int)]
            },
            {
                "var a = !1",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                "var b: bool = a matches MissingType;",
                []
            },
            {
                """
                union MyUnion {A, B(string)}
                var a = MyUnion::B("");
                var b: bool = a matches MyUnion::B(_, _);
                """,
                []
            },
            {
                """
                union MyUnion {A, B(string)}
                var a = MyUnion::B("");
                var b: bool = a matches MyUnion::C;
                """,
                []
            },
            {
                """
                union MyUnion {A { field MyField: string }}
                var a = new MyUnion::A { MyField = "" };
                var b: bool = a matches MyUnion::A { MyField: int };
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                """
                union MyUnion {A { field MyField: string }}
                var a = new MyUnion::A { MyField = "" };
                var b: bool = a matches MyUnion { MyField: int };
                """,
                []
            },
            {
                """
                union MyUnion {A { field MyField: string, field OtherField: bool }}
                var a = new MyUnion::A { MyField = "", OtherField = true };
                var b: bool = a matches MyUnion::A { MyField: _ };
                """,
                []
            },
            {
                """
                class MyClass {pub field MyField: string}
                var a = new MyClass { MyField = "" };
                var b: bool = a matches MyClass { MyField: int };
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                """
                class MyClass {pub field MyField: string, pub field OtherField: bool}
                var a = new MyClass { MyField = "", OtherField = true };
                var b: bool = a matches MyClass { MyField: string };
                """,
                []
            },
            {
                """
                class MyClass {pub field MyField: string, field OtherField: bool}
                var a = new MyClass { MyField = "", OtherField = true };
                var b: bool = a matches MyClass { MyField: string, OtherField: _ };
                """,
                []
            },
            {
                """
                class MyClass {pub field MyField: string, pub field OtherField: bool}
                var a = new MyClass { MyField = "", OtherField = true };
                var b: bool = a matches string;
                """,
                [MismatchedTypes(new TestClassReference("MyClass"), String)]
            },
            {
                """
                class MyClass {
                    field MyField: string
                }

                // MyField is not accessible
                var a = new MyClass { MyField = "" };
                """,
                []
            },
            {
                """
                class MyClass {
                    field MyField: string,
                    
                    fn MyFn() {
                        MyField = "";
                    }
                }
                """,
                [TypeCheckerError.NonMutableAssignment("MyField", SourceRange.Default)]
            },
            {
                """
                class MyClass {
                    field MyField: string,
                    
                    static fn MyFn() {
                        var a = MyField;
                    }
                }
                """,
                []
            },
            {
                """
                var a: string;
                // initial assignment succeeds
                a = "";
                // second assignment fails because it's not mutable
                a = ";";
                """,
                [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)]
            },
            {
                """
                class MyClass {
                    pub field MyField: string
                }

                var mut a = new MyClass {
                    MyField = ""
                };

                // MyField is not mutable
                a.MyField = "";
                """,
                [TypeCheckerError.NonMutableMemberAssignment(MemberAccess(VariableAccessor("a"), "MyField"))]
            },
            {
                """
                class MyClass { pub mut field MyField: string }
                var a = new MyClass { MyField = "" };

                // a is not marked as mutable
                a.MyField = "";
                """,
                [TypeCheckerError.NonMutableMemberOwnerAssignment(VariableAccessor("a"))]
            },
            {
                """
                var a = "";
                // a is not marked as mutable
                a = "";
                """,
                [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)]
            },
            {
                """
                class MyClass {
                    pub static field MyField: string
                };

                // MyField is not marked as mutable
                MyClass::MyField = "";
                """,
                [TypeCheckerError.NonMutableMemberAssignment(StaticMemberAccess(TypeIdentifier("MyClass"), "MyField"))]
            },
            {
                """
                class MyClass {
                    pub mut field MyField: string
                }
                fn MyFn(param: MyClass) { 
                    // param is not marked as mutable
                    param.MyField = "";
                }
                """,
                [TypeCheckerError.NonMutableMemberOwnerAssignment(VariableAccessor("param"))]
            },
            {
                """
                fn MyFn(param: string) {
                   // param is not marked as mutable
                   param = ""; 
                }
                """,
                [TypeCheckerError.NonMutableAssignment("param", SourceRange.Default)]
            },
            {
                """
                fn MyFn(mut param: string) {
                }
                var a = "";

                // param is mut, but a is not marked as mutable
                MyFn(a);
                """,
                [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)]
            },
            {
                """
                union MyUnion<T> {
                    A {
                        field MyField: T,
                    }
                }
                var a: MyUnion::<string> = new MyUnion::<string>::A {
                    MyField = 2
                };
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                """
                union MyUnion {
                    A {
                        field MyField: string
                    }
                }
                var a: MyUnion = new MyUnion::A {
                    MyField_ = ""
                };
                """,
                []
            },
            {
                """
                union MyUnion {
                    A {
                        field MyField: string
                    }
                }
                var a: MyUnion = new MyUnion::B {
                    MyField = ""
                };
                """,
                []
            },
            {
                """
                union MyUnion {
                    A {
                        field MyField: string
                    }
                }
                var a: MyUnion = new MyUnion::A {
                    MyField = 2
                };
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                """
                union MyUnion {
                    A(string)
                }
                var a: MyUnion = new MyUnion::A {
                    MyField = 2
                };
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                """
                union MyUnion {
                    A {
                        field MyField: string,
                    }
                }
                var a: MyUnion = new MyUnion::A {
                    MyField = 2
                };
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                """
                union MyUnion {
                    A(string)
                }
                var a = MyUnion::A(1);
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                """
                union MyUnion {
                    A,
                    A
                }
                """,
                []
            },
            {
                "union MyUnion {} union MyUnion {}",
                []
            },
            {
                "union MyUnion {} class MyUnion {}",
                []
            },
            {
                """
                union MyUnion {
                    A {
                        field MyField: string,
                        field MyField: string,
                    }
                }
                """,
                []
            },
            {
                """
                union MyUnion {
                    A()
                }
                """,
                []
            },
            {
                """
                fn MyFn(): result::<int, string> {
                    if (true) {
                        return result::<string, int>::Ok("someValue");
                    }
                    
                    return result::<string, int>::Error(1);
                }
                """,
                [
                    MismatchedTypes(Result(Int, String), Result(String, Int)),
                    MismatchedTypes(Result(Int, String), Result(String, Int)),
                ]
            },
            {
                "var a = result::<string, int>::Ok(1);",
                [MismatchedTypes(String, Int)]
            },
            {
                """
                class MyClass<T> {
                    fn MyFn<T2>(param1: T, param2: T2) {
                    }
                }

                var a = new MyClass::<int>{};
                a.MyFn::<string>("", 1);
                """,
                [MismatchedTypes(Int, String), MismatchedTypes(String, Int)]
            },
            {
                """
                class MyClass<T> {
                    fn MyFn<T2>(param1: T, param2: T2) {
                    }
                }

                var a = new MyClass::<int>{};
                a.MyFn::<string>("", "");
                """,
                [MismatchedTypes(Int, String)]
            },
            {
                """
                class MyClass<T> {
                    fn MyFn<T2>(param1: T, param2: T2) {
                    }
                }

                var a = new MyClass::<int>{};
                a.MyFn::<string>(1, 1);
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                "var a: string = 2",
                [MismatchedTypes(String, Int)]
            },
            {
                "var a: int = \"somestring\"",
                [MismatchedTypes(Int, String)]
            },
            {
                "var b;",
                []
            },
            {
                "fn MyFn(): int { return \"something\"; }",
                [MismatchedTypes(Int, String)]
            },
            {
                "fn MyFn() { return 1; }",
                [MismatchedTypes(Unit, Int)]
            },
            {
                "fn MyFn() {} fn MyFn() {}",
                []
            },
            {
                "fn MyFn(): string { return; }",
                [MismatchedTypes(String, Unit)]
            },
            {
                "var a = 2; var b: string = a",
                [MismatchedTypes(String, Int)]
            },
            {
                "var a: int; var b = a",
                []
            },
            {
                "fn MyFn(){fn InnerFn() {}} InnerFn();",
                []
            },
            {
                "CallMissingMethod();",
                []
            },
            {
                "var a = new MyClass::<int> {someField = true};",
                []
            },
            {
                "fn MyFn(param1: string, param2: int) {} MyFn(3, \"value\");",
                [MismatchedTypes(String, Int), MismatchedTypes(Int, String)]
            },
            {
                "fn MyFn(param1: string, param2: int) {} MyFn();",
                []
            },
            {
                """fn MyFn(param1: string, param2: int) {} MyFn("value", 3, 2);""",
                []
            },
            {
                "fn MyFn<T1>() {var a = T1.something;}",
                []
            },
            {
                "fn MyFn<T1>() {var a = T1::something;}",
                []
            },
            {
                "fn MyFn<T1>(param: T1): int { return param; }",
                [MismatchedTypes(Int, new TestGenericTypeReference("T1"))]
            },
            {
                "fn MyFn(){} fn MyFn(){}",
                []
            },
            {
                "fn MyFn<T, T>() {}",
                []
            },
            {
                "if (1) {}",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                "if (true) {} else if (1) {}",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                "if (true) {var a: string = 1;}",
                [MismatchedTypes(String, Int)]
            },
            {
                "if (true) {} else if (true) {var a: string = 1}",
                [MismatchedTypes(String, Int)]
            },
            {
                "if (true) {} else if (true) {} else {var a: string = 1}",
                [MismatchedTypes(String, Int)]
            },
            {
                "if (true) {} else if (true) {} else if (true) {var a: string = 1}",
                [MismatchedTypes(String, Int)]
            },
            {
                "var a: result::<>",
                []
            },
            {
                "var a: result::<string>",
                []
            },
            {
                "var a: result::<string, string, string>",
                []
            },
            {
                "fn Fn1<T1>(){} Fn1::<>();",
                []
            },
            {
                "fn Fn1<T1>(){} Fn1::<string, bool>();",
                []
            },
            {
                """
                class MyClass {
                    field someField: string,
                }
                var a = new MyClass { someField = 1 };
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                """
                class MyClass {
                    field someField: string,
                }
                var a = new MyClass { someField = "value", someField = "value" };
                """,
                []
            },
            {
                """
                class MyClass {
                    field someField: string,
                }
                var a = new MyClass { someField = "value", extraField = 1 };
                """,
                []
            },
            {
                """
                class MyClass {
                    field someField: string,
                }
                var a = new MyClass {};
                """,
                []
            },
            {
                "class MyClass { static field someField: string = 1, }",
                [MismatchedTypes(String, Int)]
            },
            {
                """
                class MyClass<T> {
                    fn MyFn<T>(){}
                }
                """,
                []
            },
            {
                "class MyClass<T, T>{}",
                []
            },
            {
                """
                class MyClass{}
                class OtherClass<MyClass>{}
                """,
                []
            },
            {
                """
                class MyClass {
                    fn MyFn(): int { return ""; }
                }
                """,
                [MismatchedTypes(Int, String)]
            },
            // binary operators
            // less than
            {
                "var a = 1 < true;",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "var a = true < 1;",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "var a: int = 1 < 2",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                // GreaterThan,
                "var a = true > 1;",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "var a = 2 > true",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "var a: int = 2 > 2",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                // Plus,
                "var a = true + 1;",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "var a = 2 + true",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "var a: bool = 2 + 2",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                // Minus,
                "var a = true - 1;",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "var a = 2 - true",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "var a: bool = 2 - 2",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                // Multiply,
                "var a = true * 1;",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "var a = 2 * true",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "var a: bool = 2 * 2",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                // Divide,
                "var a = true / 1;",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "var a = 2 / true",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "var a: bool = 2 / 2",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                // EqualityCheck,
                "var a = true == 1;",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                "var a = 2 == true",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "var a: int = 2 == 2",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                // ValueAssignment,
                "var mut a = 2; a = true",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "true = false",
                [TypeCheckerError.ExpressionNotAssignable(new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.True(SourceSpan.Default))))]
            },
            {
                // MemberAccess,
                """
                class MyClass { static field someField: int = 3, }
                var a: string = MyClass::someField;
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                // StaticMemberAccess
                """
                class MyClass { field someField: int, }
                var a: MyClass = new MyClass { someField = 3 };
                var b: string = a.someField;
                """,
                [MismatchedTypes(String, Int)]
            },
        };
    }

    private const string Mvp =
        """
        pub fn DoSomething(a: int): result::<int, string> {
            var mut b: int = 2;
            
            if (a > b) {
                return ok(a);
            }
            else if (a == b) {
                return result::<int, string>::Ok(b);
            }

            b = 3;

            var thing = new Class2 {
                A = "thing"
            };

            MyClass::StaticMethod();

            PrivateFn::<string>();
            
            if (false) {
                // lowercase error keyword
                return error("something wrong");
            }

            // Capital Error for fully resolved variant
            return result::<int, string>::Error("something wrong");
        }

        fn PrivateFn<T>() {
        }

        pub fn SomethingElse(a: int): result::<int, string> {
            var b = DoSomething(a)?;
            var mut c = 2;
            
            return result::<int, string>::Ok(b);
        }

        pub class MyClass {
            pub fn PublicMethod() {
            }

            pub static fn StaticMethod() {

            }
            
            field FieldA: string,
            mut field FieldB: string,
            pub mut field FieldC: string,
            pub field FieldD: string,
            pub static field FieldE: string = "something",
            pub static mut field FieldF: string = "something",
        }

        pub class GenericClass<T> {
            pub fn PublicMethod<T1>() {
            }
        }

        // a class
        pub class Class2 {
            pub field A: string,
        }

        /*
            A union
        */
        pub union MyUnion {
            A,
            B { field MyField: string, },
            C(string),
            
            fn SomeMethod() {
                var foo = match (this) {
                    MyUnion::A => "",
                    MyUnion::B { MyField } => MyField,
                    MyUnion::C(var value) => value
                };
                
                var bar = match (this) {
                    MyUnion::A => 1,
                    MyUnion::B => 2,
                    MyUnion::C => 3
                }
            }
        }

        fn AnotherMethod(param: MyUnion) {
            if (param matches MyUnion::A) {
            }
            else if (param matches MyUnion::B { MyField }) {
            }
            
            var a = match (param) {
                MyUnion::A => 1,
                MyUnion::B { MyField } => 2,
                MyUnion::C(var value) => 3
            };
        }

        var a = MyUnion::A;

        var c = new MyUnion::B{ MyField = ""};
        """;

    private static TypeCheckerError MismatchedTypes(ITypeReference expected, ITypeReference actual)
    {
        return TypeCheckerError.MismatchedTypes(SourceRange.Default, expected, actual);
    }
    
    private static readonly InstantiatedClass Int = InstantiatedClass.Int;
    private static readonly InstantiatedClass String = InstantiatedClass.String;
    private static readonly InstantiatedClass Boolean = InstantiatedClass.Boolean;
    private static readonly InstantiatedClass Unit = InstantiatedClass.Unit;

    private static InstantiatedUnion Result(ITypeReference value, ITypeReference error)
    {
        return new InstantiatedUnion(
            UnionSignature.Result,
            [
                new GenericTypeReference
                {
                    GenericName = UnionSignature.Result.GenericParameters[0].GenericName,
                    OwnerType = UnionSignature.Result,
                    ResolvedType = value
                },
                new GenericTypeReference
                {
                    GenericName = UnionSignature.Result.GenericParameters[1].GenericName,
                    OwnerType = UnionSignature.Result,
                    ResolvedType = error
                },
            ]);
    }

    private record TestGenericTypeReference(string Name) : ITypeReference, IEquatable<ITypeReference>
    {
        public virtual bool Equals(ITypeReference? other)
        {
            if (other is not GenericTypeReference genericTypeReference)
            {
                return false;
            }

            return genericTypeReference.GenericName == Name;
        }

        public override string ToString()
        {
            return $"{Name}=[??]";
        }
    }

    private record TestClassReference(string ClassName) : ITypeReference, IEquatable<ITypeReference>
    {
        public virtual bool Equals(ITypeReference? other)
        {
            if (other is not InstantiatedClass @class)
            {
                return false;
            }

            return @class.Signature.Name == ClassName;
        }

        public override string ToString()
        {
            return ClassName;
        }
    }
}