using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Fluid.Benchmarks
{
    /// <summary>
    /// Rendering shapes that the product-catalog benchmark doesn't reach: heterogeneous collections,
    /// deep scope chains, scopes wider than the inline slots, member names that need case conversion,
    /// and numbers that can't be interned. These are the cases where per-call-site caching and the
    /// inline scope slots are expected to cost rather than pay, so they belong next to the happy path.
    /// </summary>
    [MemoryDiagnoser]
    public class RenderScenarioBenchmarks
    {
        private const int ItemCount = 100;

        private readonly FluidParser _parser = new FluidParser();

        private readonly TemplateOptions _ordinalOptions = new TemplateOptions();
        private readonly TemplateOptions _camelCaseOptions = new TemplateOptions();
        private readonly TemplateOptions _partialOptions = new TemplateOptions();

        private readonly IFluidTemplate _polymorphicTemplate;
        private readonly IFluidTemplate _monomorphicTemplate;
        private readonly IFluidTemplate _deepScopeTemplate;
        private readonly IFluidTemplate _nestedLoopTemplate;
        private readonly IFluidTemplate _wideScopeTemplate;
        private readonly IFluidTemplate _pascalCaseTemplate;
        private readonly IFluidTemplate _decimalPriceTemplate;
        private readonly IFluidTemplate _renderScopeTemplate;
        private readonly IFluidTemplate _renderArgumentsTemplate;
        private readonly IFluidTemplate _includeScopeTemplate;

        private readonly List<object> _mixedItems = new(ItemCount);
        private readonly List<object> _uniformItems = new(ItemCount);
        private readonly List<PricedProduct> _pricedProducts = new(ItemCount);
        private readonly List<int> _outer = new();
        private readonly List<int> _inner = new();

        public sealed class Alpha { public string Name { get; set; } }
        public sealed class Beta { public string Name { get; set; } }
        public sealed class Gamma { public string Name { get; set; } }
        public sealed class Delta { public string Name { get; set; } }

        public sealed class PricedProduct
        {
            public string Name { get; set; }
            public decimal Price { get; set; }
        }

        public RenderScenarioBenchmarks()
        {
            _camelCaseOptions.ModelNamesComparer = StringComparers.CamelCase;

            for (var i = 0; i < ItemCount; i++)
            {
                // Four runtime types in rotation: one call site, many shapes.
                _mixedItems.Add((i % 4) switch
                {
                    0 => new Alpha { Name = "N" + i },
                    1 => new Beta { Name = "N" + i },
                    2 => new Gamma { Name = "N" + i },
                    _ => new Delta { Name = "N" + i },
                });

                _uniformItems.Add(new Alpha { Name = "N" + i });

                // Prices with a fractional part are never interned, unlike whole-number prices.
                _pricedProducts.Add(new PricedProduct { Name = "N" + i, Price = 19.99m + i });
            }

            for (var i = 0; i < 10; i++)
            {
                _outer.Add(i);
                _inner.Add(i);
            }

            _parser.TryParse("{% for item in items %}{{ item.Name }}{% endfor %}", out _polymorphicTemplate);
            _parser.TryParse("{% for item in items %}{{ item.Name }}{% endfor %}", out _monomorphicTemplate);

            // Six nested loops, then a read that misses at every level before resolving at the root.
            _parser.TryParse(
                "{% for a in outer %}{% for b in outer %}{% for c in outer %}" +
                "{{ root }}" +
                "{% endfor %}{% endfor %}{% endfor %}",
                out _deepScopeTemplate);

            // parentloop puts a third name in the loop scope.
            _parser.TryParse(
                "{% for a in outer %}{% for b in inner %}{{ forloop.parentloop.index }}{{ b }}{% endfor %}{% endfor %}",
                out _nestedLoopTemplate);

            // More names in one scope than the inline slots hold, forcing the dictionary path.
            _parser.TryParse(
                "{% assign a1 = 1 %}{% assign a2 = 2 %}{% assign a3 = 3 %}{% assign a4 = 4 %}" +
                "{% assign a5 = 5 %}{% assign a6 = 6 %}" +
                "{% for i in outer %}{{ a1 }}{{ a2 }}{{ a3 }}{{ a4 }}{{ a5 }}{{ a6 }}{% endfor %}",
                out _wideScopeTemplate);

            // PascalCase names under a camel-case comparer: every comparison has to convert.
            _parser.TryParse("{% for item in Items %}{{ item.Name }}{% endfor %}", out _pascalCaseTemplate);

            _parser.TryParse("{% for p in products %}{{ p.Price }}{% endfor %}", out _decimalPriceTemplate);

            var partialBytes = Encoding.UTF8.GetBytes("{{ value }}{{ root }}{{ outer }}");
            _partialOptions.FileProvider = new DelegateTemplateFileProvider(
                (_, _, _) => new ValueTask<TemplateSourceInfo>(new TemplateSourceInfo(
                    DateTimeOffset.UnixEpoch,
                    _ => new ValueTask<Stream>(new MemoryStream(partialBytes, writable: false)))));
            _parser.TryParse("{% assign outer = 'hidden' %}{% render 'partial', value: root %}", out _renderScopeTemplate);
            _parser.TryParse("{% render 'partial', value: root, outer: root %}", out _renderArgumentsTemplate);
            _parser.TryParse("{% assign outer = 'hidden' %}{% include 'partial', value: root %}", out _includeScopeTemplate);

            CheckAll();
        }

        private void CheckAll()
        {
            Verify(nameof(PolymorphicMembers), PolymorphicMembers(), "N0");
            Verify(nameof(MonomorphicMembers), MonomorphicMembers(), "N0");
            Verify(nameof(DeepScopeChain), DeepScopeChain(), "R");
            Verify(nameof(NestedLoopsWithParentLoop), NestedLoopsWithParentLoop(), "1");
            Verify(nameof(WideScope), WideScope(), "123456");
            Verify(nameof(PascalCaseMemberNames), PascalCaseMemberNames(), "N0");
            Verify(nameof(NonInternedNumbers), NonInternedNumbers(), "19.99");
            Verify(nameof(IsolatedRenderScope), IsolatedRenderScope(), "RR");
            Verify(nameof(RenderWithArguments), RenderWithArguments(), "RRR");
            Verify(nameof(WriteThroughIncludeScope), WriteThroughIncludeScope(), "RRhidden");

            static void Verify(string name, string result, string expected)
            {
                if (string.IsNullOrEmpty(result) || !result.Contains(expected))
                {
                    throw new InvalidOperationException($"{name} rendering failed: {result}");
                }
            }
        }

        /// <summary>One call site resolving members across four runtime types.</summary>
        [Benchmark]
        public string PolymorphicMembers()
        {
            var context = new TemplateContext(_ordinalOptions).SetValue("items", _mixedItems);
            return _polymorphicTemplate.Render(context);
        }

        /// <summary>The same template over a single runtime type, as the comparison point.</summary>
        [Benchmark]
        public string MonomorphicMembers()
        {
            var context = new TemplateContext(_ordinalOptions).SetValue("items", _uniformItems);
            return _monomorphicTemplate.Render(context);
        }

        /// <summary>A name that misses in every nested loop scope before resolving at the root.</summary>
        [Benchmark]
        public string DeepScopeChain()
        {
            var context = new TemplateContext(_ordinalOptions)
                .SetValue("outer", _outer)
                .SetValue("root", "R");
            return _deepScopeTemplate.Render(context);
        }

        /// <summary>Nested loops, so the loop scope also holds parentloop.</summary>
        [Benchmark]
        public string NestedLoopsWithParentLoop()
        {
            var context = new TemplateContext(_ordinalOptions)
                .SetValue("outer", _outer)
                .SetValue("inner", _inner);
            return _nestedLoopTemplate.Render(context);
        }

        /// <summary>More names in a scope than the inline slots hold.</summary>
        [Benchmark]
        public string WideScope()
        {
            var context = new TemplateContext(_ordinalOptions).SetValue("outer", _outer);
            return _wideScopeTemplate.Render(context);
        }

        /// <summary>PascalCase names resolved through the camel-case comparer.</summary>
        [Benchmark]
        public string PascalCaseMemberNames()
        {
            var context = new TemplateContext(_camelCaseOptions).SetValue("Items", _uniformItems);
            return _pascalCaseTemplate.Render(context);
        }

        /// <summary>Prices with a fractional part, which the small-integer interning never covers.</summary>
        [Benchmark]
        public string NonInternedNumbers()
        {
            var context = new TemplateContext(_ordinalOptions).SetValue("products", _pricedProducts);
            return _decimalPriceTemplate.Render(context);
        }

        /// <summary>An isolated partial render with a caller expression and named argument.</summary>
        [Benchmark]
        public string IsolatedRenderScope()
        {
            var context = new TemplateContext(_partialOptions).SetValue("root", "R");
            return _renderScopeTemplate.Render(context);
        }

        /// <summary>An isolated partial render with two named arguments.</summary>
        [Benchmark]
        public string RenderWithArguments()
        {
            var context = new TemplateContext(_partialOptions).SetValue("root", "R");
            return _renderArgumentsTemplate.Render(context);
        }

        /// <summary>A write-through include scope with a temporary named argument.</summary>
        [Benchmark]
        public string WriteThroughIncludeScope()
        {
            var context = new TemplateContext(_partialOptions).SetValue("root", "R");
            return _includeScopeTemplate.Render(context);
        }
    }
}
