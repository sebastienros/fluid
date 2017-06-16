using System.Collections.Generic;
using System.Linq;
using Fluid.Ast;
using Irony.Parsing;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Fluid.Tests
{
    public class ParserTests
    {
        private static LanguageData _language = new LanguageData(new FluidGrammar());

        private IList<Statement> Parse(string source)
        {
            FluidTemplate.TryParse(new StringSegment(source), out var template, out var errors);
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
        public void ShouldTrimTextOnStart()
        {
            var statements = Parse("  {% for a in b %}{% endfor %}");
            Assert.IsType<ForStatement>(statements.ElementAt(0));
        }

        [Fact]
        public void ShouldTrimTextOnEnd()
        {
            var statements = Parse("{% for a in b %}{% endfor %}   ");
            Assert.Equal(1, statements.Count);
            Assert.IsType<ForStatement>(statements.ElementAt(0));
        }

        [Fact]
        public void ShouldTrimTextOnLineBreak()
        {
            var statements = Parse(@"{% for a in b %}  
{% endfor %}");

            Assert.Equal(1, statements.Count);
            Assert.Equal(0, ((ForStatement)statements[0]).Statements.Count);
        }

        [Fact]
        public void ShouldTrimTextOnNewLineBreak()
        {
            var statements = Parse(@"{% for a in b %}   

{% endfor %}");
            Assert.Equal(1, statements.Count);
            var text = ((ForStatement)statements[0]).Statements[0] as TextStatement;
            Assert.Equal("\r\n", text.Text);
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
        public void ShouldParseComment()
        {
            var statements = Parse(@"{% comment %} on {{ this }} and {{{ that }}} {% endcomment %}");

            Assert.Equal(1, statements.Count);
            Assert.IsType<CommentStatement>(statements.ElementAt(0));
            Assert.Equal(" on {{ this }} and {{{ that }}} ", (statements.ElementAt(0) as CommentStatement).Text);
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
            Assert.Equal(1, ifStatement.Statements.Count);
            Assert.NotNull(ifStatement.Else);
            Assert.Equal(0, ifStatement.ElseIfs.Count);
        }

        [Fact]
        public void ShouldParseIfElseIfTag()
        {
            var statements = Parse("{% if true %}yes{%elsif a%}maybe{%else%}no{%endif%}");

            var ifStatement = statements.ElementAt(0) as IfStatement;
            Assert.NotNull(ifStatement);
            Assert.Equal(1, ifStatement.Statements.Count);
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
    }
}
