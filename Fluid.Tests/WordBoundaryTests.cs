using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Tests.Mocks;
using Xunit;

namespace Fluid.Tests;

// Liquid words (operators, tag arguments and tag names) end where a word char ([A-Za-z0-9_]) would continue them,
// like Shopify's '\w': "orb" is not "or". A '-' ends a word, so trim markers still work after a word.
public class WordBoundaryTests
{
    private static readonly FluidParser _parser = new();
    private static readonly FluidParser _liquidTagParser = new(new FluidParserOptions { AllowLiquidTag = true, AllowFunctions = true });

    [Theory]
    [InlineData("{% if false orb %}yes{% endif %}")]
    [InlineData("{% if false or_b %}yes{% endif %}")]
    [InlineData("{% if false or1 %}yes{% endif %}")]
    [InlineData("{% if true andb %}yes{% endif %}")]
    [InlineData("{% if x containsy %}yes{% endif %}")]
    [InlineData("{% for i inp %}{% endfor %}")]
    [InlineData("{% for i in_p %}{% endfor %}")]
    [InlineData("{% for i in1 %}{% endfor %}")]
    [InlineData("{% for i in-p %}{% endfor %}")]
    [InlineData("{% for i in(1..3) %}{% endfor %}")]
    [InlineData("{% for i in'abc' %}{% endfor %}")]
    [InlineData("{% for i in[p] %}{% endfor %}")]
    [InlineData("{% tablerow i inx %}{% endtablerow %}")]
    [InlineData("{% tablerow i in(1..3) %}{% endtablerow %}")]
    [InlineData("{% if false %}a{% elsewhere %}b{% endif %}")]
    [InlineData("{% if false %}a{% else_x %}b{% endif %}")]
    [InlineData("{% if false %}a{% else1 %}b{% endif %}")]
    [InlineData("{% if false %}a{% elsiftrue %}b{% endif %}")]
    [InlineData("{% if false %}a{% else %}b{% elsifx %}c{% endif %}")]
    [InlineData("{% if false %}a{% else %}b{% elsewhere %}c{% endif %}")]
    [InlineData("{% unless true %}a{% elsewhere %}b{% endunless %}")]
    [InlineData("{% for i in (1..0) %}a{% elsewhere %}b{% endfor %}")]
    [InlineData("{% case 2 %}{% when 1 %}a{% elsewhere %}b{% endcase %}")]
    [InlineData("{% case 2 %}{% when2 %}a{% endcase %}")]
    [InlineData("{% case 2 %}{% whenw %}a{% endcase %}")]
    [InlineData("{% if true %}a{% endifx %}")]
    [InlineData("{% if true %}a{% endif_ %}")]
    [InlineData("{% for i in (1..2) %}{% endforx %}")]
    [InlineData("{% capture c %}a{% endcapturex %}")]
    [InlineData("{% case 1 %}{% when 1 %}a{% endcasex %}")]
    [InlineData("{% comment %}a{% endcomment-x %}b{% endcomment %}")]
    [InlineData("{% render 'x' withx %}")]
    [InlineData("{% render 'x' forx %}")]
    [InlineData("{% render 'x' with x asy %}")]
    [InlineData("{% include 'x' withx %}")]
    [InlineData("{% include 'x' forx %}")]
    [InlineData("{% include 'x' with x as_y %}")]
    [InlineData("{% liquid if false\n echo 'a'\n elsewhere\n echo 'b'\n endif %}")]
    [InlineData("{% liquid case 2\n whenw\n echo 'a'\n endcase %}")]
    [InlineData("{% from 'macros' importhello %}")]
    [InlineData("{% comment_x %}a{% endcomment %}")]
    [InlineData("{% foo-bar %}")]
    [InlineData("{% doc x %}a{% enddoc %}")] // a golden test requires it, even though the Shopify playground accepts it
    public void ShouldNotParseWordFollowedByWordChar(string source)
    {
        Assert.False(_liquidTagParser.TryParse(source, out _, out _));

        if (!source.Contains("{% liquid") && !source.Contains("{% from"))
        {
            Assert.False(_parser.TryParse(source, out _, out _));
        }
    }

