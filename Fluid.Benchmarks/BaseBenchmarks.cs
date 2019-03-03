using System.Collections.Generic;

namespace Fluid.Benchmarks
{
    public abstract class BaseBenchmarks
    {
        protected List<Product> Products = new List<Product>(ProductCount);

        protected const int ProductCount = 500;

        protected const string Lorem = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum";

        protected const string TextTemplate = @"
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

        public BaseBenchmarks()
        {
            for (int i = 0; i < ProductCount; i++)
            {
                var product = new Product("Name" + i, i, Lorem);
                Products.Add(product);
            }
        }

        public abstract object Parse();

        public abstract string Render();

        public abstract string ParseAndRender();

    }
}
