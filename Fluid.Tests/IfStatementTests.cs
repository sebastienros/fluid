using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Values;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Fluid.Tests
{
    public class IfStatementTests
    {
        private Expression TRUE = new LiteralExpression(new BooleanValue(true));
        private Expression FALSE = new LiteralExpression(new BooleanValue(false));

        private List<Statement> TEXT(string text)
        {
            return new List<Statement> { new TextStatement(new StringSegment(text)) };
        }

        [Fact]
        public async Task IfCanProcessWhenTrue()
        {
            var e = new IfStatement(
                TRUE,
                new List<Statement> { new TextStatement(new StringSegment("x")) }
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("x", sw.ToString());
        }

        [Fact]
        public async Task IfDoesntProcessWhenFalse()
        {
            var e = new IfStatement(
                FALSE,
                new List<Statement> { new TextStatement(new StringSegment("x")) }
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("", sw.ToString());
        }

        [Fact]
        public async Task IfDoesntProcessElseWhenTrue()
        {
            var e = new IfStatement(
                TRUE,
                new List<Statement> {
                    new TextStatement(new StringSegment("x"))
                },
                new ElseStatement(new List<Statement> {
                        new TextStatement(new StringSegment("y"))
                    }));

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("x", sw.ToString());
        }

        [Fact]
        public async Task IfProcessElseWhenFalse()
        {
            var e = new IfStatement(
                FALSE,
                new List<Statement> {
                    new TextStatement(new StringSegment("x"))
                },
                new ElseStatement(new List<Statement> {
                        new TextStatement(new StringSegment("y"))
                    })
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("y", sw.ToString());
        }

        [Fact]
        public async Task IfProcessElseWhenNoOther()
        {
            var e = new IfStatement(
                FALSE,
                TEXT("a"),
                new ElseStatement(TEXT("b")),
                new List<ElseIfStatement> { new ElseIfStatement(FALSE, TEXT("c")) }
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("b", sw.ToString());
        }

        [Fact]
        public async Task IfProcessElseIf()
        {
            var e = new IfStatement(
                FALSE,
                TEXT("a"),
                new ElseStatement(TEXT("b")),
                new List<ElseIfStatement> { new ElseIfStatement(TRUE, TEXT("c")) }
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("c", sw.ToString());
        }

        [Fact]
        public async Task IfProcessMultipleElseIf()
        {
            var e = new IfStatement(
                FALSE,
                TEXT("a"),
                new ElseStatement(TEXT("b")),
                new List<ElseIfStatement> {
                    new ElseIfStatement(FALSE, TEXT("c")),
                    new ElseIfStatement(TRUE, TEXT("d"))}
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("d", sw.ToString());
        }

        [Fact]
        public async Task IfProcessFirstElseIf()
        {
            var e = new IfStatement(
                FALSE,
                TEXT("a"),
                new ElseStatement(TEXT("b")),
                new List<ElseIfStatement> {
                    new ElseIfStatement(TRUE, TEXT("c")),
                    new ElseIfStatement(TRUE, TEXT("d"))}
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("c", sw.ToString());
        }

        [Fact]
        public async Task IfProcessNoMatchElseIf()
        {
            var e = new IfStatement(
                FALSE,
                TEXT("a"),
                null,
                new List<ElseIfStatement> {
                    new ElseIfStatement(FALSE, TEXT("c")),
                    new ElseIfStatement(FALSE, TEXT("d"))}
                );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("", sw.ToString());
        }
    }
}