    [Theory]
    [InlineData("{% assign x = 'X' %}{% if x contains\"X\" %}yes{% endif %}", "yes")]
    [InlineData("{% assign x = 'X' %}{% if x contains'X' %}yes{% endif %}", "yes")]
    [InlineData("{% if 'X'contains'X' %}yes{% endif %}", "yes")]
    [InlineData("{% if 'X' contains'X'or false %}yes{% endif %}", "yes")]
    [InlineData("{% if false or'x' %}yes{% endif %}", "yes")]
    [InlineData("{% if false or\"x\" %}yes{% endif %}", "yes")]
    [InlineData("{% if true and'' %}yes{% endif %}", "yes")]
    [InlineData("{% if false or true-%} yes{% endif %}", "yes")]
    [InlineData("{% for i in\n(1..3) %}{{ i }}{% endfor %}", "123")]
    [InlineData("{% for i in (1..3) reversed-%} {{ i }}{% endfor %}", "321")]
    [InlineData("{% for i in (1..3) limit:2-%} {{ i }}{% endfor %}", "12")]
    [InlineData("{% if false %}a{%else-%} b{%endif%}", "b")]
    [InlineData("{% if false %}a{% else-x %}b{% endif %}", "b")]
    [InlineData("{% if false %}a{% else'x' %}b{% endif %}", "b")]
    [InlineData("{% if false %}a{% elsif'x' %}b{% endif %}", "b")]
    [InlineData("{% case 2 %}{% when 1 or2 %}yes{% else %}no{% endcase %}", "no")]
    [InlineData("{% assign b = 2 %}{% case 2 %}{% when 1 orb %}yes{% else %}no{% endcase %}", "no")]
    [InlineData("{% assign b = 2 %}{% case 2 %}{% when 1 or b %}yes{% else %}no{% endcase %}", "yes")]
    [InlineData("{% case 'x' %}{% when'x' %}yes{% endcase %}", "yes")]
    [InlineData("{% case 2 %}{% when 1 %}a{%else-%} b{% endcase %}", "b")]
    [InlineData("{% case 2 %}{% when 1 %}a{% else x %}b{% endcase %}", "b")]
    [InlineData("{% case 2 %}{% when 1 %}a{% else-x %}b{% endcase %}", "b")]
    [InlineData("{% for i in (1..0) %}a{% else x %}b{% endfor %}", "b")]
    [InlineData("{% for i in (1..0) %}a{% else-x %}b{% endfor %}", "b")]
    public void ShouldParseWordFollowedByOtherChar(string source, string expected)
    {
        AssertRenders(_parser, source, expected);
        AssertRenders(_liquidTagParser, source, expected);
    }

    [Theory]
    [InlineData("{% if true %}a{% endif x %}", "a")]
    [InlineData("{% if true %}a{% endif-x %}", "a")]
    [InlineData("{% if true %}a{% endif- %} b", "a b")]
    [InlineData("{% if true %}a{% endif'x' %} b", "a b")]
    [InlineData("{% if true %}a{% endif x -%} b", "ab")]
    [InlineData("{% unless false %}a{% endunless x %}", "a")]
    [InlineData("{% for i in (1..2) %}{{ i }}{% endfor x %}", "12")]
    [InlineData("{% case 1 %}{% when 1 %}a{% endcase x %}", "a")]
    [InlineData("{% capture c %}a{% endcapture x %}{{ c }}", "a")]
    [InlineData("{% capture c %}a{% endcapture-x %}{{ c }}", "a")]
    [InlineData("{% raw %}a{% endraw x %}b", "ab")]
    [InlineData("{% raw %}a{% endrawx %}b{% endraw %}", "a{% endrawx %}b")]
    [InlineData("{% comment %}a{% endcomment x %}b", "b")]
    [InlineData("{% doc %}a{% enddoc x %}b", "b")]
    [InlineData("{% doc %}a{% enddoc_x %}b{% enddoc %}c", "c")]
    [InlineData("{% tablerow i in (1..1) %}{{ i }}{% endtablerow x %}", "<tr class=\"row1\">\n<td class=\"col1\">1</td></tr>\n")]
    public void ShouldIgnoreEndTagArguments(string source, string expected)
    {
        AssertRenders(_parser, source, expected);
        AssertRenders(_liquidTagParser, source, expected);
    }

