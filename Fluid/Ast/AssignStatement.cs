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

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
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
            return new ValueTask<Completion>(Completion.Normal);
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitAssignStatement(this);

        public void WriteTo(SourceGenerationContext context)
        {
            var valueExpr = context.GetExpressionMethodName(Value);
            var identifierLiteral = SourceGenerationContext.ToCSharpStringLiteral(Identifier);

            context.WriteLine($"{context.ContextName}.IncrementSteps();");
            context.WriteLine($"var value = await {valueExpr}({context.ContextName});");
            context.WriteLine($"if ({context.ContextName}.Assigned != null)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"value = await {context.ContextName}.Assigned.Invoke({identifierLiteral}, value, {context.ContextName});");
            }
            context.WriteLine("}");
            context.WriteLine($"{context.ContextName}.SetValue({identifierLiteral}, value);");
            context.WriteLine("return Completion.Normal;");
        }
    }
}
