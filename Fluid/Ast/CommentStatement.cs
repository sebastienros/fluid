using Parlot;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public class CommentStatement : Statement
    {
        private readonly TextSpan _text;

        public CommentStatement(in TextSpan text)
        {
            _text = text;
        }

        public ref readonly TextSpan Text => ref _text;

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitCommentStatement(this);

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            return Normal();
        }
    }
}
