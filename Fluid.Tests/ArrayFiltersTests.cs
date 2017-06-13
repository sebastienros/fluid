using System.Linq;
using Fluid.Values;
using Fluid.Filters;
using Xunit;

namespace Fluid.Tests
{
    public class ArrayFiltersTests
    {
        [Fact]
        public void Join()
        {
            var input = new ArrayValue(new[] {
                new StringValue("a"),
                new StringValue("b"),
                new StringValue("c")
                });

            var arguments = new FilterArguments().Add(new StringValue(", "));
            var context = new TemplateContext();

            var result = ArrayFilters.Join(input, arguments, context);

            Assert.Equal("a, b, c", result.ToStringValue());
        }

        [Fact]
        public void First()
        {
            var input = new ArrayValue(new[] {
                new StringValue("a"),
                new StringValue("b"),
                new StringValue("c")
                });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = ArrayFilters.First(input, arguments, context);

            Assert.Equal(new StringValue("a"), result);
        }

        [Fact]
        public void Last()
        {
            var input = new ArrayValue(new[] {
                new StringValue("a"),
                new StringValue("b"),
                new StringValue("c")
                });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = ArrayFilters.Last(input, arguments, context);

            Assert.Equal(new StringValue("c"), result);
        }

        [Fact]
        public void Concat()
        {
            var input = new ArrayValue(new[] {
                new StringValue("a"),
                new StringValue("b"),
                new StringValue("c")
                });

            var arguments = new FilterArguments().Add(
                new ArrayValue(new[] {
                    new StringValue("1"),
                    new StringValue("2"),
                    new StringValue("3")
                    })
            );

            var context = new TemplateContext();

            var result = ArrayFilters.Concat(input, arguments, context);

            Assert.Equal(6, result.Enumerate().Count());
        }

        [Fact]
        public void Map()
        {
            var input = new ArrayValue(new[] {
                new ObjectValue(new { Title = "a" }),
                new ObjectValue(new { Title = "b" }),
                new ObjectValue(new { Title = "c" })
                });

            var arguments = new FilterArguments().Add(new StringValue("Title"));

            var context = new TemplateContext();
            context.MemberAccessStrategy.Register(new { Title = "a" }.GetType());

            var result = ArrayFilters.Map(input, arguments, context);

            Assert.Equal(3, result.Enumerate().Count());
            Assert.Equal(new StringValue("a"), result.Enumerate().ElementAt(0));
            Assert.Equal(new StringValue("b"), result.Enumerate().ElementAt(1));
            Assert.Equal(new StringValue("c"), result.Enumerate().ElementAt(2));
        }

        [Fact]
        public void Reverse()
        {
            var input = new ArrayValue(new[] {
                new StringValue("a"),
                new StringValue("b"),
                new StringValue("c")
                });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = ArrayFilters.Reverse(input, arguments, context);

            Assert.Equal(3, result.Enumerate().Count());
            Assert.Equal(new StringValue("c"), result.Enumerate().ElementAt(0));
            Assert.Equal(new StringValue("b"), result.Enumerate().ElementAt(1));
            Assert.Equal(new StringValue("a"), result.Enumerate().ElementAt(2));
        }

        [Fact]
        public void Size()
        {
            var input = new ArrayValue(new[] {
                new StringValue("a"),
                new StringValue("b"),
                new StringValue("c")
                });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = ArrayFilters.Size(input, arguments, context);

            Assert.Equal(new NumberValue(3), result);
        }

        [Fact]
        public void Sort()
        {
            var input = new ArrayValue(new[] {
                new ObjectValue(new { Title = "c" }),
                new ObjectValue(new { Title = "a" }),
                new ObjectValue(new { Title = "b" })
                });

            var arguments = new FilterArguments().Add(new StringValue("Title"));

            var context = new TemplateContext();
            context.MemberAccessStrategy.Register(new { Title = "" }.GetType(), "Title");

            var result = ArrayFilters.Sort(input, arguments, context);

            Assert.Equal(3, result.Enumerate().Count());
            Assert.Equal("a", ((dynamic)result.Enumerate().ElementAt(0).ToObjectValue()).Title);
            Assert.Equal("b", ((dynamic)result.Enumerate().ElementAt(1).ToObjectValue()).Title);
            Assert.Equal("c", ((dynamic)result.Enumerate().ElementAt(2).ToObjectValue()).Title);
        }

        [Fact]
        public void Uniq()
        {
            var input = new ArrayValue(new[] {
                new StringValue("a"),
                new StringValue("b"),
                new StringValue("b")
                });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = ArrayFilters.Uniq(input, arguments, context);

            Assert.Equal(2, result.Enumerate().Count());
        }
    }
}
