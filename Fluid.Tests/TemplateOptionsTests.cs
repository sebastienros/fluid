using Fluid.Accessors;
using Fluid.Values;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class TemplateOptionsTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    private sealed class Model
    {
        public string Name { get; set; }
    }

    [Fact]
    public void BuilderShouldProvideDocumentedDefaults()
    {
        var options = new TemplateOptionsBuilder().Build();

        Assert.Equal(16 * 1024, options.OutputBufferSize);
        Assert.False(options.StrictVariables);
        Assert.False(options.StrictFilters);
        Assert.Equal(".liquid", options.DefaultFileExtension);
        Assert.Equal(100, options.MaxRecursion);
        Assert.Equal(0, options.MaxSteps);
        Assert.True(options.Greedy);
        Assert.Same(StringComparer.OrdinalIgnoreCase, options.ModelNamesComparer);
        Assert.True(options.Filters.TryGetValue("upcase", out _));
        Assert.False(options.Filters.TryGetValue("money", out _));
        Assert.Empty(options.ValueConverters);
        Assert.Empty(options.GlobalValues);
    }

    [Fact]
    public void BuilderShouldApplyEverySetting()
    {
        var timeZone = TimeZoneInfo.Utc;
        var jsonOptions = new System.Text.Json.JsonSerializerOptions();
        var now = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var options = new TemplateOptionsBuilder()
            .WithOutputBufferSize(0)
            .WithStrictVariables()
            .WithStrictFilters()
            .WithDefaultFileExtension(".html")
            .WithMaxSteps(1)
            .WithMaxOutputSize(2)
            .WithMaxCollectionSize(3)
            .WithMaxRecursion(4)
            .WithModelNamesComparer(StringComparer.Ordinal)
            .WithTimeZone(timeZone)
            .WithNow(() => now)
            .WithJsonSerializerOptions(jsonOptions)
            .WithTrimming(TrimmingFlags.TagLeft)
            .WithGreedy(false)
            .WithMoneyOptions(new MoneyOptions { Currency = "EUR" })
            .Build();

        Assert.Equal(0, options.OutputBufferSize);
        Assert.True(options.StrictVariables);
        Assert.True(options.StrictFilters);
        Assert.Equal(".html", options.DefaultFileExtension);
        Assert.Equal(1, options.MaxSteps);
        Assert.Equal(2, options.MaxOutputSize);
        Assert.Equal(3, options.MaxCollectionSize);
        Assert.Equal(4, options.MaxRecursion);
        Assert.Same(StringComparer.Ordinal, options.ModelNamesComparer);
        Assert.Same(timeZone, options.TimeZone);
        Assert.Equal(now, options.Now());
        Assert.Same(jsonOptions, options.JsonSerializerOptions);
        Assert.Equal(TrimmingFlags.TagLeft, options.Trimming);
        Assert.False(options.Greedy);
        Assert.Equal("EUR", options.MoneyOptions.Currency);
    }

    [Fact]
    public void ChangingTheBuilderShouldNotAffectBuiltOptions()
    {
        var builder = new TemplateOptionsBuilder()
            .WithMaxSteps(10)
            .AddFilter("first", (input, args, ctx) => input)
            .AddValueConverter(x => null)
            .WithGlobalValue("g", StringValue.Create("one"))
            .ConfigureMemberAccess(strategy => strategy.Register(typeof(Model), "Name", new FixedMemberAccessor("one")));

        var options = builder.Build();

        builder
            .WithMaxSteps(20)
            .AddFilter("second", (input, args, ctx) => input)
            .AddValueConverter(x => null)
            .WithGlobalValue("g", StringValue.Create("two"))
            .ConfigureMemberAccess(strategy => strategy.Register(typeof(Model), "Name", new FixedMemberAccessor("two")));

        Assert.Equal(10, options.MaxSteps);
        Assert.True(options.Filters.TryGetValue("first", out _));
        Assert.False(options.Filters.TryGetValue("second", out _));
        Assert.Single(options.ValueConverters);
        Assert.Equal("one", options.GlobalValues["g"].ToStringValue());

        var template = _parser.Parse("{{ m.Name }}");
        Assert.Equal("one", template.Render(new TemplateContext(options).SetValue("m", new Model())));
    }

    [Fact]
    public void EveryBuildShouldCreateIndependentOptions()
    {
        var builder = new TemplateOptionsBuilder();

        var first = builder.Build();
        var second = builder.Build();

        Assert.NotSame(first, second);
        Assert.NotSame(first.Filters, second.Filters);
        Assert.NotSame(first.MemberAccessStrategy, second.MemberAccessStrategy);
    }

    [Fact]
    public void FiltersOfBuiltOptionsShouldBeReadOnly()
    {
        var options = new TemplateOptionsBuilder().Build();

        Assert.True(options.Filters.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => options.Filters.AddFilter("x", (input, args, ctx) => input));
        Assert.Throws<InvalidOperationException>(() => options.Filters.Remove("upcase"));
        Assert.Throws<InvalidOperationException>(() => options.Filters.Clear());
        Assert.True(options.Filters.TryGetValue("upcase", out _));
    }

    [Fact]
    public void MemberAccessStrategyOfBuiltOptionsShouldRejectRegistrations()
    {
        var options = new TemplateOptionsBuilder().Build();

        Assert.Throws<InvalidOperationException>(() =>
            options.MemberAccessStrategy.Register(typeof(Model), "Name", new FixedMemberAccessor("x")));
    }

    [Fact]
    public void TemplateOptionsDefaultShouldBeImmutable()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TemplateOptions.Default.Filters.AddFilter("x", (input, args, ctx) => input));
        Assert.Throws<InvalidOperationException>(() =>
            TemplateOptions.Default.MemberAccessStrategy.Register(typeof(Model), "Name", new FixedMemberAccessor("x")));
    }

    [Fact]
    public void ValueConvertersAndGlobalValuesShouldNotBeModifiableThroughTheOptions()
    {
        var options = new TemplateOptionsBuilder()
            .AddValueConverter(x => null)
            .WithGlobalValue("g", StringValue.Create("one"))
            .Build();

        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, FluidValue>)options.GlobalValues).Add("x", NilValue.Instance));
        Assert.Throws<NotSupportedException>(() => ((IList<Func<object, object>>)options.ValueConverters).Add(x => null));

        // A context writes to its own scope and never to the shared global values.
        var context = new TemplateContext(options);
        context.SetValue("g", "changed");
        Assert.Equal("one", options.GlobalValues["g"].ToStringValue());
        Assert.Equal("one", new TemplateContext(options).GetValue("g").ToStringValue());
    }

    [Fact]
    public void MemberAccessStrategyFactoryShouldBeInvokedForEveryBuild()
    {
        var created = new List<MemberAccessStrategy>();
        var builder = new TemplateOptionsBuilder().WithMemberAccessStrategy(() =>
        {
            var strategy = new DefaultMemberAccessStrategy();
            created.Add(strategy);
            return strategy;
        });

        var first = builder.Build();
        var second = builder.Build();

        Assert.Equal(2, created.Count);
        Assert.Same(created[0], first.MemberAccessStrategy);
        Assert.Same(created[1], second.MemberAccessStrategy);
    }

    [Fact]
    public void MemberAccessStrategyTypeShouldBeConfigurable()
    {
        var options = new TemplateOptionsBuilder().WithMemberAccessStrategy<CustomStrategy>().Build();

        Assert.IsType<CustomStrategy>(options.MemberAccessStrategy);
    }

    private sealed class CustomStrategy : DefaultMemberAccessStrategy
    {
    }

    [Fact]
    public void BuilderShouldRejectNullFactories()
    {
        var builder = new TemplateOptionsBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.WithMemberAccessStrategy((Func<MemberAccessStrategy>)null));
        Assert.Throws<ArgumentNullException>(() => builder.ConfigureMemberAccess(null));
        Assert.Throws<ArgumentNullException>(() => builder.ConfigureFilters(null));
        Assert.Throws<ArgumentNullException>(() => builder.AddValueConverter(null));
        Assert.Throws<ArgumentNullException>(() => builder.WithModelNamesComparer(null));
    }

    [Fact]
    public void ToBuilderShouldCopyTheConfigurationWithoutAffectingTheOriginal()
    {
        var original = new TemplateOptionsBuilder()
            .WithMaxSteps(10)
            .WithCultureInfo(new System.Globalization.CultureInfo("fr-FR"))
            .AddFilter("first", (input, args, ctx) => input)
            .AddValueConverter(x => null)
            .WithGlobalValue("g", StringValue.Create("one"))
            .ConfigureMemberAccess(strategy => strategy.Register(typeof(Model), "Name", new FixedMemberAccessor("original")))
            .Build();

        var derived = original.ToBuilder()
            .WithMaxSteps(20)
            .AddFilter("second", (input, args, ctx) => input)
            .AddValueConverter(x => null)
            .WithGlobalValue("g", StringValue.Create("two"))
            .ConfigureMemberAccess(strategy => strategy.Register(typeof(Model), "Name", new FixedMemberAccessor("derived")))
            .Build();

        Assert.Equal("fr-FR", derived.CultureInfo.Name);
        Assert.Equal(10, original.MaxSteps);
        Assert.Equal(20, derived.MaxSteps);
        Assert.False(original.Filters.TryGetValue("second", out _));
        Assert.True(derived.Filters.TryGetValue("first", out _));
        Assert.True(derived.Filters.TryGetValue("second", out _));
        Assert.Single(original.ValueConverters);
        Assert.Equal(2, derived.ValueConverters.Count);
        Assert.Equal("one", original.GlobalValues["g"].ToStringValue());
        Assert.Equal("two", derived.GlobalValues["g"].ToStringValue());

        var template = _parser.Parse("{{ m.Name }}");
        Assert.Equal("original", template.Render(new TemplateContext(original).SetValue("m", new Model())));
        Assert.Equal("derived", template.Render(new TemplateContext(derived).SetValue("m", new Model())));
    }

    [Fact]
    public void MoneyOptionsShouldBeImmutable()
    {
        var original = new MoneyOptions { Currency = "EUR" };
        var withCurrency = original.WithCurrency(new MoneyCurrency("BTC", "₿", 8));

        Assert.False(original.Currencies.ContainsKey("BTC"));
        Assert.True(withCurrency.Currencies.ContainsKey("btc"));
        Assert.Equal("EUR", withCurrency.Currency);
        Assert.True(original.Currencies.ContainsKey("USD"));
        Assert.True(withCurrency.Currencies.ContainsKey("USD"));
    }

    [Fact]
    public void MoneyOptionsShouldCopyAssignedCurrencies()
    {
        var currencies = new Dictionary<string, MoneyCurrency> { ["XYZ"] = new MoneyCurrency("XYZ") };
        var options = new MoneyOptions { Currencies = currencies };

        currencies["ABC"] = new MoneyCurrency("ABC");

        Assert.Single(options.Currencies);
        Assert.True(options.Currencies.ContainsKey("xyz"));
    }

    [Fact]
    public async Task SharedOptionsShouldRenderConcurrently()
    {
        var options = new TemplateOptionsBuilder()
            .WithGlobalValue("global", StringValue.Create("g"))
            .ConfigureMemberAccess(strategy => strategy.Register<Model, string>("Name", m => m.Name))
            .Build();

        var template = _parser.Parse("{{ global }}-{{ m.Name }}-{{ m.Name | upcase }}");

        var tasks = Enumerable.Range(0, 64).Select(i => Task.Run(async () =>
        {
            for (var j = 0; j < 50; j++)
            {
                var context = new TemplateContext(options).SetValue("m", new Model { Name = "n" + i });
                Assert.Equal($"g-n{i}-N{i}", await template.RenderAsync(context));
            }
        }));

        await Task.WhenAll(tasks);
    }
}
