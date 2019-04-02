using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

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

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var value = await Value.EvaluateAsync(context);
            context.SetValue(Identifier, value);

            return Completion.Normal;
        }
    }
}
