using System.Collections.Generic;
using System.Linq;
using Fluid.Values;
using Fluid.Filters;
using Xunit;
using System.Threading.Tasks;
using Fluid.Tests.Extensibility;

namespace Fluid.Tests;

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

        Assert.Equal("a, b, c", result.Result.ToStringValue(context));
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
    public async Task FirstEmptyArray()
    {
        var input = ArrayValue.Create([]);

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
        var input = ArrayValue.Create([]);

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = await ArrayFilters.Last(input, arguments, context);

        Assert.IsType<NilValue>(result);
    }

    [Fact]
    public async Task FirstAndLast_DoNotCountAStepPerItem()
    {
        var input = FluidValue.Create(Enumerable.Range(1, 1000).ToArray(), TemplateOptions.Default);

        var arguments = new FilterArguments();
        var context = new TemplateContext { MaxSteps = 10 };

        Assert.Equal(NumberValue.Create(1), await ArrayFilters.First(input, arguments, context));
        Assert.Equal(NumberValue.Create(1000), await ArrayFilters.Last(input, arguments, context));
    }

    [Fact]
    public async Task FirstAndLast_OnEnumerable()
    {
        // Not a list, so it is wrapped as an enumerable object rather than an array value.
        var input = FluidValue.Create(Enumerable.Range(1, 3).Select(x => x * 10), TemplateOptions.Default);

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        Assert.Equal(NumberValue.Create(10), await ArrayFilters.First(input, arguments, context));
        Assert.Equal(NumberValue.Create(30), await ArrayFilters.Last(input, arguments, context));
    }

    [Fact]
    public async Task Concat()
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

        var result = await ArrayFilters.Concat(input, arguments, context);

        Assert.Equal(6, await result.EnumerateAsync(context).CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcatFlattensOnlyTheInput()
    {
        // Liquid flattens the input but keeps nested arrays in the argument as items.
        var input = ArrayValue.Create(new FluidValue[] {
            StringValue.Create("a"),
            ArrayValue.Create(new[] { StringValue.Create("b") })
            });

        var arguments = new FilterArguments().Add(
            ArrayValue.Create(new FluidValue[] {
                StringValue.Create("1"),
                ArrayValue.Create(new[] { StringValue.Create("2"), StringValue.Create("3") })
                })
        );

        var context = new TemplateContext();

        var result = (ArrayValue)await ArrayFilters.Concat(input, arguments, context);

        Assert.Equal(4, result.Values.Count);
        Assert.Equal("b", result.Values[1].ToStringValue(context));
        Assert.Equal(FluidValues.Array, result.Values[3].Type);
        Assert.Equal("23", result.Values[3].ToStringValue(context));
    }

    [Fact]
    public async Task ConcatSingleValue()
    {
        var input = StringValue.Create("a");

        var arguments = new FilterArguments().Add(
            ArrayValue.Create(new[] {
                StringValue.Create("1"),
                StringValue.Create("2"),
                StringValue.Create("3")
                })
        );

        var context = new TemplateContext();

        var result = await ArrayFilters.Concat(input, arguments, context);

        Assert.Equal("a", (await result.EnumerateAsync(context).FirstAsync(cancellationToken: TestContext.Current.CancellationToken)).ToStringValue(context));
        Assert.Equal(4, await result.EnumerateAsync(context).CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Map()
    {
        var input = ArrayValue.Create(new[] {
            ObjectValue.Create(new { Title = "a" }),
            ObjectValue.Create(new { Title = "b" }),
            ObjectValue.Create(new { Title = "c" })
            });

        var arguments = new FilterArguments().Add(StringValue.Create("Title"));

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var result = await ArrayFilters.Map(input, arguments, context);

        var enumerated = await result.EnumerateAsync(context).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, enumerated.Count());
        Assert.Equal(StringValue.Create("a"), enumerated.ElementAt(0));
        Assert.Equal(StringValue.Create("b"), enumerated.ElementAt(1));
        Assert.Equal(StringValue.Create("c"), enumerated.ElementAt(2));
    }

    [Fact]
    public async Task Map_DeepProperties()
    {
        var sample = new { Title = new { Text = "a" } };
        var input = ArrayValue.Create(new[] {
            ObjectValue.Create(new { Title = new { Text = "a" }}),
            ObjectValue.Create(new { Title = new { Text = "b" }}),
            ObjectValue.Create(new { Title = new { Text = "c" }})
            });

        var arguments = new FilterArguments().Add(StringValue.Create("Title.Text"));

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var result = await ArrayFilters.Map(input, arguments, context);

        var enumerated = await result.EnumerateAsync(context).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, enumerated.Count());
        Assert.Equal(StringValue.Create("a"), enumerated.ElementAt(0));
        Assert.Equal(StringValue.Create("b"), enumerated.ElementAt(1));
        Assert.Equal(StringValue.Create("c"), enumerated.ElementAt(2));
    }

    [Fact]
    public async Task ReverseString()
    {
        // Arrange
        var input = StringValue.Create("Fluid");
        var arguments = new FilterArguments();
        var context = new TemplateContext();

        // Act
        var result = await ArrayFilters.Reverse(input, arguments, context);

        // Assert
        var enumerated = await result.EnumerateAsync(context).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(5, enumerated.Count());
        Assert.Equal(StringValue.Create("d"), enumerated.ElementAt(0));
        Assert.Equal(StringValue.Create("i"), enumerated.ElementAt(1));
        Assert.Equal(StringValue.Create("u"), enumerated.ElementAt(2));
        Assert.Equal(StringValue.Create("l"), enumerated.ElementAt(3));
        Assert.Equal(StringValue.Create("F"), enumerated.ElementAt(4));
    }

    [Fact]
    public async Task ReverseArray()
    {
        var input = ArrayValue.Create(new[] {
            StringValue.Create("a"),
            StringValue.Create("b"),
            StringValue.Create("c")
            });

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = await ArrayFilters.Reverse(input, arguments, context);

        var enumerated = await result.EnumerateAsync(context).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, enumerated.Count());
        Assert.Equal(StringValue.Create("c"), enumerated.ElementAt(0));
        Assert.Equal(StringValue.Create("b"), enumerated.ElementAt(1));
        Assert.Equal(StringValue.Create("a"), enumerated.ElementAt(2));
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
            ObjectValue.Create(new { Title = "c", Address = new { Zip = 2 } }),
            ObjectValue.Create(new { Title = "a", Address = new { Zip = 3 } }),
            ObjectValue.Create(new { Title = "b", Address = new { Zip = 1 } })
            });

        var arguments = new FilterArguments().Add(StringValue.Create("Title"));

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var result = await ArrayFilters.Sort(input, arguments, context);

        var enumerated = await result.EnumerateAsync(context).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, enumerated.Count());
        Assert.Equal("a", ((dynamic)enumerated.ElementAt(0).ToObjectValue(context)).Title);
        Assert.Equal("b", ((dynamic)enumerated.ElementAt(1).ToObjectValue(context)).Title);
        Assert.Equal("c", ((dynamic)enumerated.ElementAt(2).ToObjectValue(context)).Title);

        arguments = new FilterArguments().Add(StringValue.Create("Address.Zip"));

        options = new TemplateOptionsBuilder().Build();
        context = new TemplateContext(options);

        result = await ArrayFilters.Sort(input, arguments, context);

        enumerated = await result.EnumerateAsync(context).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, enumerated.Count());
        Assert.Equal("b", ((dynamic)enumerated.ElementAt(0).ToObjectValue(context)).Title);
        Assert.Equal("c", ((dynamic)enumerated.ElementAt(1).ToObjectValue(context)).Title);
        Assert.Equal("a", ((dynamic)enumerated.ElementAt(2).ToObjectValue(context)).Title);
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

        var enumerated = await result.EnumerateAsync(context).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, enumerated.Count());
        Assert.Equal("B", enumerated.ElementAt(0).ToStringValue(context));
        Assert.Equal("a", enumerated.ElementAt(1).ToStringValue(context));
        Assert.Equal("c", enumerated.ElementAt(2).ToStringValue(context));
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

        var enumerated = await result.EnumerateAsync(context).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, enumerated.Count());
        Assert.Equal("a", enumerated.ElementAt(0).ToStringValue(context));
        Assert.Equal("B", enumerated.ElementAt(1).ToStringValue(context));
        Assert.Equal("c", enumerated.ElementAt(2).ToStringValue(context));
    }

    [Fact]
    public async Task Uniq()
    {
        var input = ArrayValue.Create(new[] {
            StringValue.Create("a"),
            StringValue.Create("b"),
            StringValue.Create("b")
            });

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = await ArrayFilters.Uniq(input, arguments, context);

        Assert.Equal(2, await result.EnumerateAsync(context).CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UniqWithArrays()
    {
        var input = ArrayValue.Create(new[] {
            ArrayValue.Create([ StringValue.Create("a"), StringValue.Create("b") ]),
            ArrayValue.Create([ StringValue.Create("a"), StringValue.Create("c") ]),
            ArrayValue.Create([ StringValue.Create("a"), StringValue.Create("c") ]),
            ArrayValue.Create([ StringValue.Create("b"), StringValue.Create("a") ]),
        });

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = await ArrayFilters.Uniq(input, arguments, context);

        Assert.Equal(3, await result.EnumerateAsync(context).CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UniqWithDictionaries()
    {
        var input = ArrayValue.Create([
            DictionaryValue.Create(new FluidValueDictionaryFluidIndexable(
                new Dictionary<string, FluidValue>()
                {
                    ["a"] = StringValue.Create("b"),
                    ["b"] = StringValue.Create("c"),
                })),
            DictionaryValue.Create(new FluidValueDictionaryFluidIndexable(
                new Dictionary<string, FluidValue>()
                {
                    ["a"] = StringValue.Create("b"),
                    ["b"] = StringValue.Create("c"),
                })),
            DictionaryValue.Create(new FluidValueDictionaryFluidIndexable(
                new Dictionary<string, FluidValue>()
                {
                    ["b"] = StringValue.Create("c"),
                    ["a"] = StringValue.Create("b"),
                })),
            DictionaryValue.Create(new FluidValueDictionaryFluidIndexable(
                new Dictionary<string, FluidValue>()
                {
                    ["a"] = StringValue.Create("b"),
                    ["c"] = StringValue.Create("c"),
                })),
            DictionaryValue.Create(new FluidValueDictionaryFluidIndexable(
                new Dictionary<string, FluidValue>()
                {
                    ["a"] = StringValue.Create("c"),
                    ["b"] = StringValue.Create("a"),
                }))
        ]);

        var arguments = new FilterArguments();
        var context = new TemplateContext();

        var result = await ArrayFilters.Uniq(input, arguments, context);

        Assert.Equal(3, await result.EnumerateAsync(context).CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Where()
    {
        var input = ArrayValue.Create(new[] {
            ObjectValue.Create(new { Title = "a", Pinned = true }),
            ObjectValue.Create(new { Title = "b", Pinned = false }),
            ObjectValue.Create(new { Title = "c", Pinned = true })
            });

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var arguments1 = new FilterArguments().Add(StringValue.Create("Pinned"));

        var result1 = await ArrayFilters.Where(input, arguments1, context);

        Assert.Equal(2, await result1.EnumerateAsync(context).CountAsync(cancellationToken: TestContext.Current.CancellationToken));

        var arguments2 = new FilterArguments()
            .Add(StringValue.Create("Pinned"))
            .Add(BooleanValue.Create(false))
            ;

        var result2 = await ArrayFilters.Where(input, arguments2, context);

        Assert.Single(await result2.EnumerateAsync(context).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));

        var arguments3 = new FilterArguments()
            .Add(StringValue.Create("Title"))
            .Add(StringValue.Create("c"));

        var result3 = await ArrayFilters.Where(input, arguments3, context);

        Assert.Single(await result3.EnumerateAsync(context).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhereWithTruthy()
    {
        var input = ArrayValue.Create(new[]
        {
            ObjectValue.Create(new { Title = "a", Pinned = true, Missing = 1 }),
            ObjectValue.Create(new { Title = "b", Pinned = false }),
            ObjectValue.Create(new { Title = "c", Pinned = true, Missing = 1 })
        });

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        // x | where: "Missing"
        // No comparand so check for truthiness, 1 is truthy so both items with Missing=1 are returned

        var arguments1 = new FilterArguments().Add(StringValue.Create("Missing"));
        var result1 = await ArrayFilters.Where(input, arguments1, context);

        Assert.Equal(2, await result1.EnumerateAsync(context).CountAsync(cancellationToken: TestContext.Current.CancellationToken));

        // x | where: "Missing", false
        // 0 since a NumberValue is never equal to a boolean

        var arguments2 = new FilterArguments()
            .Add(StringValue.Create("Missing"))
            .Add(BooleanValue.False);

        var result2 = await ArrayFilters.Where(input, arguments2, context);
        Assert.Empty(await result2.EnumerateAsync(context).ToListAsync());

        // x | where: "Title"

        var arguments3 = new FilterArguments()
            .Add(StringValue.Create("Title"));

        var result3 = await ArrayFilters.Where(input, arguments3, context);
        Assert.Equal(3, await result3.EnumerateAsync(context).CountAsync(cancellationToken: TestContext.Current.CancellationToken));

        // x | where: "Missing", 1

        var arguments4 = new FilterArguments()
            .Add(StringValue.Create("Missing"))
            .Add(NumberValue.Create(1));

        var result4 = await ArrayFilters.Where(input, arguments4, context);
        Assert.Equal(2, await result4.EnumerateAsync(context).CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhereShouldNotThrow()
    {
        var input = ArrayValue.Create(new[] {
            ObjectValue.Create(new { Title = "a", Pinned = true }),
            ObjectValue.Create(new { Title = "b", Pinned = false }),
            ObjectValue.Create(new { Title = "c", Pinned = true })
            });

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var arguments1 = new FilterArguments().Add(StringValue.Create("a.b.c"));

        var result1 = await ArrayFilters.Where(input, arguments1, context);

        Assert.Empty(await result1.EnumerateAsync(context).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Sum()
    {
        var sample = new { Value = 0 };

        var input = ArrayValue.Create(new[] {
            ObjectValue.Create(new { Value = 12  }),
            ObjectValue.Create(new { Value = 34 }),
            ObjectValue.Create(new { Value = 56 })
        });

        var arguments = new FilterArguments().Add(StringValue.Create("Value"));

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);
        //options.MemberAccessStrategy.Register(sample.GetType(), "Value");

        var result = await ArrayFilters.Sum(input, arguments, context);

        Assert.Equal(102, result.ToNumberValue(context));
    }

    [Fact]
    public async Task SumWithoutArgument()
    {
        var input = ArrayValue.Create(new[] {
            NumberValue.Create(12),
            NumberValue.Create(34),
            NumberValue.Create(56)
        });

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var result = await ArrayFilters.Sum(input, new FilterArguments(), context);

        Assert.Equal(102, result.ToNumberValue(context));
    }

    [Fact]
    public async Task SumWithNumericStrings()
    {
        var input = ArrayValue.Create(new FluidValue[] {
            NumberValue.Create(1),
            NumberValue.Create(2),
            StringValue.Create("3"),
            StringValue.Create("4")
        });

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var result = await ArrayFilters.Sum(input, new FilterArguments(), context);

        Assert.Equal(10, result.ToNumberValue(context));
    }

    [Fact]
    public async Task SumWithNestedArrays()
    {
        var input = ArrayValue.Create(new FluidValue[] {
            NumberValue.Create(1),
            ArrayValue.Create(new FluidValue[]
            {
                NumberValue.Create(2),
                ArrayValue.Create(new FluidValue[]
                {
                    NumberValue.Create(3),
                    NumberValue.Create(4)
                })
            })
        });

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var result = await ArrayFilters.Sum(input, new FilterArguments(), context);

        Assert.Equal(10, result.ToNumberValue(context));
    }

    [Fact]
    public async Task SumWithNestedCollections()
    {
        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);
        var input = FluidValue.Create(new List<List<int>> { new() { 1, 2 }, new() { 3 } }, options);

        var result = await ArrayFilters.Sum(input, new FilterArguments(), context);

        Assert.Equal(6, result.ToNumberValue(context));
    }

    [Fact]
    public async Task SumWithMixedValues()
    {
        var input = ArrayValue.Create(new FluidValue[] {
            NumberValue.Create(1),
            BooleanValue.True,
            NilValue.Instance,
            ObjectValue.Create(new { Value = 12  })
        });

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var result = await ArrayFilters.Sum(input, new FilterArguments(), context);

        Assert.Equal(1, result.ToNumberValue(context));
    }

    [Fact]
    public void SumWithoutArgumentRender()
    {
        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        context.SetValue("foo", new[] { 1m });
        var parser = new CustomParser();

        var template = parser.Parse("{{ foo | sum }}");
        template.Render(context);
    }

    [Fact]
    public void SumWithArgumentRender()
    {
        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        context.SetValue("foo", new[] { new { Quantity = 1 } });
        var parser = new CustomParser();

        var template = parser.Parse("{{ foo | sum: 'Quantity' }}");
        template.Render(context);
    }

    [Fact]
    public async Task SumWithDecimals()
    {
        var input = ArrayValue.Create(new FluidValue[] {
            NumberValue.Create(0.1m),
            NumberValue.Create(0.2m),
            NumberValue.Create(-0.3m)
        });

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var result = await ArrayFilters.Sum(input, new FilterArguments(), context);

        Assert.Equal(0.0m, result.ToNumberValue(context));
    }

    [Fact]
    public async Task SumWithDecimalStrings()
    {
        var input = ArrayValue.Create(new FluidValue[] {
            NumberValue.Create(0.1m),
            StringValue.Create("0.2"),
            StringValue.Create("0.3")
        });

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var result = await ArrayFilters.Sum(input, new FilterArguments(), context);

        Assert.Equal(0.6m, result.ToNumberValue(context));
    }

    [Fact]
    public async Task SumResultingInNegativeDecimal()
    {
        var input = ArrayValue.Create(new FluidValue[] {
            NumberValue.Create(0.1m),
            NumberValue.Create(-0.2m),
            NumberValue.Create(-0.3m)
        });

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var result = await ArrayFilters.Sum(input, new FilterArguments(), context);

        Assert.Equal(-0.4m, result.ToNumberValue(context));
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

        var input = ArrayValue.Create(new FluidValue[]
        {
            ObjectValue.Create(new { Quantity = 1m }),
            ObjectValue.Create(new { Quantity = 0.2m, Weight = -0.3m }),
            ObjectValue.Create(new { Weight = 0.4m }),
        });

        var arguments = new FilterArguments().Add(StringValue.Create(filterArgument));

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        //options.MemberAccessStrategy.Register(quantityObjectType.GetType(), filterArgument);
        //options.MemberAccessStrategy.Register(weightObjectType.GetType(), filterArgument);
        //options.MemberAccessStrategy.Register(quantityAndWeightObjectType.GetType(), filterArgument);

        var result = await ArrayFilters.Sum(input, arguments, context);

        Assert.Equal(expectedValue, result.ToNumberValue(context));
    }

    [Fact]
    public async Task Reject()
    {
        var input = ArrayValue.Create(new[] {
            ObjectValue.Create(new { Title = "a", Pinned = true }),
            ObjectValue.Create(new { Title = "b", Pinned = false }),
            ObjectValue.Create(new { Title = "c", Pinned = true })
            });

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var arguments1 = new FilterArguments().Add(StringValue.Create("Pinned"));

        var result1 = await ArrayFilters.Reject(input, arguments1, context);

        Assert.Single(await result1.EnumerateAsync(context).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));

        var arguments2 = new FilterArguments()
            .Add(StringValue.Create("Pinned"))
            .Add(BooleanValue.Create(false))
            ;

        var result2 = await ArrayFilters.Reject(input, arguments2, context);

        Assert.Equal(2, await result2.EnumerateAsync(context).CountAsync(cancellationToken: TestContext.Current.CancellationToken));

        var arguments3 = new FilterArguments()
            .Add(StringValue.Create("Title"))
            .Add(StringValue.Create("c"));

        var result3 = await ArrayFilters.Reject(input, arguments3, context);

        Assert.Equal(2, await result3.EnumerateAsync(context).CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Find()
    {
        var input = ArrayValue.Create(new[] {
            ObjectValue.Create(new { Title = "a", Pinned = true }),
            ObjectValue.Create(new { Title = "b", Pinned = false }),
            ObjectValue.Create(new { Title = "c", Pinned = true })
            });

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var arguments1 = new FilterArguments().Add(StringValue.Create("Pinned")).Add(BooleanValue.True);

        var result1 = await ArrayFilters.Find(input, arguments1, context);

        Assert.Equal(input.Values[0], result1);

        var arguments2 = new FilterArguments()
            .Add(StringValue.Create("Pinned"))
            .Add(BooleanValue.Create(false))
            ;

        var result2 = await ArrayFilters.Find(input, arguments2, context);

        Assert.Equal(input.Values[1], result2);

        var arguments3 = new FilterArguments()
            .Add(StringValue.Create("Title"))
            .Add(StringValue.Create("c"));

        var result3 = await ArrayFilters.Find(input, arguments3, context);

        Assert.Equal(input.Values[2], result3);

        var arguments4 = new FilterArguments();

        var result4 = await ArrayFilters.Find(input, arguments4, context);

        Assert.Equal(NilValue.Instance, result4);

        var arguments5 = new FilterArguments()
            .Add(StringValue.Create("Title"))
            .Add(StringValue.Create("d"));

        var result5 = await ArrayFilters.Find(input, arguments5, context);

        Assert.Equal(NilValue.Instance, result5);
    }

    [Fact]
    public async Task FindIndex()
    {
        var input = ArrayValue.Create(new[] {
            ObjectValue.Create(new { Title = "a", Pinned = true }),
            ObjectValue.Create(new { Title = "b", Pinned = false }),
            ObjectValue.Create(new { Title = "c", Pinned = true })
            });

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var arguments1 = new FilterArguments().Add(StringValue.Create("Pinned")).Add(BooleanValue.True);

        var result1 = await ArrayFilters.FindIndex(input, arguments1, context);

        Assert.Equal(0, result1.ToNumberValue(context));

        var arguments2 = new FilterArguments()
            .Add(StringValue.Create("Pinned"))
            .Add(BooleanValue.Create(false))
            ;

        var result2 = await ArrayFilters.FindIndex(input, arguments2, context);

        Assert.Equal(1, result2.ToNumberValue(context));

        var arguments3 = new FilterArguments()
            .Add(StringValue.Create("Title"))
            .Add(StringValue.Create("c"));

        var result3 = await ArrayFilters.FindIndex(input, arguments3, context);

        Assert.Equal(2, result3.ToNumberValue(context));

        var arguments4 = new FilterArguments();

        var result4 = await ArrayFilters.FindIndex(input, arguments4, context);

        Assert.Equal(NilValue.Instance, result4);

        var arguments5 = new FilterArguments()
            .Add(StringValue.Create("Title"))
            .Add(StringValue.Create("d"));

        var result5 = await ArrayFilters.FindIndex(input, arguments5, context);

        Assert.Equal(NilValue.Instance, result5);
    }

    [Fact]
    public async Task Has()
    {
        var input = ArrayValue.Create(new[] {
            ObjectValue.Create(new { Title = "a", Pinned = true }),
            ObjectValue.Create(new { Title = "b", Pinned = false }),
            ObjectValue.Create(new { Title = "c", Pinned = true })
            });

        var options = new TemplateOptionsBuilder().Build();
        var context = new TemplateContext(options);

        var arguments1 = new FilterArguments().Add(StringValue.Create("Pinned")).Add(BooleanValue.True);

        var result1 = await ArrayFilters.Has(input, arguments1, context);

        Assert.Equal(BooleanValue.True, result1);

        var arguments2 = new FilterArguments()
            .Add(StringValue.Create("Pinned"))
            .Add(BooleanValue.Create(false))
            ;

        var result2 = await ArrayFilters.Has(input, arguments2, context);

        Assert.Equal(BooleanValue.True, result2);

        var arguments3 = new FilterArguments()
            .Add(StringValue.Create("Title"))
            .Add(StringValue.Create("c"));

        var result3 = await ArrayFilters.Has(input, arguments3, context);

        Assert.Equal(BooleanValue.True, result3);

        var arguments5 = new FilterArguments()
            .Add(StringValue.Create("Title"))
            .Add(StringValue.Create("d"));

        var result5 = await ArrayFilters.Has(input, arguments5, context);

        Assert.Equal(BooleanValue.False, result5);
    }

    [Fact]
    public async Task FindIndex_OnEmptyArray_ReturnsNil()
    {
        // Arrange
        var input = ArrayValue.Create([]); // Empty array
        var arguments = new FilterArguments()
            .Add(StringValue.Create("foo"))
            .Add(StringValue.Create("bar"));
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
        var input = ArrayValue.Create([]); // Empty array
        var arguments = new FilterArguments()
            .Add(StringValue.Create("foo"))
            .Add(StringValue.Create("bar"));
        var context = new TemplateContext();

        // Act
        var result = await ArrayFilters.Has(input, arguments, context);

        // Assert
        Assert.Equal(BooleanValue.False, result);
    }

    [Theory]
    // Like Liquid, a string or a hash is filtered as a one-item array.
    [InlineData("{{ 'abc' | where: 'x' | size }}|{{ h | where: 'k', 'v' | size }}|{{ products | where: 'available' | size }}", "0|1|1")]
    // Like Liquid, a hash in an array matches when the property is truthy.
    [InlineData("{{ products | has: 'title' }}|{{ products | has: 'missing' }}|{{ products | has: 'available' }}", "true|false|true")]
    // Like Liquid, a scalar is a one-item array.
    [InlineData("{{ 5 | uniq | size }}|{{ 9 | uniq | last }}|{{ missing | uniq | size }}", "1|9|0")]
    [InlineData("{{ 7 | sum }}|{{ '3' | sum }}|{{ 'x' | sum }}|{{ missing | sum }}", "7|3|0|0")]
    public async Task ArrayFiltersAcceptInputsLikeLiquid(string source, string expected)
    {
        var context = new TemplateContext();
        context.SetValue("h", new Dictionary<string, object> { ["k"] = "v" });
        context.SetValue("products", new object[]
        {
            new Dictionary<string, object> { ["title"] = "Shoe", ["available"] = true },
            new Dictionary<string, object> { ["title"] = "Hat", ["available"] = false },
        });

        Assert.Equal(expected, await new FluidParser().Parse(source).RenderAsync(context));
    }

    [Fact]
    public async Task WhereStillFailsOnNumbers()
    {
        await Assert.ThrowsAsync<LiquidException>(() => new FluidParser().Parse("{{ 123 | where: 'title' }}").RenderAsync(new TemplateContext()).AsTask());
    }

    [Theory]
    // Like Liquid, a hash or a scalar is a one-item array, and nil an empty array.
    [InlineData("{{ h | sort | size }}|{{ h | reverse | size }}|{{ h | sum }}|{{ h | sum: 'n' }}|{{ eh | sort | size }}", "1|1|0|1|1")]
    [InlineData("{{ 1 | sort | size }}|{{ 1 | sort_natural | size }}|{{ 1 | reverse | size }}|{{ 'ab' | sort | size }}", "1|1|1|1")]
    [InlineData("{{ 5 | join }}|{{ 5 | join | size }}|[{{ missing | join }}]|{{ missing | sort | size }}|{{ missing | sum }}", "5|1|[]|0|0")]
    // Like Liquid, a value without a size has a size of 0.
    [InlineData("{{ 2.5 | size }}|{{ 7 | size }}|{{ true | size }}|{{ missing | size }}|{{ 'abc' | size }}|{{ h | size }}", "0|0|0|0|3|2")]
    public async Task ArrayFiltersWrapHashesAndScalars(string source, string expected)
    {
        var context = new TemplateContext();
        context.SetValue("h", new Dictionary<string, object> { ["k"] = "v", ["n"] = 1 });
        context.SetValue("eh", new Dictionary<string, object>());

        Assert.Equal(expected, await new FluidParser().Parse(source).RenderAsync(context));
    }
    [Theory]
    // Like Liquid's InputIterator, nested arrays are flattened, but not ranges.
    [InlineData("{{ nested | join: ',' }}|{{ nested | join }}|{{ nested | first | join: '-' }}|{{ nested | size }}", "1,2,3|1 2 3|1-2|2")]
    [InlineData("{{ nested | sort | join: ',' }}|{{ nested | sort_natural | join: ',' }}|{{ nested | reverse | join: ',' }}|{{ nested | uniq | size }}|{{ nested | compact | size }}|{{ nested | sum }}", "1,2,3|1,2,3|3,2,1|3|3|6")]
    [InlineData("{{ tags | join: ',' }}|{{ ranges | join: ',' }}|{{ ranges | size }}", "x,,y|1..2,3..4|2")]
    // Like Liquid, nil is an empty array (or an empty string for join).
    [InlineData("{{ nil | join | concat: a | join: ',' }}|{% assign x = nil | sort %}{{ x | size }}{% assign x = nil | reverse %}{{ x | size }}{% assign x = nil | uniq %}{{ x | size }}{% assign x = nil | sort_natural %}{{ x | size }}", ",1,2|0000")]
    // Like Liquid, a nil property is no property.
    [InlineData("{{ a | sum: nil }}|{{ 5 | sum: nil }}|{{ a | sum: missing }}|{{ nested | sum: nil }}", "3|5|3|6")]
    public async Task ArrayFiltersFlattenNestedArraysLikeLiquid(string source, string expected)
    {
        var context = new TemplateContext();
        context.SetValue("nested", new object[] { new[] { 1, 2 }, new[] { 3 } });
        context.SetValue("tags", new object[] { new[] { "x" }, new string[0], null, new[] { "y" } });
        context.SetValue("a", new[] { 1, 2 });

        context.SetValue("ranges", new FluidValue[] { RangeValue.Create(1, 2), RangeValue.Create(3, 4) });

        Assert.Equal(expected, await new FluidParser().Parse(source).RenderAsync(context));
    }
}
