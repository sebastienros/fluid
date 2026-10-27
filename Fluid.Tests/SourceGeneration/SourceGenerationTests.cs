using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Parser;
using Fluid.SourceGeneration;
using Fluid.Tests.Mocks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Fluid.Tests;

public class SourceGenerationTests
{
    [Theory]
    [InlineData("Hello", "Hello")]
    [InlineData("{{ 1 }}", "1")]
    [InlineData("{{ '' }}{{ 'a' }}{{ 'hello' }}", "ahello")]
    [InlineData("{{ ' Hello \n\t World!  ' | squish }}", "Hello World!")]
    [InlineData("{% assign x = 1 %}{{ x }}", "1")]
    [InlineData("{% if true %}a{% else %}b{% endif %}", "a")]
    [InlineData("{% comment %}{% comment %}{% endcomment %}{% raw %}{% endcomment %}{% endraw %}{% endcomment %}after", "after")]
    [InlineData("{% unless false %}a{% endunless %}", "a")]
    [InlineData("{% unless true %}a{% elsif true %}b{% else %}c{% endunless %}", "b")]
    [InlineData("{% for i in (1..3) %}{{ i }}{% endfor %}", "123")]
    [InlineData("{% for i in (1..3) %}{% if i == 2 %}{% break %}{% endif %}{{ i }}{% endfor %}", "1")]
    [InlineData("{% capture x %}hi{% endcapture %}{{ x }}", "hi")]
    [InlineData("{% cycle 'a','b' %}{% cycle 'a','b' %}{% cycle 'a','b' %}", "aba")]
    [InlineData("{% increment x %}{% increment x %}{% decrement x %}", "011")]
    [InlineData("{% increment x %}{{ x }}", "01")]
    [InlineData("{% case 2 %}{% when 1 %}a{% when 2 %}b{% else %}c{% endcase %}", "b")]
    [InlineData("{% assign s = 'hello' %}{{ s.first }}{{ s.last }}[{{ s['first'] }}]", "ho[]")]
    [InlineData("{% if 2 > 1 and 2 >= 2 and 1 < 2 and 2 <= 2 %}yes{% endif %}", "yes")]
    [InlineData("{% if '' == empty and '' == blank %}yes{% endif %}", "yes")]
    [InlineData("{% if 'x' contains nil %}yes{% else %}no{% endif %}", "no")]
    [InlineData("{{ 'a' == 'b' }}|{{ 'b' != 'a' }}|{{ 'abc' contains 'b' }}|{{ 'abc' startswith 'a' }}|{{ 'abc' endswith 'c' }}", "a|b|abc|abc|abc")]
    [InlineData("{% capture x %}&{% endcapture %}{{ x }}", "&")]
    [InlineData("{% capture x %}{% increment a %}{% endcapture %}[{{ x }}]{% increment a %}", "[0]1")]
    [InlineData("{% capture x %}{% cycle 'a', 'b' %}{% endcapture %}[{{ x }}]", "[a]")]
    [InlineData("{% capture x %}{% assign y = 2 %}{% endcapture %}[{{ x }}{{ y }}]", "[2]")]
    [InlineData("{% capture x %}  {% endcapture %}[{{ x }}]", "[  ]")]
    [InlineData("{% capture x %}{% endcapture %}[{{ x }}]", "[]")]
    [InlineData("{% for i in (1..3) %}{% capture x %}{% break %}{% endcapture %}{{ i }}{% endfor %}", "")]
    [InlineData("{% if true %} {% increment a %} {% endif %}", " 0 ")]
    [InlineData("{{ 'a' | json }}", "&quot;a&quot;")]
    [InlineData("{% macro hello() %}hi{% endmacro %}{{ hello() }}", "hi")]
    [InlineData("{% assign (a.b)[0]-c = 'x' %}{% capture (d)(e) %}y{% endcapture %}{{ ['(a.b)[0]-c'] }}{{ ['(d)(e)'] }}", "xy")]
    public async Task GeneratedTemplate_MatchesRuntime(string liquid, string expected)
    {
        var parser = new FluidParser(new FluidParserOptions { AllowFunctions = true });
        var template = parser.Parse(liquid);

        var source = template.Compile(new SourceGenerationOptions
        {
            Namespace = "Fluid.Tests.Generated",
            ClassName = "T" + Guid.NewGuid().ToString("N")
        });

        var generated = CompileToAssembly(source.SourceCode);
        var type = generated.GetType(source.FullTypeName, throwOnError: true);

        var instance = (IFluidTemplate)Activator.CreateInstance(type, nonPublic: true);

        var runtimeWriter = new StringWriter();
        await template.RenderAsync(runtimeWriter, HtmlEncoder.Default, new TemplateContext());

        var generatedWriter = new StringWriter();
        await instance.RenderAsync(generatedWriter, HtmlEncoder.Default, new TemplateContext());

        Assert.Equal(expected, runtimeWriter.ToString());
        Assert.Equal(expected, generatedWriter.ToString());
    }

