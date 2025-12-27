using Fluid.Utils;
using Fluid.Values;
using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class CaptureStatement : TagStatement, ISourceable
    {
        public CaptureStatement(string identifier, IReadOnlyList<Statement> statements) : base(statements)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var completion = Completion.Normal;

            using var sb = StringBuilderPool.GetInstance();
            using var sw = new StringWriter(sb.Builder);
            for (var i = 0; i < Statements.Count; i++)
            {
                completion = await Statements[i].WriteToAsync(sw, encoder, context);

                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    break;
                }
            }

            FluidValue result = new StringValue(sw.ToString(), false);

            // Substitute the result if a custom callback is provided
            if (context.Captured != null)
            {
                result = await context.Captured.Invoke(Identifier, result, context);
            }

            // Don't encode captured blocks
            context.SetValue(Identifier, result);

            return completion;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitCaptureStatement(this);

        public void WriteTo(SourceGenerationContext context)
        {
            var identifierLit = SourceGenerationContext.ToCSharpStringLiteral(Identifier);

            context.WriteLine("var completion = Completion.Normal;");
            context.WriteLine("using var sw = new StringWriter();");

            context.WriteLine($"for (var i = 0; i < {Statements.Count}; i++)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("switch (i)");
                context.WriteLine("{");
                using (context.Indent())
                {
                    for (var i = 0; i < Statements.Count; i++)
                    {
                        var stmtMethod = context.GetStatementMethodName(Statements[i]);
                        context.WriteLine($"case {i}: completion = await {stmtMethod}(sw, {context.EncoderName}, {context.ContextName}); break;");
                    }
                    context.WriteLine("default: completion = Completion.Normal; break;");
                }
                context.WriteLine("}");

                context.WriteLine("if (completion != Completion.Normal) break;");
            }
            context.WriteLine("}");

            context.WriteLine("FluidValue result = new StringValue(sw.ToString(), false);");
            context.WriteLine($"if ({context.ContextName}.Captured != null)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"result = await {context.ContextName}.Captured.Invoke({identifierLit}, result, {context.ContextName});");
            }
            context.WriteLine("}");
            context.WriteLine($"{context.ContextName}.SetValue({identifierLit}, result);");
            context.WriteLine("return completion;");
        }
    }
}
