using System;
using System.Diagnostics;
using BenchmarkDotNet.Running;

namespace Fluid.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            // Steady-state loop for sampling profilers (for instance `ultra profile -- Fluid.Benchmarks.exe profile render 25`).
            // BenchmarkDotNet spawns short-lived child processes, which a profiler can't follow.
            if (args.Length > 0 && args[0].Equals("profile", StringComparison.OrdinalIgnoreCase))
            {
                RunProfileLoop(args);
                return;
            }

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }

        static void RunProfileLoop(string[] args)
        {
            var what = args.Length > 1 ? args[1] : "render";
            var seconds = args.Length > 2 ? int.Parse(args[2]) : 25;

            var benchmark = new FluidBenchmarks();

            Func<object> work = what.ToLowerInvariant() switch
            {
                "render" => () => benchmark.Render(),
                "parse" => () => benchmark.Parse(),
                "parsebig" => () => benchmark.ParseBig(),
                "parseandrender" => () => benchmark.ParseAndRender(),
                _ => throw new ArgumentException("Unknown profile workload: " + what)
            };

            // Warm up so the capture isn't dominated by tiered JIT.
            for (var i = 0; i < 200; i++)
            {
                if (work() is null) throw new InvalidOperationException("null result");
            }

            var sw = Stopwatch.StartNew();
            long iterations = 0;
            while (sw.Elapsed.TotalSeconds < seconds)
            {
                for (var i = 0; i < 50; i++)
                {
                    if (work() is null) throw new InvalidOperationException("null result");
                }

                iterations += 50;
            }

            Console.Error.WriteLine($"{what}: {iterations} iterations in {sw.Elapsed.TotalSeconds:F1}s ({iterations / sw.Elapsed.TotalSeconds:F0} ops/s)");
        }
    }
}
