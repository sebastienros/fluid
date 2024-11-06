using BenchmarkDotNet.Attributes;
using Fluid.Filters;
using System.Text;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser, ShortRunJob]
    public class HashingBenchmarks
    {
        private readonly string _value = "this is some text to hash";

        [Benchmark]
        public string HashSha256ToHex()
        {
            var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(_value));
            return MiscFilters.ToHexLower(hash);
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
                builder.Append(b.ToString("x2").ToLowerInvariant());
            }

            return builder.ToString();
        }
    }
}