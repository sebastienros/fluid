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
    // Like Liquid, a nil limit or offset is ignored.
    [InlineData("{% for i in (1..3) limit: missing %}{{ i }}{% endfor %}", "123")]
    [InlineData("{% for i in (1..3) offset: missing limit: 2 %}{{ i }}{% endfor %}", "12")]
    [InlineData("{% capture x %}hi{% endcapture %}{{ x }}", "hi")]
    [InlineData("{% cycle 'a','b' %}{% cycle 'a','b' %}{% cycle 'a','b' %}", "aba")]
    [InlineData("{% increment x %}{% increment x %}{% decrement x %}", "011")]
    [InlineData("{% increment x %}{{ x }}", "01")]
    [InlineData("{% case 2 %}{% when 1 %}a{% when 2 %}b{% else %}c{% endcase %}", "b")]
    [InlineData("{% assign s = 'hello' %}{{ s.first }}{{ s.last }}[{{ s['first'] }}]", "ho[]")]
    [InlineData("{% assign n = 5 %}[{{ n[0] }}{{ n['x'] }}]", "[]")]
    [InlineData("{% if 2 > 1 and 2 >= 2 and 1 < 2 and 2 <= 2 %}yes{% endif %}", "yes")]
    [InlineData("{% if true or 1 < 'a' %}y{% endif %}{% if false and 1 < 'a' %}n{% endif %}", "y")]
    [InlineData("{% if nil >= nil or nil <= missing or 1 > (2..3) or 'a' < (1..2) %}yes{% else %}no{% endif %}", "no")]
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
    // Like Liquid, a break or continue outside of a loop ends the template.
    [InlineData("a{% break %}b", "a")]
    [InlineData("a{% continue %}b", "a")]
    [InlineData("a{% if true %}{% break %}{% endif %}b{{ 'c' }}", "a")]
    [InlineData("{% if true %} {% increment a %} {% endif %}", " 0 ")]
    // Like Liquid, a loop whose body and else branch only contain whitespace, comments or assignments is blank.
    [InlineData("[{% for i in (1..3) %}\n  {% assign x = i %}\n{% endfor %}]", "[]")]
    [InlineData("[{% for i in (1..3) %} {% capture y %}{{ i }}{% endcapture %} {% endfor %}{{ y }}]", "[3]")]
    [InlineData("[{% for i in (1..3) %} {%# note %} {% if i %}{% endif %} {% endfor %}]", "[]")]
    [InlineData("[{% for i in (1..2) %}\n{% else %}.{% endfor %}]", "[\n\n]")]
    [InlineData("[{% for i in (1..2) %} {% raw %}{% endraw %} {% endfor %}]", "[]")]
    // Like Liquid, break and continue aren't blank.
    [InlineData("[{% for i in (1..3) %} {% if i == 2 %}{% break %}{% endif %} {% endfor %}]", "[   ]")]
    [InlineData("[{% for i in (1..3) %} {% continue %} {% endfor %}]", "[   ]")]
    // Like Liquid, text before the first when makes the case tag non-blank.
    [InlineData("[{% case 1 %}={% when 1 %} {% endcase %}]", "[ ]")]
    [InlineData("[{% case 1 %} {% when 1 %} {% endcase %}]", "[]")]
    // Like Liquid, whitespace is only removed when every branch of the tag is blank.
    [InlineData("{% if false %}x{% else %} {% endif %}|", " |")]
    [InlineData("{% if false %}x{% elsif true %} {% endif %}|", " |")]
    [InlineData("{% unless true %}x{% else %}\t{% endunless %}|", "\t|")]
    [InlineData("{% unless true %}x{% elsif true %} {% endunless %}|", " |")]
    [InlineData("{% for i in (1..0) %}x{% else %} {% endfor %}|", " |")]
    [InlineData("{% if false %} {% else %} {% endif %}|", "|")]
    [InlineData("{% if false %}{% assign y = 1 %}{% elsif true %} {% endif %}|", "|")]
    [InlineData("{% for i in (1..0) %}{% assign y = 1 %}{% else %} {% endfor %}|", "|")]
    [InlineData("{{ 'a' | json }}", "&quot;a&quot;")]
    [InlineData("{% macro hello() %}hi{% endmacro %}{{ hello() }}", "hi")]
    [InlineData("{% assign (a.b)[0]-c = 'x' %}{% capture (d)(e) %}y{% endcapture %}{{ ['(a.b)[0]-c'] }}{{ ['(d)(e)'] }}", "xy")]
    [InlineData("{% for i in (1..3) %}a{% endfor %}", "aaa")]
    [InlineData("{% if true %}a{% endif %}{% unless false %}b{% endunless %}{% if false %}{% else %}c{% endif %}", "abc")]
    [InlineData("{% case 1 %}{% when 1 %}a{% else %}b{% endcase %}{% case 2 %}{% when 1 %}a{% else %}b{% endcase %}", "ab")]
    [InlineData("{{ 'a,b' | split: ',' | join: '-' }}", "a-b")]
    [InlineData("{% assign x = 'b' %}{{ 'a' | append: x }}", "ab")]
    [InlineData("{{ false | default: 'x', allow_false: true }}|{{ nil | default: 'x', allow_false: true }}", "false|x")]
    [InlineData("{{ 'a' | missing_filter: 1 }}", "a")]
    [InlineData("{{ 1.50 }}|{{ 2 | times: 1.50 }}|{{ 'a' | append: nil }}{{ 'b' | append: true }}", "1.5|3.0|abtrue")]
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
    public async Task GeneratedTemplate_ResolvesMembersOfAlternatingTypes()
    {
        var template = new FluidParser().Parse("{{ item.Name }}{{ item.Name | upcase }}");

        var source = template.Compile(new SourceGenerationOptions
        {
            Namespace = "Fluid.Tests.Generated",
            ClassName = "T" + Guid.NewGuid().ToString("N")
        });

        var generated = CompileToAssembly(source.SourceCode);
        var type = generated.GetType(source.FullTypeName, throwOnError: true);
        var instance = (IFluidTemplate)Activator.CreateInstance(type, nonPublic: true);

        var options = new TemplateOptionsBuilder()
            .ConfigureMemberAccess(strategy =>
            {
                strategy.Register<NamedA, string>("Name", m => m.Name);
                strategy.Register<NamedB, string>("Name", m => m.Name);
            })
            .Build();

        // The call sites cache the accessors they resolve, and stop caching when the type keeps changing.
        for (var i = 0; i < 10; i++)
        {
            object item = i % 2 == 0 ? new NamedA() : new NamedB();
            var context = new TemplateContext(options).SetValue("item", item);

            var writer = new StringWriter();
            await instance.RenderAsync(writer, HtmlEncoder.Default, context);

            Assert.Equal(i % 2 == 0 ? "aA" : "bB", writer.ToString());
        }
    }

    private sealed class NamedA
    {
        public string Name => "a";
    }

    private sealed class NamedB
    {
        public string Name => "b";
    }

    [Fact]
    public async Task GeneratedTemplate_StrictFilters_ThrowsOnMissingFilter()
    {
        var template = new FluidParser().Parse("{{ 'a' | missing_filter: 1 }}");

        var source = template.Compile(new SourceGenerationOptions
        {
            Namespace = "Fluid.Tests.Generated",
            ClassName = "T" + Guid.NewGuid().ToString("N")
        });

        var generated = CompileToAssembly(source.SourceCode);
        var type = generated.GetType(source.FullTypeName, throwOnError: true);
        var instance = (IFluidTemplate)Activator.CreateInstance(type, nonPublic: true);
        var context = new TemplateContext(new TemplateOptionsBuilder().WithStrictFilters(true).Build());

        var ex = await Assert.ThrowsAsync<FluidException>(() => instance.RenderAsync(new StringWriter(), HtmlEncoder.Default, context).AsTask());

        Assert.Contains("missing_filter", ex.Message, StringComparison.Ordinal);
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
            .WithGlobalValue("global", Values.StringValue.Create("global"))
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
    // Like Liquid, render ... for only loops over a collection, any other value is rendered once.
    [InlineData("{% render 'ctx' for s as v %}|{% render 'ctx' for 5 as v %}|{% render 'ctx' for nil as v %}|{% render 'ctx' for (1..2) as v %}", "Hello-|5-|-|1-12-2")]
    [InlineData("{% render 'ctx' for s as v, x: 1 %}", "Hello-")]
    public async Task GeneratedRenderFor_RendersNonCollectionsOnce(string liquid, string expected)
    {
        var provider = new MockFileProvider().Add("ctx", "{{ v }}-{{ forloop.index }}").Add("ctx.liquid", "{{ v }}-{{ forloop.index }}");
        var instance = CompileTemplate(liquid, provider);
        var options = new TemplateOptionsBuilder().WithFileProvider(provider).Build();

        var runtimeWriter = new StringWriter();
        await new FluidParser().Parse(liquid).RenderAsync(runtimeWriter, HtmlEncoder.Default, new TemplateContext(options).SetValue("s", "Hello"));

        var generatedWriter = new StringWriter();
        await instance.RenderAsync(generatedWriter, HtmlEncoder.Default, new TemplateContext(options).SetValue("s", "Hello"));

        Assert.Equal(expected, runtimeWriter.ToString());
        Assert.Equal(expected, generatedWriter.ToString());
    }

    [Theory]
    // Like Liquid, each item of render ... for is rendered in its own context: counters and variables start over.
    [InlineData("{% render 'counter' for (1..3) %}|{% render 'counter' with 1 %}{% render 'counter' with 1 %}", "0x0x0x|0x0x")]
    [InlineData("{% render 'counter' for (1..2) as v, y: 'Y' %}", "0Y0Y")]
    // A variable of the template hides a counter even when it is nil.
    [InlineData("{% increment i %}{% assign i = nil %}[{{ i }}]{% assign n = 'i' %}[{{ [n] }}]", "0[][]")]
    public async Task GeneratedCounters_MatchLiquidScopes(string liquid, string expected)
    {
        const string counter = "{% increment c %}{{ y | default: 'x' }}{% assign y = 'leaked' %}";
        var provider = new MockFileProvider().Add("counter", counter).Add("counter.liquid", counter);
        var instance = CompileTemplate(liquid, provider);
        var options = new TemplateOptionsBuilder().WithFileProvider(provider).Build();

        var runtimeWriter = new StringWriter();
        await new FluidParser().Parse(liquid).RenderAsync(runtimeWriter, HtmlEncoder.Default, new TemplateContext(options));

        var generatedWriter = new StringWriter();
        await instance.RenderAsync(generatedWriter, HtmlEncoder.Default, new TemplateContext(options));

        Assert.Equal(expected, runtimeWriter.ToString());
        Assert.Equal(expected, generatedWriter.ToString());
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
    // A limit or an offset out of an int's range means all or none.
    [InlineData("{% for i in items limit: 9999999999 %}{{ i }},{% endfor %}|{% for i in items offset: 9999999999 %}{{ i }},{% else %}none{% endfor %}", "0,1,2,3,4,|none")]
    [InlineData("{% assign n = '9999999999' %}{% for i in items limit: n %}{{ i }},{% endfor %}|{% for i in items offset: n %}{{ i }},{% else %}none{% endfor %}", "0,1,2,3,4,|none")]
    public async Task GeneratedLoops_StartANegativeOffsetAtTheFirstItem(string liquid, string expected)
    {
        var instance = CompileTemplate(liquid, new MockFileProvider());

        var writer = new StringWriter();
        await instance.RenderAsync(writer, HtmlEncoder.Default, new TemplateContext().SetValue("items", new[] { 0, 1, 2, 3, 4 }));

        Assert.Equal(expected, writer.ToString());
    }

    [Theory]
    [InlineData("{% for i in items limit: 1 %}{{ i }}{% endfor %}|{% for i in items offset: 7 %}{{ i }}{% else %}E{% endfor %}|{% for i in items offset: continue %}{{ i }}{% endfor %}", "0|E|")]
    [InlineData("{% for i in items limit: 1 %}{{ i }}{% endfor %}|{% for i in items offset: 3 limit: 0 %}{{ i }}{% else %}E{% endfor %}|{% for i in items offset: continue %}{{ i }}{% endfor %}", "0|E|34")]
    public async Task GeneratedLoops_EmptySliceUpdatesContinueCursor(string liquid, string expected)
    {
        var instance = CompileTemplate(liquid, new MockFileProvider());

        var writer = new StringWriter();
        await instance.RenderAsync(writer, HtmlEncoder.Default, new TemplateContext().SetValue("items", new[] { 0, 1, 2, 3, 4 }));

        Assert.Equal(expected, writer.ToString());
    }

    [Theory]
    [InlineData("{% include 'icon' with 'star', class: 'big' %}", "[star big]")]
    [InlineData("{% include 'icon' for items as icon, class: 'big' %}", "[a big][b big]")]
    public async Task GeneratedInclude_WithOrFor_And_NamedArguments(string liquid, string expected)
    {
        var provider = new MockFileProvider().Add("icon", "[{{ icon }} {{ class }}]").Add("icon.liquid", "[{{ icon }} {{ class }}]");
        var instance = CompileTemplate(liquid, provider);
        var options = new TemplateOptionsBuilder().WithFileProvider(provider).Build();

        var writer = new StringWriter();
        await instance.RenderAsync(writer, HtmlEncoder.Default, new TemplateContext(options).SetValue("items", new[] { "a", "b" }));

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

    [Theory]
    [InlineData("{% for i in (1..2) %}{{ forloop.name }} {% endfor %}", "i-(1..2) i-(1..2) ")]
    [InlineData("{% for i in (first..last) %}{{ forloop.name }} {% endfor %}", "i-(2..3) i-(2..3) ")]
    [InlineData("{% for i in (-1..0) %}{{ forloop.name }} {% endfor %}", "i-(-1..0) i-(-1..0) ")]
    [InlineData("{% for i in (1..4) limit: 2 %}{{ i }}{% endfor %}|{% for i in (1..4) offset: continue %}{{ i }}{% endfor %}", "12|34")]
    public async Task GeneratedRangeLoops_NameTheirSource(string liquid, string expected)
    {
        var instance = CompileTemplate(liquid, new MockFileProvider());
        var context = new TemplateContext().SetValue("first", 2).SetValue("last", 3);

        var writer = new StringWriter();
        await instance.RenderAsync(writer, HtmlEncoder.Default, context);

        Assert.Equal(expected, writer.ToString());
    }

    [Theory]
    [InlineData("{{ m(c = 3) }}", "[|B|3]")]
    [InlineData("{{ m(b = 2) }}", "[|2|C]")]
    [InlineData("{{ m(1, c = 3) }}", "[1|B|3]")]
    [InlineData("{{ m(c = 3, a = 1) }}", "[1|B|3]")]
    [InlineData("{{ m(1, 2) }}", "[1|2|C]")]
    // An argument without a name after a named one has no position to fill.
    [InlineData("{{ m(b = 2, 9) }}", "[|2|C]")]
    public async Task GeneratedMacros_BindNamedArgumentsByName(string call, string expected)
    {
        const string macro = "{% macro m(a, b='B', c='C') %}[{{ a }}|{{ b }}|{{ c }}]{% endmacro %}";
        var parser = new FluidParser(new FluidParserOptions { AllowFunctions = true });
        var instance = CompileTemplate(macro + call, new MockFileProvider(), parser);

        var writer = new StringWriter();
        await instance.RenderAsync(writer, HtmlEncoder.Default, new TemplateContext());

        Assert.Equal(expected, writer.ToString());
    }

    private static IFluidTemplate CompileTemplate(string liquid, MockFileProvider provider, FluidParser parser = null)
    {
        var template = (parser ?? new FluidParser()).Parse(liquid);
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
            // Warnings are errors, as generated code is compiled in projects that treat them so.
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, generalDiagnosticOption: ReportDiagnostic.Error));

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
