using Fluid.Ast.Values;

namespace Fluid.Ast
{
    public abstract class MemberSegment
    {
        public abstract FluidValue Resolve(INamedSet properties, TemplateContext context);
    }

    public class IdentifierSegment : MemberSegment
    {
        public IdentifierSegment(string identifier)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }

        public override FluidValue Resolve(INamedSet properties, TemplateContext context)
        {
            return properties.GetValue(Identifier);
        }
    }

    public class IndexerSegment : MemberSegment
    {
        public IndexerSegment(Expression expression)
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
