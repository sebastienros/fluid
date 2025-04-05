using BenchmarkDotNet.Running;

namespace Fluid.Benchmarks
{
    // main
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
