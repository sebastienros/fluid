using System.Linq;
using Fluid.Filters;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class MiscFiltersTests
    {
        [Fact]
        public void DefaultReturnsValueIfDefined()
        {
            var input = new StringValue("foo");

            var arguments = new FilterArguments().Add(new StringValue("bar"));
            var context = new TemplateContext();

            var result = MiscFilters.Default(input, arguments, context);

            Assert.Equal("foo", result.ToStringValue());
        }

        [Fact]
        public void DefaultReturnsDefaultIfNotDefined()
        {
            var input = EmptyValue.Instance;

            var arguments = new FilterArguments().Add(new StringValue("bar"));
            var context = new TemplateContext();

            var result = MiscFilters.Default(input, arguments, context);

            Assert.Equal("bar", result.ToStringValue());
        }

        [Fact]
        public void CompactRemovesNilValues()
        {
            var input = new ArrayValue(new FluidValue[] { 
                new StringValue("a"), 
                new NumberValue(0),
                NilValue.Instance,
                new StringValue("b")
                });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = MiscFilters.Compact(input, arguments, context);

            Assert.Equal(3, result.Enumerate().Count());
        }

        
        [Fact]
        public void EncodeUrl()
        {
            var input = new StringValue("john@liquid.com");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = MiscFilters.UrlEncode(input, arguments, context);

            Assert.Equal("john%40liquid.com", result.ToStringValue());
        }
        
        [Fact]
        public void DecodeUrl()
        {
            var input = new StringValue("john%40liquid.com");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = MiscFilters.UrlDecode(input, arguments, context);

            Assert.Equal("john@liquid.com", result.ToStringValue());
        }
        
        [Fact]
        public void StripHtml()
        {
            var input = new StringValue("Have <em>you</em> read <strong>Ulysses</strong>?");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = MiscFilters.StripHtml(input, arguments, context);

            Assert.Equal("Have you read Ulysses?", result.ToStringValue());
        }
        
        
    }
}
