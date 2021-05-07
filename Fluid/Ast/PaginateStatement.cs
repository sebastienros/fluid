using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Objects;
using Fluid.Values;

namespace Fluid.Ast
{
    public class PaginateStatement : TagStatement
    {
        private readonly Expression _expression;
        private readonly long _pageSize;

        public PaginateStatement(Expression expression, long pageSize, List<Statement> statements) : base(statements)
        {
            _expression = expression;
            _pageSize = pageSize;
        }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.EnterChildScope();
            var value = await _expression.EvaluateAsync(context);
            if (value is PaginationValue paginationValue)
            {
                var pageSize = Convert.ToInt32(_pageSize);
                if (pageSize > paginationValue.MaxPageSize)
                {
                    pageSize = paginationValue.MaxPageSize;
                }

                paginationValue.PageSize = pageSize;
                var paginate = await PaginateObject.Create(paginationValue, pageSize);
                context.SetValue("paginate", paginate);
            }

            await Statements.RenderStatementsAsync(writer, encoder, context);
            context.ReleaseScope();
            return Completion.Normal;
        }
    }
}