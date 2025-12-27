using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid;
using Fluid.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using System.Threading;

namespace Fluid.Tests
{
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

        private static CSharpCompilation CreateCompilation(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(TextEncoder).Assembly.Location),
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
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
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
    }
}
