using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System.Xml.Linq;
using Fluid.Ast.BinaryExpressions;
using Fluid.SourceGeneration;
using Fluid.SourceGenerator;
using Fluid.Tests.Mocks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using System.Threading;

namespace Fluid.Tests;

public class FluidSourceGeneratorTests
{
    [Fact]
    public async Task TemplatesAttribute_Generates_Properties_FromAdditionalFiles()
    {
        var userSource = @"
using Fluid;
using Fluid.SourceGenerator;

namespace MyApp;

[FluidTemplates(""**/*.liquid"")]
public static partial class Templates
{
}
";

        var compilation = CreateCompilation(userSource);

        var additional = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("hello.liquid", "hi"),
            new InMemoryAdditionalText("_ignore.liquid", "ignored"));

        var generator = new FluidTemplateGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { generator.AsSourceGenerator() }, additionalTexts: additional);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var emit = EmitToAssembly(outputCompilation);
        var template = (IFluidTemplate)emit.Assembly.GetType("MyApp.Templates")!
            .GetProperty("Hello", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

        var sw = new StringWriter();
        await template.RenderAsync(sw, HtmlEncoder.Default, new TemplateContext());

        Assert.Equal("hi", sw.ToString());
    }

    [Fact]
    public async Task TemplatesAttribute_Compiles_Rendered_AdditionalFiles()
    {
        var userSource = @"
using Fluid;
using Fluid.SourceGenerator;

namespace MyApp;

[FluidTemplates(""**/*.liquid"")]
public static partial class Templates
{
}
";

        var compilation = CreateCompilation(userSource);

        var additional = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("hello.liquid", "{% render 'partial' %}"),
            new InMemoryAdditionalText("partial.liquid", "hi"));

