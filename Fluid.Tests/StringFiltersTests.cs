using System.Linq;
using Fluid.Values;
using Fluid.Filters;
using Xunit;

namespace Fluid.Tests
{
    public class StringFiltersTests
    {
        [Fact]
        public void Append()
        {
            var input = new StringValue("Hello");

            var arguments = new FilterArguments().Add(new StringValue(" World"));
            var context = new TemplateContext();

            var result = StringFilters.Append(input, arguments, context);

            Assert.Equal("Hello World", result.Result.ToStringValue());
        }

        [Fact]
        public void Capitalize()
        {
            var input = new StringValue("hello world");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = StringFilters.Capitalize(input, arguments, context);

            Assert.Equal("Hello World", result.Result.ToStringValue());
        }

        [Fact]
        public void Downcase()
        {
            var input = new StringValue("Hello World");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = StringFilters.Downcase(input, arguments, context);

            Assert.Equal("hello world", result.Result.ToStringValue());
        }

        [Fact]
        public void LStrip()
        {
            var input = new StringValue("   Hello World   ");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = StringFilters.LStrip(input, arguments, context);

            Assert.Equal("Hello World   ", result.Result.ToStringValue());
        }

        [Fact]
        public void RStrip()
        {
            var input = new StringValue("   Hello World   ");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = StringFilters.RStrip(input, arguments, context);

            Assert.Equal("   Hello World", result.Result.ToStringValue());
        }

        [Fact]
        public void Strip()
        {
            var input = new StringValue("   Hello World   ");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = StringFilters.Strip(input, arguments, context);

            Assert.Equal("Hello World", result.Result.ToStringValue());
        }

