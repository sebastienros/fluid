using BenchmarkDotNet.Running;
using System;

namespace Fluid.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            //BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

            Console.WriteLine(new DotLiquidBenchmarks().ParseAndRender());
        }
    }
}
