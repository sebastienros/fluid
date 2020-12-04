using Fluid.Ast;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Fluid.Tests
{
    public class ParserTests
    {
        private List<Statement> Parse(string source)
        {
            FluidTemplate.TryParse(source, out var template, out var errors);
            return template.Statements;
        }

        [Fact]
        public void ShouldParseText()
        {
            var statements = Parse("Hello World");

            var textStatement = statements.First() as TextStatement;

            Assert.Single(statements);
            Assert.NotNull(textStatement);
            Assert.Equal("Hello World", textStatement.Text.ToString());
        }

        [Fact]
        public void ShouldParseOutput()
        {
            var statements = Parse("{{ 1 }}");

            var outputStatement = statements.First() as OutputStatement;

            Assert.Single(statements);
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

            Assert.Single(statements);
            Assert.NotNull(outputStatement);
        }

        [Fact]
        public void ShouldParseForTag()
        {
            var statements = Parse("{% for a in b %}{% endfor %}");

            Assert.IsType<ForStatement>(statements.ElementAt(0));
        }

        [Fact]
        public void ShouldParseForElseTag()
        {
            var statements = Parse("{% for a in b %}x{% else %}y{% endfor %}");

            Assert.IsType<ForStatement>(statements.ElementAt(0));
            var forStatement = statements.ElementAt(0) as ForStatement;
            Assert.True(forStatement.Statements.Count == 1);
            Assert.NotNull(forStatement.Else);
            Assert.True((forStatement.Else is ElseStatement s) && s.Statements.Count == 1);
        }

        [Fact]
        public void ShouldReadSingleCharInTag()
        {
            var statements = Parse(@"{% for a in b %};{% endfor %}");
            Assert.Single(statements);
            var text = ((ForStatement)statements[0]).Statements[0] as TextStatement;
            Assert.Equal(";", text.Text.ToString());
        }

        [Fact]
        public void ShouldParseRaw()
        {
            var statements = Parse(@"{% raw %} on {{ this }} and {{{ that }}} {% endraw %}");

            Assert.Single(statements);
            Assert.IsType<TextStatement>(statements.ElementAt(0));
            Assert.Equal(" on {{ this }} and {{{ that }}} ", (statements.ElementAt(0) as TextStatement).Text.ToString());
        }

        [Fact]
        public void ShouldParseRawWithBlocks()
        {
            var statements = Parse(@"{% raw %} {%if true%} {%endif%} {% endraw %}");

            Assert.Single(statements);
            Assert.IsType<TextStatement>(statements.ElementAt(0));
            Assert.Equal(" {%if true%} {%endif%} ", (statements.ElementAt(0) as TextStatement).Text.ToString());
        }

        [Fact]
        public void ShouldParseComment()
        {
            var statements = Parse(@"{% comment %} on {{ this }} and {{{ that }}} {% endcomment %}");

            Assert.Single(statements);
            Assert.IsType<CommentStatement>(statements.ElementAt(0));
            Assert.Equal(" on {{ this }} and {{{ that }}} ", (statements.ElementAt(0) as CommentStatement).Text.ToString());
        }

        [Fact]
        public void ShouldParseCommentWithBlocks()
        {
            var statements = Parse(@"{% comment %} {%if true%} {%endif%} {% endcomment %}");

            Assert.Single(statements);
            Assert.IsType<CommentStatement>(statements.ElementAt(0));
            Assert.Equal(" {%if true%} {%endif%} ", (statements.ElementAt(0) as CommentStatement).Text.ToString());
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
        [InlineData("{% assign _foo = 1 %}")]
        [InlineData("{% assign __foo = 1 %}")]
        [InlineData("{% assign fo-o = 1 %}")]
        [InlineData("{% assign fo_o = 1 %}")]
        [InlineData("{% assign fo--o = 1 %}")]
        [InlineData("{% assign fo__o = 1 %}")]
        public void ShouldAcceptDashesInIdentifiers(string source)
        {
            var result = FluidTemplate.TryParse(source, out var template, out var errors);

            Assert.True(result);
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

            Assert.True(result, String.Join(", ", errors));
            Assert.NotNull(template);
            Assert.Empty(errors);

            var rendered = template.Render();

            Assert.Equal(expected, rendered);
        }

        [Fact]
        public void ShouldIndexStringSegment()
        {
            var segment = new StringSegment("012345");

            Assert.Equal('0', segment.Index(0));
            Assert.Equal('5', segment.Index(-1));

            segment = segment.Subsegment(1, 4);

            Assert.Equal('1', segment.Index(0));
            Assert.Equal('4', segment.Index(-1));
        }

        [Fact]
        public void ShouldParseCurlyBraceInOutputStatements()
        {
            Parse("{{ 'on {0}' }}");
        }

        [Fact]
        public void ShouldBeAbleToCompareNilValues()
        {
            // [1, 2, 3] | map will return [nil, nil, nil] then | uniq will try to call NilValue.GetHashCode()

            var model = new
            {
                Doubles = new List<double> { 1.1, 2.2, 3.3 }
            };

            var template = "{{Doubles |map |uniq}}";

            if (FluidTemplate.TryParse(template, out var result))
            {
                result.Render(new TemplateContext(model));
            }
        }

        [Fact]
        public void ShouldBeAbleToCompareNilValues()
        {
            var model = new
            {
                name = "Tobi"
            };

            var template = "{{name}}";

            FluidTemplate.TryParse(template, out var template);
            var rendered = template.Render(new TemplateContext(model, false));

            Assert.Equal("Tobi", rendered);
        }

        [Theory]
        [InlineData("{% for %}")]
        [InlineData("{% endfor %}")]
        [InlineData("{% case %}")]
        [InlineData("{% endcase %}")]
        [InlineData("{% if %}")]
        [InlineData("{% endif %}")]
        [InlineData("{% unless %}")]
        [InlineData("{% endunless %}")]
        [InlineData("{% comment %}")]
        [InlineData("{% endcomment %}")]
        [InlineData("{% raw %}")]
        [InlineData("{% endraw %}")]
        [InlineData("{% capture %}")]
        [InlineData("{% endcapture %}")]

        public void ShouldThrowParseExceptionMissingTag(string template)
        {
            Assert.Throws<ParseException>(() => FluidTemplate.Parse(template));
        }

        [Theory]
        [InlineData("{{ 'a\\nb' }}", "a\nb")]
        [InlineData("{{ 'a\\tb' }}", "a\tb")]
        public void ShouldParseEscapeSequences(string source, string expected)
        {
            var result = FluidTemplate.TryParse(source, out var template, out var errors);

            Assert.True(result, String.Join(", ", errors));
            Assert.NotNull(template);
            Assert.Empty(errors);

            var rendered = template.Render();

            Assert.Equal(expected, rendered);
        }

        [Theory]
        [InlineData("{{ 'a\nb' }}", "a\nb")]
        [InlineData("{{ 'a\r\nb' }}", "a\r\nb")]
        public void ShouldParseLineBreaksInStringLiterals(string source, string expected)
        {
            var result = FluidTemplate.TryParse(source, out var template, out var errors);

            Assert.True(result, String.Join(", ", errors));
            Assert.NotNull(template);
            Assert.Empty(errors);

            var rendered = template.Render();

            Assert.Equal(expected, rendered);
        }

        [Theory]
        [InlineData("{{ -3 }}", "-3")]
        public void ShouldParseNegativeNumbers(string source, string expected)
        {
            var result = FluidTemplate.TryParse(source, out var template, out var errors);

            Assert.True(result, String.Join(", ", errors));
            Assert.NotNull(template);
            Assert.Empty(errors);

            var rendered = template.Render();

            Assert.Equal(expected, rendered);
        }

        [Theory]
        [InlineData("{% assign my_integer = 7 %}{{ 20 | divided_by: my_integer }}", "2")]
        [InlineData("{% assign my_integer = 7 %}{% assign my_float = my_integer | times: 1.0 %}{{ 20 | divided_by: my_float | round: 5 }}", "2.85714")]
        [InlineData("{{ 183.357 | times: 12 }}", "2200.284")]
        public void ShouldChangeVariableType(string source, string expected)
        {
            var result = FluidTemplate.TryParse(source, out var template, out var errors);

            Assert.True(result, String.Join(", ", errors));
            Assert.NotNull(template);
            Assert.Empty(errors);

            var rendered = template.Render();

            Assert.Equal(expected, rendered);
        }

        [Theory]
        [InlineData("{% assign my_string = 'abcd' %}{{ my_string.size }}", "4")]
        public void SizeAppliedToStrings(string source, string expected)
        {
            var result = FluidTemplate.TryParse(source, out var template, out var errors);

            Assert.True(result, String.Join(", ", errors));
            Assert.NotNull(template);
            Assert.Empty(errors);

            var rendered = template.Render();

            Assert.Equal(expected, rendered);
        }

    }    
}
