using System;
using System.Collections.Generic;
using System.IO;

namespace Fluid.Benchmarks
{
    public abstract class BaseBenchmarks
    {
        protected List<Product> Products = new(ProductCount);

        protected const int ProductCount = 100;

        protected const string Lorem = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum";

        protected readonly static string ProductTemplate;
        protected readonly static string ProductTemplateMustache;
        protected readonly static string BlogPostTemplate;

        static BaseBenchmarks()
        {
            var assembly = typeof(BaseBenchmarks).Assembly;

            using (var stream = assembly.GetManifestResourceStream("Fluid.Benchmarks.product.liquid"))
            {
                using var streamReader = new StreamReader(stream);
                ProductTemplate = streamReader.ReadToEnd();
            }

            using (var stream = assembly.GetManifestResourceStream("Fluid.Benchmarks.product.mustache"))
            {
                using var streamReader = new StreamReader(stream);
                ProductTemplateMustache = streamReader.ReadToEnd();
            }

            using (var stream = assembly.GetManifestResourceStream("Fluid.Benchmarks.blogpost.liquid"))
            {
                using var streamReader = new StreamReader(stream);
                BlogPostTemplate = streamReader.ReadToEnd();
            }
        }

        public BaseBenchmarks()
        {
            for (int i = 0; i < ProductCount; i++)
            {
                var product = new Product("Name" + i, i, Lorem);
                Products.Add(product);
            }
        }

        public void CheckBenchmark()
        {
            var result = ParseAndRender();
            if (string.IsNullOrEmpty(result) ||
                !result.Contains("<h2>Name0</h2>") ||
                !result.Contains($"<h2>Name{ProductCount - 1}</h2>") ||
                !result.Contains($"Lorem ipsum ...") ||
                !result.Contains($"Only 0") ||
                !result.Contains($"Only {ProductCount - 1}"))
            {
                throw new InvalidOperationException($"Template rendering failed: \n {result}");
            }
        }

        public abstract object Parse();
        public abstract object ParseBig();
        public abstract string Render();
        public abstract string ParseAndRender();

    }
}
