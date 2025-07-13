using BenchmarkDotNet.Running;
using Reef.Core.Benchmarks;

BenchmarkRunner.Run<ParserBenchmarks>();
// BenchmarkRunner.Run<TokenizerBenchmarks>();

/*
 * | Method         | Source                 | Mean         | Error       | StdDev      | Gen0    | Gen1   | Allocated |
   |--------------- |----------------------- |-------------:|------------:|------------:|--------:|-------:|----------:|
   | BenchmarkParse | if ((...)}\r\n} [208]  |   9,011.6 ns |   103.58 ns |    91.82 ns |  1.2665 | 0.0458 |  10.45 KB |
   | BenchmarkParse | if ((...)}\r\n} [5458] | 231,946.8 ns | 2,138.76 ns | 1,895.95 ns | 32.4707 | 0.2441 | 266.33 KB |
   | BenchmarkParse | var a = 2;             |     570.3 ns |     9.22 ns |     8.17 ns |  0.1411 |      - |   1.16 KB |

   | Method         | Source                 | Mean         | Error     | StdDev    | Gen0    | Gen1   | Allocated |
   |--------------- |----------------------- |-------------:|----------:|----------:|--------:|-------:|----------:|
   | BenchmarkParse | if ((...)}\r\n} [208]  |   4,752.0 ns |  37.12 ns |  32.91 ns |  0.4959 |      - |    4176 B |
   | BenchmarkParse | if ((...)}\r\n} [5458] | 120,861.7 ns | 680.21 ns | 636.27 ns | 11.9629 | 3.0518 |  100432 B |
   | BenchmarkParse | var a = 2;             |     360.7 ns |   5.18 ns |   4.85 ns |  0.0715 |      - |     600 B |

   | Method         | Source                 | Mean         | Error       | StdDev      | Gen0    | Gen1    | Allocated |
   |--------------- |----------------------- |-------------:|------------:|------------:|--------:|--------:|----------:|
   | BenchmarkParse | pub (...);\r\n} [1020] |  15,547.0 ns |   145.15 ns |   128.67 ns |  2.0752 |  0.0916 |   17408 B |
   | BenchmarkParse | pub(...)\r\n} [26570]  | 419,857.0 ns | 1,628.07 ns | 1,271.09 ns | 59.0820 | 41.0156 |  494280 B |
   | BenchmarkParse | var a = 2;             |     353.6 ns |     4.53 ns |     4.01 ns |  0.0715 |       - |     600 B |

   | Method         | Source                 | Mean         | Error       | StdDev      | Gen0    | Gen1   | Allocated |
   |--------------- |----------------------- |-------------:|------------:|------------:|--------:|-------:|----------:|
   | BenchmarkParse | pub (...);\r\n} [1020] |  12,790.3 ns |    93.48 ns |    87.44 ns |  1.7242 | 0.0763 |   14536 B |
   | BenchmarkParse | pub(...)\r\n} [26570]  | 347,622.5 ns | 2,335.81 ns | 2,070.64 ns | 43.9453 | 1.9531 |  371248 B |
   | BenchmarkParse | var a = 2;             |     315.8 ns |     6.14 ns |     4.79 ns |  0.0715 |      - |     600 B |
*/