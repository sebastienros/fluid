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
    public class CaseStatementTests
    {
        private LiteralExpression A = new LiteralExpression(new StringValue("a"));
        private LiteralExpression B = new LiteralExpression(new StringValue("b"));
        private LiteralExpression C = new LiteralExpression(new StringValue("c"));
        private LiteralExpression D = new LiteralExpression(new StringValue("d"));

        private List<Statement> TEXT(string text)
        {
            return new List<Statement> { new TextSpanStatement(text) };
        }

        [Fact]
        public async Task CaseCanProcessWhenMatch()
        {
            var e = new CaseStatement(
                A,
                null,
                new[] {
                    new WhenStatement(new List<Expression> { A }, TEXT("x"))
                }
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("x", sw.ToString());
        }

        [Fact]
        public async Task CaseProcessesMultipleStatements()
        {
            var e = new CaseStatement(
                A,
                null,
                new[] {
                    new WhenStatement(new List<Expression> { A }, 
                    new List<Statement> { new TextSpanStatement("x"), new TextSpanStatement("y") })
                }
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("xy", sw.ToString());
        }

        [Fact]
        public async Task CaseEvaluateMember()
        {
            var e = new CaseStatement(
                new MemberExpression(
                    new IdentifierSegment("val")
                ),
                null,
                new[] {
                    new WhenStatement(new List<Expression> { A }, TEXT("x"))
                }
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("val", "a");

            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("x", sw.ToString());
        }

        [Fact]
        public async Task CaseCanProcessWhenMatchMultiple()
        {
            var e = new CaseStatement(
                A,
                null,
                new[] {
                    new WhenStatement(new List<Expression> { A, B, C }, TEXT("x"))
                }
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("x", sw.ToString());
        }

        [Fact]
        public async Task CaseDoesntProcessWhenNoMatch()
        {
            var e = new CaseStatement(
                A,
                null,
                new[] {
                    new WhenStatement(new List<Expression> { B, C, D }, TEXT("x"))
                }
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("", sw.ToString());
        }

        [Fact]
        public async Task CaseDoesntProcessElseWhenMatch()
        {
            var e = new CaseStatement(
                A,
                new ElseStatement(new List<Statement> { new TextSpanStatement("y") }),
                new[] {
                    new WhenStatement(new List<Expression> { A }, TEXT("x"))
                }
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("x", sw.ToString());
        }

        [Fact]
        public async Task CaseProcessElseWhenNoMatch()
        {
            var e = new CaseStatement(
                A,
                new ElseStatement(new List<Statement> { new TextSpanStatement("y") }),
                new[] {
                    new WhenStatement(new List<Expression> { B, C }, TEXT("x"))
                }
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("y", sw.ToString());
        }        

        [Fact]
        public async Task CaseProcessFirstWhen()
        {
            var e = new CaseStatement(
                B,
                new ElseStatement(new List<Statement> { new TextSpanStatement("y") }),
                new[] {
                    new WhenStatement(new List<Expression> { A, C }, TEXT("1")),
                    new WhenStatement(new List<Expression> { B, C }, TEXT("2")),
                    new WhenStatement(new List<Expression> { C }, TEXT("3"))
                }
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("2", sw.ToString());
        }

        [Fact]
        public async Task CaseProcessNoMatchWhen()
        {
            var e = new CaseStatement(
                A,
                null,
                new[] {
                    new WhenStatement(new List<Expression> { B }, TEXT("2")),
                    new WhenStatement(new List<Expression> { C }, TEXT("3"))
                }
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("", sw.ToString());
        }
    }
}
