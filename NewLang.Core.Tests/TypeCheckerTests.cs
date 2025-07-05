using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using NewLang.Core.Tests.ParserTests;

using static NewLang.Core.TypeChecker;
using static NewLang.Core.Tests.ExpressionHelpers;

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
    public void Should_FailTypeChecking_When_ExpressionsAreNotValid(
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")] string description,
        string source,
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
                var b;
                if (true) {
                    b = "";
                }
                var a: string = b;
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
            var a;
            if (true) {
                a = "";
            }
            else if (true) {
                a = "";
            }
            else {
                a = "";
            }
            var b = a;
            """,
            """
            var a;
            if (true) {
                a = "";
            }
            else {
                a = "";
            }
            var b: string;
            """,
            """
            var a;
            a = "";
            """,
            """
            union MyEnum {
                A()
            }

            var a = MyEnum::A();
            """,
            """
            union MyUnion {
                A()
            }  
            
            var a = MyUnion::A();
            if (a matches MyUnion::A() var b) {
            }
            """,
            """
            fn SomeFn<T>(param: T){}
            
            SomeFn::<>("");
            """,
            """
            union MyUnion { A(string) }
            var a = MyUnion::A("hi");
            var z = a matches MyUnion::A(string var b);
            // b is never used
            """,
            """
            var a = "";
            // match that exactly matches type is exhaustive if it's not a union
            match (a) {
                string => 1
            }
            """,
            """
            union MyUnion {A, B}
            class MyClass {pub field MyField: MyUnion}
            var a = new MyClass {MyField = MyUnion::A};
            match (a) {
                MyClass { MyField: MyUnion::A } => 1,
                MyClass { MyField: MyUnion::B } => 1,
            }
            """,
            """
            union MyUnion {A, B}
            class MyClass {pub field MyField: MyUnion}
            var a = new MyClass {MyField = MyUnion::A};
            match (a) {
                MyClass => 1
            }
            """,
            """
            union MyUnion {A, B}
            class MyClass {pub field MyField: MyUnion}
            var a = new MyClass {MyField = MyUnion::A};
            match (a) {
                MyClass { MyField: _ } => 1
            }
            """,
            """
            union MyUnion {A, B}
            class MyClass {pub field MyField: MyUnion}
            var a = new MyClass {MyField = MyUnion::A};
            match (a) {
                MyClass { MyField: var b } => b
            }
            """,
            """
            union MyUnion {A, B}
            class MyClass {pub field MyField: MyUnion}
            var a = new MyClass {MyField = MyUnion::A};
            match (a) {
                var b => b
            }
            """,
            """
            union MyUnion {A, B, C}
            class MyClass {pub field MyField: MyUnion}
            var a = new MyClass {MyField = MyUnion::A};
            match (a) {
                MyClass { MyField: MyUnion::A } => 1,
                MyClass { MyField: var b } => 1
            }
            """,
            """
            fn MyFn() {
            }

            // type arguments can have the same name as Functions
            class MyClass<MyFn> {}
            """,
            """
            // type arguments can have the same name as Functions
            class MyClass<MyFn> {
                fn MyFn() {}
            }
            """,
            """
            class MyClass {
                pub field MyField: string,
                field OtherField: bool,
                
                pub static fn Create(): MyClass {
                    return new MyClass {
                        MyField = "",
                        OtherField = true
                    };
                }
            }
            var a = MyClass::Create();
            var b: bool = a matches MyClass { MyField: string };
            """,
            """
            var mut a = ok(1);
            a = ok(2);
            a = error("");
            """,
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

    public static TheoryData<string, string, IReadOnlyList<TypeCheckerError>> FailedExpressionTestCases()
    {
        return new TheoryData<string, string, IReadOnlyList<TypeCheckerError>>
        {
            {
                "type inference from same function variable into two variables",
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
                "nested function parameter mismatched types return types",
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
                [MismatchedTypes(GenericTypeReference("T2"), GenericTypeReference("T"))]
            },
            {
                "Unresolved inferred function generic",
                """
                fn SomeFn<T>() {
                }

                SomeFn();
                """,
                [TypeCheckerError.UnresolvedInferredType()]
            },
            {
                "incompatible inferred types",
                """
                var mut a = ok(1);
                a = ok(true);
                a = error("");
                """,
                [MismatchedTypes(Result(Int, GenericTypeReference("TError")), Result(Boolean, GenericTypeReference("TError")))]
            },
            {
                "incompatible inferred result type",
                "var a: result::<int, string> = error(1);",
                [MismatchedTypes(Result(Int, String), Result(Int, Int))]
            },
            {
                "missing inferred result type parameter",
                "var a = ok(1);",
                [TypeCheckerError.UnresolvedInferredType()]
            },
            {
                "this used outside of class instance",
                "var a = this;",
                [TypeCheckerError.ThisAccessedOutsideOfInstanceMethod(Identifier("this"))]
            },
            {
                "this used in static class function",
                """
                class MyClass {
                    static fn SomeFn() {
                        var a = this;
                    }
                }
                """,
                [TypeCheckerError.ThisAccessedOutsideOfInstanceMethod(Identifier("this"))]
            },
            {
                "this used in static union function",
                """
                union MyUnion {
                    static fn SomeFn() {
                        var a = this;
                    }
                }
                """,
                [TypeCheckerError.ThisAccessedOutsideOfInstanceMethod(Identifier("this"))]
            },
            {
                "pattern variable used in wrong match arm",
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
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "match expression not exhaustive",
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
                [TypeCheckerError.MatchNonExhaustive()]
            },
            {
                "match expression not exhaustive",
                """
                union MyUnion {A, B}
                class MyClass {pub field MyField: MyUnion}
                var a = new MyClass {MyField = MyUnion::A};
                match (a) {
                    MyClass { MyField: MyUnion::A } => 1
                }
                """,
                [TypeCheckerError.MatchNonExhaustive()]
            },
            {
                "incompatible pattern type",
                """
                var a = "";
                match (a) {
                    int => 1, // incompatible pattern
                    _ => 2
                }
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                "Unknown type used in pattern",
                """
                var a = "";
                match (a) {
                    SomeType => 1, // missing type
                    _ => 1
                }
                """,
                [TypeCheckerError.SymbolNotFound(Identifier("SomeType"))]
            },
            {
                "match arms provide incompatible types",
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
                "matches pattern variable used outside of true if check",
                """
                union MyUnion { A }
                var a = MyUnion::A;
                var z = a matches MyUnion::A var b;

                var c: MyUnion = b;
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "matches pattern variable used outside of true if check",
                """
                union MyUnion { A(string) }
                var a = MyUnion::A("hi");
                var z = a matches MyUnion::A(_) var b;
                var c: MyUnion = b;
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "matches pattern variable used outside of true if check",
                """
                union MyUnion { A(string) }
                var a = MyUnion::A("hi");
                var z = a matches MyUnion::A(string var b);
                var c: string = b;
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "matches pattern variable used outside of true if check",
                """
                union MyUnion { A(string) }
                var a = MyUnion::A("hi");
                var z = a matches MyUnion::A(string var b);
                var c: string = b;
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "union struct pattern field used outside of true if check",
                """
                union MyUnion { A { field MyField: string } }
                var a = new MyUnion::A { MyField = "" };
                var z = a matches MyUnion::A { MyField };
                var c: string = MyField;
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("MyField"))]
            },
            {
                "matches pattern variable used outside of true if check",
                """
                union MyUnion { A { field MyField: string } }
                var a = new MyUnion::A { MyField = "" };
                var z = a matches MyUnion::A { MyField: var b };
                var c: string = b;
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "matches pattern variable used outside of true if check",
                """
                union MyUnion { A }
                var a = MyUnion::A;
                var z = a matches var b;
                var c: MyUnion = b;
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "matches pattern variable used outside of true if check",
                """
                union MyUnion { A }
                var a = MyUnion::A;
                var z = a matches MyUnion var b;
                var c: MyUnion = b;
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "matches pattern variable used outside of true if check",
                """
                union MyUnion { A }
                union OtherUnion { B(MyUnion) }
                var a = OtherUnion::B(MyUnion::A);
                var z = a matches OtherUnion::B(MyUnion::A var c) var b;
                var d: OtherUnion = b;
                var e: MyUnion = c;
                """,
                [
                    TypeCheckerError.AccessUninitializedVariable(Identifier("b")),
                    TypeCheckerError.AccessUninitializedVariable(Identifier("c")),
                ]
            },
            {
                "matches pattern variable used in false if check",
                """
                union MyUnion { A }
                var a = MyUnion::A;
                if (!(a matches MyUnion::A var b)) {
                    var c: MyUnion = b;
                }
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "matches pattern variable used in false if check",
                """
                union MyUnion { A(string) }
                var a = MyUnion::A("hi");
                if (!(a matches MyUnion::A(_) var b)) {
                    var c: MyUnion = b;
                }
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "matches pattern variable used in false if check",
                """
                union MyUnion { A(string) }
                var a = MyUnion::A("hi");
                if (!(a matches MyUnion::A(string var b))) {
                    var c: string = b;
                }
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "matches pattern variable used in false if check",
                """
                union MyUnion { A(string) }
                var a = MyUnion::A("hi");
                if (!(a matches MyUnion::A(string var b))) {
                    var c: string = b;
                }
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "matches pattern variable used in false if check",
                """
                union MyUnion { A { field MyField: string } }
                var a = new MyUnion::A { MyField = "" };
                if (!(a matches MyUnion::A { MyField })) {
                    var c: string = MyField;
                }
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("MyField"))]
            },
            {
                "matches pattern variable used in false if check",
                """
                union MyUnion { A { field MyField: string } }
                var a = new MyUnion::A { MyField = "" };
                if (!(a matches MyUnion::A { MyField: var b })) {
                    var c: string = b;
                }
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "matches pattern variable used in false if check",
                """
                union MyUnion { A }
                var a = MyUnion::A;
                if (!(a matches var b)) {
                    var c: MyUnion = b;
                }
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "matches pattern variable used in false if check",
                """
                union MyUnion { A }
                var a = MyUnion::A;
                if (!(a matches MyUnion var b)) {
                    var c: MyUnion = b;
                }
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "matches pattern variable used in false if check",
                """
                union MyUnion { A }
                union OtherUnion { B(MyUnion) }
                var a = OtherUnion::B(MyUnion::A);
                if (!(a matches OtherUnion::B(MyUnion::A var c) var b)) {
                    var d: OtherUnion = b;
                    var e: MyUnion = c;
                }
                """,
                [
                    TypeCheckerError.AccessUninitializedVariable(Identifier("b")),
                    TypeCheckerError.AccessUninitializedVariable(Identifier("c")),
                ]
            },
            {
                "mismatched variable declaration assignment in union method",
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
                "incorrect not operator expression type",
                "var a = !1",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                "missing type in pattern",
                "var b: bool = 1 matches MissingType;",
                [TypeCheckerError.SymbolNotFound(Identifier("MissingType"))]
            },
            {
                "extra patterns in union tuple pattern",
                """
                union MyUnion {A, B(string)}
                var a = MyUnion::B("");
                var b: bool = a matches MyUnion::B(_, _);
                """,
                [TypeCheckerError.IncorrectNumberOfPatternsInTupleVariantUnionPattern(UnionTupleVariantPattern(
                    TypeIdentifier("MyUnion"),
                    "B",
                    [new DiscardPattern(SourceRange.Default), new DiscardPattern(SourceRange.Default)]),
                    1)]
            },
            {
                "missing patterns in union tuple pattern",
                """
                union MyUnion {A, B(string, bool, int)}
                var a = MyUnion::B("", true, 1);
                var b: bool = a matches MyUnion::B(_, _);
                """,
                [TypeCheckerError.IncorrectNumberOfPatternsInTupleVariantUnionPattern(UnionTupleVariantPattern(
                        TypeIdentifier("MyUnion"),
                        "B",
                        [new DiscardPattern(SourceRange.Default), new DiscardPattern(SourceRange.Default)]),
                    3)]
            },
            {
                "Unknown variant used in matches union variant pattern",
                """
                union MyUnion {A, B(string)}
                var a = MyUnion::B("");
                var b: bool = a matches MyUnion::C;
                """,
                [TypeCheckerError.UnknownVariant(Identifier("C"), "MyUnion")]
            },
            {
                "mismatched type used for field in struct variant pattern",
                """
                union MyUnion {A { field MyField: string }}
                var a = new MyUnion::A { MyField = "" };
                var b: bool = a matches MyUnion::A { MyField: int };
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                "variant name not specified for struct union pattern",
                """
                union MyUnion {A { field MyField: string }}
                var a = new MyUnion::A { MyField = "" };
                var b: bool = a matches MyUnion { MyField: int };
                """,
                [TypeCheckerError.NonClassUsedInClassPattern(TypeIdentifier("MyUnion"))]
            },
            {
                "struct union pattern does not list all fields",
                """
                union MyUnion {A { field MyField: string, field OtherField: bool }}
                var a = new MyUnion::A { MyField = "", OtherField = true };
                var b: bool = a matches MyUnion::A { MyField: _ };
                """,
                [TypeCheckerError.MissingFieldsInStructVariantUnionPattern(UnionStructVariantPattern(
                    TypeIdentifier("MyUnion"),
                    "A"),
                    ["OtherField"])]
            },
            {
                "incompatible field type used in class pattern",
                """
                class MyClass {pub field MyField: string}
                var a = new MyClass { MyField = "" };
                var b: bool = a matches MyClass { MyField: int };
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                "class pattern does not list all fields",
                """
                class MyClass {pub field MyField: string, pub field OtherField: bool}
                var a = new MyClass { MyField = "", OtherField = true };
                var b: bool = a matches MyClass { MyField: string };
                """,
                [TypeCheckerError.MissingFieldsInClassPattern(["OtherField"], TypeIdentifier("MyClass"))]
            },
            {
                "non public field in class pattern",
                """
                class MyClass {
                    pub field MyField: string,
                    field OtherField: bool,
                    
                    pub static fn Create(): MyClass {
                        return new MyClass {
                            MyField = "",
                            OtherField = true
                        };
                    }
                }
                var a = MyClass::Create();
                var b: bool = a matches MyClass { MyField: string, OtherField: _ };
                """,
                [TypeCheckerError.PrivateFieldReferenced(Identifier("OtherField"))]
            },
            {
                "mismatched pattern type",
                """
                class MyClass {pub field MyField: string, pub field OtherField: bool}
                var a = new MyClass { MyField = "", OtherField = true };
                var b: bool = a matches string;
                """,
                [MismatchedTypes(new TestClassReference("MyClass"), String)]
            },
            {
                "class initializer sets private field",
                """
                class MyClass {
                    field MyField: string
                }

                // MyField is not accessible
                var a = new MyClass { MyField = "" };
                """,
                [TypeCheckerError.PrivateFieldReferenced(Identifier("MyField"))]
            },
            {
                "non mutable field assigned in class method",
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
                "instance field used in static class method",
                """
                class MyClass {
                    field MyField: string,
                    
                    static fn MyFn() {
                        var a = MyField;
                    }
                }
                """,
                [TypeCheckerError.SymbolNotFound(Identifier("MyField"))]
            },
            {
                "non mutable variable assigned twice",
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
                "non mutable field assigned through member access",
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
                "mutable field assigned from non mutable instance variable",
                """
                class MyClass { pub mut field MyField: string }
                var a = new MyClass { MyField = "" };

                // a is not marked as mutable
                a.MyField = "";
                """,
                [TypeCheckerError.NonMutableMemberOwnerAssignment(VariableAccessor("a"))]
            },
            {
                "non mutable variable assigned",
                """
                var a = "";
                // a is not marked as mutable
                a = "";
                """,
                [TypeCheckerError.NonMutableAssignment("a", SourceRange.Default)]
            },
            {
                "non mutable static field assigned through static member access",
                """
                class MyClass {
                    pub static field MyField: string = ""
                };

                // MyField is not marked as mutable
                MyClass::MyField = "";
                """,
                [TypeCheckerError.NonMutableMemberAssignment(StaticMemberAccess(TypeIdentifier("MyClass"), "MyField"))]
            },
            {
                "non mutable param member assigned",
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
                "not mutable param assigned",
                """
                fn MyFn(param: string) {
                   // param is not marked as mutable
                   param = ""; 
                }
                """,
                [TypeCheckerError.NonMutableAssignment("param", SourceRange.Default)]
            },
            {
                "non mutable variable passed to mutable function parameter",
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
                "mismatched type when assigning field to generic field",
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
                "unknown field assigned in struct union initializer",
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
                [TypeCheckerError.UnknownField(Identifier("MyField_"), "union variant MyUnion::A")]
            },
            {
                "Unknown variant name used in union struct initializer",
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
                [TypeCheckerError.UnknownVariant(Identifier("B"), "MyUnion")]
            },
            {
                "incorrect expression type used in union struct initializer",
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
                "Union struct initializer used for tuple union variant",
                """
                union MyUnion {
                    A(string)
                }
                var a: MyUnion = new MyUnion::A {
                    MyField = 2
                };
                """,
                [TypeCheckerError.UnionStructVariantInitializerNotStructVariant(Identifier("A"))]
            },
            {
                "incorrect expression type used in union struct initializer",
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
                "incorrect expression type used in union tuple",
                """
                union MyUnion {
                    A(string)
                }
                var a = MyUnion::A(1);
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                "duplicate union variant name in union declaration",
                """
                union MyUnion {
                    A,
                    A
                }
                """,
                [TypeCheckerError.DuplicateVariantName(Identifier("A"))]
            },
            {
                "duplicate union names",
                "union MyUnion {} union MyUnion {}",
                [TypeCheckerError.ConflictingTypeName(Identifier("MyUnion"))]
            },
            {
                "duplicate union names with type errors",
                "union MyUnion {A, A} union MyUnion {B, B}",
                [
                    TypeCheckerError.ConflictingTypeName(Identifier("MyUnion")),
                    TypeCheckerError.DuplicateVariantName(Identifier("A")),
                    TypeCheckerError.DuplicateVariantName(Identifier("B")),
                ]
            },
            {
                "union name conflicts with class name",
                "union MyUnion {} class MyUnion {}",
                [TypeCheckerError.ConflictingTypeName(Identifier("MyUnion"))]
            },
            {
                "duplicate class name with inner errors",
                """
                class MyClass {
                    fn MyFn() {},
                    fn MyFn() {}
                }
                class MyClass {
                    fn OtherFn() {},
                    fn OtherFn() {}
                }
                """,
                [
                    TypeCheckerError.ConflictingTypeName(Identifier("MyClass")),
                    TypeCheckerError.ConflictingFunctionName(Identifier("MyFn")),
                    TypeCheckerError.ConflictingFunctionName(Identifier("OtherFn")),
                ]
            },
            {
                "duplicate field in union struct variant",
                """
                union MyUnion {
                    A {
                        field MyField: string,
                        field MyField: string,
                    }
                }
                """,
                [TypeCheckerError.DuplicateFieldInUnionStructVariant(
                    Identifier("MyUnion"),
                    Identifier("A"),
                    Identifier("MyField"))]
            },
            {
                "incorrect type arguments for return value of same type",
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
                "incorrect expression type for generic type in union tuple variant",
                "var a = result::<string, int>::Ok(1);",
                [MismatchedTypes(String, Int)]
            },
            {
                "incorrect expression type for generic type in generic class and generic method call",
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
                "incorrect type used in generic class and generic method call",
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
                "incorrect expression type used in generic class and generic method",
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
                "incorrect expression type in variable assignment",
                "var a: string = 2",
                [MismatchedTypes(String, Int)]
            },
            {
                "incorrect expression type in variable assignment",
                "var a: int = \"somestring\"",
                [MismatchedTypes(Int, String)]
            },
            {
                "variable declaration without type or assignment never inferred",
                "var b;",
                [TypeCheckerError.UnresolvedInferredType()]
            },
            {
                "variable declaration without type or assignment never inferred",
                """
                var a;
                if (true) {
                    a = "";
                }
                else if (true) {
                    a = "";
                }
                var b = a;
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("a"))]
            },
            {
                "variable declaration without type or assignment never inferred",
                """
                var b;
                if (true) {
                    b = "";
                }
                var a: string = b;
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "variable declaration without type or assignment never inferred",
                """
                var b;
                if (true) {
                    b = "";
                }
                else {
                    var c = b;
                }
                """,
                [TypeCheckerError.AccessUninitializedVariable(Identifier("b"))]
            },
            {
                "incorrect type in return value",
                "fn MyFn(): int { return \"something\"; }",
                [MismatchedTypes(Int, String)]
            },
            {
                "return value for function without return type",
                "fn MyFn() { return 1; }",
                [MismatchedTypes(Unit, Int)]
            },
            {
                "duplicate function declaration",
                "fn MyFn() {} fn MyFn() {}",
                [TypeCheckerError.ConflictingFunctionName(Identifier("MyFn"))]
            },
            {
                "duplicate function declaration in union",
                "union MyUnion {fn MyFn() {} fn MyFn() {}}",
                [TypeCheckerError.ConflictingFunctionName(Identifier("MyFn"))]
            },
            {
                "duplicate function declaration in class",
                "class MyClass {fn MyFn() {} fn MyFn() {}}",
                [TypeCheckerError.ConflictingFunctionName(Identifier("MyFn"))]
            },
            {
                "function contains duplicate argument",
                """
                fn SomeFn(a: int, a: string) {
                    // verify first duplicate argument is accepted
                    var b: int = a;
                    var c: string = a;
                }
                """,
                [
                    TypeCheckerError.DuplicateFunctionArgument(Identifier("a"), Identifier("SomeFn")),
                    MismatchedTypes(String, Int)
                ]
            },
            {
                "no return value provided when return value expected",
                "fn MyFn(): string { return; }",
                [MismatchedTypes(String, Unit)]
            },
            {
                "incorrect expression type in variable assignment",
                "var a = 2; var b: string = a",
                [MismatchedTypes(String, Int)]
            },
            {
                "variable used before initialization",
                "var a: int; var b = a",
                [TypeCheckerError.AccessUninitializedVariable(Identifier("a"))]
            },
            {
                "function used outside of declaration scope",
                "fn MyFn(){fn InnerFn() {}} InnerFn();",
                [TypeCheckerError.SymbolNotFound(Identifier("InnerFn"))]
            },
            {
                "call missing method",
                "CallMissingMethod(b);",
                [
                    TypeCheckerError.SymbolNotFound(Identifier("CallMissingMethod")),
                    TypeCheckerError.SymbolNotFound(Identifier("b")),
                ]
            },
            {
                "object initializer for unknown type",
                "var a = new MyClass::<int> {someField = true};",
                [TypeCheckerError.SymbolNotFound(Identifier("MyClass"))]
            },
            {
                "object initializer for unknown type",
                "var a = new MyClass::<int> {someField = b};",
                [
                    TypeCheckerError.SymbolNotFound(Identifier("MyClass")),
                    TypeCheckerError.SymbolNotFound(Identifier("b"))
                ]
            },
            {
                "incorrect expression types in method call",
                "fn MyFn(param1: string, param2: int) {} MyFn(3, \"value\");",
                [MismatchedTypes(String, Int), MismatchedTypes(Int, String)]
            },
            {
                "missing function arguments",
                "fn MyFn(param1: string, param2: int) {} MyFn();",
                [TypeCheckerError.IncorrectNumberOfMethodParameters(MethodCall(VariableAccessor("MyFn")), 2)]
            },
            {
                "too many function arguments",
                """fn MyFn(param1: string, param2: int) {} MyFn("value", 3, 2);""",
                [TypeCheckerError.IncorrectNumberOfMethodParameters(MethodCall(VariableAccessor("MyFn"), StringLiteral("value"), IntLiteral(3), IntLiteral(2)), 2)]
            },
            {
                "member accessed on generic instance variable",
                "fn MyFn<T1>(param: T1) {var a = param.something;}",
                [TypeCheckerError.MemberAccessOnGenericExpression(MemberAccess(VariableAccessor("param"), "something"))]
            },
            {
                "static member accessed on generic type",
                "fn MyFn<T1>() {var a = T1::something;}",
                [TypeCheckerError.StaticMemberAccessOnGenericReference(StaticMemberAccess(TypeIdentifier("T1"), "something"))]
            },
            {
                "generic variable returned as concrete class",
                "fn MyFn<T1>(param: T1): int { return param; }",
                [MismatchedTypes(Int, GenericTypeReference("T1"))]
            },
            {
                "duplicate function generic argument",
                "fn MyFn<T, T>() {}",
                [TypeCheckerError.DuplicateGenericArgument(Identifier("T"))]
            },
            {
                "duplicate function generic argument",
                "fn MyFn<T, T, T1, T1>() {}",
                [
                    TypeCheckerError.DuplicateGenericArgument(Identifier("T")),
                    TypeCheckerError.DuplicateGenericArgument(Identifier("T1")),
                ]
            },
            {
                "duplicate function generic argument",
                "fn MyFn<T, T, T>() {}",
                [
                    TypeCheckerError.DuplicateGenericArgument(Identifier("T")),
                    TypeCheckerError.DuplicateGenericArgument(Identifier("T")),
                ]
            },
            {
                "incorrect type in if check",
                "if (1) {}",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                "incorrect type in else if check",
                "if (true) {} else if (1) {}",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                "incorrect type in variable declaration in if body",
                "if (true) {var a: string = 1;}",
                [MismatchedTypes(String, Int)]
            },
            {
                "incorrect type in variable declaration in else if body",
                "if (true) {} else if (true) {var a: string = 1}",
                [MismatchedTypes(String, Int)]
            },
            {
                "incorrect type in variable declaration in else body",
                "if (true) {} else if (true) {} else {var a: string = 1}",
                [MismatchedTypes(String, Int)]
            },
            {
                "incorrect type in second else if body",
                "if (true) {} else if (true) {} else if (true) {var a: string = 1}",
                [MismatchedTypes(String, Int)]
            },
            {
                "unresolved inferred types",
                "var a: result::<>",
                [TypeCheckerError.UnresolvedInferredType()]
            },
            {
                "unresolved inferred types",
                "var a: result",
                [TypeCheckerError.UnresolvedInferredType()]
            },
            {
                "incorrect number of type parameters",
                "var a: result::<string>",
                [TypeCheckerError.IncorrectNumberOfTypeArguments(SourceRange.Default,1, 2)]
            },
            {
                "incorrect number of type parameters",
                "union MyUnion{A} var a = MyUnion::<string>::A",
                [TypeCheckerError.IncorrectNumberOfTypeArguments(SourceRange.Default, 1, 0)]
            },
            {
                "incorrect number of type parameters",
                "class MyClass{} var a = new MyClass::<string>{}",
                [TypeCheckerError.IncorrectNumberOfTypeArguments(SourceRange.Default, 1, 0)]
            },
            {
                "incorrect number of type parameters",
                "class MyClass<T, T2>{} var a = new MyClass::<string>{}",
                [TypeCheckerError.IncorrectNumberOfTypeArguments(SourceRange.Default, 1, 2)]
            },
            {
                "too many type parameters",
                "var a: result::<string, string, string>",
                [TypeCheckerError.IncorrectNumberOfTypeArguments(SourceRange.Default, 3, 2)]
            },
            {
                "unresolved function generic type",
                "fn Fn1<T1>(){} Fn1::<>();",
                [TypeCheckerError.UnresolvedInferredType()]
            },
            {
                "too many function type parameters",
                "fn Fn1<T1>(){} Fn1::<string, bool>();",
                [TypeCheckerError.IncorrectNumberOfTypeArguments(
                    SourceRange.Default,
                    2,
                    1)]
            },
            {
                "Unresolved function type parameter",
                "fn Fn1<T1>(){} Fn1::<>();",
                [TypeCheckerError.UnresolvedInferredType()]
            },
            {
                "unresolved function type parameters",
                "fn Fn1<T1>(){} Fn1();",
                [TypeCheckerError.UnresolvedInferredType()]
            },
            {
                "incorrect type for class initializer field assignment",
                """
                class MyClass {
                    pub field someField: string,
                }
                var a = new MyClass { someField = 1 };
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                "field assigned twice in object initializer",
                """
                class MyClass {
                    pub field someField: string,
                }
                var a = new MyClass { someField = "value", someField = "value" };
                """,
                [TypeCheckerError.ClassFieldSetMultipleTypesInInitializer(Identifier("someField"))]
            },
            {
                "unknown field assigned in object initializer",
                """
                class MyClass {
                    pub field someField: string,
                }
                var a = new MyClass { someField = "value", extraField = 1 };
                """,
                [TypeCheckerError.UnknownField(Identifier("extraField"), "class MyClass")]
            },
            {
                "field not assigned in object initializer",
                """
                class MyClass {
                    pub field someField: string,
                }
                var a = new MyClass {};
                """,
                [TypeCheckerError.FieldsLeftUnassignedInClassInitializer(new ObjectInitializerExpression(
                    new ObjectInitializer(TypeIdentifier("MyClass"), []), SourceRange.Default),
                    ["someField"])]
            },
            {
                "incorrect expression type in static field initializer",
                "class MyClass { static field someField: string = 1, }",
                [MismatchedTypes(String, Int)]
            },
            {
                "function generic type conflict with parent class",
                """
                class MyClass<T> {
                    fn MyFn<T>(){}
                }
                """,
                [TypeCheckerError.ConflictingTypeArgument(Identifier("T"))]
            },
            {
                "duplicate generic type in class definition",
                "class MyClass<T, T>{}",
                [TypeCheckerError.DuplicateGenericArgument(Identifier("T"))]
            },
            {
                "duplicate generic type in class definition",
                "class MyClass<T, T, T1, T1>{}",
                [
                    TypeCheckerError.DuplicateGenericArgument(Identifier("T")),
                    TypeCheckerError.DuplicateGenericArgument(Identifier("T1")),
                ]
            },
            {
                "duplicate generic type in class definition",
                "class MyClass<T, T, T>{}",
                [
                    TypeCheckerError.DuplicateGenericArgument(Identifier("T")),
                    TypeCheckerError.DuplicateGenericArgument(Identifier("T")),
                ]
            },
            {
                "Generic type conflicts with existing type",
                """
                class MyClass{}
                class OtherClass<MyClass>{}
                """,
                [TypeCheckerError.TypeArgumentConflictsWithType(Identifier("MyClass"))]
            },
            {
                "Generic type conflicts with existing type",
                """
                class MyClass{}
                fn MyFn<MyClass>{}
                """,
                [TypeCheckerError.TypeArgumentConflictsWithType(Identifier("MyClass"))]
            },
            {
                "Generic type conflicts with existing type",
                """
                class MyClass{}
                class SomeClass {fn MyFn<MyClass>{}}
                """,
                [TypeCheckerError.TypeArgumentConflictsWithType(Identifier("MyClass"))]
            },
            {
                "Generic type conflicts with existing type",
                """
                class MyClass{}
                union SomeUnion {fn MyFn<MyClass>{}}
                """,
                [TypeCheckerError.TypeArgumentConflictsWithType(Identifier("MyClass"))]
            },
            {
                "Generic type conflicts with existing type",
                """
                class MyClass{}
                fn SomeFn {fn MyFn<MyClass>{}}
                """,
                [TypeCheckerError.TypeArgumentConflictsWithType(Identifier("MyClass"))]
            },
            {
                "Generic type conflicts with existing type",
                """
                class MyClass{}
                union MyUnion<MyClass>{}
                """,
                [TypeCheckerError.TypeArgumentConflictsWithType(Identifier("MyClass"))]
            },
            {
                "Generic type conflicts with existing type",
                """
                union OtherUnion{}
                union MyUnion<OtherUnion>{}
                """,
                [TypeCheckerError.TypeArgumentConflictsWithType(Identifier("OtherUnion"))]
            },
            {
                "Generic type conflicts with existing type",
                """
                class OtherClass<MyClass>{}
                class MyClass{}
                """,
                [TypeCheckerError.TypeArgumentConflictsWithType(Identifier("MyClass"))]
            },
            {
                "incorrect return type",
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
                "incorrect type for less than",
                "var a = 1 < true;",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "incorrect type for less than",
                "var a = true < 1;",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "incorrect type for less than variable declaration",
                "var a: int = 1 < 2",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "incorrect type for greater than",
                // GreaterThan,
                "var a = true > 1;",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "incorrect type for greater than",
                "var a = 2 > true",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "incorrect type for greater than in variable declaration",
                "var a: int = 2 > 2",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "incorrect type for plus",
                // Plus,
                "var a = true + 1;",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "incorrect type for plus",
                "var a = 2 + true",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "incorrect type for plus in variable declaration",
                "var a: bool = 2 + 2",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                "incorrect type for minus",
                // Minus,
                "var a = true - 1;",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "incorrect type for minus",
                "var a = 2 - true",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "incorrect type for minus in variable declaration",
                "var a: bool = 2 - 2",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                // Multiply,
                "incorrect type for multiply",
                "var a = true * 1;",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "incorrect type for multiply",
                "var a = 2 * true",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "incorrect type for multiply in variable declaration",
                "var a: bool = 2 * 2",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                "incorrect type for divide",
                // Divide,
                "var a = true / 1;",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "incorrect type for divide",
                "var a = 2 / true",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "incorrect type for divide in variable declaration",
                "var a: bool = 2 / 2",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                "incorrect type for equality check",
                // EqualityCheck,
                "var a = true == 1;",
                [MismatchedTypes(Boolean, Int)]
            },
            {
                "incorrect type for equality check",
                "var a = 2 == true",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "incorrect type for equality check in variable declaration",
                "var a: int = 2 == 2",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                // ValueAssignment,
                "incompatible type used for variable assignment",
                "var mut a = 2; a = true",
                [MismatchedTypes(Int, Boolean)]
            },
            {
                "assignment to literal",
                "true = false",
                [TypeCheckerError.ExpressionNotAssignable(new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Token.True(SourceSpan.Default))))]
            },
            {
                // MemberAccess,
                "incorrect type in variable declaration",
                """
                class MyClass { pub static field someField: int = 3, }
                var a: string = MyClass::someField;
                """,
                [MismatchedTypes(String, Int)]
            },
            {
                // StaticMemberAccess
                "incorrect type in variable declaration",
                """
                class MyClass { pub field someField: int, }
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

    private sealed record TestClassReference(string ClassName) : ITypeReference, IEquatable<ITypeReference>
    {
        public bool Equals(ITypeReference? other)
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

    private static GenericTypeReference GenericTypeReference(string name)
    {
        return new GenericTypeReference
        {
            GenericName = name,
            OwnerType = null!
        };
    }
}