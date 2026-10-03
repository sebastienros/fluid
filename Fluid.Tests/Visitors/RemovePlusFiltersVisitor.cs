using Fluid.Ast;

namespace Fluid.Tests.Visitors
{
    internal class RemovePlusFiltersVisitor : AstRewriter
    {
        protected override Expression VisitFilterExpression(FilterExpression filterExpression)
        {
            if (filterExpression.Name == "plus")
            {
                return filterExpression.Input;
            }

            return filterExpression;
        }
    }
}
