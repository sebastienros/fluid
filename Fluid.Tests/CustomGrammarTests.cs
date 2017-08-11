using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Values;
using Irony.Parsing;
using Xunit;

namespace Fluid.Tests
{
    public class CustomGrammarTests
    {

        [Fact]
        public void CanAddCustomTag()
        {
            FluidTemplate.Factory.RegisterTag<YoloTag>("yolo");

            var success = FluidTemplate.TryParse("{% yolo a (1..3) %}", out var template);
            Assert.True(success);
            
            var result = template.Render();

            Assert.Equal("aaa", result);
        }

        [Fact]
        public void CanAddIdentifierTag()
        {
            FluidTemplate.Factory.RegisterTag<IceTag>("ice");

            var success = FluidTemplate.TryParse("{% ice pranav %}", out var template);
            Assert.True(success);

            var result = template.Render();

            Assert.Equal("here is some ice pranav", result);
        }

        [Fact]
        public void CanAddTermTag()
        {
            FluidTemplate.Factory.RegisterTag<MoreTag>("more");

            var success = FluidTemplate.TryParse("{% more '2' | append: 'pack' %}", out var template);
            Assert.True(success);

            var result = template.Render();

            Assert.Equal("here is some more 2pack", result);
        }

        [Fact]
        public void CanAddCustomBlock()
        {
            FluidTemplate.Factory.RegisterBlock<YoloBlock>("yolo");

            var success = FluidTemplate.TryParse("{% yolo (1..3) %}foo{{ i }}{% endyolo %}", out var template);
            Assert.True(success);

            var result = template.Render();

            Assert.Equal("foo1foo2foo3", result);
        }

        [Fact]
        public void CanCreateCustomTemplate()
        {
            FluidTemplate.Factory.RegisterTag<IceTag>("justdoit");
            FluidTemplate2.Factory.RegisterTag<MoreTag>("justdoit");

            var success1 = FluidTemplate.TryParse("{% justdoit abc %}", out var template1);
            Assert.True(success1);

            var success2 = FluidTemplate2.TryParse("{% justdoit 'abc' %}", out var template2);
            Assert.True(success2);

            var result1 = template1.Render();
            var result2 = template2.Render();

            Assert.Equal("here is some ice abc", result1);
            Assert.Equal("here is some more abc", result2);
        }
    }

    public class YoloTag : ITag
    {
        public BnfTerm GetSyntax(FluidGrammar grammar)
        {
            return grammar.Identifier + grammar.Range;
        }

        public Statement Parse(ParseTreeNode node, ParserContext context)
        {
            var identifier = node.ChildNodes[0].Token.Text;
            var range = node.ChildNodes[1];

            ForStatement yoloStatement = new ForStatement(
                new[] { new OutputStatement(new LiteralExpression(new StringValue(identifier))) },
                identifier,
                DefaultFluidParser.BuildRangeExpression(range),
                null,
                null,
                false);

            return yoloStatement;
        }        
    }

    public class IceTag : IdentifierTag
    {
        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, string identifier)
        {
            await writer.WriteAsync("here is some ice " + identifier);
            return Completion.Normal;
        }
    }

    public class MoreTag : ExpressionTag
    {
        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, Expression expression)
        {
            var value = await expression.EvaluateAsync(context);
            await writer.WriteAsync("here is some more " + value.ToStringValue());
            return Completion.Normal;
        }
    }

    public class YoloBlock : ITag
    {
        public BnfTerm GetSyntax(FluidGrammar grammar)
        {
            return grammar.Range;
        }

        public Statement Parse(ParseTreeNode node, ParserContext context)
        {
            var range = node.ChildNodes[0];

            ForStatement yoloStatement = new ForStatement(
                context.CurrentBlock.Statements,
                "i",
                DefaultFluidParser.BuildRangeExpression(range),
                null,
                null,
                false);

            return yoloStatement;
        }
    }

    public class FluidTemplate2 : BaseFluidTemplate<FluidTemplate2>
    {
    }
}
