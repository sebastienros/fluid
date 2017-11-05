namespace Fluid.Ast
{
    public class RangeExpression
    {
        public RangeExpression(Expression from, Expression to)
        {
            From = from;
            To = to;
        }

        public Expression From { get; }

        public Expression To { get; }
    }
}
