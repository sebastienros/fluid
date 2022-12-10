#if NETCOREAPP3_1_OR_GREATER
using Fluid.Ast;
using Fluid.Compilation;
using Fluid.Parser;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Encodings.Web;

namespace Fluid;

public static partial class FluidParserExtensions
{
    public static string GenerateCode(this FluidParser parser, string template)
    {
        return GenerateCodeInternal(parser, template).Code;
    }

    public static (string Code, IStatementList FluidTemplate) GenerateCodeInternal(this FluidParser parser, string template)
    {
        var fluidTemplate = parser.Parse(template);

        var compilable = fluidTemplate as FluidTemplate;

        if (compilable == null)
        {
            throw new NotSupportedException("The template could not be compiled");
        }

        var compilationContext = new CompilationContext();
        compilationContext.Caller = "_template";

        var compilationResult = compilable.Compile(compilationContext);

        return (compilationResult.StringBuilder.ToString(), compilable);
    }

    public static IFluidTemplate Compile(this FluidParser parser, string template)
    { 
        var source = """
using Fluid;
using Fluid.Ast;
using Fluid.Values;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

public class MyTemplate : IFluidTemplate
{
    private IStatementList _template;

    public MyTemplate(IStatementList template)
    {
        _template = template;
    }

    public async ValueTask RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        $SOURCE$

        await Task.CompletedTask;
    }
}
""";

        var (code, fluidTemplate) = GenerateCodeInternal(parser, template);

        var sourceCode = source.Replace("$SOURCE$", code);

        var codeString = SourceText.From(sourceCode);

        File.WriteAllText("c:\\temp\\fluid.cs", sourceCode);

        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp11);

        var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(codeString, options);

        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location);


        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "mscorlib.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Core.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Runtime.dll")),

            MetadataReference.CreateFromFile(typeof(FluidTemplate).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(TextEncoder).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ValueTask<>).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create("Hello.dll",
            new[] { parsedSyntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default));

        Assembly assembly;

        using (var stream = new MemoryStream())
        {
            var compiled = compilation.Emit(stream);

            if (!compiled.Success)
            {
                throw new ApplicationException("Errors in template compilation.");

                //var failures = compiled.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

                //foreach (var diagnostic in failures)
                //{
                //    Console.Error.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                //}
            }

            stream.Seek(0, SeekOrigin.Begin);

            var assemblyLoadContext = new SimpleUnloadableAssemblyLoadContext();

            assembly = assemblyLoadContext.LoadFromStream(stream);
        }

        var myTemplateType = assembly.GetType("MyTemplate");
        var myTemplateInstance = Activator.CreateInstance(myTemplateType, fluidTemplate);
        var myFluidTemplate = myTemplateInstance as IFluidTemplate;

        var compiledTemplate = new CompiledTemplate(myFluidTemplate);
        return compiledTemplate;
    }

    internal class SimpleUnloadableAssemblyLoadContext : AssemblyLoadContext
    {
        public SimpleUnloadableAssemblyLoadContext()
            : base(true)
        {
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return null;
        }
    }
}
#endif