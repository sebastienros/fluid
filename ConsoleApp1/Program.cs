using Fluid.Benchmarks;
using System;
using System.Diagnostics;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            var benchmark = new FastBenchmarks();

            for (var i = 0; i < 1024; i++)
            {
                benchmark.Render();
            }

            var sw = new Stopwatch();

            for (var iteration = 0; iteration < 3; iteration++)
            {
                sw.Restart();

                for (var i = 0; i < 1024; i++)
                {
                    benchmark.Render();
                }

                Console.WriteLine(sw.ElapsedMilliseconds);
            }
        }
    }
}
