using Fluid.Utils;
using Fluid.Values;
using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast;

public sealed class MacroStatement : TagStatement, ISourceable
{
    public MacroStatement(string identifier, IReadOnlyList<FunctionCallArgument> arguments, IReadOnlyList<Statement> statements) : base(statements)
    {
        Identifier = identifier;
        Arguments = arguments ?? [];
    }

    public string Identifier { get; }
    public IReadOnlyList<FunctionCallArgument> Arguments { get; }

    public override bool IsWhitespaceOrCommentOnly => true;

    public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        // Evaluate all default values only once
        var defaultValues = new Dictionary<string, FluidValue>();

        for (var i = 0; i < Arguments.Count; i++)
        {
            var argument = Arguments[i];
            defaultValues[argument.Name] = argument.Expression == null ? NilValue.Instance : await argument.Expression.EvaluateAsync(context);
        }

        var f = FunctionValue.Create(async (args, c) =>
        {
            using var macroBuffer = new BufferFluidOutput();
            var macroOutput = LimitedFluidOutput.Create(macroBuffer, context.MaxOutputSize);

            using var scope = context.EnterScope(ScopeBehavior.Local);

            {
                // Initialize the local context with the default values
                foreach (var a in defaultValues)
                {
                    context.SetValue(a.Key, a.Value);
                }

                // A named argument goes to the parameter with that name. The arguments before the first
                // named one fill the parameters in order, and the others keep their default. Arguments
                // beyond the ones the macro declares are ignored.
                var positionalCount = args.PositionalCount;

                for (var i = 0; i < Arguments.Count; i++)
                {
                    var name = Arguments[i].Name;

                    if (args.HasNamed(name))
                    {
                        context.SetValue(name, args[name]);
                    }
                    else if (i < positionalCount)
                    {
                        context.SetValue(name, args.At(i));
                    }
                }

                for (var i = 0; i < Statements.Count; i++)
                {
                    var completion = await Statements[i].WriteToAsync(macroOutput, encoder, context);

                    if (completion != Completion.Normal)
                    {
                        // Stop processing the block statements
                        // We return the completion to flow it to the outer loop
                        break;
                    }
                }

                var result = macroBuffer.ToString();

                // Don't encode the result
                return StringValue.Create(result, false);
            }
        });

        context.SetValue(Identifier, f);

        return Completion.Normal;
    }

    protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitMacroStatement(this);

    public void WriteTo(SourceGenerationContext context)
    {
        var identifierLit = SourceGenerationContext.ToCSharpStringLiteral(Identifier);

        // Evaluate all default values only once per render.
        context.WriteLine("var defaultValues = new Dictionary<string, FluidValue>();");
        for (var i = 0; i < Arguments.Count; i++)
        {
            var arg = Arguments[i];
            var argNameLit = SourceGenerationContext.ToCSharpStringLiteral(arg.Name);
            if (arg.Expression == null)
            {
                context.WriteLine($"defaultValues[{argNameLit}] = NilValue.Instance;");
            }
            else
            {
                var argExpr = context.GetExpressionMethodName(arg.Expression);
                context.WriteLine($"defaultValues[{argNameLit}] = await {argExpr}({context.ContextName});");
            }
        }

        context.WriteLine();
        context.WriteLine("var f = FunctionValue.Create(async (args, c) =>");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine("using var sw = new StringWriter();");
            context.WriteLine("await using var macroBuffer = new TextWriterFluidOutput(sw, 16 * 1024, leaveOpen: true);");
            context.WriteLine($"var macroOutput = LimitedFluidOutput.Create(macroBuffer, {context.ContextName}.MaxOutputSize);");
            context.WriteLine($"using var scope = {context.ContextName}.EnterScope(ScopeBehavior.Local);");
            context.WriteLine("try");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("// Initialize the local context with the default values");
                context.WriteLine("foreach (var a in defaultValues)");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine($"{context.ContextName}.SetValue(a.Key, a.Value);");
                }
                context.WriteLine("}");

                // Same binding as the interpreter, unrolled per parameter.
                context.WriteLine("var positionalCount = args.PositionalCount;");
                for (var i = 0; i < Arguments.Count; i++)
                {
                    var nameLit = SourceGenerationContext.ToCSharpStringLiteral(Arguments[i].Name);
                    context.WriteLine($"if (args.HasNamed({nameLit})) {context.ContextName}.SetValue({nameLit}, args[{nameLit}]);");
                    context.WriteLine($"else if (positionalCount > {i}) {context.ContextName}.SetValue({nameLit}, args.At({i}));");
                }

                context.WriteLine("var completion = Completion.Normal;");
                context.WriteLine($"for (var si = 0; si < {Statements.Count}; si++)");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine("switch (si)");
                    context.WriteLine("{");
                    using (context.Indent())
                    {
                        for (var s = 0; s < Statements.Count; s++)
                        {
                            var stmtMethod = context.GetStatementMethodName(Statements[s]);
                            context.WriteLine($"case {s}: completion = await {stmtMethod}(macroOutput, {context.EncoderName}, {context.ContextName}); break;");
                        }
                        context.WriteLine("default: completion = Completion.Normal; break;");
                    }
                    context.WriteLine("}");
                    context.WriteLine("if (completion != Completion.Normal) break;");
                }
                context.WriteLine("}");

                context.WriteLine("await macroOutput.FlushAsync();");
                context.WriteLine("var result = sw.ToString();");
                context.WriteLine("return StringValue.Create(result, false);");
            }
            context.WriteLine("}");
            context.WriteLine("finally");
            context.WriteLine("{");
            using (context.Indent())
            {
            }
            context.WriteLine("}");
        }
        context.WriteLine("});");

        context.WriteLine($"{context.ContextName}.SetValue({identifierLit}, f);");
        context.WriteLine("return Completion.Normal;");
    }
}
