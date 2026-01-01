using System.Text.Encodings.Web;
using Fluid.Values;

namespace Fluid.Ast
{
    public sealed class AssignStatement : Statement
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
            return new ValueTask<Completion>(Completion.Normal);
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitAssignStatement(this);
    }
}
