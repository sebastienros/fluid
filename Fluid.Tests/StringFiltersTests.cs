using System.Linq;
using System.Threading.Tasks;
using Fluid.Values;
using Fluid.Filters;
using Xunit;
using Fluid.Tests.Extensions;

namespace Fluid.Tests;

public class StringFiltersTests
{
    [Fact]
    public void Append()
    {
        var input = StringValue.Create("Hello");

        var arguments = new FilterArguments().Add(StringValue.Create(" World"));
        var context = new TemplateContext();

        var result = StringFilters.Append(input, arguments, context);

        Assert.Equal("Hello World", result.Result.ToStringValue(context));
    }

    [Fact]
    public void Capitalize()
    {
        var input = StringValue.Create("hello world");

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.Capitalize(input, arguments, context);

        Assert.Equal("Hello world", result.Result.ToStringValue(context));
    }

    [Fact]
    public void Downcase()
    {
        var input = StringValue.Create("Hello World");

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.Downcase(input, arguments, context);

        Assert.Equal("hello world", result.Result.ToStringValue(context));
    }

    [Fact]
    public void LStrip()
    {
        var input = StringValue.Create("   Hello World   ");

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.LStrip(input, arguments, context);

        Assert.Equal("Hello World   ", result.Result.ToStringValue(context));
    }

    [Fact]
    public void RStrip()
    {
        var input = StringValue.Create("   Hello World   ");

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.RStrip(input, arguments, context);

        Assert.Equal("   Hello World", result.Result.ToStringValue(context));
    }

    [Fact]
    public void Strip()
    {
        var input = StringValue.Create("   Hello World   ");

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.Strip(input, arguments, context);

        Assert.Equal("Hello World", result.Result.ToStringValue(context));
    }

    [Theory]
    [InlineData("   Hello World   ", "Hello World")]
    [InlineData(" Hello  \n\t\r\n World!  ", "Hello World!")]
    [InlineData("HelloWorld!", "HelloWorld!")]
    [InlineData(" \n\t\r", "")]
    public void Squish(string value, string expected)
    {
        var input = StringValue.Create(value);

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.Squish(input, arguments, context);

        Assert.Equal(expected, result.Result.ToStringValue(context));
    }

    [Fact]
    public void SquishNilReturnsNil()
    {
        var result = StringFilters.Squish(NilValue.Instance, new FilterArguments(), new TemplateContext());

        Assert.IsType<NilValue>(result.Result);
    }