    [Theory]
    [InlineData("{% comment x %}a{% endcomment %}b", "b")]
    [InlineData("{% comment'x' %}a{% endcomment %}b", "b")]
    [InlineData("{% case 1 %}{% comment x %}c{% endcomment %}{% when 1 %}a{% endcase %}", "a")]
    [InlineData("{% case 1 %}{% when 1 %}a{% comment x %}c{% endcomment %}{% when 2 %}b{% endcase %}", "a")]
    [InlineData("{% raw x %}a{% endraw %}b", "ab")]
    [InlineData("{% for i in (1..3) %}{{ i }}{% break x %}{% endfor %}", "1")]
    [InlineData("{% for i in (1..3) %}{{ i }}{% continue x %}{% endfor %}", "123")]
    [InlineData("{% ifchanged x %}a{% endifchanged %}", "a")]
    public void ShouldIgnoreArgumentsOfTagsWithoutArguments(string source, string expected)
    {
        AssertRenders(_parser, source, expected);
        AssertRenders(_liquidTagParser, source, expected);
    }

    // Like in Shopify, "reversed" is only read right after the collection, even when it starts a longer word,
    // "name: value" attributes are read anywhere after the collection, and anything else is ignored.
    [Theory]
    [InlineData("{% for i in x reversed %}{{ i }}{% endfor %}", "cba")]
    [InlineData("{% for i in x reversedx %}{{ i }}{% endfor %}", "cba")]
    [InlineData("{% for i in x reversed_ %}{{ i }}{% endfor %}", "cba")]
    [InlineData("{% for i in x reversed:2 %}{{ i }}{% endfor %}", "cba")]
    [InlineData("{% for i in x reversed-limit:2 %}{{ i }}{% endfor %}", "cba")]
    [InlineData("{% for i in x reversedx limit:2 %}{{ i }}{% endfor %}", "ba")]
    [InlineData("{% for i in x reversed limit:2 %}{{ i }}{% endfor %}", "ba")]
    [InlineData("{% for i in x limit:2 reversed %}{{ i }}{% endfor %}", "ab")]
    [InlineData("{% for i in x, reversed %}{{ i }}{% endfor %}", "abc")]
    [InlineData("{% for i in x xreversed %}{{ i }}{% endfor %}", "abc")]
    [InlineData("{% for i in x foo limit:2 %}{{ i }}{% endfor %}", "ab")]
    [InlineData("{% for i in x limit:2 foo %}{{ i }}{% endfor %}", "ab")]
    [InlineData("{% for i in x 'a' 12 | limit:2 %}{{ i }}{% endfor %}", "ab")]
    [InlineData("{% for i in x foo:2 limit :2 %}{{ i }}{% endfor %}", "ab")]
    [InlineData("{% for i in x limitx:1 %}{{ i }}{% endfor %}", "abc")]
    [InlineData("{% for i in x foolimit:1 %}{{ i }}{% endfor %}", "abc")]
    [InlineData("{% for i in x offset_:1 %}{{ i }}{% endfor %}", "abc")]
    [InlineData("{% for i in x limit %}{{ i }}{% endfor %}", "abc")]
    [InlineData("{% for i in x limit: %}{{ i }}{% endfor %}", "abc")]
    [InlineData("{% for i in x limit:2,offset:1 %}{{ i }}{% endfor %}", "bc")]
    [InlineData("{% for i in x limit:2 limit:1 %}{{ i }}{% endfor %}", "a")]
    [InlineData("{% for i in x reversed foo -%} {{ i }}{% endfor %}", "cba")]
    [InlineData("{% tablerow i in x reversed foo colsx:1 limit:2 %}{{ i }}{% endtablerow %}", "<tr class=\"row1\">\n<td class=\"col1\">a</td><td class=\"col2\">b</td></tr>\n")]
    [InlineData("{% tablerow i in (1..3) cols:1 foo offset:2 %}{{ i }}{% endtablerow %}", "<tr class=\"row1\">\n<td class=\"col1\">3</td></tr>\n")]
    public void ShouldReadLoopAttributesLikeShopify(string source, string expected)
    {
        source = "{% assign x = 'a,b,c' | split: ',' %}" + source;
        AssertRenders(_parser, source, expected);
        AssertRenders(_liquidTagParser, source, expected);
    }

    [Theory]
    [InlineData("{% comment %}a{% endcomment_x %}b{% endcomment %}c", "c")]
    [InlineData("{% comment %}a{% endcomment1 %}b{% endcomment %}c", "c")]
    [InlineData("{% comment %}a{% comment_x %}b{% endcomment %}c", "c")]
    [InlineData("{% comment %}a{% raw_x %}b{% endcomment %}c", "c")]
    [InlineData("{% comment %}a{% raw %}{% endcomment %}{% endraw_x %}{% endcomment %}{% endraw %}b{% endcomment %}c", "c")]
    public void ShouldNotMatchCommentTagsFollowedByWordChar(string source, string expected)
    {
        AssertRenders(_parser, source, expected);
    }

