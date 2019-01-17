using Fluid.Ast;
using Irony.Parsing;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Fluid.Tests
{
    public class ParserTests
    {
        private static LanguageData _language = new LanguageData(new FluidGrammar());

        private IList<Statement> Parse(string source)
        {
            FluidTemplate.TryParse(source, out var template, out var errors);
            return template.Statements;
        }

        [Fact]
        public void ShouldParseText()
        {
            var statements = Parse("Hello World");

            var textStatement = statements.First() as TextStatement;

            Assert.Equal(1, statements.Count);
            Assert.NotNull(textStatement);
            Assert.Equal("Hello World", textStatement.Text);
        }

        [Fact]
        public void ShouldParseOutput()
        {
            var statements = Parse("{{ 1 }}");

            var outputStatement = statements.First() as OutputStatement;

            Assert.Equal(1, statements.Count);
            Assert.NotNull(outputStatement);
        }

        [Theory]
        [InlineData("{{ a }}")]
        [InlineData("{{ a.b }}")]
        [InlineData("{{ a.b[1] }}")]
        public void ShouldParseOutputWithMember(string source)
        {
            var statements = Parse(source);

            var outputStatement = statements.First() as OutputStatement;

            Assert.Equal(1, statements.Count);
            Assert.NotNull(outputStatement);
        }

        [Fact]
        public void ShouldParseForTag()
        {
            var statements = Parse("{% for a in b %}{% endfor %}");

            Assert.IsType<ForStatement>(statements.ElementAt(0));
        }

        [Fact]
        public void ShouldReadSingleCharInTag()
        {
            var statements = Parse(@"{% for a in b %};{% endfor %}");
            Assert.Equal(1, statements.Count);
            var text = ((ForStatement)statements[0]).Statements[0] as TextStatement;
            Assert.Equal(";", text.Text);
        }

        [Fact]
        public void ShouldParseRaw()
        {
            var statements = Parse(@"{% raw %} on {{ this }} and {{{ that }}} {% endraw %}");

            Assert.Equal(1, statements.Count);
            Assert.IsType<TextStatement>(statements.ElementAt(0));
            Assert.Equal(" on {{ this }} and {{{ that }}} ", (statements.ElementAt(0) as TextStatement).Text);
        }

        [Fact]
        public void ShouldParseRawWithBlocks()
        {
            var statements = Parse(@"{% raw %} {%if true%} {%endif%} {% endraw %}");

            Assert.Equal(1, statements.Count);
            Assert.IsType<TextStatement>(statements.ElementAt(0));
            Assert.Equal(" {%if true%} {%endif%} ", (statements.ElementAt(0) as TextStatement).Text);
        }

        [Fact]
        public void ShouldParseComment()
        {
            var statements = Parse(@"{% comment %} on {{ this }} and {{{ that }}} {% endcomment %}");

            Assert.Equal(1, statements.Count);
            Assert.IsType<CommentStatement>(statements.ElementAt(0));
            Assert.Equal(" on {{ this }} and {{{ that }}} ", (statements.ElementAt(0) as CommentStatement).Text);
        }

        [Fact]
        public void ShouldParseCommentWithBlocks()
        {
            var statements = Parse(@"{% comment %} {%if true%} {%endif%} {% endcomment %}");

            Assert.Equal(1, statements.Count);
            Assert.IsType<CommentStatement>(statements.ElementAt(0));
            Assert.Equal(" {%if true%} {%endif%} ", (statements.ElementAt(0) as CommentStatement).Text);
        }

        [Fact]
        public void ShouldParseIfTag()
        {
            var statements = Parse("{% if true %}yes{% endif %}");

            Assert.IsType<IfStatement>(statements.ElementAt(0));
            Assert.True(statements.ElementAt(0) is IfStatement s && s.Statements.Count == 1);
        }

        [Fact]
        public void ShouldParseIfElseTag()
        {
            var statements = Parse("{% if true %}yes{%else%}no{% endif %}");

            var ifStatement = statements.ElementAt(0) as IfStatement;
            Assert.NotNull(ifStatement);
            Assert.Single(ifStatement.Statements);
            Assert.NotNull(ifStatement.Else);
            Assert.Empty(ifStatement.ElseIfs);
        }

        [Fact]
        public void ShouldParseIfElseIfTag()
        {
            var statements = Parse("{% if true %}yes{%elsif a%}maybe{%else%}no{%endif%}");

            var ifStatement = statements.ElementAt(0) as IfStatement;
            Assert.NotNull(ifStatement);
            Assert.Single(ifStatement.Statements);
            Assert.NotNull(ifStatement.Else);
            Assert.NotNull(ifStatement.ElseIfs);
        }

        [Theory]
        [InlineData("abc { def")]
        [InlineData("abc } def")]
        [InlineData("abc {{ def")]
        [InlineData("abc }} def")]
        [InlineData("abc {{ def }")]
        [InlineData("abc { def }}")]
        [InlineData("abc {% def")]
        [InlineData("abc %} def")]
        [InlineData("{% def")]
        [InlineData("abc %}")]
        [InlineData("%} def")]
        [InlineData("abc {%")]
        [InlineData("abc {{% def")]
        [InlineData("abc }%} def")]
        public void ShouldSucceedParseValidTemplate(string source)
        {
            var result = FluidTemplate.TryParse(source, out var template, out var errors);

            Assert.True(result);
            Assert.NotNull(template);
            Assert.Empty(errors);
        }

        [Theory]
        [InlineData("abc {% {{ %} def")]
        [InlineData("abc {% { %} def")]
        public void ShouldFailParseInvalidTemplate(string source)
        {
            var result = FluidTemplate.TryParse(source, out var template, out var errors);

            Assert.False(result);
            Assert.Null(template);
            Assert.NotEmpty(errors);
        }

        [Theory]
        [InlineData(@"abc 
  {% {{ %}
def", "at line:2, col:6")]
        [InlineData(@"{% assign username = ""John G. Chalmers-Smith"" %}
{% if username and username.size > 10 %}
  Wow, {{ username }}, you have a long name!
{% else %}
  Hello there {{ { }}!
{% endif %}", "at line:5, col:18")]
        [InlineData(@"{% assign username = ""John G. Chalmers-Smith"" %}
{% if username and 
      username.size > 5 &&
      username.size < 10 %}
  Wow, {{ username }}, you have a longish name!
{% else %}
  Hello there!
{% endif %}", "at line:3, col:25")]
        public void ShouldFailParseInvalidTemplateWithCorrectLineNumber(string source, string expectedErrorEndString)
        {
            var result = FluidTemplate.TryParse(source, out var template, out var errors);

            Assert.EndsWith(expectedErrorEndString, errors.FirstOrDefault());
        }

        [Theory]
        [InlineData("{% for a in b %}")]
        [InlineData("{% if true %}")]
        [InlineData("{% unless true %}")]
        [InlineData("{% case a %}")]
        [InlineData("{% capture myVar %}")]
        public void ShouldFailNotClosedBlock(string source)
        {
            var result = FluidTemplate.TryParse(source, out var template, out var errors);

            Assert.False(result);
            Assert.NotEmpty(errors);
        }

        [Theory]
        [InlineData("{% for a in b %} {% endfor %}")]
        [InlineData("{% if true %} {% endif %}")]
        [InlineData("{% unless true %} {% endunless %}")]
        [InlineData("{% case a %} {% when 'cake' %} blah {% endcase %}")]
        [InlineData("{% capture myVar %} capture me! {% endcapture %}")]
        public void ShouldSucceedClosedBlock(string source)
        {
            var result = FluidTemplate.TryParse(source, out var template, out var errors);

            Assert.True(result);
            Assert.NotNull(template);
            Assert.Empty(errors);
        }

        [Fact]
        public void ShouldAllowNewLinesInCase()
        {
            var result = FluidTemplate.TryParse(@"
                {% case food %}
                    


                    {% when 'cake' %}
                        yum
                    {% when 'rock' %}
                        yuck
                {% endcase %}
                ", out var template, out var errors);

            var context = new TemplateContext();
            context.SetValue("food", "cake");

            Assert.True(result);
            Assert.NotNull(template);
            Assert.Empty(errors);
        }

        [Theory]
        [InlineData("{{ 20 | divided_by: 7.0 | round: 2 }}", "2.86")]
        [InlineData("{{ 20 | divided_by: 7 | round: 2 }}", "2")]
        public void ShouldParseIntegralNumbers(string source, string expected)
        {
            var result = FluidTemplate.TryParse(source, out var template, out var errors);

            Assert.True(result);
            Assert.NotNull(template);
            Assert.Empty(errors);

            var rendered = template.Render();

            Assert.Equal(expected, rendered);

        }
    }
}
