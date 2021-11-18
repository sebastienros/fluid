using System.Linq;
using Fluid.Values;
using Fluid.Filters;
using Xunit;
using System.Threading.Tasks;

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

            Assert.Equal("a, b, c", result.Result.ToStringValue());
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
        public async Task First_EmptyArray()
        {
            var input = new ArrayValue(new StringValue[0]);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await ArrayFilters.First(input, arguments, context);

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
        public async Task Last_EmptyArray()
        {
            var input = new ArrayValue(new StringValue[0]);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await ArrayFilters.Last(input, arguments, context);

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

            Assert.Equal(6, result.Result.Enumerate(context).Count());
        }

        [Fact]
        public async Task Map()
        {
            var input = new ArrayValue(new[] {
                new ObjectValue(new { Title = "a" }),
                new ObjectValue(new { Title = "b" }),
                new ObjectValue(new { Title = "c" })
                });

            var arguments = new FilterArguments().Add(new StringValue("Title"));

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(new { Title = "a" }.GetType());

            var result = await ArrayFilters.Map(input, arguments, context);

            Assert.Equal(3, result.Enumerate(context).Count());
            Assert.Equal(new StringValue("a"), result.Enumerate(context).ElementAt(0));
            Assert.Equal(new StringValue("b"), result.Enumerate(context).ElementAt(1));
            Assert.Equal(new StringValue("c"), result.Enumerate(context).ElementAt(2));
        }

        [Fact]
        public async Task Map_DeepProperties() 
        {
            var sample = new { Title = new { Text = "a" } };
            var input = new ArrayValue(new[] {
                new ObjectValue(new { Title = new { Text = "a" }}),
                new ObjectValue(new { Title = new { Text = "b" }}),
                new ObjectValue(new { Title = new { Text = "c" }})
                });

            var arguments = new FilterArguments().Add(new StringValue("Title.Text"));

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(sample.GetType());
            options.MemberAccessStrategy.Register(sample.Title.GetType());

            var result = await ArrayFilters.Map(input, arguments, context);

            Assert.Equal(3, result.Enumerate(context).Count());
            Assert.Equal(new StringValue("a"), result.Enumerate(context).ElementAt(0));
            Assert.Equal(new StringValue("b"), result.Enumerate(context).ElementAt(1));
            Assert.Equal(new StringValue("c"), result.Enumerate(context).ElementAt(2));
        }

        [Fact]
        public void ReverseString()
        {
            // Arrange
            var input = new StringValue("Fluid");
            var arguments = new FilterArguments();
            var context = new TemplateContext();

            // Act
            var result = ArrayFilters.Reverse(input, arguments, context);

            // Assert
            Assert.Equal(5, result.Result.Enumerate(context).Count());
            Assert.Equal(new StringValue("d"), result.Result.Enumerate(context).ElementAt(0));
            Assert.Equal(new StringValue("i"), result.Result.Enumerate(context).ElementAt(1));
            Assert.Equal(new StringValue("u"), result.Result.Enumerate(context).ElementAt(2));
            Assert.Equal(new StringValue("l"), result.Result.Enumerate(context).ElementAt(3));
            Assert.Equal(new StringValue("F"), result.Result.Enumerate(context).ElementAt(4));
        }

        [Fact]
        public void ReverseArray()
        {
            var input = new ArrayValue(new[] {
                new StringValue("a"),
                new StringValue("b"),
                new StringValue("c")
                });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = ArrayFilters.Reverse(input, arguments, context);

            Assert.Equal(3, result.Result.Enumerate(context).Count());
            Assert.Equal(new StringValue("c"), result.Result.Enumerate(context).ElementAt(0));
            Assert.Equal(new StringValue("b"), result.Result.Enumerate(context).ElementAt(1));
            Assert.Equal(new StringValue("a"), result.Result.Enumerate(context).ElementAt(2));
        }

        [Fact]
        public async Task Size()
        {
            var input = new ArrayValue(new[] {
                new StringValue("a"),
                new StringValue("b"),
                new StringValue("c")
                });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await ArrayFilters.Size(input, arguments, context);

            Assert.Equal(NumberValue.Create(3), result);
        }

        [Fact]
        public async Task Sort()
        {
            var sample = new { Title = "", Address = new { Zip = 0 } };

            var input = new ArrayValue(new[] {
                new ObjectValue(new { Title = "c", Address = new { Zip = 2 } }),
                new ObjectValue(new { Title = "a", Address = new { Zip = 3 } }),
                new ObjectValue(new { Title = "b", Address = new { Zip = 1 } })
                });

            var arguments = new FilterArguments().Add(new StringValue("Title"));

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(sample.GetType(), "Title");

            var result = await ArrayFilters.Sort(input, arguments, context);

            Assert.Equal(3, result.Enumerate(context).Count());
            Assert.Equal("a", ((dynamic)result.Enumerate(context).ElementAt(0).ToObjectValue()).Title);
            Assert.Equal("b", ((dynamic)result.Enumerate(context).ElementAt(1).ToObjectValue()).Title);
            Assert.Equal("c", ((dynamic)result.Enumerate(context).ElementAt(2).ToObjectValue()).Title);

            arguments = new FilterArguments().Add(new StringValue("Address.Zip"));

            options = new TemplateOptions();
            context = new TemplateContext(options); 
            options.MemberAccessStrategy.Register(sample.GetType(), "Address");
            options.MemberAccessStrategy.Register(sample.Address.GetType(), "Zip");

            result = await ArrayFilters.Sort(input, arguments, context);

            Assert.Equal(3, result.Enumerate(context).Count());
            Assert.Equal("b", ((dynamic)result.Enumerate(context).ElementAt(0).ToObjectValue()).Title);
            Assert.Equal("c", ((dynamic)result.Enumerate(context).ElementAt(1).ToObjectValue()).Title);
            Assert.Equal("a", ((dynamic)result.Enumerate(context).ElementAt(2).ToObjectValue()).Title);
        }

        [Fact]
        public async Task SortWithoutArgument()
        {
            var input = new ArrayValue(new[] {
                new StringValue("c"),
                new StringValue("a"),
                new StringValue("B"),
                });

            var arguments = new FilterArguments();

            var context = new TemplateContext();

            var result = await ArrayFilters.Sort(input, arguments, context);

            Assert.Equal(3, result.Enumerate(context).Count());
            Assert.Equal("B", result.Enumerate(context).ElementAt(0).ToStringValue());
            Assert.Equal("a", result.Enumerate(context).ElementAt(1).ToStringValue());
            Assert.Equal("c", result.Enumerate(context).ElementAt(2).ToStringValue());
        }

        [Fact]
        public async Task SortNaturalWithoutArgument()
        {
            var input = new ArrayValue(new[] {
                new StringValue("c"),
                new StringValue("a"),
                new StringValue("B"),
                });

            var arguments = new FilterArguments();

            var context = new TemplateContext();

            var result = await ArrayFilters.SortNatural(input, arguments, context);

            Assert.Equal(3, result.Enumerate(context).Count());
            Assert.Equal("a", result.Enumerate(context).ElementAt(0).ToStringValue());
            Assert.Equal("B", result.Enumerate(context).ElementAt(1).ToStringValue());
            Assert.Equal("c", result.Enumerate(context).ElementAt(2).ToStringValue());
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

            Assert.Equal(2, result.Result.Enumerate(context).Count());
        }

        [Fact]
        public async Task Where()
        {
            var input = new ArrayValue(new[] {
                new ObjectValue(new { Title = "a", Pinned = true }),
                new ObjectValue(new { Title = "b", Pinned = false }),
                new ObjectValue(new { Title = "c", Pinned = true })
                });

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(new { Title = "a", Pinned = true }.GetType());

            var arguments1 = new FilterArguments().Add(new StringValue("Pinned"));

            var result1 = await ArrayFilters.Where(input, arguments1, context);

            Assert.Equal(2, result1.Enumerate(context).Count());

            var arguments2 = new FilterArguments()
                .Add(new StringValue("Pinned"))
                .Add(BooleanValue.Create(false))
                ;

            var result2 = await ArrayFilters.Where(input, arguments2, context);

            Assert.Single(result2.Enumerate(context));

            var arguments3 = new FilterArguments()
                .Add(new StringValue("Title"))
                .Add(new StringValue("c"));

            var result3 = await  ArrayFilters.Where(input, arguments3, context);

            Assert.Single(result3.Enumerate(context));
        }

        [Fact]
        public async Task WhereShouldNotThrow()
        {
            var input = new ArrayValue(new[] {
                new ObjectValue(new { Title = "a", Pinned = true }),
                new ObjectValue(new { Title = "b", Pinned = false }),
                new ObjectValue(new { Title = "c", Pinned = true })
                });

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(new { Title = "a", Pinned = true }.GetType());

            var arguments1 = new FilterArguments().Add(new StringValue("a.b.c"));

            var result1 = await ArrayFilters.Where(input, arguments1, context);

            Assert.Empty(result1.Enumerate(context));
        }
    }
}
