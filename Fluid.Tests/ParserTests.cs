﻿using Fluid.Ast;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class ParserTests
    {
        static FluidParser _parser = new FluidParser();

        private static IReadOnlyList<Statement> Parse(string source)
        {
            _parser.TryParse(source, out var template, out var errors);
            return template.Statements;
        }

        private async Task CheckAsync(string source, string expected, Action<TemplateContext> init = null)
        {
            _parser.TryParse("{% if " + source + " %}true{% else %}false{% endif %}", out var template, out var messages);

            var context = new TemplateContext();
            init?.Invoke(context);

            var result = await template.RenderAsync(context);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ShouldFiltersWithNamedArguments()
        {

            var statements = Parse("{{ a | b: c:1, 'value', d: 3 }}");
            Assert.Single(statements);

            var outputStatement = statements[0] as OutputStatement;
            Assert.NotNull(outputStatement);

            var filterExpression = outputStatement.Expression as FilterExpression;
            Assert.NotNull(filterExpression);
            Assert.Equal("b", filterExpression.Name);

            var input = filterExpression.Input as MemberExpression;
            Assert.NotNull(input);

            Assert.Equal("c", filterExpression.Parameters[0].Name);
            Assert.Null(filterExpression.Parameters[1].Name);
            Assert.Equal("d", filterExpression.Parameters[2].Name);
        }


        [Fact]
        public void ShouldParseText()
        {
            var statements = Parse("Hello World");

            var textStatement = statements[0] as TextSpanStatement;

            Assert.Single(statements);
            Assert.NotNull(textStatement);
            Assert.Equal("Hello World", textStatement.Text.ToString());
        }

        [Fact]
        public void ShouldParseOutput()
        {
            var statements = Parse("{{ 1 }}");

            var outputStatement = statements[0] as OutputStatement;

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

            var outputStatement = statements[0] as OutputStatement;

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
        public void ShouldParseForLimitLiteral()
        {
            var statements = Parse("{% for item in items limit: 1 %}x{% endfor %}");

            Assert.IsType<ForStatement>(statements.ElementAt(0));
            var forStatement = statements.ElementAt(0) as ForStatement;
            Assert.True(forStatement.Statements.Count == 1);
            Assert.True(forStatement.Limit is LiteralExpression);
        }

        [Fact]
        public void ShouldParseForLimitMember()
        {
            var statements = Parse("{% for item in items limit: limit %}x{% endfor %}");

            Assert.IsType<ForStatement>(statements.ElementAt(0));
            var forStatement = statements.ElementAt(0) as ForStatement;
            Assert.True(forStatement.Statements.Count == 1);
            Assert.True(forStatement.Limit is MemberExpression);
        }

        [Fact]
        public void ShouldReadSingleCharInTag()
        {
            var statements = Parse(@"{% for a in b %};{% endfor %}");
            Assert.Single(statements);
            var text = ((ForStatement)statements[0]).Statements[0] as TextSpanStatement;
            Assert.Equal(";", text.Text.ToString());
        }

        [Fact]
        public void ShouldParseRaw()
        {
            var statements = Parse(@"{% raw %} on {{ this }} and {{{ that }}} {% endraw %}");

            Assert.Single(statements);
            Assert.IsType<RawStatement>(statements.ElementAt(0));
            Assert.Equal(" on {{ this }} and {{{ that }}} ", (statements.ElementAt(0) as RawStatement).Text.ToString());
        }

        [Fact]
        public void ShouldParseRawWithBlocks()
        {
            var statements = Parse(@"{% raw %} {%if true%} {%endif%} {% endraw %}");

            Assert.Single(statements);
            Assert.IsType<RawStatement>(statements.ElementAt(0));
            Assert.Equal(" {%if true%} {%endif%} ", (statements.ElementAt(0) as RawStatement).Text.ToString());
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
        [InlineData("abc }} def")]
        [InlineData("abc { def }}")]
        [InlineData("abc %} def")]
        [InlineData("abc %}")]
        [InlineData("%} def")]
        [InlineData("abc }%} def")]
        public void ShouldSucceedParseValidTemplate(string source)
        {
            var result = _parser.TryParse(source, out var template, out var errors);
            Assert.True(result);
            Assert.NotNull(template);
            Assert.Null(errors);
        }

        [Theory]
        [InlineData("abc {% {{ %} def")]
        [InlineData("abc {% { %} def")]
        public void ShouldFailParseInvalidTemplate(string source)
        {
            var result = _parser.TryParse(source, out var template, out var errors);
            Assert.False(result);
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
            var result = _parser.TryParse(source, out var template, out var error);

            Assert.True(result);
        }

        [Theory]
        [InlineData(@"abc 
  {% {{ %}
def", "at (")]
        [InlineData(@"{% assign username = ""John G. Chalmers-Smith"" %}
{% if username and username.size > 10 %}
  Wow, {{ username }}, you have a long name!
{% else %}
  Hello there {{ { }}!
{% endif %}", "at (")]
        [InlineData(@"{% assign username = ""John G. Chalmers-Smith"" %}
{% if username and 
      username.size > 5 &&
      username.size < 10 %}
  Wow, {{ username }}, you have a longish name!
{% else %}
  Hello there!
{% endif %}", "at (")]
        public void ShouldFailParseInvalidTemplateWithCorrectLineNumber(string source, string expectedErrorEndString)
        {
            var result = _parser.TryParse(source, out var template, out var errors);

            Assert.Contains(expectedErrorEndString, errors);
        }

        [Theory]
        [InlineData("{% for a in b %}")]
        [InlineData("{% if true %}")]
        [InlineData("{% unless true %}")]
        [InlineData("{% case a %}")]
        [InlineData("{% capture myVar %}")]
        public void ShouldFailNotClosedBlock(string source)
        {
            var result = _parser.TryParse(source, out var template, out var errors);

            Assert.False(result);
            Assert.NotNull(errors);
        }

        [Theory]
        [InlineData("{% for a in b %} {% endfor %}")]
        [InlineData("{% if true %} {% endif %}")]
        [InlineData("{% unless true %} {% endunless %}")]
        [InlineData("{% case a %} {% when 'cake' %} blah {% endcase %}")]
        [InlineData("{% capture myVar %} capture me! {% endcapture %}")]
        public void ShouldSucceedClosedBlock(string source)
        {
            var result = _parser.TryParse(source, out var template, out var error);

            Assert.True(result);
            Assert.NotNull(template);
            Assert.Null(error);
        }

        [Fact]
        public void ShouldAllowNewLinesInCase()
        {
            var result = _parser.TryParse(@"
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
            Assert.Null(errors);
        }

        [Theory]
        [InlineData("{{ 20 | divided_by: 7.0 | round: 2 }}", "2.86")]
        [InlineData("{{ 20 | divided_by: 7 | round: 2 }}", "2")]
        public void ShouldParseIntegralNumbers(string source, string expected)
        {
            var result = _parser.TryParse(source, out var template, out var errors);

            Assert.True(result);
            Assert.NotNull(template);
            Assert.Null(errors);

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

            if (_parser.TryParse(template, out var result))
            {
                result.Render(new TemplateContext(model));
            }
        }

        [Fact]
        public void ShouldRegisterModelType()
        {
            var model = new
            {
                name = "Tobi"
            };

            var source = "{{name}}";

            _parser.TryParse(source, out var template);

            var rendered = template.Render(new TemplateContext(model));

            Assert.Equal("Tobi", rendered);
        }

        [Theory]
        [InlineData("{% for %}")]
        [InlineData("{% case %}")]
        [InlineData("{% if %}")]
        [InlineData("{% unless %}")]
        [InlineData("{% comment %}")]
        [InlineData("{% raw %}")]
        [InlineData("{% capture %}")]

        public void ShouldThrowParseExceptionMissingTag(string template)
        {
            Assert.Throws<ParseException>(() => _parser.Parse(template));
        }

        [Theory]
        [InlineData("{{ 'a\\nb' }}", "a\nb")]
        [InlineData("{{ 'a\\tb' }}", "a\tb")]
        public void ShouldParseEscapeSequences(string source, string expected)
        {
            var result = _parser.TryParse(source, out var template, out var errors);

            Assert.True(result, errors);
            Assert.NotNull(template);
            Assert.Null(errors);

            var rendered = template.Render();

            Assert.Equal(expected, rendered);
        }

        [Theory]
        [InlineData("{{ 'a\nb' }}", "a\nb")]
        [InlineData("{{ 'a\r\nb' }}", "a\r\nb")]
        public void ShouldParseLineBreaksInStringLiterals(string source, string expected)
        {
            var result = _parser.TryParse(source, out var template, out var errors);

            Assert.True(result, errors);
            Assert.NotNull(template);
            Assert.Null(errors);

            var rendered = template.Render();

            Assert.Equal(expected, rendered);
        }

        [Theory]
        [InlineData("{{ -3 }}", "-3")]
        public void ShouldParseNegativeNumbers(string source, string expected)
        {
            var result = _parser.TryParse(source, out var template, out var errors);

            Assert.NotNull(template);
            Assert.Null(errors);

            var rendered = template.Render();

            Assert.Equal(expected, rendered);
        }

        [Theory]
        [InlineData("{% assign my_integer = 7 %}{{ 20 | divided_by: my_integer }}", "2")]
        [InlineData("{% assign my_integer = 7 %}{% assign my_float = my_integer | times: 1.0 %}{{ 20 | divided_by: my_float | round: 5 }}", "2.85714")]
        [InlineData("{{ 183.357 | times: 12 }}", "2200.284")]
        public void ShouldChangeVariableType(string source, string expected)
        {
            var result = _parser.TryParse(source, out var template, out var errors);

            Assert.True(result);
            Assert.NotNull(template);
            Assert.Null(errors);

            var rendered = template.Render();

            Assert.Equal(expected, rendered);
        }

        [Theory]
        [InlineData("{% assign my_string = 'abcd' %}{{ my_string.size }}", "4")]
        public void SizeAppliedToStrings(string source, string expected)
        {
            var result = _parser.TryParse(source, out var template, out var errors);

            Assert.True(result);
            Assert.NotNull(template);
            Assert.Null(errors);

            var rendered = template.Render();

            Assert.Equal(expected, rendered);
        }


        [Theory]
        [InlineData("{{ '{{ {% %} }}' }}{% assign x = '{{ {% %} }}' %}{{ x }}", "{{ {% %} }}{{ {% %} }}")]
        public void StringsCanContainCurlies(string source, string expected)
        {
            var result = _parser.TryParse(source, out var template, out var errors);

            Assert.True(result);
            Assert.NotNull(template);
            Assert.Null(errors);

            var rendered = template.Render();

            Assert.Equal(expected, rendered);
        }

        [Fact]
        public void ShouldSkipNewLines()
        {
            var source = @"{%if true
                                    and 
                                        true%}true{%endif%}";

            var result = _parser.TryParse(source, out var template, out var errors);

            Assert.True(result);
            Assert.NotNull(template);
            Assert.Null(errors);

            var rendered = template.Render();

            Assert.Equal("true", rendered);
        }

        [Theory]

        [InlineData("'' == p", "false")]
        [InlineData("p == ''", "false")]
        [InlineData("p != ''", "true")]

        [InlineData("p == nil", "true")]
        [InlineData("p != nil", "false")]
        [InlineData("nil == p", "true")]

        [InlineData("p == blank", "true")]
        [InlineData("blank == p ", "true")]

        [InlineData("empty == blank", "true")]
        [InlineData("blank == empty", "true")]

        [InlineData("nil == blank", "true")]
        [InlineData("blank == nil", "true")]

        [InlineData("blank == ''", "true")]
        [InlineData("'' == blank", "true")]

        [InlineData("nil == ''", "false")]
        [InlineData("'' == nil", "false")]

        [InlineData("empty == ''", "true")]
        [InlineData("'' == empty", "true")]

        [InlineData("e == ''", "true")]
        [InlineData("'' == e", "true")]

        [InlineData("e == blank", "true")]
        [InlineData("blank == e", "true")]

        [InlineData("empty == nil", "false")]
        [InlineData("nil == empty", "false")]

        [InlineData("p != nil and p != ''", "false")]
        [InlineData("p != '' and p != nil", "false")]

        [InlineData("e != nil and e != ''", "false")]
        [InlineData("e != '' and e != nil", "false")]

        [InlineData("f != nil and f != ''", "true")]
        [InlineData("f != '' and f != nil", "true")]

        [InlineData("e == nil", "false")]
        [InlineData("nil == e", "false")]

        [InlineData("e == empty ", "true")]
        [InlineData("empty == e ", "true")]

        [InlineData("empty == f", "false")]
        [InlineData("f == empty", "false")]

        [InlineData("p == empty", "false")]
        [InlineData("empty == p", "false")]

        public Task EmptyShouldEqualToNil(string source, string expected)
        {
            return CheckAsync(source, expected, t => t.SetValue("e", "").SetValue("f", "hello"));
        }
    }
}
