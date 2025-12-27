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

namespace Fluid.Tests
{
    public class SourceGenerationTests
    {
        [Theory]
        [InlineData("Hello", "Hello")]
        [InlineData("{{ 1 }}", "1")]
        [InlineData("{% assign x = 1 %}{{ x }}", "1")]
        [InlineData("{% if true %}a{% else %}b{% endif %}", "a")]
        [InlineData("{% unless false %}a{% endunless %}", "a")]
        [InlineData("{% for i in (1..3) %}{{ i }}{% endfor %}", "123")]
        [InlineData("{% for i in (1..3) %}{% if i == 2 %}{% break %}{% endif %}{{ i }}{% endfor %}", "1")]
        [InlineData("{% capture x %}hi{% endcapture %}{{ x }}", "hi")]
        [InlineData("{% cycle 'a','b' %}{% cycle 'a','b' %}{% cycle 'a','b' %}", "aba")]
        [InlineData("{% increment x %}{% increment x %}{% decrement x %}", "010")]
        [InlineData("{% case 2 %}{% when 1 %}a{% when 2 %}b{% else %}c{% endcase %}", "b")]
        public async Task GeneratedTemplate_MatchesRuntime(string liquid, string expected)
        {
            var parser = new FluidParser();
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

            var runtimeContext = new TemplateContext();
            runtimeContext.Options.FileProvider = provider;

            var runtimeWriter = new StringWriter();
            await template.RenderAsync(runtimeWriter, HtmlEncoder.Default, runtimeContext);

            var generatedWriter = new StringWriter();
            await instance.RenderAsync(generatedWriter, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("hi", runtimeWriter.ToString());
            Assert.Equal("hi", generatedWriter.ToString());
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
            public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
            {
                return Normal();
            }
        }
    }
}
