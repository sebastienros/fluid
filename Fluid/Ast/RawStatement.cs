using Parlot;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Utils;

namespace Fluid.Ast
{
    public class RawStatement : Statement
    {
        private readonly TextSpan _text;

        public RawStatement(in TextSpan text)
        {
            _text = text;
        }

        public ref readonly TextSpan Text => ref _text;

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            static async ValueTask<Completion> Awaited(Task task)
            {
                await task;
                return Completion.Normal;
            }

            context.IncrementSteps();

            var task = writer.WriteAsync(_text.ToString());
            return task.IsCompletedSuccessfully()
                ? new ValueTask<Completion>(Completion.Normal)
                : Awaited(task);
        }
    }
}