        var generator = new FluidTemplateGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { generator.AsSourceGenerator() }, additionalTexts: additional);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var emit = EmitToAssembly(outputCompilation);
        var template = (IFluidTemplate)emit.Assembly.GetType("MyApp.Templates")!
            .GetProperty("Hello", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

        var sw = new StringWriter();
        await template.RenderAsync(sw, HtmlEncoder.Default, new TemplateContext());

        Assert.Equal("hi", sw.ToString());
    }

    [Fact]
    public void GeneratorAssembly_EmbedsTemplateCompilerDependencies()
    {
        var resources = typeof(FluidTemplateGenerator).Assembly.GetManifestResourceNames();

        Assert.Contains("Fluid.SourceGenerator.Dependencies.Fluid.SourceGenerator.Runtime.dll", resources);
        Assert.Contains("Fluid.SourceGenerator.Dependencies.Parlot.dll", resources);
    }

    [Fact]
    public async Task TemplatesAttribute_ExcludePattern_Skips_Matches()
    {
        var userSource = @"
using Fluid;
using Fluid.SourceGenerator;

namespace MyApp;

[FluidTemplates(""**/*.liquid"", Exclude = new[] { ""_*.liquid"" })]
public static partial class Templates
{
}
";

        var compilation = CreateCompilation(userSource);

        var additional = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("hello.liquid", "Hello from file"),
            new InMemoryAdditionalText("_ignore.liquid", "Ignored"));

        var generator = new FluidTemplateGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { generator.AsSourceGenerator() }, additionalTexts: additional);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var emit = EmitToAssembly(outputCompilation);
        var templatesType = emit.Assembly.GetType("MyApp.Templates")!;
        Assert.NotNull(templatesType.GetProperty("Hello", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templatesType.GetProperty("Ignore", BindingFlags.Public | BindingFlags.Static));

        var template = (IFluidTemplate)templatesType
            .GetProperty("Hello", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

        var sw = new StringWriter();
        await template.RenderAsync(sw, HtmlEncoder.Default, new TemplateContext());

        Assert.Equal("Hello from file", sw.ToString());
    }

    [Fact]
    public async Task TemplatesAttribute_Supports_Class_In_Global_Namespace()
    {
        var userSource = @"
using Fluid.SourceGenerator;

[FluidTemplates(""**/*.liquid"")]
public static partial class Templates
{
}
";

        var (outputCompilation, diagnostics) = RunGenerator(userSource, new InMemoryAdditionalText("hello.liquid", "hi"));

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal("hi", await RenderAsync(EmitToAssembly(outputCompilation).Assembly, "Templates", "Hello"));
    }

    [Fact]
    public async Task TemplatesAttribute_Reports_Unsupported_Template_And_Generates_Others()
    {
        var userSource = @"
using Fluid.SourceGenerator;

namespace MyApp;

[FluidTemplates(""**/*.liquid"")]
public static partial class Templates
{
}
";

        var (outputCompilation, diagnostics) = RunGenerator(
            userSource,
            new InMemoryAdditionalText("hello.liquid", "hi"),
            new InMemoryAdditionalText("table.liquid", "{% tablerow i in (1..2) %}{{ i }}{% endtablerow %}"),
            new InMemoryAdditionalText("missing.liquid", "{% render 'unknown' %}"));

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Equal(2, errors.Count);
        Assert.All(errors, d => Assert.Equal("FLUIDSG106", d.Id));
        Assert.Contains(errors, d => d.GetMessage().Contains("table.liquid") && d.GetMessage().Contains("TableRowStatement"));
        Assert.Contains(errors, d => d.GetMessage().Contains("missing.liquid") && d.GetMessage().Contains("unknown"));

        var assembly = EmitToAssembly(outputCompilation).Assembly;
        var templatesType = assembly.GetType("MyApp.Templates")!;
        Assert.Null(templatesType.GetProperty("Table", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templatesType.GetProperty("Missing", BindingFlags.Public | BindingFlags.Static));
        Assert.Equal("hi", await RenderAsync(assembly, "MyApp.Templates", "Hello"));
    }

    [Theory]
    [InlineData("{% case x %}{% when 1 %}one{% endcase %}")]
    [InlineData("{% case x %}{% when 1, 2 %}one{% when 'a' or 'b' %}two{% else %}other{% endcase %}")]
    [InlineData("{% case x %}{% when 1 %}   {% endcase %}")]
    [InlineData("{% if x %}a{% elsif y %}b{% else %}c{% endif %}{% unless x %}d{% elsif y %}e{% else %}f{% endunless %}")]
    [InlineData("{% if x %}  {% assign a = 1 %}  {% else %}  {% endif %}")]
    [InlineData("{% for i in (1..3) %}{% if i == 2 %}{% continue %}{% endif %}{{ forloop.index }}{{ forloop.rindex0 }}{{ forloop.first }}{% else %}none{% endfor %}")]
    [InlineData("{% for i in items limit: 2 offset: 1 reversed %}{{ i }}{% endfor %}{% for i in items offset: continue %}{{ i }}{% endfor %}")]
    [InlineData("{% for i in 'abc' %}{{ i }}{% endfor %}{% for i in (1..2) %}{% for j in (1..2) %}{{ forloop.parentloop.index }}{% break %}{% endfor %}{% endfor %}")]
    [InlineData("{% for i in (1..2) %}\n{% endfor %}")]
    [InlineData("{% assign a = 'x' | upcase %}{% capture b %}{{ a }}{% endcapture %}{{ b }}{% cycle 'c', 'd' %}{% cycle 'g': 'e', 'f' %}")]
    [InlineData("{% increment c %}{% decrement d %}{% raw %}{{ e }}{% endraw %}{% comment %}f{% endcomment %}{% echo 'g' %}")]
    [InlineData("{% liquid\nassign a = 1\necho a\nif a == 1\necho 'one'\nendif\n%}")]
    [InlineData("{% if 1 < 2 and 2 > 1 or 3 <= 3 and 3 >= 4 %}y{% endif %}{% if 'abc' contains 'b' and items contains 1 %}c{% endif %}")]
    [InlineData("{% if 'abc' startswith 'a' and 'abc' endswith 'c' %}s{% endif %}{% if x == nil and x != empty and x == blank %}n{% endif %}")]
    [InlineData("{% if 'b' > 'a' and 'a' < 'b' and true and false == false %}t{% endif %}{{ 1.5 }}{{ -3 }}{{ (1..x) | join: ',' }}")]
    [InlineData("{% if (1 == 1) and (2 > 1 or false) %}p{% endif %}")]
    [InlineData("{{ items[0] }}{{ items.first }}{{ items.size }}{{ page.title | default: 'none' | truncate: 3, '' }}{{ page['title'] }}")]
    [InlineData("{{ 'a,b' | split: ',' | join: '-' }}{{ 12.345 | round: 2 }}{{ 'x' | append: page.title }}{{ items | where: 'a', 1 | map: 'b' | first }}")]
    [InlineData("{% macro field(name, type = 'text') %}<input name=\"{{ name }}\" type=\"{{ type }}\">{% endmacro %}{{ field('a') }}{{ field('b', type = 'password') }}")]
    [InlineData("{% render 'partial' %}{% render 'partial' with 'w' as p %}{% render 'partial' for items as p %}{% render 'partial', p: 1 %}")]
    [InlineData("{% include 'partial' %}{% include 'partial' with 'w' as p %}{% include 'partial' for items as p %}{% include 'partial', p: 1 %}")]
    [InlineData("{% from 'macros' import field %}{{ field('a') }}{% from 'macros' %}")]
    [InlineData("  {%- if x -%}  {{- 'a' -}}  {%- endif -%}  \n  {{ 'b' }}  ")]
    public void TemplatesAttribute_Generates_Code_Without_Warnings(string source)
    {
        var userSource = @"
using Fluid.SourceGenerator;

namespace MyApp;

[FluidTemplates(""template.liquid"", AllowFunctions = true, AllowLiquidTag = true, AllowParentheses = true)]
public static partial class Templates
{
}
";

        var (outputCompilation, diagnostics) = RunGenerator(
            userSource,
            new InMemoryAdditionalText("template.liquid", source),
            new InMemoryAdditionalText("partial.liquid", "[{{ p }}]{% if p %}{% endif %}"),
            new InMemoryAdditionalText("macros.liquid", "{% macro field(name) %}{{ name }}{% endmacro %}"));

        Assert.Empty(diagnostics);

        // Users shouldn't have to suppress anything in the generated code: no obsolete API (CS0618) and no unused
        // variable (CS0219), at the warning level of the latest SDK.
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning));
    }

    [Fact]
    public void Custom_Arithmetic_Operators_Generate_Code_Without_Warnings()
    {
        // The arithmetic expressions are only created by operators an application registers, and are compiled with
        // the TemplateSourceGenerator API rather than the source generator, which uses its own parser.
        var parser = new FluidParser();
        parser.RegisteredOperators["plus"] = (a, b) => new AddBinaryExpression(a, b);
        parser.RegisteredOperators["minus"] = (a, b) => new SubtractBinaryExpression(a, b);
        parser.RegisteredOperators["times"] = (a, b) => new MultiplyBinaryExpression(a, b);
        parser.RegisteredOperators["divided"] = (a, b) => new DivideBinaryExpression(a, b);
        parser.RegisteredOperators["modulo"] = (a, b) => new ModuloBinaryExpression(a, b);

        var template = parser.Parse("{{ 1 plus 2 }}{{ 'a' plus 'b' }}{{ 3 minus 1 }}{{ 2 times 3 }}{{ 6 divided 4 }}{{ 7 modulo 3 }}");
        var source = template.Compile(new SourceGenerationOptions { Namespace = "MyApp", ClassName = "Arithmetic" });

        var compilation = CreateCompilation(source.SourceCode);

        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning));
    }

    [Theory]
    [InlineData("{% for i in (1..3) %}{% if i == 2 %}{% continue %}{% endif %}{{ i }}{% endfor %}", "13")]
    [InlineData("{% for i in (1..3) %}{% if i == 2 %}{% break %}{% endif %}{{ i }}{% endfor %}", "1")]
    [InlineData("{% for i in (1..2) %}{% endfor %}done", "done")]
    public async Task TemplatesAttribute_Loops_Honor_Break_And_Continue(string source, string expected)
    {
        var userSource = @"
using Fluid.SourceGenerator;

namespace MyApp;

[FluidTemplates(""**/*.liquid"")]
public static partial class Templates
{
}
";

        var (outputCompilation, diagnostics) = RunGenerator(userSource, new InMemoryAdditionalText("loop.liquid", source));

        Assert.Empty(diagnostics);
        Assert.Equal(expected, await RenderAsync(EmitToAssembly(outputCompilation).Assembly, "MyApp.Templates", "Loop"));
    }

    [Fact]
    public async Task Compiled_Templates_Render_Like_Interpreted_Templates()
    {
        var files = new Dictionary<string, string>
        {
            // Whitespace control, with the markers and with TemplateOptions.Trimming and Greedy
            ["t1.liquid"] = "a {%- assign x = 1 -%} b",
            ["t2.liquid"] = "{{- 'x' -}}  y \n {{ 'z' }} \n",
            ["t3.liquid"] = "a\n  {% if true %}\n  b\n  {% endif %}\n c",
            ["t4.liquid"] = "  {{ 'v' }}  \n  {% for i in (1..2) %} {{ i }} {% endfor %}  \n",
            ["t5.liquid"] = "\n\n  {%- if true -%}  \n  x  \n  {%- endif %}\n\n",
            ["t6.liquid"] = "{% if true %}   {% endif %}|{% for i in (1..2) %}\n{% endfor %}|",

            // include shares the scope, takes arguments and lets break and continue reach the loop
            ["t7.liquid"] = "{% include 'p_set' %}{{ v }}|{% include 'p_echo' with 'w' %}|{% include 'p_echo', p_echo: 'kw' %}",
            ["t8.liquid"] = "{% include 'p_echo' for (1..3) %}|{% include 'p_echo' for (1..2) as other %}",
            ["t9.liquid"] = "{% for i in (1..4) %}{% include 'p_skip' %}{{ i }}{% endfor %}",
            ["t10.liquid"] = "{% render 'p_echo' with 'r' %}|{% assign v = 1 %}{% render 'p_set' %}{{ v }}",

            // macros, local and imported
            ["t11.liquid"] = "{% macro hello(name, greeting = 'Hi') %}{{ greeting }} {{ name }}{% endmacro %}{{ hello('Ada') }} {{ hello('Bob', greeting = 'Yo') }}",
            ["t12.liquid"] = "{% from 'p_macros' import hello %}{{ hello('Ada') }}",
            ["t13.liquid"] = "{% from 'p_macros' %}{{ hello('Ada') }} {{ bye('Bob') }}",

            ["p_set.liquid"] = "{% assign v = 'set' %}",
            ["p_echo.liquid"] = "[{{ p_echo }}{{ other }}]",
            ["p_skip.liquid"] = "{% if i == 2 %}{% continue %}{% endif %}{% if i == 4 %}{% break %}{% endif %}",
            ["p_macros.liquid"] = "{% macro hello(name) %}Hello {{ name }}{% endmacro %}{% macro bye(name) %}Bye {{ name }}{% endmacro %}",
        };

        var userSource = @"
using Fluid.SourceGenerator;

namespace MyApp;

[FluidTemplates(""**/*.liquid"", AllowFunctions = true)]
public static partial class Templates
{
}
";

        var (outputCompilation, diagnostics) = RunGenerator(
            userSource,
            files.Select(x => (AdditionalText)new InMemoryAdditionalText(x.Key, x.Value)).ToArray());

        Assert.Empty(diagnostics);
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning));

        var templatesType = EmitToAssembly(outputCompilation).Assembly.GetType("MyApp.Templates")!;
        var parser = new FluidParser(new FluidParserOptions { AllowFunctions = true });
        var fileProvider = new MockFileProvider();
        foreach (var file in files)
        {
            fileProvider.Add(file.Key, file.Value);
        }

        var trimmings = new[] { TrimmingFlags.None, TrimmingFlags.TagLeft | TrimmingFlags.TagRight, TrimmingFlags.OutputLeft | TrimmingFlags.OutputRight, TrimmingFlags.TagLeft | TrimmingFlags.OutputRight };
        var mismatches = new List<string>();

        foreach (var file in files.Where(x => x.Key.StartsWith('t')))
        {
            var compiled = (IFluidTemplate)templatesType
                .GetProperty(Path.GetFileNameWithoutExtension(file.Key).ToUpperInvariant(), BindingFlags.Public | BindingFlags.Static)!
                .GetValue(null)!;

            foreach (var trimming in trimmings)
            {
                foreach (var greedy in new[] { true, false })
                {
                    var options = new TemplateOptionsBuilder()
                        .WithFileProvider(fileProvider)
                        .WithTrimming(trimming)
                        .WithGreedy(greedy)
                        .Build();

                    // An interpreted template keeps the whitespace of the first options it's rendered with.
                    var expected = await parser.Parse(file.Value).RenderAsync(new TemplateContext(options));
                    var actual = await compiled.RenderAsync(new TemplateContext(options));

                    if (expected != actual)
                    {
                        mismatches.Add($"{file.Key} ({trimming}, greedy: {greedy}): expected '{expected}', got '{actual}'");
                    }
                }
            }
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    [Fact]
    public void TemplatesAttribute_Reports_Include_With_Dynamic_Path()
    {
        var userSource = @"
using Fluid.SourceGenerator;

namespace MyApp;

[FluidTemplates(""**/*.liquid"")]
public static partial class Templates
{
}
";

        var (_, diagnostics) = RunGenerator(userSource, new InMemoryAdditionalText("hello.liquid", "{% assign name = 'x' %}{% include name %}"));

        var error = Assert.Single(diagnostics);
        Assert.Equal("FLUIDSG106", error.Id);
        Assert.Contains("dynamic path", error.GetMessage());
    }

    [Fact]
    public async Task TemplatesFolder_Makes_Paths_Relative()
    {
        var userSource = @"
using Fluid.SourceGenerator;

namespace MyApp;

[FluidTemplates(""*.liquid"")]
public static partial class Templates
{
}
";

        var compilation = CreateCompilation(userSource);
        var additional = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("/app/Templates/hello.liquid", "{% render 'partials/footer' %}"),
            new InMemoryAdditionalText("/app/Templates/partials/footer.liquid", "footer"),
            new InMemoryAdditionalText("/app/Other/other.liquid", "other"));

        var optionsProvider = new GlobalOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.ProjectDir"] = "/app/",
            ["build_property.FluidTemplatesFolder"] = "Templates"
        });

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new FluidTemplateGenerator().AsSourceGenerator() },
            additionalTexts: additional,
            optionsProvider: optionsProvider);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics);

        var assembly = EmitToAssembly(outputCompilation).Assembly;
        Assert.Null(assembly.GetType("MyApp.Templates")!.GetProperty("Other", BindingFlags.Public | BindingFlags.Static));
        Assert.Equal("footer", await RenderAsync(assembly, "MyApp.Templates", "Hello"));
    }

    [Fact]
    public void Package_Makes_TemplatesFolder_Visible_To_Generator()
    {
        var project = XDocument.Load(Path.Combine(FindRepositoryRoot(), "Fluid.SourceGenerator", "Fluid.SourceGenerator.csproj"));
        var packedProps = project.Descendants("None")
            .Single(e => (string)e.Attribute("Include") == @"build\Fluid.SourceGenerator.props");

        Assert.Equal("build;buildTransitive", (string)packedProps.Attribute("PackagePath"));

        var props = XDocument.Load(Path.Combine(FindRepositoryRoot(), "Fluid.SourceGenerator", "build", "Fluid.SourceGenerator.props"));
        Assert.Contains(props.Descendants("CompilerVisibleProperty"), e => (string)e.Attribute("Include") == "FluidTemplatesFolder");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Fluid.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static (Compilation Compilation, ImmutableArray<Diagnostic> Diagnostics) RunGenerator(string userSource, params AdditionalText[] additionalTexts)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new FluidTemplateGenerator().AsSourceGenerator() },
            additionalTexts: additionalTexts);

        driver.RunGeneratorsAndUpdateCompilation(CreateCompilation(userSource), out var outputCompilation, out var diagnostics);
        return (outputCompilation, diagnostics);
    }

    private static async Task<string> RenderAsync(Assembly assembly, string typeName, string propertyName)
    {
        var template = (IFluidTemplate)assembly.GetType(typeName)!
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

        var sw = new StringWriter();
        await template.RenderAsync(sw, HtmlEncoder.Default, new TemplateContext());
        return sw.ToString();
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Buffers.IBufferWriter<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(TextEncoder).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Collections.dll")),
            MetadataReference.CreateFromFile(typeof(IFluidTemplate).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(FluidTemplateGenerator).Assembly.Location)
        };

        // Some BCL assemblies are not directly referenced by the above depending on runtime.
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location))
            {
                continue;
            }

            // Avoid duplicates
            if (references.Any(r => string.Equals(r.Display, asm.Location, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (asm.GetName().Name is "System.Runtime" or "netstandard")
            {
                references.Add(MetadataReference.CreateFromFile(asm.Location));
            }
        }

        return CSharpCompilation.Create(
            assemblyName: "MyApp.GeneratedTests",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, warningLevel: 9999));
    }

    private static (Assembly Assembly, ImmutableArray<Diagnostic> Diagnostics) EmitToAssembly(Compilation compilation)
    {
        using var peStream = new MemoryStream();
        using var pdbStream = new MemoryStream();

        var emitResult = compilation.Emit(peStream, pdbStream);

        if (!emitResult.Success)
        {
            var diagText = string.Join("\n", emitResult.Diagnostics.Select(d => d.ToString()));
            throw new InvalidOperationException(diagText);
        }

        return (Assembly.Load(peStream.ToArray()), emitResult.Diagnostics);
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content, Encoding.UTF8);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }

    private sealed class GlobalOptionsProvider : AnalyzerConfigOptionsProvider
    {
        public GlobalOptionsProvider(Dictionary<string, string> values)
        {
            GlobalOptions = new DictionaryOptions(values);
        }

        public override AnalyzerConfigOptions GlobalOptions { get; }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => DictionaryOptions.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => DictionaryOptions.Empty;
    }

    private sealed class DictionaryOptions : AnalyzerConfigOptions
    {
        public static readonly DictionaryOptions Empty = new(new Dictionary<string, string>());

        private readonly Dictionary<string, string> _values;

        public DictionaryOptions(Dictionary<string, string> values)
        {
            _values = values;
        }

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string value) => _values.TryGetValue(key, out value);
    }
}
