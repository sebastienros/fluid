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
        public void First_EmptyArray()
        {
            var input = new ArrayValue(new StringValue[0]);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = ArrayFilters.First(input, arguments, context);

            Assert.IsType<NilValue>(result);
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
        public void Last_EmptyArray()
        {
            var input = new ArrayValue(new StringValue[0]);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = ArrayFilters.Last(input, arguments, context);

            Assert.IsType<NilValue>(result);
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
        public void Map_DeepProperties() 
        {
            var sample = new { Title = new { Text = "a" } };
            var input = new ArrayValue(new[] {
                new ObjectValue(new { Title = new { Text = "a" }}),
                new ObjectValue(new { Title = new { Text = "b" }}),
                new ObjectValue(new { Title = new { Text = "c" }})
                });

            var arguments = new FilterArguments().Add(new StringValue("Title.Text"));

            var context = new TemplateContext();
            context.MemberAccessStrategy.Register(sample.GetType());
            context.MemberAccessStrategy.Register(sample.Title.GetType());

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

            Assert.Equal(NumberValue.Create(3), result);
        }

        [Fact]
        public void Sort()
        {
            var sample = new { Title = "", Address = new { Zip = 0 } };

            var input = new ArrayValue(new[] {
                new ObjectValue(new { Title = "c", Address = new { Zip = 2 } }),
                new ObjectValue(new { Title = "a", Address = new { Zip = 3 } }),
                new ObjectValue(new { Title = "b", Address = new { Zip = 1 } })
                });

            var arguments = new FilterArguments().Add(new StringValue("Title"));

            var context = new TemplateContext();
            context.MemberAccessStrategy.Register(sample.GetType(), "Title");

            var result = ArrayFilters.Sort(input, arguments, context);

            Assert.Equal(3, result.Enumerate().Count());
            Assert.Equal("a", ((dynamic)result.Enumerate().ElementAt(0).ToObjectValue()).Title);
            Assert.Equal("b", ((dynamic)result.Enumerate().ElementAt(1).ToObjectValue()).Title);
            Assert.Equal("c", ((dynamic)result.Enumerate().ElementAt(2).ToObjectValue()).Title);

            arguments = new FilterArguments().Add(new StringValue("Address.Zip"));

            context = new TemplateContext();
            context.MemberAccessStrategy.Register(sample.GetType(), "Title");
            context.MemberAccessStrategy.Register(sample.GetType(), "Address");
            context.MemberAccessStrategy.Register(sample.Address.GetType(), "Zip");

            result = ArrayFilters.Sort(input, arguments, context);

            Assert.Equal(3, result.Enumerate().Count());
            Assert.Equal("b", ((dynamic)result.Enumerate().ElementAt(0).ToObjectValue()).Title);
            Assert.Equal("c", ((dynamic)result.Enumerate().ElementAt(1).ToObjectValue()).Title);
            Assert.Equal("a", ((dynamic)result.Enumerate().ElementAt(2).ToObjectValue()).Title);
        }

        [Fact]
        public void SortWithoutArgument()
        {
            var input = new ArrayValue(new[] {
                new StringValue("c"),
                new StringValue("a"),
                new StringValue("B"),
                });

            var arguments = new FilterArguments();

            var context = new TemplateContext();

            var result = ArrayFilters.Sort(input, arguments, context);

            Assert.Equal(3, result.Enumerate().Count());
            Assert.Equal("B", result.Enumerate().ElementAt(0).ToStringValue());
            Assert.Equal("a", result.Enumerate().ElementAt(1).ToStringValue());
            Assert.Equal("c", result.Enumerate().ElementAt(2).ToStringValue());
        }

        [Fact]
        public void SortNaturalWithoutArgument()
        {
            var input = new ArrayValue(new[] {
                new StringValue("c"),
                new StringValue("a"),
                new StringValue("B"),
                });

            var arguments = new FilterArguments();

            var context = new TemplateContext();

            var result = ArrayFilters.SortNatural(input, arguments, context);

            Assert.Equal(3, result.Enumerate().Count());
            Assert.Equal("a", result.Enumerate().ElementAt(0).ToStringValue());
            Assert.Equal("B", result.Enumerate().ElementAt(1).ToStringValue());
            Assert.Equal("c", result.Enumerate().ElementAt(2).ToStringValue());
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

        [Fact]
        public void Where()
        {
            var input = new ArrayValue(new[] {
                new ObjectValue(new { Title = "a", Pinned = true }),
                new ObjectValue(new { Title = "b", Pinned = false }),
                new ObjectValue(new { Title = "c", Pinned = true })
                });

            var context = new TemplateContext();
            context.MemberAccessStrategy.Register(new { Title = "a", Pinned = true }.GetType());

            var arguments1 = new FilterArguments().Add(new StringValue("Pinned"));

            var result1 = ArrayFilters.Where(input, arguments1, context);

            Assert.Equal(2, result1.Enumerate().Count());

            var arguments2 = new FilterArguments()
                .Add(new StringValue("Pinned"))
                .Add(BooleanValue.Create(false))
                ;

            var result2 = ArrayFilters.Where(input, arguments2, context);

            Assert.Single(result2.Enumerate());

            var arguments3 = new FilterArguments()
                .Add(new StringValue("Title"))
                .Add(new StringValue("c"));

            var result3 = ArrayFilters.Where(input, arguments3, context);

            Assert.Single(result3.Enumerate());
        }

        [Fact]
        public void WhereShouldNotThrow()
        {
            var input = new ArrayValue(new[] {
                new ObjectValue(new { Title = "a", Pinned = true }),
                new ObjectValue(new { Title = "b", Pinned = false }),
                new ObjectValue(new { Title = "c", Pinned = true })
                });

            var context = new TemplateContext();
            context.MemberAccessStrategy.Register(new { Title = "a", Pinned = true }.GetType());

            var arguments1 = new FilterArguments().Add(new StringValue("a.b.c"));

            var result1 = ArrayFilters.Where(input, arguments1, context);

            Assert.Equal(0, result1.Enumerate().Count());
        }
    }
}
