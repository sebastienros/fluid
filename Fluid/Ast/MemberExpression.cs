using Fluid.Ast.Values;

namespace Fluid.Ast
{
    public class MemberExpression : Expression
    {
        public MemberExpression(MemberSegmentExpression[] segments)
        {
            Segments = segments;
        }

        public MemberSegmentExpression[] Segments { get; }

        public override FluidValue Evaluate(TemplateContext context)
        {
            return FluidValue.Nil;
        }
    }
}
