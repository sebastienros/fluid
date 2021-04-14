using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class AssignStatement : Statement
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
                context.SetValue(identifier, value);
                return Completion.Normal;
            }

            context.IncrementSteps();

            var task = Value.EvaluateAsync(context);
            if (!task.IsCompletedSuccessfully)
            {
                return Awaited(task, context, Identifier);
            }

            context.SetValue(Identifier, task.Result);
            return new ValueTask<Completion>(Completion.Normal);
        }
    }
}
