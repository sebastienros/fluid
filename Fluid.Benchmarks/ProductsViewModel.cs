using System.Collections.Generic;

namespace Fluid.Benchmarks;

/// <summary>
/// The model of the product template. Creating a <see cref="TemplateContext"/> with it and custom options lets the source
/// generator write its accessor, and <see cref="BenchmarkTemplateOptionsBuilder"/> registers the products' one.
/// </summary>
public sealed class ProductsViewModel
{
    public ProductsViewModel(List<Product> products)
    {
        Products = products;
    }

    public List<Product> Products { get; }
}
