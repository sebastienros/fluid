#if !NETCOREAPP2_1
using Fluid.Ast;
using Fluid.MvcViewEngine;
using Fluid.ViewEngine;
using Xunit;

namespace Fluid.Tests.MvcViewEngine
{
    public class SampleTests
    {
        [Fact]
        public void ShouldParseIndex()
        {
            var index = 
@"Hello World from Liquid 2

<h1>{{ ViewData[""Title""] }}</h1>

{% for p in Model %}
  <p>{{ p.Firstname }} {{ p.Lastname }}</p>
{% endfor %}

{% assign invoker = 'Index' %}
{% include 'Home/_Partial' %}

{% section footer %}
This is the footer
{% endsection %}

{% mytag %}
";
            var parser = new FluidViewParser(new FluidParserOptions());

            parser.RegisterEmptyTag("mytag", static async (w, e, c) =>
            {
                await w.WriteAsync("Hello from MyTag");

                return Completion.Normal;
            });

            var result = parser.TryParse(index, out var template, out var error);
            Assert.True(result, error);
            Assert.NotNull(template);
        }
    }
}
#endif