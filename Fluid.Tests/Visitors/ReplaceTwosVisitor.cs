using Fluid.Ast;
using Fluid.Values;

namespace Fluid.Tests.Visitors
{
    internal class ReplaceTwosVisitor : AstRewriter
    {
        private readonly FluidValue _replacement;

        public ReplaceTwosVisitor(FluidValue replacement)
        {
            _replacement = replacement;
        }

        protected override Expression VisitLiteralExpression(LiteralExpression literalExpression)
        {
            if (literalExpression.Value is NumberValue n && n.ToNumberValue() == 2)
            {
                return new LiteralExpression(_replacement);
            }

            return literalExpression;
        }
    }
}
