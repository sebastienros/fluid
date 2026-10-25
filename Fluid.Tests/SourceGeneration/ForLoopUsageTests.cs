using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fluid.SourceGeneration;
using Fluid.Tests.Mocks;
using Fluid.Values;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Fluid.Tests;

/// <summary>
/// Compiled templates only maintain the forloop variable of the loops where something can read it.
/// </summary>
public class ForLoopUsageTests
{
    private static readonly FluidParser Parser = new(new FluidParserOptions { AllowFunctions = true, AllowLiquidTag = true, AllowParentheses = true });

    // The loops are identified by their item variable, "render" and "include" by the tag of a loop over a template.

    [Theory]
    [InlineData("{% for i in (1..3) %}{{ i }}{% endfor %}", "")]
    [InlineData("{% for i in (1..3) %}{% endfor %}", "")]
    [InlineData("{% for i in (1..3) %}{{ forloop.index }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..3) %}{% if forloop.first %}first{% endif %}{% endfor %}", "i")]
    [InlineData("{% for i in (1..3) %}{% unless forloop.last %},{% endunless %}{% endfor %}", "i")]
    [InlineData("{% for i in (1..3) %}{% if false %}{% elsif forloop.last %}last{% endif %}{% endfor %}", "i")]
    [InlineData("{% for i in (1..3) %}{% case forloop.index %}{% when 1 %}one{% endcase %}{% endfor %}", "i")]
    [InlineData("{% for i in (1..3) %}{% case i %}{% when forloop.length %}last{% endcase %}{% endfor %}", "i")]
    [InlineData("{% for i in (1..3) %}{{ 'a' | append: forloop.index }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..3) %}{% assign x = forloop.index | plus: 1 %}{{ x }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..3) %}{% capture c %}{{ forloop.index }}{% endcapture %}{{ c }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..3) %}{% echo forloop.index %}{% endfor %}", "i")]
    [InlineData("{% liquid\nfor i in (1..3)\necho forloop.index\nendfor\n%}", "i")]
    [InlineData("{% for i in (1..3) %}{% cycle 'a', 'b' %}{% endfor %}", "")]
    [InlineData("{% for i in (1..3) %}{% increment c %}{% decrement d %}{% endfor %}", "")]
    [InlineData("{% for i in (1..3) %}{% if i == 2 %}{% break %}{% endif %}{% continue %}{% endfor %}", "")]
    [InlineData("{% for i in (1..3) %}{{ items[forloop.index0] }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..3) %}{{ items[i] }}{% endfor %}", "")]
    public void Loop_Uses_ForLoop_When_Its_Body_Reads_It(string template, string expected)
    {
        Assert.Equal(expected, UsedLoops(template));
    }

    [Theory]
    [InlineData("index")]
    [InlineData("index0")]
    [InlineData("rindex")]
    [InlineData("rindex0")]
    [InlineData("first")]
    [InlineData("last")]
    [InlineData("length")]
    [InlineData("name")]
    [InlineData("parentloop")]
    [InlineData("unknown")]
    public void Any_Property_Uses_ForLoop(string property)
    {
        Assert.Equal("i", UsedLoops($"{{% for i in (1..3) %}}{{{{ forloop.{property} }}}}{{% endfor %}}"));
    }

    [Theory]
    // Names are compared with TemplateOptions.ModelNamesComparer at render time, so every casing is assumed to match.
    [InlineData("{% for i in (1..3) %}{{ ForLoop.index }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..3) %}{{ FORLOOP.index }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{{ ParentLoop.index }}{% endfor %}{% endfor %}", "i,j")]
    [InlineData("{% for i in (1..3) %}{{ forloop.Index }}{% endfor %}", "i")]
    // Other variables are not the loop variable.
    [InlineData("{% for i in (1..3) %}{{ forloops.index }}{% endfor %}", "")]
    [InlineData("{% for i in (1..3) %}{{ loop.index }}{% endfor %}", "")]
    [InlineData("{% for i in (1..3) %}{{ i.forloop }}{% endfor %}", "")]
    public void Variable_Names_Are_Matched_Like_At_Render_Time(string template, string expected)
    {
        Assert.Equal(expected, UsedLoops(template));
    }

