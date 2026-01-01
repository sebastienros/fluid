using System;
using BenchmarkDotNet.Running;

namespace Fluid.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            // var benchmark = new FluidBenchmarks();
            // for (var i = 0; i < 100000 ;i++)
            // {
            //     var x = benchmark.Render();

            //     ArgumentNullException.ThrowIfNullOrEmpty(x);
            // }

             BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
