using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Fluid.Tests.Issues
{
    public class Issue148ExceptionsTests
    {
        private FluidTemplate _fluidTemplate;
        private IEnumerable<string> _errors;
        private readonly User _model;

        public Issue148ExceptionsTests()
        {
            _model = new User
            {
                String = "ABC",
                Integer = 123,
                Doubles = new List<double> {1.1, 2.2, 3.3}
            };
        }


        [Theory]
        [InlineData("{%endfor%}", "No start tag 'for' found for tag 'endfor'")]
        [InlineData("{%comment%}{%", "End tag 'endcomment' was not found")]
        public void Issue148ParsingErrors(string template, string expectedErrorString)
        {
            Run(_model, template);
            _errors.ShouldContain(expectedErrorString);
        }

        [Theory(Skip =
            "Fix the exceptions throw at RenderAsync time, need to be caught earlier (BuildFilterExpression?)")]
        [InlineData("{{0|remove}}", "'remove' requires string argument.  i.e.  'remove \"word_to_remove\"")]
        [InlineData("{{0|modulo}}", "'modulo' requires one numeric argument.  i.e.  'modulo: 5'")]
        [InlineData("<p>{{false|divided_by|modulo|urlode}}<<",
            "'modulo' requires one numeric argument.  i.e.  'modulo: 5'")]
        public void Issue148RuntimeErrors(string template, string expectedErrorString)
        {
            Run(_model, template);
            _errors.ShouldContain(expectedErrorString);
        }


        public class User
        {
            public string String { get; set; }
            public int Integer { get; set; }
            public List<double> Doubles { get; set; }
        }

        private void Run(User model, string template)
        {
            if (FluidTemplate.TryParse(template, out _fluidTemplate, out _errors))
            {
                TemplateContext.GlobalMemberAccessStrategy.Register<User>();
                _fluidTemplate.Render(new TemplateContext {Model = model});
            }
        }
    }
}