    [Theory]
    [InlineData("{% for i in (1..3) %}{{ forloop['index'] }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..3) %}{{ ['forloop'].index }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..3) %}{{ ['forloop']['index'] }}{% endfor %}", "i")]
    // A computed variable name can be the loop variable.
    [InlineData("{% for i in (1..3) %}{% assign n = 'forloop' %}{{ [n].index }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{% assign n = 'x' %}{{ [n] }}{% endfor %}{% endfor %}", "i,j")]
    [InlineData("{% assign n = 'forloop' %}{{ [n].index }}{% for i in (1..3) %}{{ i }}{% endfor %}", "")]
    // A computed property can be parentloop.
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{% assign p = 'index' %}{{ forloop[p] }}{% endfor %}{% endfor %}", "i,j")]
    public void Indexers_Are_Followed(string template, string expected)
    {
        Assert.Equal(expected, UsedLoops(template));
    }

    [Theory]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{{ forloop.index }}{% endfor %}{% endfor %}", "j")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{{ j }}{% endfor %}{{ forloop.index }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{{ forloop.parentloop.index }}{% endfor %}{% endfor %}", "i,j")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{{ parentloop.index }}{% endfor %}{% endfor %}", "i,j")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{% for k in (1..2) %}{{ forloop.parentloop.parentloop.index }}{% endfor %}{% endfor %}{% endfor %}", "i,j,k")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{% for k in (1..2) %}{{ parentloop.parentloop.index }}{% endfor %}{% endfor %}{% endfor %}", "i,j,k")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{% for k in (1..2) %}{{ forloop.parentloop.index }}{% endfor %}{% endfor %}{% endfor %}", "j,k")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{% for k in (1..2) %}{{ k }}{% endfor %}{{ forloop.index }}{% endfor %}{% endfor %}", "j")]
    // A chain that goes past the loops of the template still goes through them.
    [InlineData("{% for i in (1..2) %}{{ parentloop.index }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{{ forloop.parentloop.index }}{% endfor %}", "i")]
    // Using the loop value itself makes its outer loops readable.
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{{ forloop }}{% endfor %}{% endfor %}", "i,j")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{% assign f = forloop %}{% endfor %}{{ f.parentloop.index }}{% endfor %}", "i,j")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{{ forloop | json }}{% endfor %}{% endfor %}", "i,j")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{{ forloop.parentloop }}{% endfor %}{% endfor %}", "i,j")]
    // Sibling loops are decided separately.
    [InlineData("{% for i in (1..2) %}{{ forloop.index }}{% endfor %}{% for j in (1..2) %}{{ j }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{{ i }}{% endfor %}{% for j in (1..2) %}{{ forloop.index }}{% endfor %}", "j")]
    public void Nested_Loops_Are_Decided_Separately(string template, string expected)
    {
        Assert.Equal(expected, UsedLoops(template));
    }

    [Theory]
    // The source, limit and offset are evaluated in the enclosing loop.
    [InlineData("{% for i in (1..2) %}{% for j in (1..forloop.index) %}{{ j }}{% endfor %}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..3) limit: forloop.index %}{{ j }}{% endfor %}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..3) offset: forloop.index0 %}{{ j }}{% endfor %}{% endfor %}", "i")]
    [InlineData("{% for i in (1..forloop.index) %}{{ i }}{% endfor %}", "")]
    // The else block runs without the loop.
    [InlineData("{% for i in (1..2) %}{% for j in empty_list %}{% else %}{{ forloop.index }}{% endfor %}{% endfor %}", "i")]
    [InlineData("{% for i in empty_list %}{% else %}{{ forloop.index }}{% endfor %}", "")]
    // The variable outside of any loop comes from the model.
    [InlineData("{{ forloop.index }}{% for i in (1..2) %}{{ i }}{% endfor %}{{ forloop.index }}", "")]
    public void Loop_Arguments_And_Else_Belong_To_The_Enclosing_Loop(string template, string expected)
    {
        Assert.Equal(expected, UsedLoops(template));
    }

    [Theory]
    [InlineData("{% for i in (1..2) %}{{ i | plus: 1 | append: 'x' | upcase }}{% endfor %}", "")]
    [InlineData("{% for i in (1..2) %}{{ '#fff' | color_to_rgb }}{{ 1 | money }}{% endfor %}", "")]
    [InlineData("{% for i in (1..2) %}{{ i | date: '%Y' | default: 'x' }}{% endfor %}", "")]
    // A filter that isn't built in receives the TemplateContext and can read the variable.
    [InlineData("{% for i in (1..2) %}{{ i | loop_index }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{{ j | loop_index }}{% endfor %}{% endfor %}", "i,j")]
    [InlineData("{% for i in (1..2) %}{% assign x = i | loop_index %}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{{ 'a' | append: i | loop_index }}{% endfor %}", "i")]
    [InlineData("{{ 1 | loop_index }}{% for i in (1..2) %}{{ i }}{% endfor %}", "")]
    // Filter names are case-sensitive, a different casing isn't a built-in filter.
    [InlineData("{% for i in (1..2) %}{{ i | Upcase }}{% endfor %}", "i")]
    public void Custom_Filters_Use_ForLoop(string template, string expected)
    {
        Assert.Equal(expected, UsedLoops(template));
    }

    [Theory]
    [InlineData("{% macro m() %}{{ forloop.index }}{% endmacro %}{% for i in (1..2) %}{{ m() }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{{ m() }}{% endfor %}{% macro m() %}{{ forloop.index }}{% endmacro %}", "i")]
    [InlineData("{% macro m() %}{{ 'x' }}{% endmacro %}{% for i in (1..2) %}{{ m() }}{% endfor %}", "")]
    [InlineData("{% macro m() %}{{ forloop.index }}{% endmacro %}{% for i in (1..2) %}{{ i }}{% endfor %}{{ m() }}", "")]
    [InlineData("{% macro m(x) %}{{ x }}{% endmacro %}{% for i in (1..2) %}{{ m(forloop.index) }}{% endfor %}", "i")]
    [InlineData("{% macro m(x) %}{{ x }}{% endmacro %}{% for i in (1..2) %}{{ m(x = forloop.index) }}{% endfor %}", "i")]
    // Default values are evaluated where the macro is defined.
    [InlineData("{% for i in (1..2) %}{% macro m(x = forloop.index) %}{{ x }}{% endmacro %}{% endfor %}", "i")]
    [InlineData("{% macro m(x = forloop.index) %}{{ x }}{% endmacro %}{% for i in (1..2) %}{{ m() }}{% endfor %}", "")]
    // Calls go through the scope, so a macro is found with any casing.
    [InlineData("{% macro m() %}{{ 'x' }}{% endmacro %}{% for i in (1..2) %}{{ M() }}{% endfor %}", "")]
    [InlineData("{% macro m() %}{{ forloop.index }}{% endmacro %}{% for i in (1..2) %}{{ M() }}{% endfor %}", "i")]
    // Macros calling macros, and recursive macros.
    [InlineData("{% macro a() %}{{ b() }}{% endmacro %}{% macro b() %}{{ forloop.index }}{% endmacro %}{% for i in (1..2) %}{{ a() }}{% endfor %}", "i")]
    [InlineData("{% macro a() %}{{ b() }}{% endmacro %}{% macro b() %}{{ 'x' }}{% endmacro %}{% for i in (1..2) %}{{ a() }}{% endfor %}", "")]
    [InlineData("{% macro a(n) %}{% if n > 0 %}{% assign m = n | minus: 1 %}{{ a(m) }}{% endif %}{% endmacro %}{% for i in (1..2) %}{{ a(2) }}{% endfor %}", "i")]
    // The loops of a macro run inside the loop of the call.
    [InlineData("{% macro m() %}{% for k in (1..2) %}{{ forloop.index }}{% endfor %}{% endmacro %}{% for i in (1..2) %}{{ m() }}{% endfor %}", "k")]
    [InlineData("{% macro m() %}{% for k in (1..2) %}{{ parentloop.index }}{% endfor %}{% endmacro %}{% for i in (1..2) %}{{ m() }}{% endfor %}", "i,k")]
    [InlineData("{% macro m() %}{% for k in (1..2) %}{{ forloop.index }}{% endfor %}{% endmacro %}", "k")]
    // Any other function can be .NET code that receives the TemplateContext.
    [InlineData("{% for i in (1..2) %}{{ unknown() }}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{{ unknown(j) }}{% endfor %}{% endfor %}", "i,j")]
    [InlineData("{% for i in (1..2) %}{{ obj.method() }}{% endfor %}", "i")]
    [InlineData("{% macro m() %}{{ 'x' }}{% endmacro %}{% for i in (1..2) %}{{ m()() }}{% endfor %}", "i")]
    [InlineData("{{ unknown() }}{% for i in (1..2) %}{{ i }}{% endfor %}", "")]
    public void Macros_Run_In_The_Loop_Of_The_Call(string template, string expected)
    {
        Assert.Equal(expected, UsedLoops(template));
    }

    [Theory]
    [InlineData("{% from 'macros' import uses %}{% for i in (1..2) %}{{ uses() }}{% endfor %}", "i")]
    [InlineData("{% from 'macros' import ignores %}{% for i in (1..2) %}{{ ignores() }}{% endfor %}", "")]
    [InlineData("{% from 'macros' %}{% for i in (1..2) %}{{ uses() }}{{ ignores() }}{% endfor %}", "i")]
    [InlineData("{% from 'macros' %}{% for i in (1..2) %}{{ ignores() }}{% endfor %}", "")]
    public void Imported_Macros_Are_Followed(string template, string expected)
    {
        Assert.Equal(expected, UsedLoops(template, ("macros", "{% macro uses() %}{{ forloop.index }}{% endmacro %}{% macro ignores() %}x{% endmacro %}")));
    }

    [Theory]
    // An included template runs in the scope of the include.
    [InlineData("{% for i in (1..2) %}{% include 'uses' %}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{% include 'ignores' %}{% endfor %}", "")]
    [InlineData("{% for i in (1..2) %}{% include 'ignores' with forloop.index %}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{% include 'ignores', x: forloop.index %}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{% include 'nested' %}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{% include 'parent' %}{% endfor %}{% endfor %}", "i,j")]
    [InlineData("{% include 'uses' %}{% for i in (1..2) %}{{ i }}{% endfor %}", "")]
    [InlineData("{% for i in (1..2) %}{% include 'loop' %}{% endfor %}", "k")]
    [InlineData("{% for i in (1..2) %}{% include 'recursive' %}{% endfor %}", "i")]
    // The loop of an include ... for is the innermost loop of the included template.
    [InlineData("{% include 'uses' for (1..2) %}", "include")]
    [InlineData("{% include 'ignores' for (1..2) %}", "")]
    [InlineData("{% for i in (1..2) %}{% include 'uses' for (1..2) %}{% endfor %}", "include")]
    [InlineData("{% for i in (1..2) %}{% include 'parent' for (1..2) %}{% endfor %}", "i,include")]
    [InlineData("{% for i in (1..2) %}{% include 'ignores' for (1..forloop.index) %}{% endfor %}", "i")]
    public void Included_Templates_Run_In_The_Loop(string template, string expected)
    {
        Assert.Equal(expected, UsedLoops(template, Partials));
    }

    [Theory]
    // A rendered template is isolated and only sees the loop of a render ... for.
    [InlineData("{% for i in (1..2) %}{% render 'uses' %}{% endfor %}", "")]
    [InlineData("{% for i in (1..2) %}{% render 'parent' %}{% endfor %}", "")]
    [InlineData("{% for i in (1..2) %}{% render 'ignores' with forloop.index %}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{% render 'ignores', x: forloop.index %}{% endfor %}", "i")]
    [InlineData("{% for i in (1..2) %}{% render 'loop' %}{% endfor %}", "k")]
    [InlineData("{% render 'uses' for (1..2) %}", "render")]
    [InlineData("{% render 'ignores' for (1..2) %}", "")]
    [InlineData("{% for i in (1..2) %}{% render 'uses' for (1..2) %}{% endfor %}", "render")]
    [InlineData("{% render 'nested' for (1..2) %}", "render")]
    [InlineData("{% render 'loop' for (1..2) %}", "k")]
    [InlineData("{% render 'recursive_render' for (1..2) %}", "render")]
    public void Rendered_Templates_Only_See_Their_Own_Loop(string template, string expected)
    {
        Assert.Equal(expected, UsedLoops(template, Partials));
    }

    [Fact]
    public void Template_Without_Used_Loops_Has_No_Loop_Variable()
    {
        var source = Compile("{% for i in (1..2) %}{% for j in items %}{{ i }}{{ j }}{% endfor %}{% endfor %}{% render 'ignores' for (1..2) %}{% include 'ignores' for (1..2) %}", Partials).SourceCode;

        Assert.DoesNotContain("ForLoopValue", source);
        Assert.DoesNotContain("\"forloop\"", source);
        Assert.DoesNotContain("\"parentloop\"", source);
    }

    public static IEnumerable<object[]> RenderedTemplates()
    {
        string[] templates =
        [
            "{% for i in items %}{{ i }}{% endfor %}",
            "{% for i in items %}{{ forloop.index }}/{{ forloop.index0 }}/{{ forloop.rindex }}/{{ forloop.rindex0 }}/{{ forloop.first }}/{{ forloop.last }}/{{ forloop.length }}/{{ forloop.name }} {% endfor %}",
            "{% for i in items reversed %}{{ forloop.index }}{{ i }}{% endfor %}",
            "{% for i in items limit: 2 offset: 1 %}{{ forloop.index }}{{ i }}{% endfor %}{% for i in items offset: continue %}{{ forloop.index }}{{ i }}{% endfor %}",
            "{% for i in items %}{% if forloop.index == 2 %}{% continue %}{% endif %}{% if forloop.index == 4 %}{% break %}{% endif %}{{ i }}{% endfor %}",
            "{% for i in empty_list %}{{ forloop.index }}{% else %}none{{ forloop.index }}{% endfor %}",
            "{% for i in (1..2) %}{% for j in (1..3) %}{{ forloop.index }}{{ forloop.parentloop.index }}{{ parentloop.index }} {% endfor %}{% endfor %}",
            "{% for i in (1..2) %}{% for j in (1..2) %}{% for k in (1..2) %}{{ forloop.parentloop.parentloop.index }}{{ parentloop.parentloop.index }}{{ forloop.parentloop.index }}{% endfor %}{% endfor %}{% endfor %}",
            "{% for i in (1..2) %}{% for j in (1..2) %}{{ j }}{% endfor %}{{ forloop.index }}{% endfor %}",
            "{% for i in (1..2) %}{% for j in (1..2) %}{{ forloop.parentloop.index }}{% endfor %}{% for k in (1..2) %}{{ k }}{% endfor %}{% endfor %}",
            "{% for i in (1..2) %}{% for j in (1..forloop.index) %}{{ j }}{% endfor %};{% endfor %}",
            "{% for i in (1..2) %}{% for j in (1..2) %}{% assign f = forloop %}{% endfor %}{{ f.index }}{{ f.parentloop.index }}{% endfor %}",
            "{% for i in (1..2) %}{% for j in (1..2) %}{% assign p = 'index' %}{{ forloop[p] }}{{ forloop.parentloop[p] }}{% endfor %}{% endfor %}",
            "{% for i in (1..3) %}{% assign n = 'forloop' %}{{ [n].index }}{% endfor %}",
            "{% for i in (1..3) %}{{ ForLoop.index }}{{ FORLOOP.last }}{% endfor %}",
            "{% for i in (1..3) %}{{ forloop['index'] }}{{ ['forloop'].first }}{% endfor %}",
            "{% for i in (1..3) %}{% capture c %}{{ forloop.index }}{% endcapture %}{{ c }}{% cycle 'a', 'b' %}{% endfor %}",
            "{% liquid\nfor i in (1..3)\necho forloop.index\nendfor\n%}",
            "{% for i in (1..3) %}{{ i | loop_index }}{% endfor %}",
            "{% for i in (1..2) %}{% for j in (1..2) %}{{ j | parent_loop_index }}{% endfor %}{% endfor %}",
            "{% macro m() %}{{ forloop.index }}{% endmacro %}{% for i in (1..3) %}{{ m() }}{% endfor %}",
            "{% macro m() %}{% for k in (1..2) %}{{ parentloop.index }}{{ forloop.index }}{% endfor %}{% endmacro %}{% for i in (1..2) %}{{ m() }}{% endfor %}",
            "{% macro a(n) %}{% if n > 0 %}{{ forloop.index }}{% assign m = n | minus: 1 %}{{ a(m) }}{% endif %}{% endmacro %}{% for i in (1..2) %}{{ a(2) }}{% endfor %}",
            "{% from 'macros' import uses %}{% for i in (1..3) %}{{ uses() }}{% endfor %}",
            "{% for i in (1..2) %}{% include 'uses' %}{% include 'ignores' %}{% include 'nested' %}{% endfor %}",
            "{% for i in (1..2) %}{% for j in (1..2) %}{% include 'parent' %}{% endfor %}{% endfor %}",
            "{% for i in (1..2) %}{% include 'loop' %}{% include 'recursive' %}{% endfor %}",
            "{% include 'uses' for items %}|{% include 'uses' for items as x %}|{% include 'ignores' for items %}",
            "{% for i in (1..2) %}{% include 'parent' for (1..2) %}{% endfor %}",
            "{% for i in (1..2) %}{% render 'uses' %}{% render 'parent' %}{% render 'loop' %}{% endfor %}",
            "{% render 'uses' for items %}|{% render 'uses' for items as x %}|{% render 'ignores' for items %}|{% render 'nested' for items %}",
            "{% render 'loop' for (1..2) %}{% render 'recursive_render' for (1..2) %}",
        ];

        return templates.Select(t => new object[] { t });
    }

    [Theory]
    [MemberData(nameof(RenderedTemplates))]
    public async Task Compiled_Loops_Render_Like_Interpreted_Loops(string template)
    {
        var compiled = Instantiate(Compile(template, Partials));

        var fileProvider = new MockFileProvider();
        foreach (var (path, content) in Partials)
        {
            fileProvider.Add(path + ".liquid", content);
        }

        var options = new TemplateOptionsBuilder()
            .WithFileProvider(fileProvider)
            // .NET code can read the loop variable through the context, the loops it runs in keep it.
            .AddFilter("loop_index", (input, arguments, context) => context.GetValue("forloop") is ForLoopValue loop ? NumberValue.Create(loop.Index) : NilValue.Instance)
            .AddFilter("parent_loop_index", (input, arguments, context) => context.GetValue("forloop") is ForLoopValue { ParentLoop: { } parent } ? NumberValue.Create(parent.Index) : NilValue.Instance)
            .Build();

        TemplateContext CreateContext()
        {
            var context = new TemplateContext(options);
            context.SetValue("items", new[] { "a", "b", "c", "d", "e" });
            context.SetValue("empty_list", Array.Empty<string>());
            return context;
        }

        var expected = await Parser.Parse(template).RenderAsync(CreateContext());
        var actual = await compiled.RenderAsync(CreateContext());

        Assert.Equal(expected, actual);
    }

    private static readonly (string Path, string Content)[] Partials =
    [
        ("uses", "[{{ forloop.index }}]"),
        ("ignores", "[{{ x }}]"),
        ("nested", "{% include 'uses' %}"),
        ("parent", "[{{ forloop.parentloop.index }}]"),
        ("loop", "{% for k in (1..2) %}{{ forloop.index }}{% endfor %}"),
        ("recursive", "{% assign depth = depth | plus: 1 %}{% if depth < 3 %}{% include 'recursive' %}{% endif %}"),
        ("recursive_render", "{% unless depth %}{% render 'recursive_render' for (1..1), depth: 1 %}{% endunless %}[{{ forloop.index }}]"),
        ("macros", "{% macro uses() %}{{ forloop.index }}{% endmacro %}{% macro ignores() %}x{% endmacro %}"),
    ];

    private static string UsedLoops(string template, params (string Path, string Content)[] partials)
    {
        var source = Compile(template, partials).SourceCode;

        // Each loop that keeps its variable creates one, then names it after its item or flags its kind.
        var loops = new List<string>();
        foreach (Match match in Regex.Matches(source, @"ForLoopValue\.Create\(\);\s*\r?\n\s*(?<next>[^\r\n]*)"))
        {
            var next = match.Groups["next"].Value;
            var identifier = Regex.Match(next, @"forloop\.Identifier = ""(?<name>[^""]+)""");

            loops.Add(identifier.Success ? identifier.Groups["name"].Value
                : next.Contains("IsRenderLoop") ? "render"
                : "include");
        }

        return string.Join(",", loops.Distinct().OrderBy(x => x == "render" || x == "include" ? 1 : 0).ThenBy(x => x, StringComparer.Ordinal));
    }

    private static TemplateSource Compile(string template, params (string Path, string Content)[] partials)
    {
        var contents = partials.ToDictionary(x => x.Path, x => x.Content);

        return Parser.Parse(template).Compile(new SourceGenerationOptions
        {
            Namespace = "Fluid.Tests.Generated",
            ClassName = "T" + Guid.NewGuid().ToString("N"),
            TemplateContentResolver = path => contents.TryGetValue(Path.ChangeExtension(path, null), out var content) ? content : contents[path],
        });
    }

    private static IFluidTemplate Instantiate(TemplateSource source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source.SourceCode, new CSharpParseOptions(LanguageVersion.Latest));

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Append(typeof(FluidParser).Assembly.Location)
            .Distinct()
            .Select(path => MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            "Fluid.Generated." + Guid.NewGuid().ToString("N"),
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var peStream = new MemoryStream();
        var emit = compilation.Emit(peStream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics) + Environment.NewLine + source.SourceCode);

        peStream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(peStream);
        return (IFluidTemplate)Activator.CreateInstance(assembly.GetType(source.FullTypeName, throwOnError: true), nonPublic: true);
    }
}
