using System.Collections.Generic;
using System.Linq;
using Fluid.Values;
using Fluid.Filters;
using Xunit;
using System.Threading.Tasks;
using Fluid.Tests.Extensibility;

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
        public async Task FirstEmptyArray()
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
        public async Task Concat()
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

            var result = await ArrayFilters.Concat(input, arguments, context);

            Assert.Equal(6, (await result.EnumerateAsync(context)).Count());
        }

        [Fact]
        public async Task ConcatSingleValue()
        {
            var input = new StringValue("a");

            var arguments = new FilterArguments().Add(
                new ArrayValue(new[] {
                    new StringValue("1"),
                    new StringValue("2"),
                    new StringValue("3")
                    })
            );

            var context = new TemplateContext();

            var result = await ArrayFilters.Concat(input, arguments, context);

            Assert.Equal("a", (await result.EnumerateAsync(context)).First().ToStringValue());
            Assert.Equal(4, (await result.EnumerateAsync(context)).Count());
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

            var enumerated = await result.EnumerateAsync(context);
            Assert.Equal(3, enumerated.Count());
            Assert.Equal(new StringValue("a"), enumerated.ElementAt(0));
            Assert.Equal(new StringValue("b"), enumerated.ElementAt(1));
            Assert.Equal(new StringValue("c"), enumerated.ElementAt(2));
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

            var enumerated = await result.EnumerateAsync(context);
            Assert.Equal(3, enumerated.Count());
            Assert.Equal(new StringValue("a"), enumerated.ElementAt(0));
            Assert.Equal(new StringValue("b"), enumerated.ElementAt(1));
            Assert.Equal(new StringValue("c"), enumerated.ElementAt(2));
        }

        [Fact]
        public async Task ReverseString()
        {
            // Arrange
            var input = new StringValue("Fluid");
            var arguments = new FilterArguments();
            var context = new TemplateContext();

            // Act
            var result = await ArrayFilters.Reverse(input, arguments, context);

            // Assert
            var enumerated = await result.EnumerateAsync(context);
            Assert.Equal(5, enumerated.Count());
            Assert.Equal(new StringValue("d"), enumerated.ElementAt(0));
            Assert.Equal(new StringValue("i"), enumerated.ElementAt(1));
            Assert.Equal(new StringValue("u"), enumerated.ElementAt(2));
            Assert.Equal(new StringValue("l"), enumerated.ElementAt(3));
            Assert.Equal(new StringValue("F"), enumerated.ElementAt(4));
        }

        [Fact]
        public async Task ReverseArray()
        {
            var input = new ArrayValue(new[] {
                new StringValue("a"),
                new StringValue("b"),
                new StringValue("c")
                });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await ArrayFilters.Reverse(input, arguments, context);

            var enumerated = await result.EnumerateAsync(context);
            Assert.Equal(3, enumerated.Count());
            Assert.Equal(new StringValue("c"), enumerated.ElementAt(0));
            Assert.Equal(new StringValue("b"), enumerated.ElementAt(1));
            Assert.Equal(new StringValue("a"), enumerated.ElementAt(2));
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

            var enumerated = await result.EnumerateAsync(context);
            Assert.Equal(3, enumerated.Count());
            Assert.Equal("a", ((dynamic)enumerated.ElementAt(0).ToObjectValue()).Title);
            Assert.Equal("b", ((dynamic)enumerated.ElementAt(1).ToObjectValue()).Title);
            Assert.Equal("c", ((dynamic)enumerated.ElementAt(2).ToObjectValue()).Title);

            arguments = new FilterArguments().Add(new StringValue("Address.Zip"));

            options = new TemplateOptions();
            context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(sample.GetType(), "Address");
            options.MemberAccessStrategy.Register(sample.Address.GetType(), "Zip");

            result = await ArrayFilters.Sort(input, arguments, context);

            enumerated = await result.EnumerateAsync(context);
            Assert.Equal(3, enumerated.Count());
            Assert.Equal("b", ((dynamic)enumerated.ElementAt(0).ToObjectValue()).Title);
            Assert.Equal("c", ((dynamic)enumerated.ElementAt(1).ToObjectValue()).Title);
            Assert.Equal("a", ((dynamic)enumerated.ElementAt(2).ToObjectValue()).Title);
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

            var enumerated = await result.EnumerateAsync(context);
            Assert.Equal(3, enumerated.Count());
            Assert.Equal("B", enumerated.ElementAt(0).ToStringValue());
            Assert.Equal("a", enumerated.ElementAt(1).ToStringValue());
            Assert.Equal("c", enumerated.ElementAt(2).ToStringValue());
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

            var enumerated = await result.EnumerateAsync(context);
            Assert.Equal(3, enumerated.Count());
            Assert.Equal("a", enumerated.ElementAt(0).ToStringValue());
            Assert.Equal("B", enumerated.ElementAt(1).ToStringValue());
            Assert.Equal("c", enumerated.ElementAt(2).ToStringValue());
        }

        [Fact]
        public async Task Uniq()
        {
            var input = new ArrayValue(new[] {
                new StringValue("a"),
                new StringValue("b"),
                new StringValue("b")
                });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await ArrayFilters.Uniq(input, arguments, context);

            Assert.Equal(2, (await result.EnumerateAsync(context)).Count());
        }

        [Fact]
        public async Task UniqWithArrays()
        {
            var input = new ArrayValue(new[] {
                new ArrayValue([ StringValue.Create("a"), StringValue.Create("b") ]),
                new ArrayValue([ StringValue.Create("a"), StringValue.Create("c") ]),
                new ArrayValue([ StringValue.Create("a"), StringValue.Create("c") ]),
                new ArrayValue([ StringValue.Create("b"), StringValue.Create("a") ]),
            });

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await ArrayFilters.Uniq(input, arguments, context);

            Assert.Equal(3, (await result.EnumerateAsync(context)).Count());
        }

        [Fact]
        public async Task UniqWithDictionaries()
        {
            var input = new ArrayValue([
                new DictionaryValue(new FluidValueDictionaryFluidIndexable(
                    new Dictionary<string, FluidValue>()
                    {
                        ["a"] = StringValue.Create("b"),
                        ["b"] = StringValue.Create("c"),
                    })),
                new DictionaryValue(new FluidValueDictionaryFluidIndexable(
                    new Dictionary<string, FluidValue>()
                    {
                        ["a"] = StringValue.Create("b"),
                        ["b"] = StringValue.Create("c"),
                    })),
                new DictionaryValue(new FluidValueDictionaryFluidIndexable(
                    new Dictionary<string, FluidValue>()
                    {
                        ["b"] = StringValue.Create("c"),
                        ["a"] = StringValue.Create("b"),
                    })),
                new DictionaryValue(new FluidValueDictionaryFluidIndexable(
                    new Dictionary<string, FluidValue>()
                    {
                        ["a"] = StringValue.Create("b"),
                        ["c"] = StringValue.Create("c"),
                    })),
                new DictionaryValue(new FluidValueDictionaryFluidIndexable(
                    new Dictionary<string, FluidValue>()
                    {
                        ["a"] = StringValue.Create("c"),
                        ["b"] = StringValue.Create("a"),
                    }))
            ]);

            var arguments = new FilterArguments();
            var context = new TemplateContext();

            var result = await ArrayFilters.Uniq(input, arguments, context);

            Assert.Equal(3, (await result.EnumerateAsync(context)).Count());
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

            Assert.Equal(2, (await result1.EnumerateAsync(context)).Count());

            var arguments2 = new FilterArguments()
                .Add(new StringValue("Pinned"))
                .Add(BooleanValue.Create(false))
                ;

            var result2 = await ArrayFilters.Where(input, arguments2, context);

            Assert.Single(await result2.EnumerateAsync(context));

            var arguments3 = new FilterArguments()
                .Add(new StringValue("Title"))
                .Add(new StringValue("c"));

            var result3 = await ArrayFilters.Where(input, arguments3, context);

            Assert.Single(await result3.EnumerateAsync(context));
        }

        [Fact]
        public async Task WhereWithTruthy()
        {
            var input = new ArrayValue(new[]
            {
                new ObjectValue(new { Title = "a", Pinned = true, Missing = 1 }),
                new ObjectValue(new { Title = "b", Pinned = false }),
                new ObjectValue(new { Title = "c", Pinned = true, Missing = 1 })
            });

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(new { Title = "a", Pinned = true }.GetType());
            options.MemberAccessStrategy.Register(new { Title = "a", Pinned = true, Missing = 1 }.GetType());

            // x | where: "Missing"

            var arguments1 = new FilterArguments().Add(new StringValue("Missing"));
            var result1 = await ArrayFilters.Where(input, arguments1, context);

            Assert.Equal(2, (await result1.EnumerateAsync(context)).Count());

            // x | where: "Missing", false

            var arguments2 = new FilterArguments()
                .Add(new StringValue("Missing"))
                .Add(BooleanValue.False);

            var result2 = await ArrayFilters.Where(input, arguments2, context);
            Assert.Single(await result2.EnumerateAsync(context));

            // x | where: "Title"

            var arguments3 = new FilterArguments()
                .Add(new StringValue("Title"));

            var result3 = await ArrayFilters.Where(input, arguments3, context);
            Assert.Equal(3, (await result3.EnumerateAsync(context)).Count());

            // x | where: "Missing", true

            var arguments4 = new FilterArguments()
                .Add(new StringValue("Missing"))
                .Add(BooleanValue.True);

            var result4 = await ArrayFilters.Where(input, arguments4, context);
            Assert.Equal(2, (await result4.EnumerateAsync(context)).Count());
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

            Assert.Empty(await result1.EnumerateAsync(context));
        }

        [Fact]
        public async Task Sum()
        {
            var sample = new { Value = 0 };

            var input = new ArrayValue(new[] {
                new ObjectValue(new { Value = 12  }),
                new ObjectValue(new { Value = 34 }),
                new ObjectValue(new { Value = 56 })
            });

            var arguments = new FilterArguments().Add(new StringValue("Value"));

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(sample.GetType(), "Value");

            var result = await ArrayFilters.Sum(input, arguments, context);

            Assert.Equal(102, result.ToNumberValue());
        }

        [Fact]
        public async Task SumWithoutArgument()
        {
            var input = new ArrayValue(new[] {
                NumberValue.Create(12),
                NumberValue.Create(34),
                NumberValue.Create(56)
            });

            var options = new TemplateOptions();
            var context = new TemplateContext(options);

            var result = await ArrayFilters.Sum(input, new FilterArguments(), context);

            Assert.Equal(102, result.ToNumberValue());
        }

        [Fact]
        public async Task SumWithNumericStrings()
        {
            var input = new ArrayValue(new FluidValue[] {
                NumberValue.Create(1),
                NumberValue.Create(2),
                StringValue.Create("3"),
                StringValue.Create("4")
            });

            var options = new TemplateOptions();
            var context = new TemplateContext(options);

            var result = await ArrayFilters.Sum(input, new FilterArguments(), context);

            Assert.Equal(10, result.ToNumberValue());
        }

        [Fact]
        public async Task SumWithNestedArrays()
        {
            var input = new ArrayValue(new FluidValue[] {
                NumberValue.Create(1),
                new ArrayValue(new FluidValue[]
                {
                    NumberValue.Create(2),
                    new ArrayValue(new FluidValue[]
                    {
                        NumberValue.Create(3),
                        NumberValue.Create(4)
                    })
                })
            });

            var options = new TemplateOptions();
            var context = new TemplateContext(options);

            var result = await ArrayFilters.Sum(input, new FilterArguments(), context);

            Assert.Equal(10, result.ToNumberValue());
        }

        [Fact]
        public async Task SumWithMixedValues()
        {
            var input = new ArrayValue(new FluidValue[] {
                NumberValue.Create(1),
                BooleanValue.True,
                NilValue.Instance,
                new ObjectValue(new { Value = 12  })
            });

            var options = new TemplateOptions();
            var context = new TemplateContext(options);

            var result = await ArrayFilters.Sum(input, new FilterArguments(), context);

            Assert.Equal(1, result.ToNumberValue());
        }

        [Fact]
        public void SumWithoutArgumentRender()
        {
            var options = new TemplateOptions();
            var context = new TemplateContext(options);

            context.SetValue("foo", new[] { 1m });
            var parser = new CustomParser();

            var template = parser.Parse("{{ foo | sum }}");
            template.Render(context);
        }

        [Fact]
        public void SumWithArgumentRender()
        {
            var options = new TemplateOptions();
            var context = new TemplateContext(options);

            context.SetValue("foo", new[] { new { Quantity = 1 } });
            var parser = new CustomParser();

            var template = parser.Parse("{{ foo | sum: 'Quantity' }}");
            template.Render(context);
        }

        [Fact]
        public async Task SumWithDecimals()
        {
            var input = new ArrayValue(new FluidValue[] {
                NumberValue.Create(0.1m),
                NumberValue.Create(0.2m),
                NumberValue.Create(-0.3m)
            });

            var options = new TemplateOptions();
            var context = new TemplateContext(options);

            var result = await ArrayFilters.Sum(input, new FilterArguments(), context);

            Assert.Equal(0.0m, result.ToNumberValue());
        }

        [Fact]
        public async Task SumWithDecimalStrings()
        {
            var input = new ArrayValue(new FluidValue[] {
                NumberValue.Create(0.1m),
                StringValue.Create("0.2"),
                StringValue.Create("0.3")
            });

            var options = new TemplateOptions();
            var context = new TemplateContext(options);

            var result = await ArrayFilters.Sum(input, new FilterArguments(), context);

            Assert.Equal(0.6m, result.ToNumberValue());
        }

        [Fact]
        public async Task SumResultingInNegativeDecimal()
        {
            var input = new ArrayValue(new FluidValue[] {
                NumberValue.Create(0.1m),
                NumberValue.Create(-0.2m),
                NumberValue.Create(-0.3m)
            });

            var options = new TemplateOptions();
            var context = new TemplateContext(options);

            var result = await ArrayFilters.Sum(input, new FilterArguments(), context);

            Assert.Equal(-0.4m, result.ToNumberValue());
        }

        [Theory]
        [InlineData("", 0.0)]
        [InlineData("Quantity", 1.2)]
        [InlineData("Weight", 0.1)]
        [InlineData("Subtotal", 0.0)]
        public async Task SumWithDecimalsAndArguments(string filterArgument, decimal expectedValue)
        {
            var quantityAndWeightObjectType = new
            {
                Quantity = (decimal)0,
                Weight = (decimal)0
            };

            var quantityObjectType = new
            {
                Quantity = (decimal)0
            };

            var weightObjectType = new
            {
                Weight = (decimal)0
            };

            var input = new ArrayValue(new FluidValue[]
            {
                new ObjectValue(new { Quantity = 1m }),
                new ObjectValue(new { Quantity = 0.2m, Weight = -0.3m }),
                new ObjectValue(new { Weight = 0.4m }),
            });

            var arguments = new FilterArguments().Add(new StringValue(filterArgument));

            var options = new TemplateOptions();
            var context = new TemplateContext(options);

            options.MemberAccessStrategy.Register(quantityObjectType.GetType(), filterArgument);
            options.MemberAccessStrategy.Register(weightObjectType.GetType(), filterArgument);
            options.MemberAccessStrategy.Register(quantityAndWeightObjectType.GetType(), filterArgument);

            var result = await ArrayFilters.Sum(input, arguments, context);

            Assert.Equal(expectedValue, result.ToNumberValue());
        }

        [Fact]
        public async Task Reject()
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

            var result1 = await ArrayFilters.Reject(input, arguments1, context);

            Assert.Single(await result1.EnumerateAsync(context));

            var arguments2 = new FilterArguments()
                .Add(new StringValue("Pinned"))
                .Add(BooleanValue.Create(false))
                ;

            var result2 = await ArrayFilters.Reject(input, arguments2, context);

            Assert.Equal(2, (await result2.EnumerateAsync(context)).Count());

            var arguments3 = new FilterArguments()
                .Add(new StringValue("Title"))
                .Add(new StringValue("c"));

            var result3 = await ArrayFilters.Reject(input, arguments3, context);

            Assert.Equal(2, (await result3.EnumerateAsync(context)).Count());
        }

        [Fact]
        public async Task Find()
        {
            var input = new ArrayValue(new[] {
                new ObjectValue(new { Title = "a", Pinned = true }),
                new ObjectValue(new { Title = "b", Pinned = false }),
                new ObjectValue(new { Title = "c", Pinned = true })
                });

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(new { Title = "a", Pinned = true }.GetType());

            var arguments1 = new FilterArguments().Add(new StringValue("Pinned")).Add(BooleanValue.True);

            var result1 = await ArrayFilters.Find(input, arguments1, context);

            Assert.Equal(input.Values[0], result1);

            var arguments2 = new FilterArguments()
                .Add(new StringValue("Pinned"))
                .Add(BooleanValue.Create(false))
                ;

            var result2 = await ArrayFilters.Find(input, arguments2, context);

            Assert.Equal(input.Values[1], result2);

            var arguments3 = new FilterArguments()
                .Add(new StringValue("Title"))
                .Add(new StringValue("c"));

            var result3 = await ArrayFilters.Find(input, arguments3, context);

            Assert.Equal(input.Values[2], result3);

            var arguments4 = new FilterArguments();

            var result4 = await ArrayFilters.Find(input, arguments4, context);

            Assert.Equal(NilValue.Instance, result4);

            var arguments5 = new FilterArguments()
                .Add(new StringValue("Title"))
                .Add(new StringValue("d"));

            var result5 = await ArrayFilters.Find(input, arguments5, context);

            Assert.Equal(NilValue.Instance, result5);
        }

        [Fact]
        public async Task FindIndex()
        {
            var input = new ArrayValue(new[] {
                new ObjectValue(new { Title = "a", Pinned = true }),
                new ObjectValue(new { Title = "b", Pinned = false }),
                new ObjectValue(new { Title = "c", Pinned = true })
                });

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(new { Title = "a", Pinned = true }.GetType());

            var arguments1 = new FilterArguments().Add(new StringValue("Pinned")).Add(BooleanValue.True);

            var result1 = await ArrayFilters.FindIndex(input, arguments1, context);

            Assert.Equal(0, result1.ToNumberValue());

            var arguments2 = new FilterArguments()
                .Add(new StringValue("Pinned"))
                .Add(BooleanValue.Create(false))
                ;

            var result2 = await ArrayFilters.FindIndex(input, arguments2, context);

            Assert.Equal(1, result2.ToNumberValue());

            var arguments3 = new FilterArguments()
                .Add(new StringValue("Title"))
                .Add(new StringValue("c"));

            var result3 = await ArrayFilters.FindIndex(input, arguments3, context);

            Assert.Equal(2, result3.ToNumberValue());

            var arguments4 = new FilterArguments();

            var result4 = await ArrayFilters.FindIndex(input, arguments4, context);

            Assert.Equal(NilValue.Instance, result4);

            var arguments5 = new FilterArguments()
                .Add(new StringValue("Title"))
                .Add(new StringValue("d"));

            var result5 = await ArrayFilters.FindIndex(input, arguments5, context);

            Assert.Equal(NilValue.Instance, result5);
        }

        [Fact]
        public async Task Has()
        {
            var input = new ArrayValue(new[] {
                new ObjectValue(new { Title = "a", Pinned = true }),
                new ObjectValue(new { Title = "b", Pinned = false }),
                new ObjectValue(new { Title = "c", Pinned = true })
                });

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MemberAccessStrategy.Register(new { Title = "a", Pinned = true }.GetType());

            var arguments1 = new FilterArguments().Add(new StringValue("Pinned")).Add(BooleanValue.True);

            var result1 = await ArrayFilters.Has(input, arguments1, context);

            Assert.Equal(BooleanValue.True, result1);

            var arguments2 = new FilterArguments()
                .Add(new StringValue("Pinned"))
                .Add(BooleanValue.Create(false))
                ;

            var result2 = await ArrayFilters.Has(input, arguments2, context);

            Assert.Equal(BooleanValue.True, result2);

            var arguments3 = new FilterArguments()
                .Add(new StringValue("Title"))
                .Add(new StringValue("c"));

            var result3 = await ArrayFilters.Has(input, arguments3, context);

            Assert.Equal(BooleanValue.True, result3);

            var arguments4 = new FilterArguments();

            var result4 = await ArrayFilters.Has(input, arguments4, context);

            Assert.Equal(BooleanValue.False, result4);

            var arguments5 = new FilterArguments()
                .Add(new StringValue("Title"))
                .Add(new StringValue("d"));

            var result5 = await ArrayFilters.Has(input, arguments5, context);

            Assert.Equal(BooleanValue.False, result5);
        }

        [Fact]
        public async Task FindIndex_OnEmptyArray_ReturnsNil()
        {
            // Arrange
            var input = new ArrayValue([]); // Empty array
            var arguments = new FilterArguments()
                .Add(new StringValue("foo"))
                .Add(new StringValue("bar"));
            var context = new TemplateContext();

            // Act
            var result = await ArrayFilters.FindIndex(input, arguments, context);

            // Assert
            Assert.IsType<NilValue>(result);
        }

        [Fact]
        public async Task Has_OnEmptyArray_ReturnsFalse()
        {
            // Arrange
            var input = new ArrayValue([]); // Empty array
            var arguments = new FilterArguments()
                .Add(new StringValue("foo"))
                .Add(new StringValue("bar"));
            var context = new TemplateContext();

            // Act
            var result = await ArrayFilters.Has(input, arguments, context);

            // Assert
            Assert.Equal(BooleanValue.False, result);
        }
    }
}
