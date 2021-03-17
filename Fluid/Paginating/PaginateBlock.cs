using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;

namespace Fluid.Paginating
{
    internal static class PaginateBlock
    {
        public static async ValueTask<Completion> Render(
            (Expression Expression, long PageSize) args,
            IReadOnlyList<Statement> statements,
            TextWriter writer,
            TextEncoder encoder,
            TemplateContext ctx)
        {
            ctx.EnterChildScope();
            var value = await args.Expression.EvaluateAsync(ctx);
            if (value is PaginationValue paginationValue)
            {
                var pageSize = Convert.ToInt32(args.PageSize);
                if (pageSize > paginationValue.MaxPageSize)
                {
                    pageSize = paginationValue.MaxPageSize;
                }

                paginationValue.PageSize = pageSize;
                var paginate = await PaginateObject.Create(paginationValue, pageSize);
                ctx.SetValue("paginate", paginate);
            }

            await statements.RenderStatementsAsync(writer, encoder, ctx);
            ctx.ReleaseScope();
            return Completion.Normal;
        }
    }
}