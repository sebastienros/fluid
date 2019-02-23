using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class FastBenchmarks
    {
        private FluidTemplate _fluidTemplate;

        protected const string TextTemplateDotLiquid = @"
<ul id='products'>
  {% for product in products %}
    <li>
      <h2>{{ product.name }}</h2>
           Only {{ product.price }}
           {{ product.description | truncate: 15 }}
    </li>
  {% endfor %}
</ul>
";
        public class Product
        {
            public Product(string name, float price, string description)
            {
                Name = name;
                Price = price;
                Description = description;
            }

            public string Name { get; set; }

            public float Price { get; set; }

            public string Description { get; set; }
        }

        protected List<Product> _products = new List<Product>(ProductCount);
        const int ProductCount = 500;
        private const string Lorem = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum";

        public FastBenchmarks()
        {
            FluidTemplate.TryParse(TextTemplateDotLiquid, out _fluidTemplate, out var messages);

            for (int i = 0; i < ProductCount; i++)
            {
                var product = new Product("Name" + i, i, Lorem);
                _products.Add(product);
            }
        }

        //[Benchmark(Description = "Fluid - Parser")]
        //public FluidTemplate Parse()
        //{
        //    if (!Fluid.FluidTemplate.TryParse(TextTemplateDotLiquid, false, out var template, out var errors))
        //    {
        //        throw new InvalidOperationException("Fluid template not parsed");
        //    }

        //    return template;
        //}

        [Benchmark(Description = "Fluid - Renderer")]
        public Task<string> Render()
        {
            var templateContext = new TemplateContext();
            templateContext.SetValue("products", _products);
            return _fluidTemplate.RenderAsync(templateContext, NullEncoder.Default);
        }
    }
}
