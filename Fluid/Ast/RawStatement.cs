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

        public override ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            output.Write(_text.ToString());
            return Statement.NormalCompletion;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitRawStatement(this);
    }
}