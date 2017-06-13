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

        [Fact]
        public void Slice()
        {
            var input = new StringValue("hello");

            var arguments = new FilterArguments().Add(new NumberValue(-3)).Add(new NumberValue(3));
            var context = new TemplateContext();

            var result = StringFilters.Slice(input, arguments, context);

            Assert.Equal("llo", result.ToStringValue());
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

        [Fact]
        public void Truncate()
        {
            var input = new StringValue("Hello World");

            var arguments = new FilterArguments().Add(new NumberValue(5)).Add(new StringValue("..."));
            var context = new TemplateContext();

            var result = StringFilters.Truncate(input, arguments, context);

            Assert.Equal("Hello...", result.ToStringValue());
        }

        [Fact]
        public void TruncateWords()
        {
            var input = new StringValue("This is a nice story with a bad end.");

            var arguments = new FilterArguments().Add(new NumberValue(5)).Add(new StringValue("..."));
            var context = new TemplateContext();

            var result = StringFilters.TruncateWords(input, arguments, context);

            Assert.Equal("This is a nice story...", result.ToStringValue());
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
