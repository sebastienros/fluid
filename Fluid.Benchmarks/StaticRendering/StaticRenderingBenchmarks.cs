using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Fluid.Benchmarks.StaticRendering.Slices;
using Lissic;
using Lissic.Parsing;
using RazorSlices;

namespace Fluid.Benchmarks.StaticRendering;

// Compares the engines that compile a template into the application with Fluid rendering a template parsed at
// runtime. They all render the product template with the same data, and encode the values for HTML.
// The "String" group renders to a string, the "Utf8" group to a reused UTF-8 buffer, like an HTTP response body.
[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), CategoriesColumn]
public class StaticRenderingBenchmarks : BaseBenchmarks
{
    private readonly TemplateOptions _options = new TemplateOptionsBuilder()
        .WithOutputBufferSize(40 * 1024)
        .WithModelNamesComparer(StringComparers.CamelCase)
        .Build();

    private readonly IFluidTemplate _fluidTemplate;
    private readonly IFluidTemplate _sourceGeneratedTemplate = SourceGeneratedTemplates.Product;

    private readonly LissicProcessor _lissicProcessor;
    private readonly LissicProductList _lissicPrebuiltModel;

    private readonly BufferPipeWriter _buffer = new();

    public StaticRenderingBenchmarks()
    {
        _fluidTemplate = new FluidParser().Parse(ProductTemplate);

        if (!LissicConfigurator.IsInitialized)
        {
            LissicConfigurator.SetOptions(new LissicOptions());
        }

        Lissic_FilterRegistration.Register();
        var templateCache = new TemplateCache();
        templateCache.Initialize(new TemplateManifest()).GetAwaiter().GetResult();

        // Not thread-safe, so a web application uses one per request. A benchmark only uses one thread.
        _lissicProcessor = new LissicProcessor(templateCache);
        _lissicPrebuiltModel = CreateLissicModel();
    }

    public override object Parse() => null;
    public override object ParseBig() => null;
    public override string Render() => Fluid_Render();
    public override string ParseAndRender() => Fluid_Render();

    [GlobalSetup]
    public void CheckOutputs()
    {
        CheckBenchmark();

        // Engines handle whitespace around their tags differently, so only the rest has to be identical.
        var expected = Normalize(Fluid_Render());

        Check(nameof(Fluid_SourceGenerated_Render), Fluid_SourceGenerated_Render());
        Check(nameof(RazorSlices_Render), RazorSlices_Render());
        Check(nameof(Lissic_Render), Lissic_Render());
        Check(nameof(HandWritten_Render), HandWritten_Render());

        Fluid_RenderUtf8();
        Check(nameof(Fluid_RenderUtf8), Encoding.UTF8.GetString(_buffer.WrittenSpan));
        Fluid_SourceGenerated_RenderUtf8();
        Check(nameof(Fluid_SourceGenerated_RenderUtf8), Encoding.UTF8.GetString(_buffer.WrittenSpan));
        RazorSlices_RenderUtf8();
        Check(nameof(RazorSlices_RenderUtf8), Encoding.UTF8.GetString(_buffer.WrittenSpan));
        Lissic_RenderUtf8();
        Check(nameof(Lissic_RenderUtf8), Encoding.UTF8.GetString(_buffer.WrittenSpan));
        Lissic_PrebuiltModel_RenderUtf8();
        Check(nameof(Lissic_PrebuiltModel_RenderUtf8), Encoding.UTF8.GetString(_buffer.WrittenSpan));

        void Check(string name, string result)
        {
            if (Normalize(result) != expected)
            {
                throw new InvalidOperationException($"{name} doesn't render the same output as Fluid:\n{result}");
            }
        }

        static string Normalize(string s) => Regex.Replace(s, @"\s+", " ").Trim();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("String")]
    public string Fluid_Render()
    {
        var context = new TemplateContext(_options).SetValue("products", Products);
        return _fluidTemplate.Render(context, HtmlEncoder.Default);
    }

    [Benchmark, BenchmarkCategory("String")]
    public string Fluid_SourceGenerated_Render()
    {
        var context = new TemplateContext(_options).SetValue("products", Products);
        return _sourceGeneratedTemplate.Render(context, HtmlEncoder.Default);
    }

    [Benchmark, BenchmarkCategory("String")]
    public string RazorSlices_Render()
    {
        var slice = ProductList.Create(Products);
        return slice.RenderAsync(HtmlEncoder.Default).GetAwaiter().GetResult();
    }

    [Benchmark, BenchmarkCategory("String")]
    public string Lissic_Render()
    {
        return _lissicProcessor.RenderToString(CreateLissicModel(), 16 * 1024);
    }

    // What the template compiles to with no engine at all, for reference.
    [Benchmark, BenchmarkCategory("String")]
    public string HandWritten_Render()
    {
        var encoder = HtmlEncoder.Default;
        var writer = new StringWriter(new StringBuilder(9 * 1024), CultureInfo.InvariantCulture);

        writer.Write("<ul id='products'>\n");
        foreach (var product in Products)
        {
            writer.Write("    <li>\n      <h2>");
            encoder.Encode(writer, product.Name);
            writer.Write("</h2>\n           Only ");
            writer.Write(product.Price);
            writer.Write("\n           ");
            encoder.Encode(writer, StaticRenderingHelpers.Truncate(product.Description, 15));
            writer.Write("\n    </li>\n");
        }
        writer.Write("</ul>\n");

        return writer.ToString();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Utf8")]
    public void Fluid_RenderUtf8()
    {
        _buffer.Reset();
        var context = new TemplateContext(_options).SetValue("products", Products);
        _fluidTemplate.RenderAsync(_buffer, HtmlEncoder.Default, context).GetAwaiter().GetResult();
    }

    [Benchmark, BenchmarkCategory("Utf8")]
    public void Fluid_SourceGenerated_RenderUtf8()
    {
        _buffer.Reset();
        var context = new TemplateContext(_options).SetValue("products", Products);
        _sourceGeneratedTemplate.RenderAsync(_buffer, HtmlEncoder.Default, context).GetAwaiter().GetResult();
    }

    [Benchmark, BenchmarkCategory("Utf8")]
    public void RazorSlices_RenderUtf8()
    {
        _buffer.Reset();
        var slice = ProductList.Create(Products);
        slice.RenderAsync(_buffer, HtmlEncoder.Default).GetAwaiter().GetResult();
    }

    [Benchmark, BenchmarkCategory("Utf8")]
    public void Lissic_RenderUtf8()
    {
        _buffer.Reset();
        _lissicProcessor.RenderToBuffer(CreateLissicModel(), _buffer);
    }

    // The model built once and reused for every render, which is how Lissic renders without allocating.
    [Benchmark, BenchmarkCategory("Utf8")]
    public void Lissic_PrebuiltModel_RenderUtf8()
    {
        _buffer.Reset();
        _lissicProcessor.RenderToBuffer(_lissicPrebuiltModel, _buffer);
    }

    // Lissic renders its own types: the products are copied into templates, their text converted to UTF-8.
    private LissicProductList CreateLissicModel()
    {
        var products = new List<LissicTemplate>(Products.Count);
        foreach (var product in Products)
        {
            products.Add(new LissicProduct
            {
                name = product.Name,
                price = product.Price,
                description = product.Description
            });
        }

        return new LissicProductList { products = products };
    }
}
