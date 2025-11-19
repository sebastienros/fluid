using Fluid.Ast;
using Fluid.Parser;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class TrailingQuestionTests
    {
        [Fact]
        public void ShouldNotParseTrailingQuestionByDefault()
        {
            var parser = new FluidParser();
            var result = parser.TryParse("{{ product.empty? }}", out var template, out var errors);
            
            Assert.False(result);
            Assert.NotNull(errors);
        }

        [Fact]
        public void ShouldParseTrailingQuestionWhenEnabled()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            var result = parser.TryParse("{{ product.empty? }}", out var template, out var errors);
            
            Assert.True(result);
            Assert.Null(errors);
        }

        [Fact]
        public void ShouldStripTrailingQuestionFromIdentifier()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            parser.TryParse("{{ product.empty? }}", out var template, out var errors);

            var statements = ((FluidTemplate)template).Statements;
            var outputStatement = statements[0] as OutputStatement;
            Assert.NotNull(outputStatement);

            var memberExpression = outputStatement.Expression as MemberExpression;
            Assert.NotNull(memberExpression);
            Assert.Equal(2, memberExpression.Segments.Count);

            var firstSegment = memberExpression.Segments[0] as IdentifierSegment;
            Assert.NotNull(firstSegment);
            Assert.Equal("product", firstSegment.Identifier);

            var secondSegment = memberExpression.Segments[1] as IdentifierSegment;
            Assert.NotNull(secondSegment);
            Assert.Equal("empty", secondSegment.Identifier); // Should NOT contain '?'
        }

        [Fact]
        public async Task ShouldResolveIdentifierWithoutQuestionMark()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            parser.TryParse("{{ products.empty? }}", out var template, out var errors);

            var context = new TemplateContext();
            var sampleObj = new { empty = true };
            context.SetValue("products", sampleObj);

            var result = await template.RenderAsync(context);
            Assert.Equal("true", result);
        }

        [Theory]
        [InlineData("{{ a? }}", "a")]
        [InlineData("{{ product.quantity_price_breaks_configured? }}", "quantity_price_breaks_configured")]
        [InlineData("{{ collection.products.empty? }}", "empty")]
        public void ShouldStripTrailingQuestionFromVariousIdentifiers(string template, string expectedLastSegment)
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            parser.TryParse(template, out var parsedTemplate, out var errors);

            var statements = ((FluidTemplate)parsedTemplate).Statements;
            var outputStatement = statements[0] as OutputStatement;
            Assert.NotNull(outputStatement);

            var memberExpression = outputStatement.Expression as MemberExpression;
            Assert.NotNull(memberExpression);

            var lastSegment = memberExpression.Segments[^1] as IdentifierSegment;
            Assert.NotNull(lastSegment);
            Assert.Equal(expectedLastSegment, lastSegment.Identifier);
        }

        [Fact]
        public void ShouldParseTrailingQuestionInIfStatement()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            var result = parser.TryParse("{% if collection.products.empty? %}No products{% endif %}", out var template, out var errors);
            
            Assert.True(result);
            Assert.Null(errors);
        }

        [Fact]
        public async Task ShouldEvaluateTrailingQuestionInIfStatement()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            parser.TryParse("{% if collection.products.empty? %}No products{% endif %}", out var template, out var errors);

            var context = new TemplateContext();
            var products = new { empty = true };
            var collection = new { products };
            context.SetValue("collection", collection);

            var result = await template.RenderAsync(context);
            Assert.Equal("No products", result);
        }

        [Fact]
        public void ShouldParseTrailingQuestionInFilterArgument()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            var result = parser.TryParse("{{ value | filter: item.empty? }}", out var template, out var errors);
            
            Assert.True(result);
            Assert.Null(errors);
        }

        [Fact]
        public void ShouldParseTrailingQuestionInAssignment()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            var result = parser.TryParse("{% assign x = product.empty? %}", out var template, out var errors);
            
            Assert.True(result);
            Assert.Null(errors);
        }

        [Fact]
        public async Task ShouldEvaluateTrailingQuestionInAssignment()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            parser.TryParse("{% assign x = product.empty? %}{{ x }}", out var template, out var errors);

            var context = new TemplateContext();
            var product = new { empty = false };
            context.SetValue("product", product);

            var result = await template.RenderAsync(context);
            Assert.Equal("false", result);
        }

        [Fact]
        public void ShouldNotAllowMultipleTrailingQuestions()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            var result = parser.TryParse("{{ product.empty?? }}", out var template, out var errors);
            
            // Should fail because we only allow one trailing question mark
            Assert.False(result);
        }

        [Fact]
        public void ShouldParseTrailingQuestionInForLoop()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            var result = parser.TryParse("{% for item in collection.items? %}{{ item }}{% endfor %}", out var template, out var errors);
            
            Assert.True(result);
            Assert.Null(errors);
        }

        [Fact]
        public async Task ShouldWorkWithMixedIdentifiers()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            parser.TryParse("{{ a.b? }}{{ c.d }}", out var template, out var errors);

            var context = new TemplateContext();
            context.SetValue("a", new { b = "value1" });
            context.SetValue("c", new { d = "value2" });

            var result = await template.RenderAsync(context);
            Assert.Equal("value1value2", result);
        }

        [Fact]
        public void ShouldParseTrailingQuestionWithIndexer()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            var result = parser.TryParse("{{ items[0].empty? }}", out var template, out var errors);
            
            Assert.True(result);
            Assert.Null(errors);
        }

        [Theory]
        [InlineData("{{ a? | upcase }}")]
        [InlineData("{{ a.b? | append: '.txt' }}")]
        public void ShouldParseTrailingQuestionWithFilters(string templateText)
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            var result = parser.TryParse(templateText, out var template, out var errors);
            
            Assert.True(result);
            Assert.Null(errors);
        }

        [Fact]
        public async Task ShouldRenderTrailingQuestionWithFilters()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            parser.TryParse("{{ text | upcase }}", out var template, out var errors);

            var context = new TemplateContext();
            context.SetValue("text", "hello");

            var result = await template.RenderAsync(context);
            Assert.Equal("HELLO", result);
        }

        [Fact]
        public void ShouldSupportTrailingQuestionOnIntermediateIdentifiers()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            parser.TryParse("{{ a?.b.c }}", out var template, out var errors);

            var statements = ((FluidTemplate)template).Statements;
            var outputStatement = statements[0] as OutputStatement;
            Assert.NotNull(outputStatement);

            var memberExpression = outputStatement.Expression as MemberExpression;
            Assert.NotNull(memberExpression);
            Assert.Equal(3, memberExpression.Segments.Count);

            var firstSegment = memberExpression.Segments[0] as IdentifierSegment;
            Assert.NotNull(firstSegment);
            Assert.Equal("a", firstSegment.Identifier); // Should NOT contain '?'

            var secondSegment = memberExpression.Segments[1] as IdentifierSegment;
            Assert.NotNull(secondSegment);
            Assert.Equal("b", secondSegment.Identifier);

            var thirdSegment = memberExpression.Segments[2] as IdentifierSegment;
            Assert.NotNull(thirdSegment);
            Assert.Equal("c", thirdSegment.Identifier);
        }

        [Fact]
        public async Task ShouldResolveIntermediateIdentifiersWithTrailingQuestion()
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            parser.TryParse("{{ obj?.nested.value }}", out var template, out var errors);

            var context = new TemplateContext();
            var nested = new { value = "test" };
            var obj = new { nested };
            context.SetValue("obj", obj);

            var result = await template.RenderAsync(context);
            Assert.Equal("test", result);
        }

        [Theory]
        [InlineData("{{ a?.b.c }}", new[] { "a", "b", "c" })]
        [InlineData("{{ a.b?.c }}", new[] { "a", "b", "c" })]
        [InlineData("{{ a?.b?.c }}", new[] { "a", "b", "c" })]
        [InlineData("{{ a?.b?.c? }}", new[] { "a", "b", "c" })]
        public void ShouldStripTrailingQuestionFromAllSegments(string template, string[] expectedIdentifiers)
        {
            var parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true });
            parser.TryParse(template, out var parsedTemplate, out var errors);

            var statements = ((FluidTemplate)parsedTemplate).Statements;
            var outputStatement = statements[0] as OutputStatement;
            Assert.NotNull(outputStatement);

            var memberExpression = outputStatement.Expression as MemberExpression;
            Assert.NotNull(memberExpression);
            Assert.Equal(expectedIdentifiers.Length, memberExpression.Segments.Count);

            for (int i = 0; i < expectedIdentifiers.Length; i++)
            {
                var segment = memberExpression.Segments[i] as IdentifierSegment;
                Assert.NotNull(segment);
                Assert.Equal(expectedIdentifiers[i], segment.Identifier);
            }
        }
    }
}
