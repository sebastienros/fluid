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
            var input = ArrayValue.Create(new[] {
                StringValue.Create("a"),
                StringValue.Create("b"),
                StringValue.Create("c")
                });

            var arguments = new FilterArguments().Add(StringValue.Create(", "));
            var context = new TemplateContext();

            var result = ArrayFilters.Join(input, arguments, context);

            Assert.Equal("a, b, c", result.Result.ToStringValue());
        }

        [Fact]
        public void First()
        {
            var input = ArrayValue.Create(new[] {
                StringValue.Create("a"),
                StringValue.Create("b"),
                StringValue.Create("c")
                });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = ArrayFilters.First(input, arguments, context);

            Assert.Equal(StringValue.Create("a"), result);
        }

        [Fact]
        public async Task First_EmptyArray()
        {
            var input = ArrayValue.Create(new StringValue[0]);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await ArrayFilters.First(input, arguments, context);

            Assert.IsType<NilValue>(result);
        }

        [Fact]
        public void Last()
        {
            var input = ArrayValue.Create(new[] {
                StringValue.Create("a"),
                StringValue.Create("b"),
                StringValue.Create("c")
                });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = ArrayFilters.Last(input, arguments, context);

            Assert.Equal(StringValue.Create("c"), result);
        }

        [Fact]
        public async Task Last_EmptyArray()
        {
            var input = ArrayValue.Create(new StringValue[0]);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await ArrayFilters.Last(input, arguments, context);

            Assert.IsType<NilValue>(result);
        }

        [Fact]
        public void Concat()
        {
            var input = ArrayValue.Create(new[] {
                StringValue.Create("a"),
                StringValue.Create("b"),
                StringValue.Create("c")
                });

            var arguments = new FilterArguments().Add(
                ArrayValue.Create(new[] {
                    StringValue.Create("1"),
                    StringValue.Create("2"),
                    StringValue.Create("3")
                    })
            );

            var context = new TemplateContext();

            var result = ArrayFilters.Concat(input, arguments, context);

            Assert.Equal(6, result.Result.EnumerateAsync(context).Result.Count());
        }

        [Fact]
        public async Task Map()
        {
            var input = ArrayValue.Create(new[] {
                new ObjectValue(new { Title = "a" }),
                new ObjectValue(new { Title = "b" }),
                new ObjectValue(new { Title = "c" })
                });

            var arguments = new FilterArguments().Add(StringValue.Create("Title"));

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(new { Title = "a" }.GetType());

            var result = await ArrayFilters.Map(input, arguments, context);

            Assert.Equal(3, result.EnumerateAsync(context).Result.Count());
            Assert.Equal(StringValue.Create("a"), result.EnumerateAsync(context).Result.ElementAt(0));
            Assert.Equal(StringValue.Create("b"), result.EnumerateAsync(context).Result.ElementAt(1));
            Assert.Equal(StringValue.Create("c"), result.EnumerateAsync(context).Result.ElementAt(2));
        }

        [Fact]
        public async Task Map_DeepProperties() 
        {
            var sample = new { Title = new { Text = "a" } };
            var input = ArrayValue.Create(new[] {
                new ObjectValue(new { Title = new { Text = "a" }}),
                new ObjectValue(new { Title = new { Text = "b" }}),
                new ObjectValue(new { Title = new { Text = "c" }})
                });

            var arguments = new FilterArguments().Add(StringValue.Create("Title.Text"));

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(sample.GetType());
            options.MemberAccessStrategy.Register(sample.Title.GetType());

            var result = await ArrayFilters.Map(input, arguments, context);

            Assert.Equal(3, result.EnumerateAsync(context).Result.Count());
            Assert.Equal(StringValue.Create("a"), result.EnumerateAsync(context).Result.ElementAt(0));
            Assert.Equal(StringValue.Create("b"), result.EnumerateAsync(context).Result.ElementAt(1));
            Assert.Equal(StringValue.Create("c"), result.EnumerateAsync(context).Result.ElementAt(2));
        }

        [Fact]
        public void ReverseString()
        {
            // Arrange
            var input = StringValue.Create("Fluid");
            var arguments = new FilterArguments();
            var context = new TemplateContext();

            // Act
            var result = ArrayFilters.Reverse(input, arguments, context);

            // Assert
            Assert.Equal(5, result.Result.EnumerateAsync(context).Result.Count());
            Assert.Equal(StringValue.Create("d"), result.Result.EnumerateAsync(context).Result.ElementAt(0));
            Assert.Equal(StringValue.Create("i"), result.Result.EnumerateAsync(context).Result.ElementAt(1));
            Assert.Equal(StringValue.Create("u"), result.Result.EnumerateAsync(context).Result.ElementAt(2));
            Assert.Equal(StringValue.Create("l"), result.Result.EnumerateAsync(context).Result.ElementAt(3));
            Assert.Equal(StringValue.Create("F"), result.Result.EnumerateAsync(context).Result.ElementAt(4));
        }

        [Fact]
        public void ReverseArray()
        {
            var input = ArrayValue.Create(new[] {
                StringValue.Create("a"),
                StringValue.Create("b"),
                StringValue.Create("c")
                });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = ArrayFilters.Reverse(input, arguments, context);

            Assert.Equal(3, result.Result.EnumerateAsync(context).Result.Count());
            Assert.Equal(StringValue.Create("c"), result.Result.EnumerateAsync(context).Result.ElementAt(0));
            Assert.Equal(StringValue.Create("b"), result.Result.EnumerateAsync(context).Result.ElementAt(1));
            Assert.Equal(StringValue.Create("a"), result.Result.EnumerateAsync(context).Result.ElementAt(2));
        }

        [Fact]
        public async Task Size()
        {
            var input = ArrayValue.Create(new[] {
                StringValue.Create("a"),
                StringValue.Create("b"),
                StringValue.Create("c")
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

            var input = ArrayValue.Create(new[] {
                new ObjectValue(new { Title = "c", Address = new { Zip = 2 } }),
                new ObjectValue(new { Title = "a", Address = new { Zip = 3 } }),
                new ObjectValue(new { Title = "b", Address = new { Zip = 1 } })
                });

            var arguments = new FilterArguments().Add(StringValue.Create("Title"));

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(sample.GetType(), "Title");

            var result = await ArrayFilters.Sort(input, arguments, context);

            Assert.Equal(3, result.EnumerateAsync(context).Result.Count());
            Assert.Equal("a", ((dynamic)result.EnumerateAsync(context).Result.ElementAt(0).ToObjectValue()).Title);
            Assert.Equal("b", ((dynamic)result.EnumerateAsync(context).Result.ElementAt(1).ToObjectValue()).Title);
            Assert.Equal("c", ((dynamic)result.EnumerateAsync(context).Result.ElementAt(2).ToObjectValue()).Title);

            arguments = new FilterArguments().Add(StringValue.Create("Address.Zip"));

            options = new TemplateOptions();
            context = new TemplateContext(options); 
            options.MemberAccessStrategy.Register(sample.GetType(), "Address");
            options.MemberAccessStrategy.Register(sample.Address.GetType(), "Zip");

            result = await ArrayFilters.Sort(input, arguments, context);

            Assert.Equal(3, result.EnumerateAsync(context).Result.Count());
            Assert.Equal("b", ((dynamic)result.EnumerateAsync(context).Result.ElementAt(0).ToObjectValue()).Title);
            Assert.Equal("c", ((dynamic)result.EnumerateAsync(context).Result.ElementAt(1).ToObjectValue()).Title);
            Assert.Equal("a", ((dynamic)result.EnumerateAsync(context).Result.ElementAt(2).ToObjectValue()).Title);
        }

        [Fact]
        public async Task SortWithoutArgument()
        {
            var input = ArrayValue.Create(new[] {
                StringValue.Create("c"),
                StringValue.Create("a"),
                StringValue.Create("B"),
                });

            var arguments = new FilterArguments();

            var context = new TemplateContext();

            var result = await ArrayFilters.Sort(input, arguments, context);

            Assert.Equal(3, result.EnumerateAsync(context).Result.Count());
            Assert.Equal("B", result.EnumerateAsync(context).Result.ElementAt(0).ToStringValue());
            Assert.Equal("a", result.EnumerateAsync(context).Result.ElementAt(1).ToStringValue());
            Assert.Equal("c", result.EnumerateAsync(context).Result.ElementAt(2).ToStringValue());
        }

        [Fact]
        public async Task SortNaturalWithoutArgument()
        {
            var input = ArrayValue.Create(new[] {
                StringValue.Create("c"),
                StringValue.Create("a"),
                StringValue.Create("B"),
                });

            var arguments = new FilterArguments();

            var context = new TemplateContext();

            var result = await ArrayFilters.SortNatural(input, arguments, context);

            Assert.Equal(3, result.EnumerateAsync(context).Result.Count());
            Assert.Equal("a", result.EnumerateAsync(context).Result.ElementAt(0).ToStringValue());
            Assert.Equal("B", result.EnumerateAsync(context).Result.ElementAt(1).ToStringValue());
            Assert.Equal("c", result.EnumerateAsync(context).Result.ElementAt(2).ToStringValue());
        }

        [Fact]
        public void Uniq()
        {
            var input = ArrayValue.Create(new[] {
                StringValue.Create("a"),
                StringValue.Create("b"),
                StringValue.Create("b")
                });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = ArrayFilters.Uniq(input, arguments, context);

            Assert.Equal(2, result.Result.EnumerateAsync(context).Result.Count());
        }

        [Fact]
        public async Task Where()
        {
            var input = ArrayValue.Create(new[] {
                new ObjectValue(new { Title = "a", Pinned = true }),
                new ObjectValue(new { Title = "b", Pinned = false }),
                new ObjectValue(new { Title = "c", Pinned = true })
                });

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(new { Title = "a", Pinned = true }.GetType());

            var arguments1 = new FilterArguments().Add(StringValue.Create("Pinned"));

            var result1 = await ArrayFilters.Where(input, arguments1, context);

            Assert.Equal(2, result1.EnumerateAsync(context).Result.Count());

            var arguments2 = new FilterArguments()
                .Add(StringValue.Create("Pinned"))
                .Add(BooleanValue.Create(false))
                ;

            var result2 = await ArrayFilters.Where(input, arguments2, context);

            Assert.Single(result2.EnumerateAsync(context).Result);

            var arguments3 = new FilterArguments()
                .Add(StringValue.Create("Title"))
                .Add(StringValue.Create("c"));

            var result3 = await  ArrayFilters.Where(input, arguments3, context);

            Assert.Single(result3.EnumerateAsync(context).Result);
        }

        [Fact]
        public async Task WhereShouldNotThrow()
        {
            var input = ArrayValue.Create(new[] {
                new ObjectValue(new { Title = "a", Pinned = true }),
                new ObjectValue(new { Title = "b", Pinned = false }),
                new ObjectValue(new { Title = "c", Pinned = true })
                });

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(new { Title = "a", Pinned = true }.GetType());

            var arguments1 = new FilterArguments().Add(StringValue.Create("a.b.c"));

            var result1 = await ArrayFilters.Where(input, arguments1, context);

            Assert.Empty(result1.EnumerateAsync(context).Result);
        }
    }
}
