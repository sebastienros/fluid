using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Tags;
using Fluid.Tests.Mocks;
using Fluid.Values;
using Irony.Parsing;
using Xunit;

namespace Fluid.Tests
{
    public class CustomGrammarTests
    {
        public CustomGrammarTests()
        {
            new FluidTemplate2();
        }

        [Theory]
        [InlineData("{% more '2' | append: 'pack' %}", "here is some more 2pack")]
        [InlineData("{% more '_Layout' %}", "here is some more _Layout")]
        [InlineData("{% more foo %}", "here is some more bar")]
        [InlineData("{% ice pranav %}", "here is some ice pranav")]
        [InlineData("{% shout stuff (1..3) %}", "stuffstuffstuff")]
        [InlineData("{% argumentstag 'defaultvalue', arg1: 'value1', arg2: 123 %}", ":defaultvaluearg1:value1arg2:123")]
        public void CanAddCustomTag(string source, string expected)
        {
            var context = new TemplateContext();
            context.SetValue("foo", "bar");

            var success = FluidTemplate2.TryParse(source, out var template);
            Assert.True(success);

            var result = template.Render(context);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("{% repeat (1..3) %}foo {{ i }} {% endrepeat %}", "foo 1 foo 2 foo 3 ")]
        [InlineData("{% simple %} bar {% endsimple %}", "simple bar ")]
        [InlineData("{% identifier foo %} bar {% endidentifier %}", "foo bar ")]
        [InlineData("{% exp 'f' | append: 'oo' %} bar {% endexp %}", "foo bar ")]
        [InlineData("{% argumentsblock 'defaultvalue', arg1: 'value1', arg2: 123 %}bar{% endargumentsblock %}", ":defaultvaluearg1:value1arg2:123bar")]
        public void CanAddCustomBlock(string source, string expected)
        {
            var success = FluidTemplate2.TryParse(source, out var template, out var message);
            Assert.True(success, message.FirstOrDefault());

            var result = template.Render();

            Assert.Equal(expected, result);
        }

        [Fact]
        public void IncludeTagsFlowTemplateType()
        {
            var source = "A {% include 'test.liquid' %} B";
            var include = "{% more '2' | append: 'pack' %}";
            var expected = "A here is some more 2pack B";

            var fileProvider = new MockFileProvider()
                .Add("test.liquid", include);

            var context = new TemplateContext() { FileProvider = fileProvider };

            var success = FluidTemplate2.TryParse(source, out var template);
            Assert.True(success);

            var result = template.Render(context);

            Assert.Equal(expected, result);
        }
    }

    public class ShoutTag : ITag
    {
        public BnfTerm GetSyntax(FluidGrammar grammar)
        {
            return grammar.Identifier + grammar.Range;
        }

        public Statement Parse(ParseTreeNode node, ParserContext context)
        {
            var identifier = node.ChildNodes[0].ChildNodes[0].Token.Text;
            var range = node.ChildNodes[0].ChildNodes[1];

            return new ForStatement(
                new List<Statement> { new OutputStatement(new LiteralExpression(new StringValue(identifier))) },
                identifier,
                DefaultFluidParser.BuildRangeExpression(range),
                null,
                null,
                false);
        }        
    }

    public class IceTag : IdentifierTag
    {
        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, string identifier)
        {
            await writer.WriteAsync("here is some ice " + identifier);
            return Completion.Normal;
        }
    }

    public class MoreTag : ExpressionTag
    {
        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, Expression expression)
        {
            var value = await expression.EvaluateAsync(context);
            await writer.WriteAsync("here is some more " + value.ToStringValue());
            return Completion.Normal;
        }
    }

    public class RepeatBlock : ITag
    {
        public BnfTerm GetSyntax(FluidGrammar grammar)
        {
            return grammar.Range;
        }

        public Statement Parse(ParseTreeNode node, ParserContext context)
        {
            var range = context.CurrentBlock.Tag.ChildNodes[0];

            return new ForStatement(
                context.CurrentBlock.Statements,
                "i",
                DefaultFluidParser.BuildRangeExpression(range),
                null,
                null,
                false);
        }
    }

    public class CustomIdentifierBlock : IdentifierBlock
    {
        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, string identifier, List<Statement> statements)
        {
            await writer.WriteAsync(identifier);

            await RenderStatementsAsync(writer, encoder, context, statements);

            return Completion.Normal;
        }
    }

    public class CustomExpressionBlock : ExpressionBlock
    {
        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, Expression expression, List<Statement> statements)
        {
            await writer.WriteAsync((await expression.EvaluateAsync(context)).ToStringValue());

            await RenderStatementsAsync(writer, encoder, context, statements);

            return Completion.Normal;
        }
    }

    public class CustomSimpleBlock : SimpleBlock
    {
        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, List<Statement> statements)
        {
            await writer.WriteAsync("simple");

            await RenderStatementsAsync(writer, encoder, context, statements);

            return Completion.Normal;
        }
    }

    public class CustomArgumentsBlock : ArgumentsBlock
    {
        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, FilterArgument[] arguments, List<Statement> statements)
        {
            foreach (var argument in arguments)
            {
                await writer.WriteAsync(argument.Name + ":");
                await writer.WriteAsync((await argument.Expression.EvaluateAsync(context)).ToStringValue());
            }            

            await RenderStatementsAsync(writer, encoder, context, statements);

            return Completion.Normal;
        }
    }

    public class CustomArgumentsTag : ArgumentsTag
    {
        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, FilterArgument[] arguments)
        {
            foreach (var argument in arguments)
            {
                await writer.WriteAsync(argument.Name + ":");
                await writer.WriteAsync((await argument.Expression.EvaluateAsync(context)).ToStringValue());
            }

            return Completion.Normal;
        }
    }

    public class FluidTemplate2 : BaseFluidTemplate<FluidTemplate2>
    {
        static FluidTemplate2()
        {
            Factory.RegisterTag<ShoutTag>("shout");
            Factory.RegisterTag<IceTag>("ice");
            Factory.RegisterTag<MoreTag>("more");
            Factory.RegisterTag<CustomArgumentsTag>("argumentstag");

            Factory.RegisterBlock<RepeatBlock>("repeat");
            Factory.RegisterBlock<CustomIdentifierBlock>("identifier");
            Factory.RegisterBlock<CustomSimpleBlock>("simple");
            Factory.RegisterBlock<CustomExpressionBlock>("exp");
            Factory.RegisterBlock<CustomArgumentsBlock>("argumentsblock");
        }
    }
}