        [Fact]
        public void StripNewLines()
        {
            var input = new StringValue(@"
Hello
world
");
            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = StringFilters.StripNewLines(input, arguments, context);

            Assert.Equal("Helloworld", result.Result.ToStringValue());
        }

        [Fact]
        public void NewLineToBr()
        {
            var input = new StringValue("Hello\nWorld");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = StringFilters.NewLineToBr(input, arguments, context);

            Assert.Equal("Hello<br />World", result.Result.ToStringValue());
        }

        [Fact]
        public void Prepend()
        {
            var input = new StringValue("World");

            var arguments = new FilterArguments().Add(new StringValue("Hello "));
            var context = new TemplateContext();

            var result = StringFilters.Prepend(input, arguments, context);

            Assert.Equal("Hello World", result.Result.ToStringValue());
        }

        [Fact]
        public void RemoveFirst()
        {
            var input = new StringValue("abcabc");

            var arguments = new FilterArguments().Add(new StringValue("b"));
            var context = new TemplateContext();

            var result = StringFilters.RemoveFirst(input, arguments, context);

            Assert.Equal("acabc", result.Result.ToStringValue());
        }

        [Fact]
        public void Remove()
        {
            var input = new StringValue("abcabc");

            var arguments = new FilterArguments().Add(new StringValue("b"));
            var context = new TemplateContext();

            var result = StringFilters.Remove(input, arguments, context);

            Assert.Equal("acac", result.Result.ToStringValue());
        }

        [Fact]
        public void RemovesReturnsInputWhenArgumentIsEmpty()
        {
            var input = new StringValue("abcabc");

            var arguments = FilterArguments.Empty;
            var context = new TemplateContext();

            var result = StringFilters.Remove(input, arguments, context);
        }

        [Fact]
        public void ReplaceFirst()
        {
            var input = new StringValue("abcabc");

            var arguments = new FilterArguments().Add(new StringValue("b")).Add(new StringValue("B"));
            var context = new TemplateContext();

            var result = StringFilters.ReplaceFirst(input, arguments, context);

            Assert.Equal("aBcabc", result.Result.ToStringValue());
        }

        [Fact]
        public void Replace()
        {
            var input = new StringValue("abcabc");

            var arguments = new FilterArguments().Add(new StringValue("b")).Add(new StringValue("B"));
            var context = new TemplateContext();

            var result = StringFilters.Replace(input, arguments, context);

            Assert.Equal("aBcaBc", result.Result.ToStringValue());
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
            var filterArguments = new FilterArguments(arguments.Select(x => FluidValue.Create(x, TemplateOptions.Default)).ToArray());
            var context = new TemplateContext();

            var result = StringFilters.Slice(filterInput, filterArguments, context);

            Assert.Equal(expected, result.Result.ToStringValue());
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
        public void SliceOutsideBounds(object input, object[] arguments, string expected)
        {
            var filterInput = FluidValue.Create(input, TemplateOptions.Default);
            var filterArguments = new FilterArguments(arguments.Select(x => FluidValue.Create(x, TemplateOptions.Default)).ToArray());
            var context = new TemplateContext();

            var result = StringFilters.Slice(filterInput, filterArguments, context);

            Assert.Equal(expected, result.Result.ToStringValue());
        }

        [Fact]
        public void Split()
        {
            var input = new StringValue("a.b.c");

            var arguments = new FilterArguments().Add(new StringValue("."));
            var context = new TemplateContext();

            var result = StringFilters.Split(input, arguments, context);

            Assert.Equal(3, result.Result.Enumerate(context).Count());
            Assert.Equal(new StringValue("a"), result.Result.Enumerate(context).ElementAt(0));
            Assert.Equal(new StringValue("b"), result.Result.Enumerate(context).ElementAt(1));
            Assert.Equal(new StringValue("c"), result.Result.Enumerate(context).ElementAt(2));
        }

        [Fact]
        public void SplitWithEmptyString()
        {
            var input = new StringValue("abc");

            var arguments = new FilterArguments().Add(StringValue.Empty);
            var context = new TemplateContext();

            var result = StringFilters.Split(input, arguments, context);

            Assert.Equal(3, result.Result.Enumerate(context).Count());
            Assert.Equal(new StringValue("a"), result.Result.Enumerate(context).ElementAt(0));
            Assert.Equal(new StringValue("b"), result.Result.Enumerate(context).ElementAt(1));
            Assert.Equal(new StringValue("c"), result.Result.Enumerate(context).ElementAt(2));
        }

        [Theory]
        [InlineData("The cat came back the very next day", 13, "The cat ca...")]
        [InlineData("Hello", 3, "...")]
        [InlineData("Hello", 10, "Hello")]
        [InlineData("Hello", 0, "...")]
        [InlineData(null, 5, "")]
        public void Truncate(string input, int size, string output)
        {
            var source = new StringValue(input);
            var arguments = new FilterArguments().Add(NumberValue.Create(size));
            var context = new TemplateContext();
            var result = StringFilters.Truncate(source, arguments, context);

            Assert.Equal(output, result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ", 18, ", and so on", "ABCDEFG, and so on")]
        [InlineData("I'm a little teapot, short and stout.", 15, "", "I'm a little te")]
        [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ", 5, ", and so on", ", and so on")]
        [InlineData("ABCD EFGH IJKLM NOPQRS TUVWXYZ", 3, "", "ABC")]
        [InlineData("ABCD EFGH IJKLM NOPQRS TUVWXYZ", 0, "", "")]
        public void TruncateWithCustomEllipsis(string input, int size, string ellipsis, string output)
        {
            var source = new StringValue(input);
            var arguments = new FilterArguments()
                .Add(NumberValue.Create(size))
                .Add(new StringValue(ellipsis));
            var context = new TemplateContext();
            var result = StringFilters.Truncate(source, arguments, context);

            Assert.Equal(output, result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("one two three", 4, "one two three")]
        [InlineData("one two three", 2, "one two...")]
        [InlineData("one two three", null, "one two three")]
        [InlineData("Two small (13&#8221; x 5.5&#8221; x 10&#8221; high) baskets fit inside one large basket (13&#8221; x 16&#8221; x 10.5&#8221; high) with cover.", 15, "Two small (13&#8221; x 5.5&#8221; x 10&#8221; high) baskets fit inside one large basket (13&#8221;...")]
        [InlineData("测试测试测试测试", 5, "测试测试测试测试")]
        [InlineData("one  two\tthree\nfour", 3, "one two three...")]
        [InlineData("one two three four", 2, "one two...")]
        [InlineData("one two three four", 0, "one...")]
        public void TruncateWords(string input, object size, string output)
        {
            var options = new TemplateOptions();
            var source = new StringValue(input);
            var arguments = new FilterArguments()
                .Add(FluidValue.Create(size, options));
            var context = new TemplateContext();

            var result = StringFilters.TruncateWords(source, arguments, context);

            Assert.Equal(output, result.Result.ToStringValue());
        }

        [Theory]
        [InlineData("The cat came back the very next day", 4, "--", "The cat came back--")]
        [InlineData("The cat came back the very next day", 4, "", "The cat came back")]
        [InlineData("The cat came back the very next day", 0, "", "The")]
        public void TruncateWordsWithCustomEllipsis(string input, int size, string ellispsis, string output)
        {
            var source = new StringValue(input);
            var arguments = new FilterArguments()
                .Add(NumberValue.Create(size))
                .Add(new StringValue(ellispsis));

            var context = new TemplateContext();

            var result = StringFilters.TruncateWords(source, arguments, context);

            Assert.Equal(output, result.Result.ToStringValue());
        }

        [Fact]
        public void Upcase()
        {
            var input = new StringValue("Hello World");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = StringFilters.Upcase(input, arguments, context);

            Assert.Equal("HELLO WORLD", result.Result.ToStringValue());
        }
    }
}
