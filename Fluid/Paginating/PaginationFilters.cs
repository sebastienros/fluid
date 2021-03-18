using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Paginating
{
    public static class PaginationFilters
    {
        private const string DefaultPaginationTemplate =
            @"{%- if paginate.__Previous__ and paginate.__Previous__.__IsLink__ -%}
<span class=""prev""><a href=""{{paginate.__Previous__.__Url__}}"">{{ txt_previous | default: paginate.__Previous__.__Title__ }}</a></span>
{%-endif-%}
{%- for pair in paginate.__Parts__ -%}
{%- if pair.__IsLink__ -%}
<span class=""page""><a href=""{{pair.__Url__}}"">{{pair.__Title__}}</a></span>
{%- else -%}
<span class=""deco"">{{pair.__Title__}}</span>
{%- endif -%}
{%- endfor -%}
{%- if paginate.__Next__ and paginate.__Next__.__IsLink__ -%}
<span class=""prev""><a href=""{{paginate.__Next__.__Url__}}"">{{ txt_next | default: paginate.__Next__.__Title__ }}</a></span>
{%- endif -%}";

        private static readonly FluidParser Parser = new();

        private static readonly object FakeObject = new
        {
            CurrentOffset = 0,
            CurrentPage = 0,
            Items = 0,
            Parts = 0,
            Previous = 0,
            Next = 0,
            PageSize = 0,
            Pages = 0,
            IsLink = 0,
            Title = 0,
            Url = 0
        };

        public static FilterCollection WithPaginationFilters(this FilterCollection filters)
        {
            filters.AddFilter("default_pagination", DefaultPaginationFilter);
            return filters;
        }

        private static string BuildPaginationTemplate(MemberNameStrategy strategy)
        {
            var template = DefaultPaginationTemplate;
            var type = FakeObject.GetType();
            foreach (var property in type.GetProperties())
            {
                var holder = $"__{property.Name}__";
                var name = strategy.Invoke(property);
                template = template.Replace(holder, name);
            }

            return template;
        }

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
                var template =
                    Parser.Parse(BuildPaginationTemplate(context.Options.MemberAccessStrategy.MemberNameStrategy));
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