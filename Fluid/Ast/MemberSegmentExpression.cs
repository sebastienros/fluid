using Fluid.Values;

namespace Fluid.Ast
{
    public abstract class MemberSegment
    {
        public abstract FluidValue Resolve(FluidValue value, TemplateContext context);
        public abstract FluidValue Resolve(Scope value, TemplateContext context);
    }

    public class IdentifierSegment : MemberSegment
    {
        public IdentifierSegment(string identifier)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }

        public override FluidValue Resolve(FluidValue value, TemplateContext context)
        {
            return value.GetValue(Identifier);
        }

        public override FluidValue Resolve(Scope value, TemplateContext context)
        {
            return value.GetValue(Identifier);
        }
    }

    public class IndexerSegment : MemberSegment
    {
        public IndexerSegment(Expression expression)
        {
            Expression = expression;
        }

        public Expression Expression { get; }

        public override FluidValue Resolve(FluidValue value, TemplateContext context)
        {
            var index = Expression.Evaluate(context);
            return value.GetIndex(index);
        }

        public override FluidValue Resolve(Scope value, TemplateContext context)
        {
            var index = Expression.Evaluate(context);
            return value.GetIndex(index);
        }
    }
}
