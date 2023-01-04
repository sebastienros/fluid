#if NETCOREAPP3_1_OR_GREATER
using Fluid.Ast;
using Fluid.Compilation;
using Fluid.Parser;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Encodings.Web;

namespace Fluid;

public static partial class FluidParserExtensions
{
    public static (string Body, string StaticStatements, string GlobalStatements, string Variables) GenerateCode(this FluidParser parser, string template)
    {
        var result = GenerateCodeInternal(parser, template);

        return (result.Body, result.StaticStatements, result.GlobalStatements, result.Variables);
    }

    public static (string Body, string StaticStatements, string GlobalStatements, string Variables, IStatementList FluidTemplate) GenerateCodeInternal(this FluidParser parser, string template)
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

        return (compilationResult.ToString(), 
            String.Join(Environment.NewLine, compilationContext.StaticStatements),
            String.Join(Environment.NewLine, compilationContext.GlobalStatements),
            String.Join(Environment.NewLine, compilationContext.GlobalMembers), compilable);
    }

    public static IFluidTemplate Compile<T>(this FluidParser parser, string template)
    {
        var fluidTemplate = parser.Parse(template) as FluidTemplate;

        var compiler = new AstCompiler(TemplateOptions.Default);

        var builder = new StringBuilder();

        builder.AppendLine($@"
using Fluid;
using Fluid.Ast;
using Fluid.Compilation;
using Fluid.Values;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

public class MyTemplate : CompiledTemplateBase, IFluidTemplate
{{
");

        compiler.RenderTemplate(typeof(T), "", fluidTemplate, builder);

        builder.AppendLine($@"
}}
");

        var source = builder.ToString();

        var codeString = SourceText.From(source);

        // Debug generated code
        File.WriteAllText("c:\\temp\\fluid.cs", source);

        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp11);

        var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(codeString, options);

        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location);


        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "mscorlib.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Core.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Linq.dll")),

            MetadataReference.CreateFromFile(typeof(FluidTemplate).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(TextEncoder).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ValueTask<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(T).Assembly.Location),
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
                var failures = compiled.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

                var errors = new StringBuilder();

                foreach (var diagnostic in failures)
                {
                    errors.AppendFormat("{0}: {1}\n", diagnostic.Id, diagnostic.GetMessage());
                }

                Console.Error.WriteLine(errors.ToString());

                throw new ApplicationException("Errors in template compilation: \n" + errors.ToString());
            }

            stream.Seek(0, SeekOrigin.Begin);

            var assemblyLoadContext = new SimpleUnloadableAssemblyLoadContext();

            assembly = assemblyLoadContext.LoadFromStream(stream);
        }

        var myTemplateType = assembly.GetType("MyTemplate");
        var myTemplateInstance = Activator.CreateInstance(myTemplateType);
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