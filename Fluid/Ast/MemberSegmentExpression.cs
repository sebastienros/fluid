using Fluid.Ast.Values;

namespace Fluid.Ast
{
    public abstract class MemberSegment
    {
        public abstract FluidValue Resolve(INamedSet properties, TemplateContext context);
    }

    public class IdentifierSegmentIdentifer : MemberSegment
    {
        public IdentifierSegmentIdentifer(string identifier)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }

        public override FluidValue Resolve(INamedSet properties, TemplateContext context)
        {
            return properties.GetProperty(Identifier);
        }
    }

    public class IndexerSegmentIdentifer : MemberSegment
    {
        public IndexerSegmentIdentifer(Expression expression)
        {
            Expression = expression;
        }

        public Expression Expression { get; }

        public override FluidValue Resolve(INamedSet properties, TemplateContext context)
        {
            var index = Expression.Evaluate(context);
            return properties.GetIndex(index);
        }
    }
}
