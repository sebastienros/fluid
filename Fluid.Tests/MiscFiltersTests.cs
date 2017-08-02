using System;
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

        [Fact]
        public void Escape()
        {
            var input = new StringValue("Have you read 'James & the Giant Peach'?");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = MiscFilters.Escape(input, arguments, context);

            Assert.Equal("Have you read &#39;James &amp; the Giant Peach&#39;?", result.ToStringValue());
        }

        [Fact]
        public void EscapeOnce()
        {
            var input = new StringValue("1 &lt; 2 &amp; 3");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = MiscFilters.EscapeOnce(input, arguments, context);

            Assert.Equal("1 &lt; 2 &amp; 3", result.ToStringValue());
        }

        [Theory]
        [InlineData("%a", "Tue")]
        [InlineData("%A", "Tuesday")]
        [InlineData("%b", "Aug")]
        [InlineData("%B", "August")]
        [InlineData("%c", "Tue Aug 01 17:04:36 2017")]
        [InlineData("%C", "20")]
        [InlineData("%d", "01")]
        [InlineData("%D", "08/01/17")]
        [InlineData("%e", " 1")]
        [InlineData("%F", "2017-08-01")]
        [InlineData("%H", "17")]
        [InlineData("%I", "05")]
        [InlineData("%j", "213")]
        [InlineData("%k", "17")]
        [InlineData("%l", " 5")]
        [InlineData("%L", "123")]
        [InlineData("%m", "08")]
        [InlineData("%M", "04")]
        [InlineData("%p", "PM")]
        [InlineData("%P", "pm")]
        [InlineData("%r", "05:04:36 PM")]
        [InlineData("%R", "17:04")]
        [InlineData("%s", "1501578276")]
        [InlineData("%S", "36")]
        [InlineData("%T", "17:04:36")]
        [InlineData("%u", "2")]
        [InlineData("%U", "31")]
        [InlineData("%v", " 1-Aug-2017")]
        [InlineData("%V", "31")]
        [InlineData("%W", "32")]
        [InlineData("%y", "17")]
        [InlineData("%Y", "2017")]
        [InlineData("%z", "+08:00")]
        [InlineData("%%", "%")]
        [InlineData("It is %r", "It is 05:04:36 PM")]
        public void Date(string format, string expected)
        {
            var input = new ObjectValue(new DateTimeOffset(new DateTime(2017, 8, 1, 17, 4, 36, 123), TimeSpan.FromHours(8)));

            var arguments = new FilterArguments(new StringValue(format));
            var context = new TemplateContext();

            var result = MiscFilters.Date(input, arguments, context);

            Assert.Equal(expected, result.ToStringValue());
        }

        [Fact]
        public void DateResolvesNow()
        {
            var input = new StringValue("now");
            var format = "%D";

            var arguments = new FilterArguments(new StringValue(format));
            var context = new TemplateContext();
            context.Now = () => new DateTimeOffset(new DateTime(2017, 8, 1, 5, 4, 36, 123), new TimeSpan(0));
            context.CultureInfo = System.Globalization.CultureInfo.InvariantCulture;

            var result = MiscFilters.Date(input, arguments, context);

            Assert.Equal("08/01/17", result.ToStringValue());
        }

        [Fact]
        public void FormatDate()
        {
            var input = new StringValue("now");
            var format = "d";

            var arguments = new FilterArguments(new StringValue(format));
            var context = new TemplateContext();
            context.Now = () => new DateTimeOffset(new DateTime(2017, 8, 1, 5, 4, 36, 123), new TimeSpan(0));
            context.CultureInfo = System.Globalization.CultureInfo.InvariantCulture;

            var result = MiscFilters.FormatDate(input, arguments, context);

            Assert.Equal("08/01/2017", result.ToStringValue());
        }
    }
}