    [Theory]
    [InlineData("{% liquid for i in (1..2)\n echo i\n endfor x %}", "12")]
    [InlineData("{% liquid if false\n echo 'a'\n else x\n echo 'b'\n endif %}", "b")]
    [InlineData("{% liquid assign x = 'X'\n if x contains'X'\n echo 'yes'\n endif %}", "yes")]
    [InlineData("{% liquid case 2\n when 1 or2\n echo 'yes'\n else\n echo 'no'\n endcase %}", "no")]
    [InlineData("{% liquid comment\n a\n endcomment_x\n b\n endcomment\n echo 'c' %}", "c")]
    [InlineData("{% liquid comment\n a\n endcomment x\n echo 'c' %}", "c")]
    [InlineData("{% liquid comment x\n a\n endcomment\n echo 'c' %}", "c")]
    [InlineData("{% liquid echo-1\n comment-x\n a\n endcomment %}", "-1")]
    [InlineData("{% liquid for i in (1..3) foo limit:2\n echo i\n endfor %}", "12")]
    [InlineData("{% liquid for i in (1..3) reversedx\n echo i\n endfor %}", "321")]
    public void ShouldUseWordBoundariesInLiquidTag(string source, string expected)
    {
        AssertRenders(_liquidTagParser, source, expected);
    }

    [Theory]
    [InlineData("{% render 'args' format: 1 %}", "1||||")]
    [InlineData("{% render 'args' with_tax: 2 %}", "|2|||")]
    [InlineData("{% render 'args' assets: 3 %}", "||3||")]
    [InlineData("{% render 'args' with 'a' as item as_json: 4 %}", "|||4|a")]
    [InlineData("{% include 'args' format: 1 %}", "1||||")]
    [InlineData("{% include 'args' with_tax: 2 %}", "|2|||")]
    [InlineData("{% include 'args' with 'a' as item assets: 3 %}", "||3||a")]
    [InlineData("{% include 'args' for 'a' as item as_json: 4 %}", "|||4|a")]
    public async Task ShouldReadKeywordArgumentsStartingWithAWord(string source, string expected)
    {
        var fileProvider = new MockFileProvider();
        fileProvider.Add("args.liquid", "{{ format }}|{{ with_tax }}|{{ assets }}|{{ as_json }}|{{ item }}");
        var options = new TemplateOptionsBuilder().WithFileProvider(fileProvider).Build();

        Assert.True(_parser.TryParse(source, out var template, out var errors), errors);
        Assert.Equal(expected, await template.RenderAsync(new TemplateContext(options)));
    }

    // Like in Shopify, a tag name is made of word chars, and a '-' starts the markup of the tag.
    [Theory]
    [InlineData("{% comment-x %}a{% endcomment %}b", "b")]
    [InlineData("{% raw-x %}a{% endraw %}b", "ab")]
    [InlineData("{% echo-1 %}", "-1")]
    [InlineData("{% echo'a' %}", "a")]
    [InlineData("{% if-1 %}yes{% endif %}", "yes")]
    [InlineData("{% if true %}a{% endif-%} b", "ab")]
    public void ShouldReadTagNamesAsWords(string source, string expected)
    {
        AssertRenders(_parser, source, expected);
        AssertRenders(_liquidTagParser, source, expected);
    }

    [Fact]
    public void ShouldNotReadCustomTagNamesWithDashes()
    {
        var parser = new FluidParser();
        parser.RegisterEmptyTag("my-tag", static (output, encoder, context) => Statement.NormalCompletion);

        Assert.False(parser.TryParse("{% my-tag %}", out _, out var error));
        Assert.Contains("Unknown tag 'my'", error);
    }

    [Fact]
    public void ShouldIgnoreCustomEndTagArguments()
    {
        var parser = new FluidParser();
        parser.RegisterEmptyBlock("hello", static (statements, output, encoder, context) => statements.RenderStatementsAsync(output, encoder, context));

        Assert.True(parser.TryParse("{% hello %}a{% endhello x %}", out var template, out var errors), errors);
        Assert.Equal("a", template.Render());
        Assert.False(parser.TryParse("{% hello %}a{% endhellox %}", out _, out _));
    }

    private static void AssertRenders(FluidParser parser, string source, string expected)
    {
        Assert.True(parser.TryParse(source, out var template, out var errors), errors);
        Assert.Equal(expected, template.Render());
    }
}