    [Fact]
    public void StripNewLines()
    {
        var input = StringValue.Create(@"
Hello
world
");
        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.StripNewLines(input, arguments, context);

        Assert.Equal("Helloworld", result.Result.ToStringValue(context));
    }

    [Fact]
    public void NewLineToBr()
    {
        var input = StringValue.Create("Hello\nWorld");

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.NewLineToBr(input, arguments, context);

        Assert.Equal("Hello<br />\nWorld", result.Result.ToStringValue(context));
    }

    [Fact]
    public void Prepend()
    {
        var input = StringValue.Create("World");

        var arguments = new FilterArguments().Add(StringValue.Create("Hello "));
        var context = new TemplateContext();

        var result = StringFilters.Prepend(input, arguments, context);

        Assert.Equal("Hello World", result.Result.ToStringValue(context));
    }

    [Theory]
    [InlineData("a b a a", new object[] { "a " }, "b a a")]
    [InlineData("1 1 1 1", new object[] { 1 }, " 1 1 1")]
    public void RemoveFirst(string input, object[] arguments, string expected)
    {
        var filterInput = StringValue.Create(input);
        var filterArguments = arguments.ToFilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.RemoveFirst(filterInput, filterArguments, context);

        Assert.Equal(expected, result.Result.ToStringValue(context));
    }

    [Theory]
    [InlineData("a a a a", new object[] { "a" }, "   ")]
    [InlineData("1 1 1 1", new object[] { 1 }, "   ")]
    public void Remove(string input, object[] arguments, string expected)
    {
        var filterInput = StringValue.Create(input);
        var filterArguments = arguments.ToFilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.Remove(filterInput, filterArguments, context);

        Assert.Equal(expected, result.Result.ToStringValue(context));
    }

    [Theory]
    [InlineData("a a b a", new object[] { " a" }, "a a b")]
    [InlineData("1 1 1 1", new object[] { 1 }, "1 1 1 ")]
    public void RemoveLast(string input, object[] arguments, string expected)
    {
        var filterInput = StringValue.Create(input);
        var filterArguments = arguments.ToFilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.RemoveLast(filterInput, filterArguments, context);

        Assert.Equal(expected, result.Result.ToStringValue(context));
    }

    [Theory]
    [InlineData("a a a a", new object[] { "a", "b" }, "b a a a")]
    [InlineData("1 1 1 1", new object[] { 1, 2 }, "2 1 1 1")]
    [InlineData("1 1 1 1", new object[] { 2, 3 }, "1 1 1 1")]
    [InlineData("1 1 1 1", new object[] { "1", 2 }, "2 1 1 1")]
    [InlineData("aa bb cc aa bb cc", new object[] { "cc", "dd" }, "aa bb dd aa bb cc")]
    public void ReplaceFirst(string input, object[] arguments, string expected)
    {
        var filterInput = StringValue.Create(input);
        var filterArguments = arguments.ToFilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.ReplaceFirst(filterInput, filterArguments, context);

        Assert.Equal(expected, result.Result.ToStringValue(context));
    }

    [Theory]
    [InlineData("a a a a", new object[] { "a", "b" }, "b b b b")]
    [InlineData("1 1 1 1", new object[] { 1, 2 }, "2 2 2 2")]
    [InlineData("1 1 1 1", new object[] { 2, 3 }, "1 1 1 1")]
    [InlineData("1 1 1 1", new object[] { "1", 2 }, "2 2 2 2")]
    [InlineData("aa bb cc aa bb cc", new object[] { "cc", "dd" }, "aa bb dd aa bb dd")]
    public void Replace(string input, object[] arguments, string expected)
    {
        var filterInput = StringValue.Create(input);
        var filterArguments = arguments.ToFilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.Replace(filterInput, filterArguments, context);

        Assert.Equal(expected, result.Result.ToStringValue(context));
    }

    [Theory]
    [InlineData("a a a a", new object[] { "a", "b" }, "a a a b")]
    [InlineData("1 1 1 1", new object[] { 1, 2 }, "1 1 1 2")]
    [InlineData("1 1 1 1", new object[] { 2, 3 }, "1 1 1 1")]
    [InlineData("1 1 1 1", new object[] { "1", 2 }, "1 1 1 2")]
    [InlineData("aa bb cc aa bb cc", new object[] { "cc", "dd" }, "aa bb cc aa bb dd")]
    public void ReplaceLast(string input, object[] arguments, string expected)
    {
        var filterInput = StringValue.Create(input);
        var filterArguments = arguments.ToFilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.ReplaceLast(filterInput, filterArguments, context);

        Assert.Equal(expected, result.Result.ToStringValue(context));
    }

    [Theory]
    [InlineData("hello", new object[] { 0 }, "h")]
    [InlineData("hello", new object[] { 1 }, "e")]
    [InlineData("hello", new object[] { 1, 3 }, "ell")]
    [InlineData("hello", new object[] { -3, 3 }, "llo")]
    [InlineData("hello", new object[] { -3 }, "l")]
    [InlineData("abcdefg", new object[] { -3, 2 }, "ef")]
    public void Slice(object input, object[] arguments, string expected)
    {
        var filterInput = FluidValue.Create(input, TemplateOptions.Default);
        var filterArguments = arguments.ToFilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.Slice(filterInput, filterArguments, context);

        Assert.Equal(expected, result.Result.ToStringValue(context));
    }

    [Theory]
    [InlineData("hello", new object[] { 0, 100 }, "hello")]
    [InlineData("hello", new object[] { 2, 100 }, "llo")]
    [InlineData("hello", new object[] { 100, 100 }, "")]
    [InlineData("hello", new object[] { -3, 100 }, "llo")]
    [InlineData("hello", new object[] { -5, 200 }, "hello")]
    [InlineData("hello", new object[] { -100, 100 }, "")]
    [InlineData("hello", new object[] { -100, 200 }, "")]
    [InlineData("hello", new object[] { -5, 100 }, "hello")]
    [InlineData("hello", new object[] { 0, -100 }, "")]
    [InlineData("hello", new object[] { -100, -100 }, "")]
    [InlineData("hello", new object[] { 4, 2 }, "o")]
    [InlineData("hello", new object[] { 5, 1 }, "")]
    public void SliceOutsideBounds(object input, object[] arguments, string expected)
    {
        var filterInput = FluidValue.Create(input, TemplateOptions.Default);
        var filterArguments = arguments.ToFilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.Slice(filterInput, filterArguments, context);

        Assert.Equal(expected, result.Result.ToStringValue(context));
    }

    [Fact]
    public async Task SliceArrayClampsLengthAtTheEnd()
    {
        var input = FluidValue.Create(new[] { "a", "b", "c", "d", "e" }, TemplateOptions.Default);
        var arguments = new FilterArguments(NumberValue.Create(3), NumberValue.Create(4));
        var context = new TemplateContext();

        var result = await StringFilters.Slice(input, arguments, context);
        var values = await result.EnumerateAsync(context).ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["d", "e"], values.Select(x => x.ToStringValue(context)));
    }

