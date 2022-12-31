using Fluid.Ast;
using Fluid.Values;

namespace Fluid.Tests.Visitors
{
    internal class ReplacePlustFiltersVisitor : AstRewriter
    {
        protected override Expression VisitFilterExpression(FilterExpression filterExpression)
        {
            if (filterExpression.Name == "plus")
            {
                return new FilterExpression(filterExpression.Input, "minus", filterExpression.Parameters);
            }

            return filterExpression;
        }
    }
}
