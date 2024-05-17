using Parlot;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class CommentStatement : Statement
    {
        private readonly TextSpan _text;

        public CommentStatement(in TextSpan text)
        {
            _text = text;
        }

        public ref readonly TextSpan Text => ref _text;

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            return Normal();
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitCommentStatement(this);
    }
}
