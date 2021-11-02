using Fluid.Values;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class PaginateStatement : Statement
    {
        private readonly Expression _expression;
        private readonly long _pageSize;
        private readonly List<Statement> _statements;

        public PaginateStatement(Expression expression, long pageSize, List<Statement> statements)
        {
            _expression = expression ?? throw new ArgumentNullException(nameof(expression));
            _pageSize = pageSize;
            _statements = statements ?? new List<Statement>();
        }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var value = await _expression.EvaluateAsync(context);
            if (value == null || value is not PaginateableValue paginateableValue) return Completion.Normal;
            context.EnterChildScope();
            try
            {
                paginateableValue.PageSize = (int)_pageSize;

                var data = paginateableValue.GetPaginatedData();
                var paginate = CreatePaginate(paginateableValue, data);

                context.SetValue("paginate", paginate);

                await _statements.RenderStatementsAsync(writer, encoder, context);
            }
            finally
            {
                context.ReleaseScope();
            }
            return Completion.Normal;
        }

        private PaginateValue CreatePaginate(PaginateableValue value, PaginatedData data)
        {
            var ret = new PaginateValue
            {
                Items = data.Total,
                CurrentOffset = (value.CurrentPage - 1) * value.PageSize,
                CurrentPage = value.CurrentPage,
                PageSize = value.PageSize,
                Pages = data.Total / value.PageSize
            };

            if (data.Total % value.PageSize > 0) ret.Pages++;

            if (ret.Pages <= 1) return ret;

            if (ret.CurrentPage > 1)
            {
                ret.Previous = new PartValue
                {
                    IsLink = true,
                    Title = "«",
                    Url = value.GetUrl(ret.CurrentPage - 1)
                };
            }

            if (ret.CurrentPage < ret.Pages)
            {
                ret.Next = new PartValue
                {
                    IsLink = true,
                    Title = "»",
                    Url = value.GetUrl(ret.CurrentPage + 1)
                };
            }

            var min = ret.CurrentPage - 2;
            var max = ret.CurrentPage + 2;

            if (min <= 1) min = 2;
            if (max >= ret.Pages) max = ret.Pages - 1;

            var last = 0;
            for (var page = 1; page <= ret.Pages; page++)
            {
                var add = false;
                if (page == 1)
                {
                    add = true;
                }
                else if (page == ret.Pages)
                {
                    add = true;
                }
                else if (page >= min && page <= max)
                {
                    add = true;
                }

                if (!add) continue;

                if (last + 1 != page)
                {
                    ret.Parts.Add(new PartValue
                    {
                        IsLink = false,
                        Title = "…"
                    });
                }

                last = page;

                var item = new PartValue
                {
                    Title = page.ToString(),
                    IsLink = page != ret.CurrentPage
                };

                if (item.IsLink) item.Url = value.GetUrl(page);

                ret.Parts.Add(item);
            }

            return ret;
        }

        /// <summary>
        /// https://shopify.dev/api/liquid/objects/part
        /// </summary>
        internal sealed class PartValue : FluidValue
        {
            public bool IsLink { get; set; }
            public string Title { get; set; }
            public string Url { get; set; }

            public override FluidValues Type => FluidValues.Dictionary;

            public override bool Equals(FluidValue other)
            {
                return false;
            }

            public override bool ToBooleanValue()
            {
                return false;
            }

            public override decimal ToNumberValue()
            {
                return 0;
            }

            public override object ToObjectValue()
            {
                return null;
            }

            public override string ToStringValue()
            {
                return "part";
            }

            public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
            {
                return name switch
                {
                    "is_link" => new ValueTask<FluidValue>(BooleanValue.Create(IsLink)),
                    "title" => new ValueTask<FluidValue>(StringValue.Create(Title)),
                    "url" => new ValueTask<FluidValue>(StringValue.Create(Url)),
                    _ => new ValueTask<FluidValue>(NilValue.Instance),
                };
            }

            public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
            {
            }
        }

        /// <summary>
        /// https://shopify.dev/api/liquid/objects/paginate
        /// </summary>
        internal sealed class PaginateValue : FluidValue
        {
            public int CurrentOffset { get; set; }
            public int CurrentPage { get; set; }
            public int Items { get; set; }
            public List<PartValue> Parts { get; } = new();
            public PartValue Previous { get; set; }
            public PartValue Next { get; set; }
            public int PageSize { get; set; }
            public int Pages { get; set; }

            public override FluidValues Type => FluidValues.Dictionary;

            public override bool Equals(FluidValue other)
            {
                return false;
            }

            public override bool ToBooleanValue()
            {
                return false;
            }

            public override decimal ToNumberValue()
            {
                return 0;
            }

            public override object ToObjectValue()
            {
                return null;
            }

            public override string ToStringValue()
            {
                return "paginate";
            }

            public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
            {
                return name switch
                {
                    "current_offset" => new ValueTask<FluidValue>(NumberValue.Create(CurrentOffset)),
                    "current_page" => new ValueTask<FluidValue>(NumberValue.Create(CurrentPage)),
                    "items" => new ValueTask<FluidValue>(NumberValue.Create(Items)),
                    "parts" => new ValueTask<FluidValue>(Create(Parts, context.Options)),
                    "previous" => new ValueTask<FluidValue>(Previous),
                    "next" => new ValueTask<FluidValue>(Next),
                    "page_size" => new ValueTask<FluidValue>(NumberValue.Create(PageSize)),
                    "pages" => new ValueTask<FluidValue>(NumberValue.Create(Pages)),
                    _ => new ValueTask<FluidValue>(NilValue.Instance),
                };
            }

            public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
            {
            }
        }
    }
}
