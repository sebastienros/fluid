using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    internal sealed class LiquidStatement : TagStatement
    {
        public LiquidStatement(List<Statement> statements) : base(statements)
        {
        }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            static async ValueTask<Completion> Awaited(
                ValueTask<Completion> t,
                TextWriter w,
                TextEncoder enc,
                TemplateContext ctx,
                List<Statement> statements,
                int startIndex)
            {
                await t;
                for (var i = startIndex; i < statements.Count; ++i)
                {
                    await statements[i].WriteToAsync(w, enc, ctx);
                }
                return Completion.Normal;
            }

            context.IncrementSteps();

            var i = 0;
            foreach (var statement in _statements.AsSpan())
            {
                var task = statement.WriteToAsync(writer, encoder, context);
                if (!task.IsCompletedSuccessfully)
                {
                    return Awaited(task, writer, encoder, context, _statements, i + 1);
                }

                i++;
            }

            return Normal();
        }
    }
}
