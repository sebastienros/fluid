using Fluid;
using Fluid.Compilation;
using Fluid.Parser;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Diagnostics;
using System.Reflection;
using System.Text;

#nullable enable

[Generator]
public class LiquidGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        Debug.WriteLine("Execute code generator");

#if DEBUG
        //if (!Debugger.IsAttached)
        //{
        //    Debugger.Launch();
        //}
#endif

        var receiver = context.SyntaxReceiver as LiquidRenderReceiver;
        if (receiver is null) return;

        StringBuilder sb = new();
        sb.AppendLine($@"// Source Generated at {DateTimeOffset.Now:R}
using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Fluid;
using Fluid.Values;

public class LiquidTemplates
{{
");
        sb.AppendLine("// Seeking for additional files...");
        foreach (var file in context.AdditionalFiles)
        {
            sb.AppendLine($"// Processing '{file.Path}'");

            var isLiquidTemplate = string.Equals(
                context.AnalyzerConfigOptions.GetOptions(file).TryGetAdditionalFileMetadataValue("IsLiquidTemplate"),
                "true",
                StringComparison.OrdinalIgnoreCase
            );

            if (!isLiquidTemplate)
                continue;

            var content = file.GetText(context.CancellationToken)?.ToString();

            if (string.IsNullOrWhiteSpace(content))
                continue;

            ProcessFile(context, file.Path, content!, receiver, sb);
        }

        sb.AppendLine(@"
} // end
");
        context.AddSource("LiquidTemplates", sb.ToString());
    }

    const int SpacesPerIndent = 4;
    private static void ProcessFile(in GeneratorExecutionContext context, string filePath, string content, LiquidRenderReceiver? receiver, StringBuilder builder)
    {
        // Generate class name from file name
        var templateName = SanitizeIdentifier(Path.GetFileNameWithoutExtension(filePath));

        // Always output non-specific writer
        builder.AppendLine(@$"
        public static void Render{templateName}<T>(T model, TextWriter writer)
        {{
            // Emitted as an initial call site for the template,
            // when actually called a specific call site for the exact model will be additionally be emitted.
            throw new NotImplementedException();        
        }}
");

        List<InvocationExpressionSyntax>? invocations = null;
        if (receiver?.Invocations?.TryGetValue(templateName, out invocations) ?? false)
        {
            Debug.Assert(invocations != null);

            foreach (var invocation in invocations!)
            {
                var arguments = invocation.ArgumentList.Arguments;
                if (arguments.Count != 2) continue;

                var semanticModel = context.Compilation.GetSemanticModel(invocation.SyntaxTree);
                var modelType = semanticModel.GetTypeInfo(arguments[0].Expression).Type;

                // var template = new FluidParser().Parse(content) as FluidTemplate;

                // var compiler = new AstCompiler();

                // compiler.RenderTemplate(modelType!, templateName, template!, builder);
            }
        }
    }

    private static string SanitizeIdentifier(string symbolName)
    {
        if (string.IsNullOrWhiteSpace(symbolName)) return string.Empty;

        var sb = new StringBuilder(symbolName.Length);
        if (!char.IsLetter(symbolName[0]))
        {
            // Must start with a letter or an underscore
            sb.Append('_');
        }

        var capitalize = true;
        foreach (var ch in symbolName)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                capitalize = true;
                continue;
            }

            sb.Append(capitalize ? char.ToUpper(ch) : ch);
            capitalize = false;
        }

        return sb.ToString();
    }

    public void Initialize(GeneratorInitializationContext context)
        => context.RegisterForSyntaxNotifications(() => new LiquidRenderReceiver());

    class LiquidRenderReceiver : ISyntaxReceiver
    {
        public Dictionary<string, List<InvocationExpressionSyntax>>? Invocations { get; private set; }

        public void OnVisitSyntaxNode(SyntaxNode node)
        {
            if (node.IsKind(SyntaxKind.InvocationExpression) &&
                node is InvocationExpressionSyntax invocation)
            {
                var expression = invocation.Expression;
                if (expression is MemberAccessExpressionSyntax member)
                {
                    var isLiquid = false;
                    string? template = null;
                    if (member.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    {
                        foreach (SyntaxNode child in expression.ChildNodes())
                        {
                            if (!isLiquid)
                            {
                                if (child is IdentifierNameSyntax classIdent)
                                {
                                    var valueText = classIdent.Identifier.ValueText;
                                    // Console.Error.WriteLine(valueText);
                                    if (classIdent.Identifier.ValueText == "LiquidTemplates")
                                    {
                                        isLiquid = true;
                                        continue;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                else
                                {
                                    break;
                                }
                            }

                            if (child is IdentifierNameSyntax methodIdent)
                            {
                                var valueText = methodIdent.Identifier.ValueText;
                                if (valueText.IndexOf("Render", StringComparison.Ordinal) == 0)
                                {
                                    template = valueText.Substring("Render".Length);
                                }
                                break;
                            }
                        }

                        if (isLiquid && template is not null)
                        {
                            if ((Invocations ??= new()).TryGetValue(template, out var list))
                            {
                                list.Add(invocation);
                            }
                            else
                            {
                                Invocations.Add(template, new() { invocation });
                            }
                        }
                    }
                }
            }
        }
    }
}

internal static class SourceGeneratorExtensions
{
    public static string? TryGetValue(this AnalyzerConfigOptions options, string key) =>
        options.TryGetValue(key, out var value) ? value : null;

    public static string? TryGetAdditionalFileMetadataValue(this AnalyzerConfigOptions options, string propertyName) =>
        options.TryGetValue($"build_metadata.AdditionalFiles.{propertyName}");
}