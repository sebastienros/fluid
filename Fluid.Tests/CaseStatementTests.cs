using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Values;
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
        public async Task CaseProcessAllsWhensMatchMultiple()
        {
            var e = new CaseStatement(
                A,
                null,
                new[] {
                    new WhenStatement(new List<Expression> { A, B, C }, TEXT("x")),
                    new WhenStatement(new List<Expression> { D }, TEXT("y")),
                    new WhenStatement(new List<Expression> { A }, TEXT("z"))
                }
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("xz", sw.ToString());
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

        [Fact]
        public async Task CaseWithMixedWhenElse_NoMatch_AllElse()
        {
            var parser = new FluidParser();
            var template = @"{%  case 'x' %}
  {% when 'y' %}match1
  {% when 'y' %}match2
  {% else %} else1
  {% else %} else2
  {% when 'y' %}match3
  {% when 'y' %}match4
  {% else %} else3
  {% else %} else4
{% endcase %}";
            
            var result = parser.Parse(template);
            var context = new TemplateContext();
            var output = await result.RenderAsync(context);
            
            Assert.Equal(" else1\n   else2\n   else3\n   else4\n", output);
        }
        
        [Fact]
        public async Task CaseWithMixedWhenElse_Match_OnlyMatches()
        {
            var parser = new FluidParser();
            var template = @"{%  case 'x' %}
  {% when 'x' %}match1
  {% when 'x' %}match2
  {% else %} else1
  {% else %} else2
  {% when 'y' %}match3
  {% when 'y' %}match4
  {% else %} else3
  {% else %} else4
{% endcase %}";
            
            var result = parser.Parse(template);
            var context = new TemplateContext();
            var output = await result.RenderAsync(context);
            
            Assert.Equal("match1\n  match2\n  ", output);
        }

        [Fact]
        public async Task CaseWithMultipleElseBlocks()
        {
            var parser = new FluidParser();
            var template = "{% case 'x' %}{% when 'y' %}foo{% else %}bar{% else %}baz{% endcase %}";
            
            var result = parser.Parse(template);
            var context = new TemplateContext();
            var output = await result.RenderAsync(context);
            
            Assert.Equal("barbaz", output);
        }

        [Fact]
        public async Task CaseWithFalsyWhenBeforeAndTruthyWhenAfterElse()
        {
            var parser = new FluidParser();
            var template = "{% case 'x' %}{% when 'y' %}foo{% else %}bar{% when 'x' %}baz{% endcase %}";
            
            var result = parser.Parse(template);
            var context = new TemplateContext();
            var output = await result.RenderAsync(context);
            
            Assert.Equal("barbaz", output);
        }

        [Fact]
        public async Task CaseWithFalsyWhenBeforeAndTruthyWhenAfterMultipleElseBlocks()
        {
            var parser = new FluidParser();
            var template = "{% case 'x' %}{% when 'y' %}foo{% else %}bar{% else %}baz{% when 'x' %}qux{% endcase %}";
            
            var result = parser.Parse(template);
            var context = new TemplateContext();
            var output = await result.RenderAsync(context);
            
            Assert.Equal("barbazqux", output);
        }

        [Fact]
        public async Task CaseWithTruthyWhenBeforeAndAfterElse()
        {
            var parser = new FluidParser();
            var template = "{% case 'x' %}{% when 'x' %}foo{% else %}bar{% when 'x' %}baz{% endcase %}";
            
            var result = parser.Parse(template);
            var context = new TemplateContext();
            var output = await result.RenderAsync(context);
            
            Assert.Equal("foobaz", output);
        }

        [Fact]
        public async Task CaseEvaluateMultipleMatchingBlocks()
        {
            var parser = new FluidParser();
            var template = "{% case title %}{% when 'Hello' %}foo{% when a, 'Hello' %}bar{% endcase %}";
            
            var result = parser.Parse(template);
            var context = new TemplateContext();
            context.SetValue("title", "Hello");
            context.SetValue("a", "Hello");
            var output = await result.RenderAsync(context);
            
            Assert.Equal("foobarbar", output);
        }
    }
}
