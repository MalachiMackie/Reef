using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace NewLang.Core.Benchmarks;

[MemoryDiagnoser]
public class ExpressionTreeBenchmarks
{
    [Params(SmallSource, MediumSource, LargeSource)]
    public string Source;

    private IReadOnlyCollection<Token> _tokens = [];

    private readonly Consumer _consumer = new();

    [GlobalSetup]
    public void Setup()
    {
        _tokens = new Parser().Parse(Source).ToArray();
    }
    
    [Benchmark]
    public void BenchmarkExpressionTree()
    {
        ExpressionTreeBuilder.Build(_tokens).Consume(_consumer);
    }
    

    private const string SmallSource = "var a = 2;";

    private const string MediumSource = """
                                        if (a) {
                                            if (b > 2 < 4) {
                                                var c = a + b * d / 2;
                                            } else {
                                                var d = "hi";
                                            }
                                        }
                                        if (a) {
                                            if (b > 2) {
                                                var c = a + b;
                                            } else {
                                                var d = "hi";
                                            }
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
}