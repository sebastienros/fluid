#if COMPILATION_SUPPORTED
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
    private static readonly object _synLock = new();

    public static IFluidTemplate Compile<T>(this FluidParser parser, string template) => parser.Compile(template, typeof(T));
    public static IFluidTemplate Compile<T>(this FluidTemplate fluidTemplate) => fluidTemplate.Compile(typeof(T));

    public static IFluidTemplate Compile(this FluidParser parser, string template, Type modelType)
    {
        var fluidTemplate = parser.Parse(template) as FluidTemplate;
        return fluidTemplate.Compile(modelType);
    }

    public static IFluidTemplate Compile(this FluidTemplate fluidTemplate, Type modelType)
    {
        var compiler = new AstCompiler(TemplateOptions.Default);

        var mainBuilder = new StringBuilder();

        var builder = new StringBuilder();
        var staticsBuilder = new StringBuilder();
        var staticConstructorBuilder = new StringBuilder();

        compiler.RenderTemplate(modelType, "", fluidTemplate, builder, staticsBuilder, staticConstructorBuilder);

        var className = "MyTemplate";

        mainBuilder.AppendLine($@"
using Fluid;
using Fluid.Ast;
using Fluid.Ast.BinaryExpressions;
using Fluid.Compilation;
using Fluid.Values;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

public sealed class {className} : CompiledTemplateBase, IFluidTemplate
{{
");
        mainBuilder.Append(staticsBuilder);

        if (staticConstructorBuilder.Length > 0)
        {
            mainBuilder.AppendLine($@"
    static {className}()
    {{
");
            mainBuilder.Append(staticConstructorBuilder);
            mainBuilder.AppendLine($@"    }}
");

        }
        mainBuilder.Append(builder);
        mainBuilder.AppendLine($@"
}}
");

        var source = mainBuilder.ToString();

        var codeString = SourceText.From(source);

        // Debug generated code
        lock (_synLock)
        {
            var filename = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fluid.cs"));
            Console.WriteLine(filename);
            Console.WriteLine(source);
            File.WriteAllText(filename, source);
        }

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
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Collections.dll")),

            MetadataReference.CreateFromFile(typeof(FluidTemplate).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(TextEncoder).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(TextWriter).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ValueTask<>).Assembly.Location),
            MetadataReference.CreateFromFile(modelType.Assembly.Location),
        };

        var compilation = CSharpCompilation.Create("Hello.dll",
            new[] { parsedSyntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default
                )
        );

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