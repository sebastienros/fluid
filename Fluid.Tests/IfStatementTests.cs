using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class IfStatementTests
    {
        private static List<Statement> TEXT(string text)
        {
            return new List<Statement> { new TextSpanStatement(text) };
        }

        private static Expression BooleanExpression(bool value, bool async)
        {
            var boolean = BooleanValue.Create(value);
            return async ? new AwaitedExpression(boolean) : new LiteralExpression(boolean);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task IfCanProcessWhenTrue(bool async)
        {
            var e = new IfStatement(
                BooleanExpression(true, async),
                new List<Statement> { new TextSpanStatement("x") }
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("x", sw.ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task IfDoesntProcessWhenFalse(bool async)
        {
            var e = new IfStatement(
                BooleanExpression(false, async),
                new List<Statement> { new TextSpanStatement("x") }
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("", sw.ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task IfDoesntProcessElseWhenTrue(bool async)
        {
            var e = new IfStatement(
                BooleanExpression(true, async),
                new List<Statement> {
                    new TextSpanStatement("x")
                },
                new ElseStatement(new List<Statement> {
                        new TextSpanStatement("y")
                    }));

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("x", sw.ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task IfProcessElseWhenFalse(bool async)
        {
            var e = new IfStatement(
                BooleanExpression(false, async),
                new List<Statement> {
                    new TextSpanStatement("x")
                },
                new ElseStatement(new List<Statement> {
                        new TextSpanStatement("y")
                    })
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("y", sw.ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task IfProcessElseWhenNoOther(bool async)
        {
            var e = new IfStatement(
                BooleanExpression(false, async),
                TEXT("a"),
                new ElseStatement(TEXT("b")),
                new List<ElseIfStatement> { new ElseIfStatement(BooleanExpression(false, async), TEXT("c")) }
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("b", sw.ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task IfProcessElseIf(bool async)
        {
            var e = new IfStatement(
                BooleanExpression(false, async),
                TEXT("a"),
                new ElseStatement(TEXT("b")),
                new List<ElseIfStatement> { new ElseIfStatement(BooleanExpression(true, async), TEXT("c")) }
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("c", sw.ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task IfProcessMultipleElseIf(bool async)
        {
            var e = new IfStatement(
                BooleanExpression(false, async),
                TEXT("a"),
                new ElseStatement(TEXT("b")),
                new List<ElseIfStatement> {
                    new ElseIfStatement(BooleanExpression(false, async), TEXT("c")),
                    new ElseIfStatement(BooleanExpression(true, async), TEXT("d"))}
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("d", sw.ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task IfProcessFirstElseIf(bool async)
        {
            var e = new IfStatement(
                BooleanExpression(false, async),
                TEXT("a"),
                new ElseStatement(TEXT("b")),
                new List<ElseIfStatement> {
                    new ElseIfStatement(BooleanExpression(true, async), TEXT("c")),
                    new ElseIfStatement(BooleanExpression(true, async), TEXT("d"))}
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("c", sw.ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task IfProcessNoMatchElseIf(bool async)
        {
            var e = new IfStatement(
                BooleanExpression(false, async: false),
                TEXT("a"),
                null,
                new List<ElseIfStatement> {
                    new ElseIfStatement(BooleanExpression(false, async), TEXT("c")),
                    new ElseIfStatement(BooleanExpression(false, async), TEXT("d"))}
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("", sw.ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task IfProcessAwaitedAndElse(bool async)
        {
            var e = new IfStatement(
                BooleanExpression(false, async: false),
                TEXT("a"),
                new ElseStatement(TEXT("c")),
                new List<ElseIfStatement>
                {
                    new ElseIfStatement(BooleanExpression(false, async), TEXT("b"))
                }
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("c", sw.ToString());
        }
    }

    sealed class AwaitedExpression : Expression
    {
        private readonly FluidValue _result;

        public AwaitedExpression(FluidValue result)
        {
            _result = result;
        }

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            await Task.Delay(10);
            return _result;
        }
    }
}
