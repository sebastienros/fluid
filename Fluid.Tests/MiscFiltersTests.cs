using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Fluid.Filters;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class MiscFiltersTests
    {
        private static readonly TimeZoneInfo Pacific = TimeZoneConverter.TZConvert.GetTimeZoneInfo("America/Los_Angeles");
        private static readonly TimeZoneInfo Eastern = TimeZoneConverter.TZConvert.GetTimeZoneInfo("America/New_York");

        [Fact]
        public async Task DefaultReturnsValueIfDefined()
        {
            var input = new StringValue("foo");

            var arguments = new FilterArguments().Add(new StringValue("bar"));
            var context = new TemplateContext();

            var result = await MiscFilters.Default(input, arguments, context);

            Assert.Equal("foo", result.ToStringValue());
        }

        [Theory]
        [InlineData("foo", "foo", "bar", false)]
        [InlineData("bar", null, "bar", false)]
        [InlineData("bar", false, "bar", false)]
        [InlineData("bar", new int[0], "bar", false)]
        [InlineData("bar", "", "bar", false)]
        [InlineData("bar", "empty", "bar", false)]
        [InlineData("foo", "foo", "bar", true)]
        [InlineData("bar", null, "bar", true)]
        [InlineData("bar", "", "bar", true)]
        [InlineData(false, false, "bar", true)]
        [InlineData("bar", new int[0], "bar", true)]
        [InlineData("bar", "empty", "bar", true)]
        public async Task DefaultReturnsDefaultIfNotDefinedOrEmptyOrFalse(object expected, object input, object @default, bool allowFalse)
        {
            var arguments = new FilterArguments()
                .Add("default", FluidValue.Create(@default, TemplateOptions.Default))
                .Add("allow_false", FluidValue.Create(allowFalse, TemplateOptions.Default));

            var context = new TemplateContext();
            var result = await MiscFilters.Default("empty" == input as string ? EmptyValue.Instance : FluidValue.Create(input, TemplateOptions.Default), arguments, context);

            Assert.Equal(expected, result.ToObjectValue());
        }

        [Fact]
        public async Task CompactRemovesNilValues()
        {
            var input = new ArrayValue(new FluidValue[] {
                new StringValue("a"),
                NumberValue.Zero,
                NilValue.Instance,
                new StringValue("b")
                });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await MiscFilters.Compact(input, arguments, context);

            Assert.Equal(3, result.Enumerate(context).Count());
        }


        [Fact]
        public async Task EncodeUrl()
        {
            var input = new StringValue("john@liquid.com");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await MiscFilters.UrlEncode(input, arguments, context);

            Assert.Equal("john%40liquid.com", result.ToStringValue());
        }

        [Fact]
        public async Task DecodeUrl()
        {
            var input = new StringValue("john%40liquid.com");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await MiscFilters.UrlDecode(input, arguments, context);

            Assert.Equal("john@liquid.com", result.ToStringValue());
        }

        [Theory]
        [InlineData("a<>:a?", "YTw+OmE/")]
        public async Task Base64Encode(string value, string expected)
        {
            var input = new StringValue(value);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await MiscFilters.Base64Encode(input, arguments, context);

            Assert.Equal(expected, result.ToStringValue());
        }

        [Theory]
        [InlineData("YTw+OmE/", "a<>:a?")]
        public async Task Base64Decode(string value, string expected)
        {
            var input = new StringValue(value);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await MiscFilters.Base64Decode(input, arguments, context);

            Assert.Equal(expected, result.ToStringValue());
        }

        [Theory]
        [InlineData("a<>:a?", "YTw-OmE_")]
        public async Task Base64UrlSafeEncode(string value, string expected)
        {
            // Arrange
            var input = new StringValue(value);
            var arguments = new FilterArguments();
            var context = new TemplateContext();

            // Act
            var result = await MiscFilters.Base64UrlSafeEncode(input, arguments, context);

            // Assert
            Assert.Equal(expected, result.ToStringValue());
        }

        [Theory]
        [InlineData("YTw-OmE_", "a<>:a?")]
        public async Task Base64UrlSafeDecode(string value, string expected)
        {
            // Arrange
            var input = new StringValue(value);
            var arguments = new FilterArguments();
            var context = new TemplateContext();

            // Act
            var result = await MiscFilters.Base64UrlSafeDecode(input, arguments, context);

            // Assert
            Assert.Equal(expected, result.ToStringValue());
        }

        [Theory]
        [InlineData("Have <em>you</em> read <strong>Ulysses</strong>?", "Have you read Ulysses?")]
        [InlineData("Have you read Ulysses?", "Have you read Ulysses?")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public async Task StripHtml(string value, string expected)
        {
            var input = new StringValue(value);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await MiscFilters.StripHtml(input, arguments, context);

            Assert.Equal(expected, result.ToStringValue());
        }

        [Fact]
        public async Task Escape()
        {
            var input = new StringValue("Have you read 'James & the Giant Peach'?");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await MiscFilters.Escape(input, arguments, context);

            Assert.Equal("Have you read &#39;James &amp; the Giant Peach&#39;?", result.ToStringValue());
        }

        [Fact]
        public async Task EscapeOnce()
        {
            var input = new StringValue("1 &lt; 2 &amp; 3");

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await MiscFilters.EscapeOnce(input, arguments, context);

            Assert.Equal("1 &lt; 2 &amp; 3", result.ToStringValue());
        }

        [Theory]
        [InlineData("%a", "Tue")]
        [InlineData("%^a", "TUE")]
        [InlineData("%A", "Tuesday")]
        [InlineData("%^A", "TUESDAY")]
        [InlineData("%b", "Aug")]
        [InlineData("%^b", "AUG")]
        [InlineData("%B", "August")]
        [InlineData("%^B", "AUGUST")]
        [InlineData("%c", "Tuesday, August 1, 2017 5:04:36 PM")]
        [InlineData("%^c", "TUESDAY, AUGUST 1, 2017 5:04:36 PM")]
        [InlineData("%C", "20")]
        [InlineData("%d", "01")]
        [InlineData("%_d", " 1")]
        [InlineData("%-d", "1")]
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
        [InlineData("%_m", " 8")]
        [InlineData("%-m", "8")]
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
        [InlineData("%^v", " 1-AUG-2017")]
        [InlineData("%V", "31")]
        [InlineData("%W", "32")]
        [InlineData("%y", "17")]
        [InlineData("%Y", "2017")]
        [InlineData("%z", "+0800")]
        [InlineData("%Z", "+08:00")]
        [InlineData("%:z", "+08:00")]
        [InlineData("%+", "Tue Aug  1 17:04:36 +08:00 2017")]
        [InlineData("%%", "%")]
        [InlineData("It is %r", "It is 05:04:36 PM")]
        [InlineData("Chained %z%:z%a%a%^a", "Chained +0800+08:00TueTueTUE")]
        [InlineData("%Y-%m-%dT%H:%M:%S.%L", "2017-08-01T17:04:36.123")]
        public async Task Date(string format, string expected)
        {
            var input = new DateTimeValue(new DateTimeOffset(
                new DateTime(2017, 8, 1, 17, 4, 36, 123), TimeSpan.FromHours(8)));

            var arguments = new FilterArguments(new StringValue(format));
            var options = new TemplateOptions() { CultureInfo = new CultureInfo("en-US", useUserOverride: false), TimeZone = TimeZoneInfo.Utc };
            var context = new TemplateContext(options);

            var result = await MiscFilters.Date(input, arguments, context);

            Assert.Equal(expected, result.ToStringValue());
        }

        [Theory]
        [InlineData("2020-05-18T12:00:00+01:00", "%l:%M%P", "12:00pm")]
        [InlineData("2020-05-18T08:00:00+01:00", "%l:%M%P", "8:00am")]
        [InlineData("2020-05-18T20:00:00+01:00", "%l:%M%P", "8:00pm")]
        [InlineData("2020-05-18T23:59:00+01:00", "%l:%M%P", "11:59pm")]
        [InlineData("2020-05-18T00:00:00+01:00", "%l:%M%P", "12:00am")]
        [InlineData("2020-05-18T11:59:00+01:00", "%l:%M%P", "11:59am")]
        public async Task Time12hFormatFormDateTimeOffset(string dateTimeOffset, string format, string expected)
        {
            var input = new DateTimeValue(DateTimeOffset.Parse(dateTimeOffset));

            var arguments = new FilterArguments(new StringValue(format));
            var options = new TemplateOptions() { CultureInfo = CultureInfo.InvariantCulture };
            var context = new TemplateContext(options);

            var result = await MiscFilters.Date(input, arguments, context);

            Assert.Equal(expected, result.ToStringValue().Trim());
        }

        [Theory]
        [InlineData("2020-05-18T02:13:09+00:00", "America/New_York", "2020-05-17T22:13:09-04:00")]
        [InlineData("2020-05-18T02:13:09+00:00", "Europe/London", "2020-05-18T03:13:09+01:00")]
        [InlineData("2020-05-18T02:13:09+00:00", "Europe/wrongTZ", "2020-05-18T02:13:09+00:00")]
        public async Task ChangeTimeZone(string initialDateTime, string timeZone, string expected)
        {
            var input = new DateTimeValue(DateTimeOffset.Parse(initialDateTime));

            var arguments = new FilterArguments(new StringValue(timeZone));
            var options = new TemplateOptions() { CultureInfo = CultureInfo.InvariantCulture };
            var context = new TemplateContext(options);

            var result = await MiscFilters.ChangeTimeZone(input, arguments, context);

            Assert.Equal(expected, ((DateTimeOffset)result.ToObjectValue()).ToString("yyyy-MM-ddTHH:mm:ssK"));
        }

        [Theory]
        [InlineData("2020-05-18T02:13:09+00:00", "America/New_York", "%l:%M%P", "10:13pm")]
        [InlineData("2020-05-18T02:13:09+00:00", "Europe/London", "%l:%M%P", "3:13am")]
        [InlineData("2020-05-18T02:13:09+00:00", "Europe/wrongTZ", "%l:%M%P", "2:13am")]
        [InlineData("2020-05-18T02:13:09+00:00", "Australia/Adelaide", "%l:%M%P", "11:43am")]
        public async Task ChangeTimeZoneAndApply12hFormat(string initialDateTime, string timeZone, string format, string expected)
        {
            var input = new DateTimeValue(DateTimeOffset.Parse(initialDateTime));
            var timeZoneArgument = new FilterArguments(new StringValue(timeZone));
            var formatArgument = new FilterArguments(new StringValue(format));
            var options = new TemplateOptions() { CultureInfo = CultureInfo.InvariantCulture };
            var context = new TemplateContext(options);

            var result = await MiscFilters.ChangeTimeZone(input, timeZoneArgument, context);
            result = await MiscFilters.Date(result, formatArgument, context);

            Assert.Equal(expected, result.ToStringValue().Trim());
        }

        [Fact]
        public async Task DateResolvesNow()
        {
            var input = new StringValue("now");
            var format = "%D";

            var arguments = new FilterArguments(new StringValue(format));
            var options = new TemplateOptions()
            {
                CultureInfo = CultureInfo.InvariantCulture,
                Now = () => new DateTimeOffset(new DateTime(2017, 8, 1, 5, 4, 36, 123), new TimeSpan(0))
            };
            var context = new TemplateContext(options);

            var result = await MiscFilters.Date(input, arguments, context);

            Assert.Equal("08/01/17", result.ToStringValue());
        }

        [Fact]
        public async Task DateResolvesToday()
        {
            var input = new StringValue("today");
            var format = "%D";

            var arguments = new FilterArguments(new StringValue(format));
            var options = new TemplateOptions()
            {
                CultureInfo = CultureInfo.InvariantCulture,
                Now = () => new DateTimeOffset(new DateTime(2017, 8, 1, 5, 4, 36, 123), new TimeSpan(0))
            };
            var context = new TemplateContext(options);

            var result = await MiscFilters.Date(input, arguments, context);

            Assert.Equal("08/01/17", result.ToStringValue());
        }

        [Fact]
        public async Task FormatDate()
        {
            var input = new StringValue("now");
            var format = "d";

            var arguments = new FilterArguments(new StringValue(format));
            var options = new TemplateOptions()
            {
                CultureInfo = CultureInfo.InvariantCulture,
                Now = () => new DateTimeOffset(new DateTime(2017, 8, 1, 5, 4, 36, 123), new TimeSpan(0))
            };
            var context = new TemplateContext(options);

            var result = await MiscFilters.FormatDate(input, arguments, context);

            Assert.Equal("08/01/2017", result.ToStringValue());
        }

        [Fact]
        public async Task DateIsParsed()
        {
            var input = new StringValue("08/01/2017");
            var format = "%D";

            var arguments = new FilterArguments(new StringValue(format));
            var options = new TemplateOptions() { CultureInfo = CultureInfo.InvariantCulture, TimeZone = TimeZoneInfo.Utc };
            var context = new TemplateContext(options);

            var result = await MiscFilters.Date(input, arguments, context);

            Assert.Equal("08/01/17", result.ToStringValue());
        }

        [Theory]
        [InlineData(0, "0")]
        [InlineData(10, "10")]
        [InlineData(-10, "-10")]
        public async Task DateNumberIsParsedAsSeconds(long number, string expected)
        {
            // Converting to Unix time should not vary by TimeSone

            var input = NumberValue.Create(number);
            var format = new FilterArguments(new StringValue("%s"));
            var context = new TemplateContext { TimeZone = Eastern };

            var result = await MiscFilters.Date(input, format, context);

            Assert.Equal(expected, result.ToStringValue());
        }

        [Theory]
        [InlineData("0", "00:00:00.000")]
        [InlineData("1:2", "01:02:00.000")]
        [InlineData("1:2:3.1", "01:02:03.100")]
        public async Task DateTimeSpan(string timespan, string expected)
        {
            // Converting to Unix time should not vary by TimeSone

            var input = FluidValue.Create(TimeSpan.Parse(timespan), new TemplateOptions());
            var format = new FilterArguments(new StringValue("%H:%M:%S.%L"));
            var context = new TemplateContext { TimeZone = Eastern };

            var result = await MiscFilters.Date(input, format, context);

            Assert.Equal(expected, result.ToStringValue());
        }

        [Fact]
        public async Task NoTimeZoneIsParsedAsLocal()
        {
            var input = StringValue.Create("1970-01-01 00:00:00");
            var format = new FilterArguments(new StringValue("%a %b %e %H:%M:%S %Y %z"));
            var context = new TemplateContext { TimeZone = Pacific };

            var result = await MiscFilters.Date(input, format, context);

            Assert.Equal("Thu Jan  1 00:00:00 1970 -0800", result.ToStringValue());
        }

        [Fact]
        public async Task TimeZoneIsParsed()
        {
            // This test ensures that when a TZ is specified it uses it instead of the settings one
            var input = StringValue.Create("1970-01-01 00:00:00 -05:00");

            var format = new FilterArguments(new StringValue("%s"));
            var context = new TemplateContext { TimeZone = TimeZoneInfo.Utc };

            var result = await MiscFilters.Date(input, format, context);

            Assert.Equal("18000", result.ToStringValue());
        }

        [Fact]
        public async Task DefaultTimeZoneIsSetWhenNotParsed()
        {
            // This test ensures that when a TZ is specified it uses it instead of the settings one
            var input = StringValue.Create("1970-01-01 00:00:00");

            var format = new FilterArguments(new StringValue("%s"));
            var context = new TemplateContext { TimeZone = Eastern };

            var result = await MiscFilters.Date(input, format, context);

            Assert.Equal("18000", result.ToStringValue());
        }

        [Fact]
        public async Task DefaultTimeZoneAppliesDaylightSaving()
        {
            // This test ensures that the DST is respected when TZ is rendered

            var input = StringValue.Create("2021-01-01 00:00:00");

            var format = new FilterArguments(new StringValue("%z"));
            var context = new TemplateContext { TimeZone = Pacific };

            var result = await MiscFilters.Date(input, format, context);

            Assert.Equal("-0800", result.ToStringValue());

            input = StringValue.Create("2021-06-01 00:00:00");

            result = await MiscFilters.Date(input, format, context);

            Assert.Equal("-0700", result.ToStringValue());
        }

        [Fact]
        public async Task DateNumberIsParsedInLocalTimeZone()
        {
            // This test is issued from a template running in Ruby on a system with -5 TZ
            // {{ 0 | date: '%c' }}

            var input = NumberValue.Create(0);
            var format = new FilterArguments(new StringValue("%+"));
            var context = new TemplateContext { TimeZone = Eastern };

            var result = await MiscFilters.Date(input, format, context);

            Assert.Equal("Wed Dec 31 19:00:00 -05:00 1969", result.ToStringValue());
        }

        [Fact]
        public async Task DateIsParsedWithCulture()
        {
            var input = new StringValue("08/01/2017");
            var format = "%d/%m/%Y";

            var arguments = new FilterArguments(new StringValue(format));

            var context = new TemplateContext(new TemplateOptions { CultureInfo = new CultureInfo("fr-FR", useUserOverride: false), TimeZone = TimeZoneInfo.Utc });
            var resultFR = await MiscFilters.Date(input, arguments, context);

            context = new TemplateContext(new TemplateOptions { CultureInfo = new CultureInfo("en-US", useUserOverride: false), TimeZone = TimeZoneInfo.Utc });
            var resultUS = await MiscFilters.Date(input, arguments, context);

            Assert.Equal("08/01/2017", resultFR.ToStringValue());
            Assert.Equal("01/08/2017", resultUS.ToStringValue());
        }

        [Fact]
        public async Task DateIsRenderedWithCulture()
        {
            var input = new StringValue("08/01/2017");
            var format = "%c";

            var arguments = new FilterArguments(new StringValue(format));

            var context = new TemplateContext { CultureInfo = new CultureInfo("fr-FR", useUserOverride: false), TimeZone = TimeZoneInfo.Utc };
            var resultFR = await MiscFilters.Date(input, arguments, context);

            context = new TemplateContext { CultureInfo = new CultureInfo("en-US", useUserOverride: false), TimeZone = TimeZoneInfo.Utc };
            var resultUS = await MiscFilters.Date(input, arguments, context);

            Assert.Equal("dimanche 8 janvier 2017 00:00:00", resultFR.ToStringValue());
            Assert.Equal("Tuesday, August 1, 2017 12:00:00 AM", resultUS.ToStringValue());
        }

        [Theory]
        [InlineData("SomeThing", "some-thing")]
        [InlineData("capsONInside", "caps-on-inside")]
        [InlineData("CAPSOnOUTSIDE", "caps-on-outside")]
        [InlineData("ALLCAPS", "allcaps")]
        [InlineData("One1Two2Three3", "one1-two2-three3")]
        [InlineData("ONE1TWO2THREE3", "one1two2three3")]
        [InlineData("First_Second_ThirdHi", "first-second-third-hi")]
        [InlineData("100% M & Ms!!!", "100-m-ms")]
        [InlineData("!!!100% M & Ms", "100-m-ms")]
        public async Task Handleize(string text, string expected)
        {
            var input = new StringValue(text);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await MiscFilters.Handleize(input, arguments, context);

            Assert.Equal(expected, result.ToStringValue());
        }

        [Theory]
        [InlineData("Hello World!", "\"Hello World!\"")]
        [InlineData("\"", "\"\\u0022\"")]
        [InlineData("'", "\"\\u0027\"")]
        [InlineData(123, "123")]
        [InlineData(123.12, "123.12")]
        [InlineData(-123.12, "-123.12")]
        [InlineData(null, "null")]
        [InlineData("", "\"\"")]
        [InlineData(new int[] { 1, 2, 3 }, "[1,2,3]")]
        [InlineData(new string[] { "a", "b", "c" }, "[\"a\",\"b\",\"c\"]")]
        [InlineData(new object[0], "[]")]
        [InlineData(new object[] { 1, "a", true }, "[1,\"a\",true]")]
        public async Task Json(object value, string expected)
        {
            var input = FluidValue.Create(value, TemplateOptions.Default);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await MiscFilters.Json(input, arguments, context);

            Assert.Equal(expected, result.ToStringValue());
        }

        [Fact]
        public async Task JsonShouldHideMembers()
        {
            var inputObject = new JsonAccessStrategy();
            var templateOptions = new TemplateOptions();
            templateOptions.MemberAccessStrategy.Register<JsonAccessStrategy, FluidValue>((obj, name, context) =>
            {
                return name switch
                {
                    nameof(JsonAccessStrategy.Visible) => new StringValue(obj.Visible),
                    nameof(JsonAccessStrategy.Null) => new StringValue(obj.Null),
                    _ => NilValue.Instance
                };
            });

            var input = FluidValue.Create(inputObject, templateOptions);
            var expected = "{\"Visible\":\"Visible\",\"Null\":\"\"}";

            var arguments = new FilterArguments();
            var context = new TemplateContext(templateOptions);

            var result = await MiscFilters.Json(input, arguments, context);

            Assert.Equal(expected, result.ToStringValue());
        }

        [Fact]
        public async Task JsonShouldHandleCircularReferences()
        {
            var model = TestObjects.RecursiveReferenceObject;
            var input = FluidValue.Create(model, TemplateOptions.Default);
            var to = new TemplateOptions();
            to.MemberAccessStrategy.Register<TestObjects.Node>();

            var result = await MiscFilters.Json(input, new FilterArguments(), new TemplateContext(to));

            Assert.Equal("{\"Name\":\"Object1\",\"NodeRef\":{\"Name\":\"Child1\",\"NodeRef\":\"Circular reference has been detected.\"}}", result.ToStringValue());
        }

        [Fact]
        public async Task JsonShouldHandleCircularReferencesOnSiblingPropertiesSeparately()
        {
            var model = TestObjects.SiblingPropertiesHaveSameReferenceObject;
            var input = FluidValue.Create(model, TemplateOptions.Default);
            var to = new TemplateOptions();
            to.MemberAccessStrategy.Register<TestObjects.Node>();
            to.MemberAccessStrategy.Register<TestObjects.MultipleNode>();

            var result = await MiscFilters.Json(input, new FilterArguments(), new TemplateContext(to));

            Assert.Equal("{\"Name\":\"MultipleNode1\",\"Node1\":{\"Name\":\"Object1\",\"NodeRef\":{\"Name\":\"Child1\",\"NodeRef\":\"Circular reference has been detected.\"}},\"Node2\":{\"Name\":\"Object1\",\"NodeRef\":{\"Name\":\"Child1\",\"NodeRef\":\"Circular reference has been detected.\"}}}", result.ToStringValue());
        }

        [Fact]
        public async Task JsonShouldIgnoreStaticMembers()
        {
            var model = new JsonWithStaticMember { Id = 100 };
            var input = FluidValue.Create(model, TemplateOptions.Default);
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<JsonWithStaticMember>();

            var result = await MiscFilters.Json(input, new FilterArguments(), new TemplateContext(options));
            Assert.Equal("{\"Id\":100}", result.ToStringValue());
        }

        [Fact]
        public async Task JsonShouldWriteNullIfDictionaryNotReturnFluidIndexable()
        {
            var model = new
            {
                Id = 1,
                WithoutIndexable = new DictionaryWithoutIndexableTestObjects(new { }),
                Bool = true
            };
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register(model.GetType());
            var input = FluidValue.Create(model, options);
            var result = await MiscFilters.Json(input, new FilterArguments(), new TemplateContext(options));
            Assert.Equal("{\"Id\":1,\"WithoutIndexable\":null,\"Bool\":true}", result.ToStringValue());
        }

        [Theory]
        [InlineData("", "", "", "0")]
        [InlineData(123456, "", "", "123456")]
        [InlineData(123456.00, "", "en-US", "123456")]
        [InlineData(123456.00, "N2", "en-US", "123,456.00")]
        [InlineData(123456.00, "C2", "en-US", "$123,456.00")]

        // Skip tests with spaces as Linux and Windows implementation don't use the same space
        //[InlineData(123456.00, "C2", "fr-FR", "123 456,00 €")]
        //[InlineData("123456.00", "C2", "fr-FR", "123 456,00 €")]
        public async Task FormatNumber(object input, string format, string culture, string expected)
        {
            var cultureInfo = String.IsNullOrEmpty(culture)
                ? CultureInfo.InvariantCulture
                : CultureInfo.CreateSpecificCulture(culture)
                ;

            var arguments = new FilterArguments(new StringValue(format));
            var context = new TemplateContext(new TemplateOptions { CultureInfo = cultureInfo });

            var result = await MiscFilters.FormatNumber(FluidValue.Create(input, context.Options), arguments, context);

            Assert.Equal(expected, result.ToStringValue());
        }

        [Theory]
        [InlineData("", new object[] { 123 }, "", "")]
        [InlineData("{0}", new object[] { 123 }, "", "123")]
        [InlineData("hello {0}", new object[] { "world", 123 }, "", "hello world")]
        [InlineData("{0:C2} {1:N2}", new object[] { 123, 456 }, "fr-FR", "123,00 € 456,00")]
        public async Task FormatString(object input, object[] args, string culture, string expected)
        {
            var cultureInfo = String.IsNullOrEmpty(culture)
                ? CultureInfo.InvariantCulture
                : CultureInfo.CreateSpecificCulture(culture)
                ;

            var context = new TemplateContext(new TemplateOptions { CultureInfo = cultureInfo });
            var arguments = new FilterArguments(args.Select(x => FluidValue.Create(x, context.Options)).ToArray());

            var result = await MiscFilters.FormatString(FluidValue.Create(input, context.Options), arguments, context);

            Assert.Equal(expected, result.ToStringValue());
        }

        [Fact]
        public async Task MD5()
        {
            // Arrange
            var input = new StringValue("Fluid");
            var arguments = new FilterArguments();
            var context = new TemplateContext();

            // Act
            var result = await MiscFilters.MD5(input, arguments, context);

            // Assert
            Assert.Equal("c2e7db5ac7ab74ea4bb9a7a89d251f3a", result.ToStringValue());
        }

        [Fact]
        public async Task Sha1()
        {
            // Arrange
            var input = new StringValue("Fluid");
            var arguments = new FilterArguments();
            var context = new TemplateContext();

            // Act
            var result = await MiscFilters.Sha1(input, arguments, context);

            // Assert
            Assert.Equal("8bc9b7abbb676300656203a17863a0f0b8a2c2bf", result.ToStringValue());
        }

        [Fact]
        public async Task Sha256()
        {
            // Arrange
            var input = new StringValue("Fluid");
            var arguments = new FilterArguments();
            var context = new TemplateContext();

            // Act
            var result = await MiscFilters.Sha256(input, arguments, context);

            // Assert
            Assert.Equal("c7ac4687585ab5d3d5030db5a5cfc959fdf4e608cc396f1f615db345e35adb9e", result.ToStringValue());
        }

        public static class TestObjects
        {
            public class Node
            {
                public string Name { get; set; }
                public Node NodeRef { get; set; }
            }

            public class MultipleNode
            {
                public string Name { get; set; }

                public Node Node1 { get; set; }

                public Node Node2 { get; set; }
            }

            public static Node RecursiveReferenceObject
            {
                get
                {
                    var parent = new Node
                    {
                        Name = "Object1",
                    };
                    var child = new Node
                    {
                        Name = "Child1",
                        NodeRef = parent
                    };
                    parent.NodeRef = child;
                    return parent;
                }
            }

            public static object SiblingPropertiesHaveSameReferenceObject
            {
                get
                {
                    var n = RecursiveReferenceObject;
                    var m = new MultipleNode
                    {
                        Name = "MultipleNode1",
                        Node1 = n,
                        Node2 = n
                    };
                    return m;
                }
            }
        }

        private class JsonAccessStrategy
        {
            public string Visible { get; set; } = "Visible";
            public string Null { get; set; }
            public string Hidden { get; set; } = "Hidden";
        }

        private class JsonWithStaticMember
        {
            public static int StaticMember { get; set; } = 1;
            public int Id { get; set; }
        }

        private class DictionaryWithoutIndexableTestObjects : ObjectValueBase
        {
            public override FluidValues Type => FluidValues.Dictionary;
            public DictionaryWithoutIndexableTestObjects(object value) : base(value)
            {

            }
        }
    }
}
