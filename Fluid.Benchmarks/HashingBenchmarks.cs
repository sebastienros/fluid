using BenchmarkDotNet.Attributes;
using System.Text;

namespace Fluid.Benchmarks
{
    // | Method                  | Mean     | Error     | StdDev   | Gen0   | Allocated |
    // |------------------------ |---------:|----------:|---------:|-------:|----------:|
    // | HashSha256ToHex         | 213.7 ns |  48.62 ns |  2.67 ns | 0.0279 |     264 B |
    // | HashSha256StringBuilder | 666.4 ns | 369.69 ns | 20.26 ns | 0.1774 |    1672 B |

    [MemoryDiagnoser, ShortRunJob]
    public class HashingBenchmarks
    {
        private readonly string _value = "this is some text to hash";

        [Benchmark]
        public string HashSha256ToHex()
        {
            var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(_value));
            return Fluid.Utils.HexUtilities.ToHexLower(hash);
        }

        [Benchmark]
        public string HashSha256StringBuilder()
        {
            using var provider = System.Security.Cryptography.SHA256.Create();
            var builder = new StringBuilder(64);
#pragma warning disable CA1850 // Prefer static 'System.Security.Cryptography.MD5.HashData' method over 'ComputeHash'
            foreach (var b in provider.ComputeHash(Encoding.UTF8.GetBytes(_value)))
#pragma warning restore CA1850
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
