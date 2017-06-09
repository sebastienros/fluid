using Fluid.Ast.Values;

namespace Fluid.Ast
{
    public class MemberExpression : Expression
    {
        public MemberExpression(params MemberSegment[] segments)
        {
            Segments = segments;
        }

        public MemberSegment[] Segments { get; }

        public override FluidValue Evaluate(TemplateContext context)
        {
            FluidValue value = null;

            foreach(var segment in Segments)
            {
                var namedSet = ((object)value ?? context.Scope) as INamedSet;

                if (namedSet == null)
                {
                    return UndefinedValue.Instance;
                }

                value = segment.Resolve(namedSet, context);

                // Stop processing as soon as a member returns nothing
                if (value.IsUndefined())
                {
                    return value;
                }
            }

            return value;
        }
    }
}
