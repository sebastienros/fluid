#pragma warning disable FLUID001

using Fluid.Ast;
using Fluid.Parser;
using Fluid.Tests.Visitors;
using Fluid.Values;
using Fluid.ViewEngine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class StatementLocationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRequireExperimentalOptIn(bool suppressDiagnostic)
    {
        var source = """
            using Fluid;
            using Fluid.Ast;

            public static class Consumer
            {
                public static FluidParser Create() => new(new FluidParserOptions { TrackStatementLocations = true });
                public static int Read(Statement statement) => statement.SourceOffset + statement.SourceLength;
            }
            """;

        if (suppressDiagnostic)
        {
            source = "#pragma warning disable FLUID001\n" + source;
        }

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location));
        var compilation = CSharpCompilation.Create(
            "StatementLocationConsumer",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = compilation.GetDiagnostics();
        if (suppressDiagnostic)
        {
            Assert.Empty(diagnostics);
        }
        else
        {
            Assert.Equal(3, diagnostics.Length);
            Assert.All(diagnostics, diagnostic => Assert.Equal("FLUID001", diagnostic.Id));
        }
    }

    private static FluidParser CreateParser(bool track = true)
    {
        var parser = new FluidParser(new FluidParserOptions
        {
            TrackStatementLocations = track,
            AllowLiquidTag = true,
            AllowFunctions = true
        });
#if COMPILED
        parser.Compile();
#endif
        return parser;
    }

    private static IReadOnlyList<Statement> Parse(FluidParser parser, string source)
        => ((FluidTemplate)parser.Parse(source)).Statements;

    private static void AssertRange(string source, Statement statement, string expected, int? offset = null)
    {
        Assert.Equal(offset ?? source.IndexOf(expected, StringComparison.Ordinal), statement.SourceOffset);
        Assert.Equal(expected.Length, statement.SourceLength);
        Assert.Equal(expected, source.Substring(statement.SourceOffset, statement.SourceLength));
    }

    [Theory]
    [InlineData("{{ value | default: 'x' }}")]
    [InlineData("{{- value -}}")]
    [InlineData("{% assign x = 2 %}")]
    [InlineData("{% break %}")]
    [InlineData("{% continue %}")]
    [InlineData("{% increment x %}")]
    [InlineData("{% decrement x %}")]
    [InlineData("{% cycle 'a', 'b' %}")]
    [InlineData("{% include 'x' %}")]
    [InlineData("{% include 'x' with value %}")]
    [InlineData("{% include 'x' for values %}")]
    [InlineData("{% render 'x' %}")]
    [InlineData("{% render 'x' with value %}")]
    [InlineData("{% render 'x' for values %}")]
    [InlineData("{% echo 2 %}")]
    [InlineData("{% from 'x' import value %}")]
    [InlineData("{% # comment %}")]
    [InlineData("{% capture x %}{{ 2 }}{% endcapture %}")]
    [InlineData("{% capture x %}{% endcapture %}")]
    [InlineData("{% if true %}yes{% endif %}")]
    [InlineData("{% unless false %}yes{% endunless %}")]
    [InlineData("{% for x in (1..2) %}{{ x }}{% endfor %}")]
    [InlineData("{% tablerow x in (1..2) %}{{ x }}{% endtablerow %}")]
    [InlineData("{% tablerow x in values %}{{ x }}{% endtablerow %}")]
    [InlineData("{% case x %}{% when 1 %}yes{% else %}no{% endcase %}")]
    [InlineData("{% ifchanged %}yes{% endifchanged %}")]
    [InlineData("{% raw %}{{ unparsed }}{% endraw %}")]
    [InlineData("{% raw %}{% endraw %}")]
    [InlineData("{% comment %}{{ unparsed }}{% endcomment %}")]
    [InlineData("{% doc %}Documentation{% enddoc %}")]
    [InlineData("{% macro x() %}yes{% endmacro %}")]
    [InlineData("{%- capture x -%}{{- 2 -}}{%- endcapture -%}")]
    public void ShouldTrackFullStatement(string syntax)
    {
        var source = "prefix" + syntax + "suffix";
        var statements = Parse(CreateParser(), source);

        AssertRange(source, statements[0], "prefix");
        AssertRange(source, statements[1], syntax);
        AssertRange(source, statements[2], "suffix");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldKeepLocationsUnavailableByDefault(bool compiled)
    {
        var parser = new FluidParser();
        if (compiled)
        {
            parser.Compile();
        }

        var visitor = new LocationVisitor();
        visitor.VisitTemplate(parser.Parse("text{% if true %}{{ 2 }}{% elsif false %}other{% else %}end{% endif %}"));
        Assert.NotEmpty(visitor.Statements);
        foreach (var statement in visitor.Statements)
        {
            Assert.Equal(-1, statement.SourceOffset);
            Assert.Equal(0, statement.SourceLength);
        }

        var manual = new TextSpanStatement("manual");
        Assert.Equal(-1, manual.SourceOffset);
        Assert.Equal(0, manual.SourceLength);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldTrackWithLiquidModeDisabled(bool compiled)
    {
        const string source = "prefix{{ \n 2 \n }}{%- assign x = 2 -%}suffix";
        var parser = new FluidParser(new FluidParserOptions { TrackStatementLocations = true });
        if (compiled)
        {
            parser.Compile();
        }

        var statements = Parse(parser, source);
        AssertRange(source, statements[1], "{{ \n 2 \n }}");
        AssertRange(source, statements[2], "{%- assign x = 2 -%}");
    }

    [Fact]
    public void ShouldTrackEmptyAndWhitespaceOnlyTemplates()
    {
        Assert.Empty(Parse(CreateParser(), ""));
        const string source = " \r\n\t ";
        AssertRange(source, Assert.Single(Parse(CreateParser(), source)), source);
    }

    [Theory]
    [InlineData("if true", "endif")]
    [InlineData("unless false", "endunless")]
    public void ShouldTrackNestedBranches(string opening, string closing)
    {
        var source = "prefix{% " + opening + " %}{{ 2 }}{% elsif false %}middle{% else ignored %}last{% " + closing + " %}suffix";
        var statement = Parse(CreateParser(), source)[1];
        var branches = statement is IfStatement ifStatement ? ifStatement.ElseIfs : ((UnlessStatement)statement).ElseIfs;
        var otherwise = statement is IfStatement conditional ? conditional.Else : ((UnlessStatement)statement).Else;
        var body = ((TagStatement)statement).Statements;

        AssertRange(source, body[0], "{{ 2 }}");
        AssertRange(source, branches[0], "{% elsif false %}middle");
        AssertRange(source, branches[0].Statements[0], "middle");
        AssertRange(source, otherwise, "{% else ignored %}last");
        AssertRange(source, otherwise.Statements[0], "last");
    }

    [Fact]
    public void ShouldKeepAbsentElseUnlocated()
    {
        var statement = Assert.IsType<IfStatement>(Parse(CreateParser(), "{% if true %}{% endif %}")[0]);
        Assert.Equal(-1, statement.Else.SourceOffset);
        Assert.Equal(0, statement.Else.SourceLength);
    }

    [Fact]
    public void ShouldTrackFirstLenientElseWithoutChangingIgnoredBranches()
    {
        const string source = "{% if false %}a{% else %}b{% elsif true %}ignored{% else %}ignored too{% endif %}";
        var parser = CreateParser();
        var statement = Assert.IsType<IfStatement>(Parse(parser, source)[0]);

        AssertRange(source, statement, source);
        AssertRange(source, statement.Else, "{% else %}b");
        Assert.Equal("b", parser.Parse(source).Render());
    }

    [Fact]
    public void ShouldTrackForElseAndCaseChildren()
    {
        const string source = "{% for x in values %}{% case x %}{% when 1 %}one{% else %}other{% endcase %}{% else %}empty{% endfor %}";
        var loop = Assert.IsType<ForStatement>(Parse(CreateParser(), source)[0]);
        var @case = Assert.IsType<CaseStatement>(loop.Statements[0]);

        AssertRange(source, loop, source);
        AssertRange(source, @case, "{% case x %}{% when 1 %}one{% else %}other{% endcase %}");
        AssertRange(source, @case.Blocks[0].Statements[0], "one");
        AssertRange(source, @case.Blocks[1].Statements[0], "other");
        AssertRange(source, loop.Else, "{% else %}empty");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void ShouldTrackLiquidChildren(string newline)
    {
        var source = "prefix{% liquid" + newline +
            "  assign x = 2" + newline +
            "  if x" + newline +
            "    echo x" + newline +
            "  else" + newline +
            "    echo 0" + newline +
            "  endif" + newline +
            "%}suffix";
        var liquid = Assert.IsType<LiquidStatement>(Parse(CreateParser(), source)[1]);
        AssertRange(source, liquid, source.Substring(6, source.Length - 12));
        AssertRange(source, liquid.Statements[0], "assign x = 2" + newline);
        var conditional = Assert.IsType<IfStatement>(liquid.Statements[1]);
        AssertRange(source, conditional, "if x" + newline + "    echo x" + newline + "  else" + newline + "    echo 0" + newline + "  endif" + newline);
        AssertRange(source, conditional.Statements[0], "echo x" + newline);
        AssertRange(source, conditional.Else, "else" + newline + "    echo 0" + newline);
    }

    [Fact]
    public void ShouldTrackLiquidLastLineWithoutNewline()
    {
        const string source = "{% liquid echo 2 %}";
        var liquid = Assert.IsType<LiquidStatement>(Parse(CreateParser(), source)[0]);
        AssertRange(source, liquid, source);
        AssertRange(source, liquid.Statements[0], "echo 2");
    }

    [Theory]
    [InlineData("include")]
    [InlineData("render")]
    public void ShouldTrackArgumentAssignments(string tag)
    {
        var source = "{% " + tag + " 'x', a: 2, b: 'value' %}";
        var statement = Parse(CreateParser(), source)[0];
        var assignments = statement is IncludeStatement include ? include.AssignStatements : ((RenderStatement)statement).AssignStatements;
        AssertRange(source, assignments[0], "a: 2");
        AssertRange(source, assignments[1], "b: 'value'");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldTrackCustomRegistrations(bool compiled)
    {
        var parser = new FluidViewParser(new FluidParserOptions { TrackStatementLocations = true });
        parser.RegisterEmptyTag("empty", (output, encoder, context) => Statement.Normal());
        parser.RegisterEmptyBlock("block", (statements, output, encoder, context) => Statement.Normal());
        parser.RegisterExpressionTag("expression", (expression, output, encoder, context) => Statement.Normal());
        parser.RegisterExpressionBlock("expressionblock", (expression, statements, output, encoder, context) => Statement.Normal());
        parser.RegisteredTags["direct"] = Parlot.Fluent.Parsers.Terms.Text("%}").Then<Statement>(_ => new NoOpStatement());
        if (compiled)
        {
            parser.Compile();
        }

        foreach (var syntax in new[]
        {
            "{% empty %}", "{% block %}text{% endblock %}", "{% expression 2 %}",
            "{% expressionblock 2 %}text{% endexpressionblock %}", "{% direct %}",
            "{% layout 'x' %}", "{% section body %}text{% endsection %}",
            "{% partial 'x', a: 2 %}", "{% renderbody %}", "{% rendersection body %}"
        })
        {
            var source = "prefix" + syntax + "suffix";
            AssertRange(source, Parse(parser, source)[1], syntax);
        }

        const string blockSource = "{% block %}{{ 2 }}{% endblock %}";
        var block = Assert.IsType<EmptyBlockStatement>(Parse(parser, blockSource)[0]);
        AssertRange(blockSource, block.Statements[0], "{{ 2 }}");
    }

    [Fact]
    public void ShouldTrackViewEngineArgumentAssignments()
    {
        const string source = "{% partial 'x', a: 2 %}";
        var parser = new FluidViewParser(new FluidParserOptions { TrackStatementLocations = true });
#if COMPILED
        parser.Compile();
#endif
        var visitor = new ParserVisitor();
        visitor.VisitTemplate(parser.Parse(source));
        var assignments = Assert.IsAssignableFrom<IReadOnlyList<AssignStatement>>(
            visitor.Value.GetType().GetProperty("Assignments").GetValue(visitor.Value));
        AssertRange(source, Assert.Single(assignments), "a: 2");
    }

    [Fact]
    public async Task ShouldKeepOriginalTextLocationsAfterTrimming()
    {
        const string source = " \n {{- 2 -}} \n ";
        var template = CreateParser().Parse(source);
        var statements = ((FluidTemplate)template).Statements;
        Assert.Equal("2", await template.RenderAsync());
        AssertRange(source, statements[0], " \n ", 0);
        AssertRange(source, statements[1], "{{- 2 -}}");
        AssertRange(source, statements[2], " \n ", 12);
        Assert.Equal("2", await template.RenderAsync());
    }

    [Fact]
    public void ShouldUseUtf16OffsetsAndSupportConcurrentReuse()
    {
        var parser = CreateParser();
        Parallel.For(0, 20, i =>
        {
            var prefix = "\uD83D\uDE00" + new string('x', i);
            var source = prefix + "{{ 2 }}";
            AssertRange(source, Parse(parser, source)[1], "{{ 2 }}", prefix.Length);
        });
        Assert.False(parser.TryParse("{% if true %}", out _, out _));
        AssertRange("{{ 2 }}", Parse(parser, "{{ 2 }}")[0], "{{ 2 }}", 0);
    }

    [Fact]
    public void ShouldPreserveOriginalRangesDuringRewriting()
    {
        const string source = "{% if true %}{{ 2 }}{% else %}{{ 2 }}{% endif %}";
        var template = CreateParser().Parse(source);
        var rewritten = new ReplaceTwosVisitor(NumberValue.Create(4)).VisitTemplate(template);
        var conditional = Assert.IsType<IfStatement>(((FluidTemplate)rewritten).Statements[0]);
        AssertRange(source, conditional, source);
        AssertRange(source, conditional.Statements[0], "{{ 2 }}");
        AssertRange(source, conditional.Else, "{% else %}{{ 2 }}");
        AssertRange(source, conditional.Else.Statements[0], "{{ 2 }}", source.LastIndexOf("{{ 2 }}", StringComparison.Ordinal));
        Assert.Equal("4", rewritten.Render());
    }

    [Fact]
    public void ShouldKeepExplicitReplacementLocationsAndAllowDeletion()
    {
        var parser = CreateParser();
        var template = parser.Parse("prefix{{ 2 }}");
        var replacement = Parse(parser, "{{ 4 }}")[0];
        var replaced = new ReplaceOutputVisitor(replacement).VisitTemplate(template);
        Assert.Same(replacement, ((FluidTemplate)replaced).Statements[1]);
        AssertRange("{{ 4 }}", replacement, "{{ 4 }}", 0);

        var removed = new ReplaceOutputVisitor(null).VisitTemplate(template);
        Assert.Single(((FluidTemplate)removed).Statements);
        Assert.Equal("prefix", removed.Render());
        Assert.Same(template, new AstRewriter().VisitTemplate(template));
    }

    private sealed class ReplaceOutputVisitor(Statement replacement) : AstRewriter
    {
        protected override Statement VisitOutputStatement(OutputStatement statement) => replacement;
    }

    private sealed class LocationVisitor : AstVisitor
    {
        public List<Statement> Statements { get; } = [];

        public override Statement Visit(Statement statement)
        {
            if (statement != null)
            {
                Statements.Add(statement);
            }

            return base.Visit(statement);
        }
    }
}
