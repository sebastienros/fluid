using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Paginating
{
    public static class PaginationFilters
    {
        public static FilterCollection WithPaginationFilters(this FilterCollection filters)
        {
            filters.AddFilter("default_pagination", DefaultPaginationFilter);
            return filters;
        }

        private static readonly FluidParser Parser = new FluidParser();

        private const string DefaultPaginationTemplate = @"{%- if paginate.previous and paginate.previous.isLink -%}
<span class=""prev""><a href=""{{paginate.previous.url}}"">{{ txt_previous | default: paginate.previous.title }}</a></span>
{%-endif-%}
{%- for pair in paginate.parts -%}
{%- if pair.isLink -%}
<span class=""page""><a href=""{{pair.url}}"">{{pair.title}}</a></span>
{%- else -%}
<span class=""deco"">{{pair.title}}</span>
{%- endif -%}
{%- endfor -%}
{%- if paginate.next and paginate.next.isLink -%}
<span class=""prev""><a href=""{{paginate.next.url}}"">{{ txt_next | default: paginate.next.title }}</a></span>
{%- endif -%}";

        public static async ValueTask<FluidValue> DefaultPaginationFilter(
            FluidValue input,
            FilterArguments arguments,
            TemplateContext context)
        {
            var obj = input.ToObjectValue();
            if (obj is PaginateObject paginate)
            {
                var nextText = arguments.HasNamed("next")
                    ? arguments["next"]
                    : NilValue.Instance;
                var previousText = arguments.HasNamed("previous")
                    ? arguments["previous"]
                    : NilValue.Instance;

                var template = Parser.Parse(DefaultPaginationTemplate);
                context.EnterChildScope();
                context.SetValue("paginate", paginate);
                context.SetValue("txt_next", nextText);
                context.SetValue("txt_previous", previousText);
                var ret = await template.RenderAsync(context);
                context.ReleaseScope();
                return new StringValue(ret, false);
            }

            return StringValue.Create(string.Empty);
        }
    }
}
