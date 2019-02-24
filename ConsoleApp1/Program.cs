using Fluid.Benchmarks;
using System;
using System.Diagnostics;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            var benchmark = new FluidBenchmarks();

            for (var i = 0; i < 10; i++)
            {
                benchmark.Parse();
            }

            var sw = new Stopwatch();

            for (var iteration = 0; iteration < 1; iteration++)
            {
                sw.Restart();

                Console.ReadLine();
                for (var i = 0; i < 10; i++)
                {
                    benchmark.Parse();
                }

                Console.WriteLine(sw.ElapsedMilliseconds);
                Console.ReadLine();
            }
        }
    }
}
