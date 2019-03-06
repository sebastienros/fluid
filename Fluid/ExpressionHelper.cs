using System;
using System.Linq.Expressions;

namespace Fluid
{
    internal static class ExpressionHelper
    {
        internal static string GetPropertyName<T, TProp>(Expression<Func<T, TProp>> expression)
        {
            var me = (MemberExpression) expression.Body;
            return me.Member.Name;
        }
    }
}
