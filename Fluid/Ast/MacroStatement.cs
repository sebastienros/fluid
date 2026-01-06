using Fluid.Utils;
using Fluid.Values;
using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class MacroStatement : TagStatement, ISourceable
    {
        public MacroStatement(string identifier, IReadOnlyList<FunctionCallArgument> arguments, IReadOnlyList<Statement> statements) : base(statements)
        {
            Identifier = identifier;
            Arguments = arguments ?? [];
        }

        public string Identifier { get; }
        public IReadOnlyList<FunctionCallArgument> Arguments { get; }

        public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            // Evaluate all default values only once
            var defaultValues = new Dictionary<string, FluidValue>();

            for (var i = 0; i < Arguments.Count; i++)
            {
                var argument = Arguments[i];
                defaultValues[argument.Name] = argument.Expression == null ? NilValue.Instance : await argument.Expression.EvaluateAsync(context);
            }

            var f = new FunctionValue(async (args, c) =>
            {
                using var macroOutput = new BufferFluidOutput();

                context.EnterChildScope();

                try
                {
                    // Initialize the local context with the default values
                    foreach (var a in defaultValues)
                    {
                        context.SetValue(a.Key, a.Value);
                    }

                    var namedArguments = false;

                    // Apply all arguments from the invocation.
                    // As soon as a named argument is used, all subsequent ones need a name too.

                    for (var i = 0; i < args.Count; i++)
                    {
                        var positionalName = Arguments[i].Name;

                        namedArguments |= args.HasNamed(positionalName);

                        if (!namedArguments)
                        {
                            context.SetValue(positionalName, args.At(i));
                        }
                        else
                        {
                            context.SetValue(positionalName, args[positionalName]);
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

                    var result = macroOutput.ToString();

                    // Don't encode the result
                    return new StringValue(result, false);
                }
                finally
                {
                    context.ReleaseScope();
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
            context.WriteLine("var f = new FunctionValue(async (args, c) =>");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("using var sw = new StringWriter();");
                context.WriteLine($"{context.ContextName}.EnterChildScope();");
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

                    context.WriteLine("var namedArguments = false;");
                    context.WriteLine("for (var i = 0; i < args.Count; i++)");
                    context.WriteLine("{");
                    using (context.Indent())
                    {
                        context.WriteLine("string positionalName = null;");
                        // Generate positional name mapping from Arguments
                        for (var i = 0; i < Arguments.Count; i++)
                        {
                            var nameLit = SourceGenerationContext.ToCSharpStringLiteral(Arguments[i].Name);
                            context.WriteLine($"if (i == {i}) positionalName = {nameLit};");
                        }

                        context.WriteLine("namedArguments |= positionalName != null && args.HasNamed(positionalName);");
                        context.WriteLine("if (!namedArguments)");
                        context.WriteLine("{");
                        using (context.Indent())
                        {
                            context.WriteLine($"if (positionalName != null) {context.ContextName}.SetValue(positionalName, args.At(i));");
                        }
                        context.WriteLine("}");
                        context.WriteLine("else");
                        context.WriteLine("{");
                        using (context.Indent())
                        {
                            context.WriteLine($"if (positionalName != null) {context.ContextName}.SetValue(positionalName, args[positionalName]);");
                        }
                        context.WriteLine("}");
                    }
                    context.WriteLine("}");

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
                                context.WriteLine($"case {s}: completion = await {stmtMethod}(sw, {context.EncoderName}, {context.ContextName}); break;");
                            }
                            context.WriteLine("default: completion = Completion.Normal; break;");
                        }
                        context.WriteLine("}");
                        context.WriteLine("if (completion != Completion.Normal) break;");
                    }
                    context.WriteLine("}");

                    context.WriteLine("var result = sw.ToString();");
                    context.WriteLine("return new StringValue(result, false);");
                }
                context.WriteLine("}");
                context.WriteLine("finally");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine($"{context.ContextName}.ReleaseScope();");
                }
                context.WriteLine("}");
            }
            context.WriteLine("});");

            context.WriteLine($"{context.ContextName}.SetValue({identifierLit}, f);");
            context.WriteLine("return Completion.Normal;");
        }
    }
}
