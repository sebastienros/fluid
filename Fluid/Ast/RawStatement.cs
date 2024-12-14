using Parlot;
using System.Text.Encodings.Web;
using Fluid.Utils;

namespace Fluid.Ast
{
    public sealed class RawStatement : Statement
    {
        private readonly TextSpan _text;

        public RawStatement(in TextSpan text)
        {
            _text = text;
        }

        public ref readonly TextSpan Text => ref _text;

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitRawStatement(this);

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

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitRawStatement(this);
    }
}