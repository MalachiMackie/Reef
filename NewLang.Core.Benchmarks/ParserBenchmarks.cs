using BenchmarkDotNet.Attributes;

namespace NewLang.Core.Benchmarks;

[MemoryDiagnoser]
public class ParserBenchmarks
{
    private const string SmallSource = "var a = 2;";

    private const string MediumSource = """
                                        pub fn DoSomething(a: int): result::<int, string> {
                                            var b: int = 2;
                                            
                                            if (a > b) {
                                                return ok(a);
                                            }
                                            else if (a == b) {
                                                return ok(b);
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
                                            pub static field FieldE: string;
                                        }

                                        pub class GenericClass<T> {
                                            pub fn PublicMethod<T1>() {
                                            }
                                        }

                                        pub class Class2 {
                                            pub field A: string;
                                        }
                                        """;

    private const string LargeSource = $"""
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        {MediumSource}
                                        """;

    private IReadOnlyCollection<Token> _tokens = [];
    [Params(SmallSource, MediumSource, LargeSource)]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    // ReSharper disable once UnassignedField.Global
    public string Source;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    [GlobalSetup]
    public void Setup()
    {
        _tokens = [..Tokenizer.Tokenize(Source)];
    }

    [Benchmark]
    public void BenchmarkParse()
    {
        Parser.Parse(_tokens);
    }
}