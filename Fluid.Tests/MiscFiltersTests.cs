﻿using System;
using System.Globalization;
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
        [InlineData("%c", "Tuesday, August 1, 2017 5:04:36 PM")]
        [InlineData("%C", "20")]
        [InlineData("%d", "01")]
        [InlineData("%D", "8/1/2017")]
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
        [InlineData("%r", "5:04:36 PM")]
        [InlineData("%R", "5:04 PM")]
        [InlineData("%s", "1501578276")]
        [InlineData("%S", "36")]
        [InlineData("%T", "5:04:36 PM")]
        [InlineData("%u", "2")]
        [InlineData("%U", "31")]
        [InlineData("%v", "Tuesday, August 1, 2017")]
        [InlineData("%V", "31")]
        [InlineData("%W", "32")]
        [InlineData("%y", "17")]
        [InlineData("%Y", "2017")]
        [InlineData("%z", "+08:00")]
        [InlineData("%%", "%")]
        [InlineData("It is %r", "It is 5:04:36 PM")]
        public void Date(string format, string expected)
        {
            var input = new ObjectValue(new DateTimeOffset(new DateTime(2017, 8, 1, 17, 4, 36, 123), TimeSpan.FromHours(8)));

            var arguments = new FilterArguments(new StringValue(format));
            var context = new TemplateContext();
            context.CultureInfo = new CultureInfo("en-US");

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
            context.CultureInfo = CultureInfo.InvariantCulture;

            var result = MiscFilters.Date(input, arguments, context);

            Assert.Equal("08/01/2017", result.ToStringValue());
        }

        [Fact]
        public void DateResolvesToday()
        {
            var input = new StringValue("today");
            var format = "%D";

            var arguments = new FilterArguments(new StringValue(format));
            var context = new TemplateContext();
            context.Now = () => new DateTimeOffset(new DateTime(2017, 8, 1, 5, 4, 36, 123), new TimeSpan(0));
            context.CultureInfo = CultureInfo.InvariantCulture;

            var result = MiscFilters.Date(input, arguments, context);

            Assert.Equal("08/01/2017", result.ToStringValue());
        }

        [Fact]
        public void FormatDate()
        {
            var input = new StringValue("now");
            var format = "d";

            var arguments = new FilterArguments(new StringValue(format));
            var context = new TemplateContext();
            context.Now = () => new DateTimeOffset(new DateTime(2017, 8, 1, 5, 4, 36, 123), new TimeSpan(0));
            context.CultureInfo = CultureInfo.InvariantCulture;

            var result = MiscFilters.FormatDate(input, arguments, context);

            Assert.Equal("08/01/2017", result.ToStringValue());
        }

        [Fact]
        public void DateIsParsed()
        {
            var input = new StringValue("08/01/2017");
            var format = "%D";

            var arguments = new FilterArguments(new StringValue(format));
            var context = new TemplateContext();
            context.CultureInfo = CultureInfo.InvariantCulture;

            var result = MiscFilters.Date(input, arguments, context);

            Assert.Equal("08/01/2017", result.ToStringValue());
        }

        [Fact]
        public void DateIsParsedWithCulture()
        {
            var input = new StringValue("08/01/2017");
            var format = "%d/%m/%Y";

            var arguments = new FilterArguments(new StringValue(format));
            var context = new TemplateContext();

            context.CultureInfo = new CultureInfo("fr-FR");
            var resultFR = MiscFilters.Date(input, arguments, context);

            context.CultureInfo = new CultureInfo("en-US");
            var resultUS = MiscFilters.Date(input, arguments, context);

            Assert.Equal("08/01/2017", resultFR.ToStringValue());
            Assert.Equal("01/08/2017", resultUS.ToStringValue());
        }

        [Fact]
        public void DateIsRenderedWithCulture()
        {
            var input = new StringValue("08/01/2017");
            var format = "%c";

            var arguments = new FilterArguments(new StringValue(format));
            var context = new TemplateContext();

            context.CultureInfo = new CultureInfo("fr-FR");
            var resultFR = MiscFilters.Date(input, arguments, context);

            context.CultureInfo = new CultureInfo("en-US");
            var resultUS = MiscFilters.Date(input, arguments, context);

            Assert.Equal("dimanche 8 janvier 2017 00:00:00", resultFR.ToStringValue());
            Assert.Equal("Tuesday, August 1, 2017 12:00:00 AM", resultUS.ToStringValue());
        }
    }
}
