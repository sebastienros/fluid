using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shouldly;
using Xunit;

namespace Fluid.Tests.Issues
{
    public partial class FuzzTests
    {
        private FluidTemplate _fluidTemplate;
        private IEnumerable<string> _errors;
        private readonly User _user;

        public FuzzTests()
        {
            _user = new User
            {
                String = "ABC",
                Integer = 123,
                Doubles = new List<double> {1.1, 2.2, 3.3}
            };
            TemplateContext.GlobalMemberAccessStrategy.Register<User>();
        }


        [Theory]
        [InlineData("{%endfor%}", "No start tag 'for' found for tag 'endfor'")]
        [InlineData("{%comment%}{%", "End tag 'endcomment' was not found")]
        [InlineData(@"<p>{{ing }}</<p>{{ Inr }}l>
        {% for endcomments -%}
        <li>{{m
        }}>
        {%comment -%}{% 
            </", "Syntax error, expected: in at line:2, col:29")]
        [InlineData("<{{Doubles|map|<h1> contains true}}", "Syntax error, expected: identifier at line:1, col:16")]
        public void Issue148ParsingErrors(string template, string expectedErrorString)
        {
            Run(_user, template);
            _errors.ShouldContain(expectedErrorString);
        }

        [Fact]
        public void TestForUniqLoopOnNoArray()
        {
            var template = "<{{Doubles|map|uniq contains true}}";
            Should.NotThrow(() => Run(_user, template));
        }

        [Theory]
        [InlineData("{{0|remove}}", "'remove' filter requires string argument, E.g. 'remove: \"word_to_remove\"'")]
        [InlineData("{{0|modulo}}", "'modulo' requires one numeric argument.  E.g.  'modulo: 5'")]
        [InlineData("<p>{{false|divided_by|modulo|urlode}}<<",
            "'modulo' requires one numeric argument.  E.g.  'modulo: 5'")]
        public void Issue148RuntimeErrors(string template, string expectedErrorString)
        {
            Run(_user, template);
            _errors.ShouldContain(expectedErrorString);
        }

        [Theory]
        [MemberData(nameof(EmbeddedFuzzTests))]
        public void EmbeddedFileTest(string template)
        {
            Should.NotThrow(() => Run(_user, template));
        }


        public static IEnumerable<object[]> EmbeddedFuzzTests
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceNames = assembly.GetManifestResourceNames()
                    .Where(rn => rn.StartsWith("Fluid.Tests.Resources.FuzzData"));
                foreach (var res in resourceNames)
                {
                    yield return new object[] {ResourceHelper.GetEmbeddedResource(res, assembly)};
                }
            }
        }

        private void Run(User model, string template)
        {
            if (FluidTemplate.TryParse(template, out _fluidTemplate, out _errors))
            {
                _fluidTemplate.Render(new TemplateContext {Model = model});
            }
        }
    }
}