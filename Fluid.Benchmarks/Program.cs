using BenchmarkDotNet.Running;
using System;
using System.Diagnostics;

namespace Fluid.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
             //BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

            var benchmark = new FluidBenchmarks();
            for (var i = 0; i < 100; i++)
            {
                benchmark.Render();
            }
        }
    }
}
