using Fluid.Parlot;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class ParlotParserTests
    {
        private static IFluidParser _parser = new ParlotParser();

        [Theory]
        [InlineData("{% for i in (1..5) offset:1 limit:3 reversed%}{{ i }}{% endfor %}", "432")]
        [InlineData("{% if true %}a{% endif %}", "a")]
        [InlineData("a{% if false %}b{% endif %}c", "ac")]
        [InlineData("{% if true %}a{% if false %}b{% endif %}{% if true %}c{% endif %}{% endif %}", "ac")]
        [InlineData("{% if true or false %}a{% if false %}b{% else %}d{% endif %}{% if true %}c{% endif %}{% endif %}", "adc")]
        [InlineData("{% if false %}a{% elsif false %}b{% elsif true %}d{% endif %}", "d")]
        [InlineData("{{ true }}", "true")]
        [InlineData("{% assign a = 3 %}{{ a }}", "3")]
        [InlineData("foo", "foo")]
        [InlineData("foo {{ 1 }}", "foo 1")]
        [InlineData("foo {{ 1 }} a{% raw %}abc{% endraw %}b", "foo 1 ab")]
        public async Task ShouldEvaluateTags(string input, string expected)
        {
            _parser.TryParse(input, out var template, out var errors);

            using (var sw = new StringWriter())
            {
                var context = new TemplateContext();

                var result = await template.RenderAsync();

                Assert.Equal(expected, result);
            }
        }

        [Theory]
        [InlineData("foo {{ 1 }} a{% raw %}abc{% endraw2 %}b", "Not end tag found for {% raw %} at (1:20)")]
        public void ShouldFail(string input, string expected)
        {
            _parser.TryParse(input, out var results, out var errors);
            Assert.Equal(expected, errors);
        }
    }
}