    [Fact]
    public async Task GeneratedTemplate_StrictVariables_ThrowsOnMissingVariable()
    {
        var parser = new FluidParser();
        var template = parser.Parse("{{ missing }}");

        var source = template.Compile(new SourceGenerationOptions
        {
            Namespace = "Fluid.Tests.Generated",
            ClassName = "T" + Guid.NewGuid().ToString("N")
        });

        var generated = CompileToAssembly(source.SourceCode);
        var type = generated.GetType(source.FullTypeName, throwOnError: true);
        var instance = (IFluidTemplate)Activator.CreateInstance(type, nonPublic: true);
        var context = new TemplateContext(new TemplateOptionsBuilder().WithStrictVariables(true).Build());

        var ex = await Assert.ThrowsAsync<FluidException>(() => instance.RenderAsync(new StringWriter(), HtmlEncoder.Default, context).AsTask());

        Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratedTemplate_FlushesOutput()
    {
        var parser = new FluidParser();
        var template = parser.Parse("Hello");
        var source = template.Compile(new SourceGenerationOptions
        {
            Namespace = "Fluid.Tests.Generated",
            ClassName = "T" + Guid.NewGuid().ToString("N")
        });

        var generated = CompileToAssembly(source.SourceCode);
        var type = generated.GetType(source.FullTypeName, throwOnError: true);
        var instance = (IFluidTemplate)Activator.CreateInstance(type, nonPublic: true);
        var output = new FlushTrackingOutput();

        await instance.RenderAsync(output, HtmlEncoder.Default, new TemplateContext());

        Assert.True(output.Flushed);
    }

    [Fact]
    public async Task GeneratedTemplate_EnforcesMaxOutputSize()
    {
        var parser = new FluidParser();
        var template = parser.Parse("12345");
        var source = template.Compile(new SourceGenerationOptions
        {
            Namespace = "Fluid.Tests.Generated",
            ClassName = "T" + Guid.NewGuid().ToString("N")
        });

        var generated = CompileToAssembly(source.SourceCode);
        var type = generated.GetType(source.FullTypeName, throwOnError: true);
        var instance = (IFluidTemplate)Activator.CreateInstance(type, nonPublic: true);
        var context = new TemplateContext { MaxOutputSize = 4 };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => instance.RenderAsync(new FlushTrackingOutput(), HtmlEncoder.Default, context).AsTask());
    }

    [Fact]
    public async Task GeneratedTemplate_AppliesTrimming()
    {
        var parser = new FluidParser();
        var template = parser.Parse("Hello {{ 'name' }} !");
        var source = template.Compile(new SourceGenerationOptions
        {
            Namespace = "Fluid.Tests.Generated",
            ClassName = "T" + Guid.NewGuid().ToString("N")
        });

        var generated = CompileToAssembly(source.SourceCode);
        var type = generated.GetType(source.FullTypeName, throwOnError: true);
        var instance = (IFluidTemplate)Activator.CreateInstance(type, nonPublic: true);
        var context = new TemplateContext(new TemplateOptionsBuilder().WithTrimming(TrimmingFlags.OutputLeft).Build());

        var output = new StringWriter();
        await instance.RenderAsync(output, HtmlEncoder.Default, context);

        Assert.Equal("Helloname !", output.ToString());
    }

