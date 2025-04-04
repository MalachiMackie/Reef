using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace NewLang.Core.Benchmarks;

[MemoryDiagnoser]
public class ExpressionTreeBenchmarks
{
    [Params(SmallSource, MediumSource, LargeSource)]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    // ReSharper disable once UnassignedField.Global
    public string Source;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

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