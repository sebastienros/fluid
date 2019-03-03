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

            Assert.Equal("Hello World", result.ToStringValue());
        }

        [Fact]
        public void Capitalize()
        {
            var input = new StringValue("hello world");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = StringFilters.Capitalize(input, arguments, context);

            Assert.Equal("Hello World", result.ToStringValue());
        }

        [Fact]
        public void Downcase()
        {
            var input = new StringValue("Hello World");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = StringFilters.Downcase(input, arguments, context);

            Assert.Equal("hello world", result.ToStringValue());
        }
        
        [Fact]
        public void LStrip()
        {
            var input = new StringValue("   Hello World   ");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = StringFilters.LStrip(input, arguments, context);

            Assert.Equal("Hello World   ", result.ToStringValue());
        }
        
        [Fact]
        public void RStrip()
        {
            var input = new StringValue("   Hello World   ");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = StringFilters.RStrip(input, arguments, context);

            Assert.Equal("   Hello World", result.ToStringValue());
        }
        
        [Fact]
        public void Strip()
        {
            var input = new StringValue("   Hello World   ");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = StringFilters.Strip(input, arguments, context);

            Assert.Equal("Hello World", result.ToStringValue());
        }
                
        [Fact]
        public void NewLineToBr()
        {
            var input = new StringValue("Hello\nWorld");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = StringFilters.NewLineToBr(input, arguments, context);

            Assert.Equal("Hello<br />World", result.ToStringValue());
        }

        [Fact]
        public void Prepend()
        {
            var input = new StringValue("World");

            var arguments = new FilterArguments().Add(new StringValue("Hello "));
            var context = new TemplateContext();

            var result = StringFilters.Prepend(input, arguments, context);

            Assert.Equal("Hello World", result.ToStringValue());
        }
                
        [Fact]
        public void RemoveFirst()
        {
            var input = new StringValue("abcabc");

            var arguments = new FilterArguments().Add(new StringValue("b"));
            var context = new TemplateContext();

            var result = StringFilters.RemoveFirst(input, arguments, context);

            Assert.Equal("acabc", result.ToStringValue());
        }
                
        [Fact]
        public void Remove()
        {
            var input = new StringValue("abcabc");

            var arguments = new FilterArguments().Add(new StringValue("b"));
            var context = new TemplateContext();

            var result = StringFilters.Remove(input, arguments, context);

            Assert.Equal("acac", result.ToStringValue());
        }        
                
        [Fact]
        public void ReplaceFirst()
        {
            var input = new StringValue("abcabc");

            var arguments = new FilterArguments().Add(new StringValue("b")).Add(new StringValue("B"));
            var context = new TemplateContext();

            var result = StringFilters.ReplaceFirst(input, arguments, context);

            Assert.Equal("aBcabc", result.ToStringValue());
        }

        [Fact]
        public void Replace()
        {
            var input = new StringValue("abcabc");

            var arguments = new FilterArguments().Add(new StringValue("b")).Add(new StringValue("B"));
            var context = new TemplateContext();

            var result = StringFilters.Replace(input, arguments, context);

            Assert.Equal("aBcaBc", result.ToStringValue());
        }

        [Theory]

        [InlineData("hello", new object[] { 0 }, "h")]
        [InlineData("hello", new object[] { 1 }, "e")]
        [InlineData("hello", new object[] { 1, 3 }, "ell")]
        [InlineData("hello", new object[] { -3, 3 }, "llo")]
        public void Slice(object input, object[] arguments, string expected)
        {
            var filterInput = FluidValue.Create(input);
            var filterArguments = new FilterArguments(arguments);
            var context = new TemplateContext();

            var result = StringFilters.Slice(filterInput, filterArguments, context);

            Assert.Equal(expected, result.ToStringValue());
        }

        [Fact]
        public void Split()
        {
            var input = new StringValue("a.b.c");

            var arguments = new FilterArguments().Add(new StringValue("."));
            var context = new TemplateContext();

            var result = StringFilters.Split(input, arguments, context);

            Assert.Equal(3, result.Enumerate().Count());
            Assert.Equal(new StringValue("a"), result.Enumerate().ElementAt(0));
            Assert.Equal(new StringValue("b"), result.Enumerate().ElementAt(1));
            Assert.Equal(new StringValue("c"), result.Enumerate().ElementAt(2));
        }

        [Theory]
        [InlineData("The cat came back the very next day", 13, "The cat ca...")]
        [InlineData("Hello", 3, "...")]
        [InlineData("Hello", 10, "Hello...")]
        [InlineData("Hello", 0, "...")]
        [InlineData(null, 5, "")]
        public void Truncate(string input, int size, string output)
        {
            var source = new StringValue(input);
            var arguments = new FilterArguments().Add(NumberValue.Create(size));
            var context = new TemplateContext();
            var result = StringFilters.Truncate(source, arguments, context);

            Assert.Equal(output, result.ToStringValue());
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

            Assert.Equal(output, result.ToStringValue());
        }

        [Theory]
        [InlineData("The cat came back the very next day", 4, "The cat came back...")]
        [InlineData("The cat came back the very next day", 1, "The...")]
        [InlineData("The cat came back the very next day", 0, "...")]
        [InlineData("The    cat came  back", 10, "The    cat came  back...")]
        public void TruncateWords(string input, int size, string output)
        {
            var source = new StringValue(input);
            var arguments = new FilterArguments()
                .Add(NumberValue.Create(size));
            var context = new TemplateContext();

            var result = StringFilters.TruncateWords(source, arguments, context);

            Assert.Equal(output, result.ToStringValue());
        }

        [Theory]
        [InlineData("The cat came back the very next day", 4, "--", "The cat came back--")]
        [InlineData("The cat came back the very next day", 4, "", "The cat came back")]
        [InlineData("The cat came back the very next day", 0, "", "")]
        public void TruncateWordsWithCustomEllipsis(string input, int size, string ellispsis, string output)
        {
            var source = new StringValue(input);
            var arguments = new FilterArguments()
                .Add(NumberValue.Create(size))
                .Add(new StringValue(ellispsis));

            var context = new TemplateContext();

            var result = StringFilters.TruncateWords(source, arguments, context);

            Assert.Equal(output, result.ToStringValue());
        }

        [Fact]
        public void Upcase()
        {
            var input = new StringValue("Hello World");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = StringFilters.Upcase(input, arguments, context);

            Assert.Equal("HELLO WORLD", result.ToStringValue());
        }
    }
}