    [Fact]
    public void Compile_Fails_WhenStatementNotSourceable()
    {
        var template = new FluidTemplate(new CustomStatement());

        var ex = Assert.Throws<SourceGenerationException>(() => template.Compile(new SourceGenerationOptions
        {
            Namespace = "Fluid.Tests.Generated",
            ClassName = "Incompatible"
        }));

        Assert.Contains(nameof(CustomStatement), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_Compiles_Subtemplate_FromFileProvider()
    {
        var provider = new MockFileProvider()
            .Add("partial", "hi");

        var parser = new FluidParser();
        var template = parser.Parse("{% render 'partial' %}");

        var source = template.Compile(new SourceGenerationOptions
        {
            Namespace = "Fluid.Tests.Generated",
            ClassName = "T" + Guid.NewGuid().ToString("N"),
            FileProvider = provider
        });

        var generated = CompileToAssembly(source.SourceCode);
        var type = generated.GetType(source.FullTypeName, throwOnError: true);
        var instance = (IFluidTemplate)Activator.CreateInstance(type, nonPublic: true);

        var runtimeContext = new TemplateContext(new TemplateOptionsBuilder().WithFileProvider(provider).Build());

        var runtimeWriter = new StringWriter();
        await template.RenderAsync(runtimeWriter, HtmlEncoder.Default, runtimeContext);

        var generatedWriter = new StringWriter();
        await instance.RenderAsync(generatedWriter, HtmlEncoder.Default, new TemplateContext());

        Assert.Equal("hi", runtimeWriter.ToString());
        Assert.Equal("hi", generatedWriter.ToString());
    }

    [Fact]
    public async Task GeneratedRender_UsesContextRootWithoutCallerLocals()
    {
        var provider = new MockFileProvider()
            .Add("partial", "{{ input }}|{{ outer }}|{{ global }}");

        var parser = new FluidParser();
        var template = parser.Parse("{% assign outer = 'hidden' %}{% render 'partial' %}");
        var source = template.Compile(new SourceGenerationOptions
        {
            Namespace = "Fluid.Tests.Generated",
            ClassName = "T" + Guid.NewGuid().ToString("N"),
            FileProvider = provider
        });

        var generated = CompileToAssembly(source.SourceCode);
        var type = generated.GetType(source.FullTypeName, throwOnError: true);
        var instance = (IFluidTemplate)Activator.CreateInstance(type, nonPublic: true);

        var options = new TemplateOptionsBuilder()
            .WithFileProvider(provider)
            .WithGlobalValue("global", Fluid.Values.StringValue.Create("global"))
            .Build();

        var runtimeContext = new TemplateContext(options).SetValue("input", "root");
        var runtimeWriter = new StringWriter();
        using (runtimeContext.EnterScope())
        {
            await template.RenderAsync(runtimeWriter, HtmlEncoder.Default, runtimeContext);
        }

        var generatedContext = new TemplateContext(options).SetValue("input", "root");
        var generatedWriter = new StringWriter();
        using (generatedContext.EnterScope())
        {
            await instance.RenderAsync(generatedWriter, HtmlEncoder.Default, generatedContext);
        }

        Assert.Equal("root||global", runtimeWriter.ToString());
        Assert.Equal(runtimeWriter.ToString(), generatedWriter.ToString());
    }

    [Theory]
    [InlineData("{% for i in items offset: 2 limit: 2 %}{{ i }},{% endfor %}", "2,3,", 2)]
    [InlineData("{% for i in items reversed limit: 2 %}{{ i }},{% endfor %}", "1,0,", 2)]
    [InlineData("{% render 'item' for items %}", "0,1,2,3,4,", 5)]
    public async Task GeneratedLoops_ReadIndexedArraysByIndex(string liquid, string expected, int conversions)
    {
        var provider = new MockFileProvider().Add("item", "{{ item }},").Add("item.liquid", "{{ item }},");
        var instance = CompileTemplate(liquid, provider);
        var options = new TemplateOptionsBuilder().WithFileProvider(provider).Build();

        var items = new FluidArrayTests.CountingArrayValue(5);
        var indexedWriter = new StringWriter();
        await instance.RenderAsync(indexedWriter, HtmlEncoder.Default, new TemplateContext(options).SetValue("items", items));

        var enumerableWriter = new StringWriter();
        await instance.RenderAsync(enumerableWriter, HtmlEncoder.Default, new TemplateContext(options).SetValue("items", new FluidArrayTests.EnumerableOnlyArrayValue(5)));

        Assert.Equal(expected, indexedWriter.ToString());
        Assert.Equal(conversions, items.Conversions);
        Assert.Equal(0, items.Enumerations);
        Assert.Equal(expected, enumerableWriter.ToString());
    }

    [Theory]
    [InlineData("{% for i in items offset: -1 %}{{ i }},{% endfor %}", "0,1,2,3,4,")]
    [InlineData("{% for i in items offset: -1 limit: 2 %}{{ i }},{% endfor %}", "0,")]
    [InlineData("{% for i in items offset: -3 limit: 2 %}{{ i }},{% else %}none{% endfor %}", "none")]
    [InlineData("{% for i in items offset: -1 limit: 2 %}{{ i }},{% endfor %}|{% for i in items offset: continue %}{{ i }},{% endfor %}", "0,|0,1,2,3,4,")]
    public async Task GeneratedLoops_StartANegativeOffsetAtTheFirstItem(string liquid, string expected)
    {
        var instance = CompileTemplate(liquid, new MockFileProvider());

        var writer = new StringWriter();
        await instance.RenderAsync(writer, HtmlEncoder.Default, new TemplateContext().SetValue("items", new[] { 0, 1, 2, 3, 4 }));

        Assert.Equal(expected, writer.ToString());
    }

    [Theory]
    [InlineData("{% for i in items limit: 1 %}{{ i }}{% endfor %}|{% for i in items offset: 7 %}{{ i }}{% else %}E{% endfor %}|{% for i in items offset: continue %}{{ i }}{% endfor %}", "0|E|")]
    [InlineData("{% for i in items limit: 1 %}{{ i }}{% endfor %}|{% for i in items offset: 3 limit: 0 %}{{ i }}{% else %}E{% endfor %}|{% for i in items offset: continue %}{{ i }}{% endfor %}", "0|E|1234")]
    public async Task GeneratedLoops_EmptySliceUpdatesContinueCursor(string liquid, string expected)
    {
        var instance = CompileTemplate(liquid, new MockFileProvider());

        var writer = new StringWriter();
        await instance.RenderAsync(writer, HtmlEncoder.Default, new TemplateContext().SetValue("items", new[] { 0, 1, 2, 3, 4 }));

        Assert.Equal(expected, writer.ToString());
    }

    [Theory]
    [InlineData("{% for i in (1..99999999999) %}{% endfor %}", "The range bound 99999999999 is out of range.")]
    [InlineData("{% assign r = (-99999999999..1) %}", "The range bound -99999999999 is out of range.")]
    [InlineData("{% for i in (-2000000000..2000000000) %}{% endfor %}", "The range (-2000000000..2000000000) has too many items.")]
    public async Task GeneratedRanges_ThrowALiquidExceptionWhenTooLarge(string liquid, string message)
    {
        var instance = CompileTemplate(liquid, new MockFileProvider());

        var exception = await Assert.ThrowsAsync<LiquidException>(
            () => instance.RenderAsync(new StringWriter(), HtmlEncoder.Default, new TemplateContext()).AsTask());

        Assert.Equal(message, exception.Message);
    }

    [Theory]
    [InlineData("{{ (x..5) | join }}", "Invalid integer '2.3' in range expression.")]
    [InlineData("{% for i in (1..x) %}{{ i }}{% endfor %}", "Invalid integer '2.3' in range expression.")]
    [InlineData("{{ (b..5) | join }}", "Invalid integer 'true' in range expression.")]
    public async Task GeneratedRanges_ThrowALiquidExceptionForNonIntegerBounds(string liquid, string message)
    {
        var instance = CompileTemplate(liquid, new MockFileProvider());
        var context = new TemplateContext().SetValue("x", 2.3m).SetValue("b", true);

        var exception = await Assert.ThrowsAsync<LiquidException>(
            () => instance.RenderAsync(new StringWriter(), HtmlEncoder.Default, context).AsTask());

        Assert.Equal(message, exception.Message);
    }

    [Theory]
    [InlineData("{% for i in (1..3) %}{{ i }}{% endfor %}", "123")]
    [InlineData("{% for i in (0..5) offset: 1 limit: -1 %}{{ i }}{% endfor %}[{% for i in (0..5) offset: -2 limit: 3 %}{{ i }}{% endfor %}]", "[0]")]
    [InlineData("{{ (1..5) }}|{{ (1..5) | slice: 1, 3 }}|{% for i in (2.7..4) %}{{ i }}{% endfor %}|{{ (2.7..4) | join }}", "1..5|..5|234|2 3 4")]
    [InlineData("{% for i in (first..last) %}{{ i }}{% endfor %}", "234")]
    [InlineData("{% assign r = (3..1) %}[{{ r | size }}]", "[0]")]
    [InlineData("{% assign s = ' 2.7' %}{{ (s..4) | join }}|{% assign t = 'foo' %}{{ (t..1) | join }}|{{ (nothing..1) | join }}", "2 3 4|0 1|0 1")]
    [InlineData("{% assign r = (5..1) %}{{ r.first }}{{ r.last }}{{ r | first }}{{ r | last }}{{ r[0] }}|{% assign r = (1..5) %}{% if r contains 3.5 %}y{% endif %}", "5151|y")]
    public async Task GeneratedRanges_RenderLikeTheInterpreter(string liquid, string expected)
    {
        var instance = CompileTemplate(liquid, new MockFileProvider());
        var context = new TemplateContext().SetValue("first", 2).SetValue("last", 4);

        var writer = new StringWriter();
        await instance.RenderAsync(writer, HtmlEncoder.Default, context);

        Assert.Equal(expected, writer.ToString());
    }

    private static IFluidTemplate CompileTemplate(string liquid, MockFileProvider provider)
    {
        var template = new FluidParser().Parse(liquid);
        var source = template.Compile(new SourceGenerationOptions
        {
            Namespace = "Fluid.Tests.Generated",
            ClassName = "T" + Guid.NewGuid().ToString("N"),
            FileProvider = provider
        });

        var generated = CompileToAssembly(source.SourceCode);
        var type = generated.GetType(source.FullTypeName, throwOnError: true);
        return (IFluidTemplate)Activator.CreateInstance(type, nonPublic: true);
    }

    private static Assembly CompileToAssembly(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

        var references = new List<MetadataReference>();

        // Use TPA (trusted platform assemblies) for framework references.
        var tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        foreach (var path in tpa.Split(Path.PathSeparator))
        {
            references.Add(MetadataReference.CreateFromFile(path));
        }

        // Add Fluid assembly explicitly.
        references.Add(MetadataReference.CreateFromFile(typeof(FluidParser).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            assemblyName: "Fluid.Generated." + Guid.NewGuid().ToString("N"),
            syntaxTrees: new[] { syntaxTree },
            references: references.DistinctBy(r => ((PortableExecutableReference)r).FilePath),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

        using var peStream = new MemoryStream();
        var emit = compilation.Emit(peStream);

        if (!emit.Success)
        {
            var diagnostics = string.Join(Environment.NewLine, emit.Diagnostics.Select(d => d.ToString()));
            throw new InvalidOperationException(diagnostics + Environment.NewLine + source);
        }

        peStream.Position = 0;
        return AssemblyLoadContext.Default.LoadFromStream(peStream);
    }

    private sealed class CustomStatement : Statement
    {
        public override ValueTask<Completion> WriteToAsync(IFluidOutput writer, TextEncoder encoder, TemplateContext context)
        {
            return Normal();
        }
    }

    private sealed class FlushTrackingOutput : IFluidOutput
    {
        private char[] _buffer = new char[256];
        private int _index;

        public bool Flushed { get; private set; }

        public void Advance(int count) => _index += count;

        public Memory<char> GetMemory(int sizeHint = 0) => _buffer.AsMemory(_index);

        public Span<char> GetSpan(int sizeHint = 0) => _buffer.AsSpan(_index);

        public void Write(string value)
        {
            value.CopyTo(0, _buffer, _index, value.Length);
            _index += value.Length;
        }

        public void Write(char[] buffer, int index, int count)
        {
            buffer.AsSpan(index, count).CopyTo(_buffer.AsSpan(_index));
            _index += count;
        }

        public ValueTask FlushAsync()
        {
            Flushed = true;
            return default;
        }
    }
}
