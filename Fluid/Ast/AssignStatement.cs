using System.Text.Encodings.Web;
using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class AssignStatement : Statement, ISourceable
    {
        public AssignStatement(string identifier, Expression value)
        {
            Identifier = identifier;
            Value = value;
        }

        public string Identifier { get; }

        public Expression Value { get; }

        public override ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            static async ValueTask<Completion> Awaited(ValueTask<FluidValue> task, TemplateContext context, string identifier)
            {
                var value = await task;

                // Substitute the result if a custom callback is provided
                if (context.Assigned != null)
                {
                    value = await context.Assigned.Invoke(identifier, value, context);
                }

                context.SetValue(identifier, value);
                return Completion.Normal;
            }

            context.IncrementSteps();

            var task = Value.EvaluateAsync(context);
            if (!task.IsCompletedSuccessfully || context.Assigned != null)
            {
                return Awaited(task, context, Identifier);
            }

            context.SetValue(Identifier, task.Result);
            return Statement.NormalCompletion;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitAssignStatement(this);

        public void WriteTo(SourceGenerationContext context)
        {
            var valueExpr = context.GetExpressionMethodName(Value);
            var identifierLiteral = SourceGenerationContext.ToCSharpStringLiteral(Identifier);

            context.WriteLine($"{context.ContextName}.IncrementSteps();");
            context.WriteLine($"var task = {valueExpr}({context.ContextName});");
            context.WriteLine($"if (task.IsCompletedSuccessfully && {context.ContextName}.Assigned is null)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"{context.ContextName}.SetValue({identifierLiteral}, task.Result);");
                context.WriteLine("return Completion.Normal;");
            }
            context.WriteLine("}");
            context.WriteLine($"return await Awaited(task, {context.ContextName});");
            context.WriteLine();
            context.WriteLine($"static async ValueTask<Completion> Awaited(ValueTask<FluidValue> task, TemplateContext context)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("var value = await task;");
                context.WriteLine($"if (context.Assigned != null)");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine($"value = await context.Assigned.Invoke({identifierLiteral}, value, context);");
                }
                context.WriteLine("}");
                context.WriteLine($"context.SetValue({identifierLiteral}, value);");
                context.WriteLine("return Completion.Normal;");
            }
            context.WriteLine("}");
        }
    }
}
