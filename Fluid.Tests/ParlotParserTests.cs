using Fluid.Parlot;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class ParlotParserTests
    {
        [Theory]
        [InlineData("{% if true %}a{% endif %}", "a")]
        [InlineData("a{% if false %}b{% endif %}c", "ac")]
        [InlineData("{% if true %}a{% if false %}b{% endif %}{% if true %}c{% endif %}{% endif %}", "ac")]
        [InlineData("{% if true %}a{% if false %}b{% else %}d{% endif %}{% if true %}c{% endif %}{% endif %}", "adc")]
        [InlineData("{% if false %}a{% elsif false %}b{% elsif true %}d{% endif %}", "d")]
        [InlineData("{{ true or false }}", "true")]
        [InlineData("{% assign a = 3 + 1 %}{{ a }}", "4")]
        [InlineData("{{ 1 + 2 * 5}}", "11")]
        [InlineData("foo", "foo")]
        [InlineData("foo {{ 1 }}", "foo 1")]
        [InlineData("foo {{ 1 }} a{% raw %}abc{% endraw %}b", "foo 1 ab")]
        public async Task ShouldEvaluateTags(string input, string expected)
        {
            new ParlotParser().TryParse(input, false, out var results, out var errors);

            using (var sw = new StringWriter())
            {
                var context = new TemplateContext();

                foreach (var s in results)
                {
                    await s.WriteToAsync(sw, NullEncoder.Default, context);
                }

                Assert.Equal(expected, sw.ToString());
            }
        }

        [Theory]
        [InlineData("foo {{ 1 }} a{% raw %}abc{% endraw2 %}b", "Not end tag found for {% raw %} at (1:21)")]
        public void ShouldFail(string input, string expected)
        {
            new ParlotParser().TryParse(input, false, out var results, out var errors);
            Assert.Contains(expected, errors.FirstOrDefault());
        }
    }
}