    [Fact]
    public async Task SliceWithMinimumOffsetReturnsEmpty()
    {
        var arguments = new FilterArguments(NumberValue.Create(int.MinValue));

        var result = await StringFilters.Slice(StringValue.Create("abc"), arguments, new TemplateContext());

        Assert.Same(BlankValue.Instance, result);
    }

    [Fact]
    public async Task Split()
    {
        var input = StringValue.Create("a.b.c");

        var arguments = new FilterArguments().Add(StringValue.Create("."));
        var context = new TemplateContext();

        var result = await StringFilters.Split(input, arguments, context);

        var enumerated = await result.EnumerateAsync(context).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, enumerated.Count());
        Assert.Equal(StringValue.Create("a"), enumerated.ElementAt(0));
        Assert.Equal(StringValue.Create("b"), enumerated.ElementAt(1));
        Assert.Equal(StringValue.Create("c"), enumerated.ElementAt(2));
    }

    [Fact]
    public async Task SplitWithEmptyString()
    {
        var input = StringValue.Create("abc");

        var arguments = new FilterArguments().Add(StringValue.Empty);
        var context = new TemplateContext();

        var result = await StringFilters.Split(input, arguments, context);

        var enumerated = await result.EnumerateAsync(context).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, enumerated.Count());
        Assert.Equal(StringValue.Create("a"), enumerated.ElementAt(0));
        Assert.Equal(StringValue.Create("b"), enumerated.ElementAt(1));
        Assert.Equal(StringValue.Create("c"), enumerated.ElementAt(2));
    }

    [Theory]
    [InlineData("The cat came back the very next day", 13, "The cat ca...")]
    [InlineData("Hello", 3, "...")]
    [InlineData("Hello", 10, "Hello")]
    [InlineData("Hello", 0, "...")]
    [InlineData(null, 5, "")]
    public void Truncate(string input, int size, string output)
    {
        var source = StringValue.Create(input);
        var arguments = new FilterArguments().Add(NumberValue.Create(size));
        var context = new TemplateContext();
        var result = StringFilters.Truncate(source, arguments, context);

        Assert.Equal(output, result.Result.ToStringValue(context));
    }

    [Theory]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ", 18, ", and so on", "ABCDEFG, and so on")]
    [InlineData("I'm a little teapot, short and stout.", 15, "", "I'm a little te")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ", 5, ", and so on", ", and so on")]
    [InlineData("ABCD EFGH IJKLM NOPQRS TUVWXYZ", 3, "", "ABC")]
    [InlineData("ABCD EFGH IJKLM NOPQRS TUVWXYZ", 0, "", "")]
    public void TruncateWithCustomEllipsis(string input, int size, string ellipsis, string output)
    {
        var source = StringValue.Create(input);
        var arguments = new FilterArguments()
            .Add(NumberValue.Create(size))
            .Add(StringValue.Create(ellipsis));
        var context = new TemplateContext();
        var result = StringFilters.Truncate(source, arguments, context);

        Assert.Equal(output, result.Result.ToStringValue(context));
    }

    [Theory]
    [InlineData("one two three", 4, "one two three")]
    [InlineData("one two three", 2, "one two...")]
    [InlineData("one two three", 15, "one two three")]
    [InlineData("Two small (13&#8221; x 5.5&#8221; x 10&#8221; high) baskets fit inside one large basket (13&#8221; x 16&#8221; x 10.5&#8221; high) with cover.", 15, "Two small (13&#8221; x 5.5&#8221; x 10&#8221; high) baskets fit inside one large basket (13&#8221;...")]
    [InlineData("测试测试测试测试", 5, "测试测试测试测试")]
    [InlineData("one  two\tthree\nfour", 3, "one two three...")]
    [InlineData("one two three four", 2, "one two...")]
    [InlineData("  one two three four", 2, "one two...")]
    [InlineData("one two three four", 0, "one...")]
    public void TruncateWords(string input, object size, string output)
    {
        var options = new TemplateOptionsBuilder().Build();
        var source = StringValue.Create(input);
        var arguments = new FilterArguments()
            .Add(FluidValue.Create(size, options));
        var context = new TemplateContext();

        var result = StringFilters.TruncateWords(source, arguments, context);

        Assert.Equal(output, result.Result.ToStringValue(context));
    }

    [Theory]
    [InlineData("The cat came back the very next day", 4, "--", "The cat came back--")]
    [InlineData("The cat came back the very next day", 4, "", "The cat came back")]
    [InlineData("The cat came back the very next day", 0, "", "The")]
    public void TruncateWordsWithCustomEllipsis(string input, int size, string ellispsis, string output)
    {
        var source = StringValue.Create(input);
        var arguments = new FilterArguments()
            .Add(NumberValue.Create(size))
            .Add(StringValue.Create(ellispsis));

        var context = new TemplateContext();

        var result = StringFilters.TruncateWords(source, arguments, context);

        Assert.Equal(output, result.Result.ToStringValue(context));
    }

    [Fact]
    public void Upcase()
    {
        var input = StringValue.Create("Hello World");

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = StringFilters.Upcase(input, arguments, context);

        Assert.Equal("HELLO WORLD", result.Result.ToStringValue(context));
    }

    [Theory]
    // Like Liquid, string filters return a string even when they don't change their input.
    [InlineData("{{ 1 | truncate | first }}|{{ 12 | truncate: 5 | size }}|{{ 5 | remove_last: 'x' | size }}|{{ 5 | truncatewords: 3 | size }}", "1|2|1|1")]
    [InlineData("{{ 5 | remove_first: 'x' | size }}|{{ 5 | replace_first: 'x', 'y' | size }}|{{ 5 | replace_last: 'x', 'y' | size }}", "1|1|1")]
    [InlineData("{% assign x = nil | remove_last: 1 %}{% if x %}y{% else %}n{% endif %}", "y")]
    // Like Liquid, truncate and truncatewords return nil for nil, before checking their arguments.
    [InlineData("{% assign x = nil | truncate: 3 %}{% if x %}y{% else %}n{% endif %}", "n")]
    [InlineData("[{{ nil | truncatewords: nil }}|{{ nil | truncate: nil }}]", "[|]")]
    // Like Ruby's gsub, an empty input has a single insertion point.
    [InlineData("{{ '' | replace: '', 'x' }}|{{ nil | replace: '', 'k' }}|{{ 'abc' | replace: '', 'x' }}", "x|k|xaxbxcx")]
    public async Task StringFiltersReturnStrings(string source, string expected)
    {
        Assert.Equal(expected, await new FluidParser().Parse(source).RenderAsync(new TemplateContext()));
    }

    [Theory]
    // Like Liquid, strip_html removes script, comment and style blocks, then complete tags only.
    [InlineData("{{ '>' | strip_html }}|{{ 'a > b' | strip_html }}|{{ 'a < b' | strip_html }}|{{ 'a < b > c' | strip_html }}", ">|a > b|a < b|a  c")]
    [InlineData("{{ '<script>x</script>y<SCRIPT>z</SCRIPT><style a>s</style><!-- c -->d<!-- e' | strip_html }}", "yzd<!-- e")]
    [InlineData("{{ '<scriptx>q</script>w<p\nclass=a>t</p>' | strip_html }}", "wt")]
    // Like Liquid (Ruby's CGI.escape), url_encode only keeps letters, digits and _ . - ~.
    [InlineData("{{ \"a(b)!*'~ c\" | url_encode }}|{{ 'a+b/c?d=é&e=✓' | url_encode }}", "a%28b%29%21%2A%27~+c|a%2Bb%2Fc%3Fd%3D%C3%A9%26e%3D%E2%9C%93")]
    // Like Liquid, a lone \r isn't a new line.
    [InlineData("{{ s | newline_to_br }}|{{ s | strip_newlines }}", "a\rb<br />\nc<br />\n|a\rbc")]
    // Like Liquid, only ASCII whitespace (and NUL for strip) is whitespace.
    [InlineData("[{{ u | strip | size }}|{{ u | lstrip | size }}|{{ u | rstrip | size }}|{{ v | split: ' ' | size }}|{{ v | truncatewords: 1 }}]", "[3|4|4|2|a\u00a0b...]")]
    [InlineData("[{{ w | strip }}|{{ w | lstrip }}|{{ w | rstrip }}]", "[a|a \0| \0a]")]
    public async Task StringFiltersUseLiquidCharacterClasses(string source, string expected)
    {
        var context = new TemplateContext();
        context.SetValue("s", "a\rb\r\nc\n");
        context.SetValue("u", " \u00a0a\u3000 ");
        context.SetValue("v", "a\u00a0b c");
        context.SetValue("w", " \0a \0");

        Assert.Equal(expected, await new FluidParser().Parse(source).RenderAsync(context));
    }
    [Theory]
    // Like Liquid, strings are measured, sliced and split in code points: an emoji counts as one character.
    [InlineData("{{ '😀x' | size }}|{{ '😀x' | slice: 1 }}|{{ 'a😀b' | truncate: 2, '' }}|{{ '😀x' | split: '' | size }}|{% assign s = '😀x' %}{{ s.size }}", "2|x|a😀|2|2")]
    [InlineData("{{ 'a😀b😀c' | slice: -2 }}|{{ 'a😀b😀c' | slice: 1, 3 }}|{{ 'a😀b😀c' | truncate: 4 }}|{{ 'a😀b😀c' | truncate: 3, '😀' }}|{{ 'a😀b' | split: '' | join: '-' }}", "😀|😀b😀|a...|a😀😀|a-😀-b")]
    [InlineData("{% assign s = '😀x😀' %}{{ s.first }}|{{ s.last }}|{{ s | first }}|{{ s | last }}|{{ 'a😀b' | reverse | join: '' }}", "😀|😀|😀|😀|b😀a")]
    // Like Ruby, upcase, downcase and capitalize use the full Unicode mappings, and capitalize the titlecase.
    [InlineData("{{ 'ß' | upcase }}|{{ 'ı' | upcase }}|{{ 'İ' | downcase }}|{{ 'ß' | capitalize }}|{{ 'ǆemal' | capitalize }}|{{ 'ﬁne' | upcase }}|{{ 'ᾳ' | upcase }}|{{ 'ŉ' | upcase }}", "SS|I|i̇|Ss|ǅemal|FINE|ΑΙ|ʼN")]
    [InlineData("{{ 'Straße ÉCOLE' | downcase }}|{{ 'Straße école' | upcase }}|{{ 'éCOLE' | capitalize }}|{{ 'ა' | capitalize }}|{{ '𐐨' | upcase }}|{{ 'HELLO world' | capitalize }}", "straße école|STRASSE ÉCOLE|École|ა|𐐀|Hello world")]
    public async Task StringFiltersUseCodePointsAndFullCaseMappings(string source, string expected)
    {
        Assert.Equal(expected, await new FluidParser().Parse(source).RenderAsync(new TemplateContext()));
    }

    // Like Liquid (strip.gsub(/\s+/, ' ')), squish replaces any whitespace run, even a single new line, and trims NUL.
    [InlineData("[{{ s | squish }}]|[{{ t | squish }}]|[{{ 'a b' | squish }}]|[{{ u | squish | size }}]", "[a b]|[a b c]|[a b]|[1]")]
    // Like Liquid, escape, url_encode and url_decode return nil for nil.
    [InlineData("{% assign x = nil | escape %}{% if x == nil %}N{% endif %}{% assign x = nil | url_encode %}{% if x == nil %}N{% endif %}{% assign x = nil | url_decode %}{% if x == nil %}N{% endif %}{% assign x = nil | escape_once %}{% if x == nil %}N{% else %}S{% endif %}|{{ nil | escape | truncate: -1 }}", "NNNS|")]
    // Like Liquid (input.split(" ", words + 1)), anything after the last kept word truncates the input.
    [InlineData("{{ 'a b ' | truncatewords: 2 }}|{{ w | truncatewords: 2, '' }}|{{ 'a ' | truncatewords: 1 }}|{{ 'a b' | truncatewords: 2 }}|{{ ' ' | truncatewords: 1 }}|{{ 'a b ' | truncatewords: 3 }}", "a b...|a b|a...|a b| |a b ")]
    // Like Ruby's gsub and sub, which Liquid uses, the replacement of replace and replace_first reads backslash sequences.
    [InlineData(@"{{ 'a.b' | replace: '.', '\\' }}|{{ 'abc' | replace: 'b', '[\0]' }}|{{ 'abc' | replace_first: 'b', '\&\&' }}|{{ 'abc' | replace_last: 'b', '<\0>' }}", @"a\b|a[b]c|abbc|a<\0>c")]
    [InlineData(@"{{ 'abcb' | replace: 'b', ""<\`|\'>"" }}|{{ 'abc' | replace: 'b', '\1\9\k\x\' }}|{{ 'ab' | replace: '', '<\0\`>' }}|{{ '' | replace: '', '\&x' }}", @"a<a|cb>c<abc|>|a\k\x\c|<>a<a>b<ab>|x")]
    // Like Liquid, base64_encode gets back the bytes decoded by base64_decode, even when they aren't UTF-8.
    [InlineData("{{ '/w==' | base64_decode | base64_encode }}|{% assign b = 'true' | base64_decode %}{{ b | base64_encode }}|{{ '/w' | base64_url_safe_decode | base64_url_safe_encode }}|{{ 'aGk=' | base64_decode }}", "/w==|true|_w|hi")]
    public async Task StringFiltersMatchLiquid(string source, string expected)
    {
        var context = new TemplateContext();
        context.SetValue("s", "a\nb");
        context.SetValue("w", "a  b\n");
        context.SetValue("u", "\0a\0");
        context.SetValue("t", "a b\nc");

        Assert.Equal(expected, await new FluidParser().Parse(source).RenderAsync(context));
    }
}
