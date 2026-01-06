using Parlot;
using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class CommentStatement : Statement, ISourceable
    {
        private readonly TextSpan _text;

        public CommentStatement(in TextSpan text)
        {
            _text = text;
        }

        public ref readonly TextSpan Text => ref _text;

        public override ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            return Normal();
        }

        public void WriteTo(SourceGenerationContext context)
        {
            context.WriteLine($"{context.ContextName}.IncrementSteps();");
            context.WriteLine("return Completion.Normal;");
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitCommentStatement(this);
    }
}
