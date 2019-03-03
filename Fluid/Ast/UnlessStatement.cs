using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class UnlessStatement : TagStatement
    {
        public UnlessStatement(
            Expression condition,
            List<Statement> statements) : base(statements)
        {
            Condition = condition;
        }

        public Expression Condition { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var result = (await Condition.EvaluateAsync(context)).ToBooleanValue();

            if (!result)
            {
                for (var i = 0; i < Statements.Count; i++)
                {
                    var statement = Statements[i];
                    var completion = await statement.WriteToAsync(writer, encoder, context);

                    if (completion != Completion.Normal)
                    {
                        // Stop processing the block statements
                        // We return the completion to flow it to the outer loop
                        return completion;
                    }
                }

                return Completion.Normal;
            }

            return Completion.Normal;
        }
    }
}
