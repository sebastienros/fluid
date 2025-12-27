using System.Text.Encodings.Web;
using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class OutputStatement : Statement, ISourceable
    {
        public OutputStatement(Expression expression)
        {
            Expression = expression;
        }

        public Expression Expression { get; }

        public IReadOnlyList<FilterExpression> Filters { get; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            static async ValueTask<Completion> Awaited(
                ValueTask<FluidValue> t,
                TextWriter w,
                TextEncoder enc,
                TemplateContext ctx)
            {
                var value = await t;
                await value.WriteToAsync(w, enc, ctx.CultureInfo);
                return Completion.Normal;
            }

            context.IncrementSteps();

            var task = Expression.EvaluateAsync(context);
            if (task.IsCompletedSuccessfully)
            {
                var valueTask = task.Result.WriteToAsync(writer, encoder, context.CultureInfo);

                if (valueTask.IsCompletedSuccessfully)
                {
                    return new ValueTask<Completion>(Completion.Normal);
                }

                return AwaitedWriteTo(valueTask);

                static async ValueTask<Completion> AwaitedWriteTo(ValueTask t)
                {
                    await t;
                    return Completion.Normal;
                }
            }

            return Awaited(task, writer, encoder, context);
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitOutputStatement(this);

        public void WriteTo(SourceGenerationContext context)
        {
            var exprMethod = context.GetExpressionMethodName(Expression);

            context.WriteLine($"{context.ContextName}.IncrementSteps();");

            context.WriteLine($"var task = {exprMethod}({context.ContextName});");
            context.WriteLine("if (task.IsCompletedSuccessfully)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"var valueTask = task.Result.WriteToAsync({context.WriterName}, {context.EncoderName}, {context.ContextName}.CultureInfo);");
                context.WriteLine("if (valueTask.IsCompletedSuccessfully)");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine("return Completion.Normal;");
                }
                context.WriteLine("}");

                context.WriteLine("return await AwaitedWriteTo(valueTask);");
                context.WriteLine();
                context.WriteLine("static async ValueTask<Completion> AwaitedWriteTo(ValueTask t)");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine("await t;");
                    context.WriteLine("return Completion.Normal;");
                }
                context.WriteLine("}");
            }
            context.WriteLine("}");

            context.WriteLine($"return await Awaited(task, {context.WriterName}, {context.EncoderName}, {context.ContextName});");
            context.WriteLine();
            context.WriteLine("static async ValueTask<Completion> Awaited(ValueTask<FluidValue> t, TextWriter w, TextEncoder enc, TemplateContext ctx)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("var value = await t;");
                context.WriteLine("await value.WriteToAsync(w, enc, ctx.CultureInfo);");
                context.WriteLine("return Completion.Normal;");
            }
            context.WriteLine("}");
        }
    }
}
