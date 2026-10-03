using Fluid.Ast;

namespace Fluid.Tests.Visitors
{
    internal class ReplacePlusFiltersVisitor : AstRewriter
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
