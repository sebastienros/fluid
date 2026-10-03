using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fluid.Tests.Mocks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class EnumerableArrayValueTests
    {
#if COMPILED
        private static readonly FluidParser _parser = new FluidParser().Compile();
#else
        private static readonly FluidParser _parser = new FluidParser();
#endif

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ShouldPreserveMaterializedArraySemantics(bool empty)
        {
            var items = empty ? Array.Empty<int>() : new[] { 1, 2, 3 };
            var source = new SinglePassEnumerable(items);
            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            var value = Assert.IsType<ArrayValue>(FluidValue.Create(source, options));
            var plain = Assert.IsType<ArrayValue>(FluidValue.Create(items, options));

            Assert.Equal(FluidValues.Array, value.Type);
            Assert.Equal(plain.Values, value.Values);
            Assert.Equal(items.Select(item => (object)(decimal)item).ToArray(), Assert.IsType<object[]>(value.ToObjectValue()));
            Assert.True(value.ToBooleanValue());
            Assert.Equal(items.Length, value.ToNumberValue());
            Assert.Equal(plain.ToStringValue(), value.ToStringValue());
            Assert.True(value.Equals((FluidValue)plain));
            Assert.True(plain.Equals((FluidValue)value));
            Assert.True(value.Equals((object)plain));
            Assert.True(plain.Equals((object)value));
            Assert.Equal(plain.GetHashCode(), value.GetHashCode());
            Assert.Equal(empty, value.Equals(NilValue.Instance));
            Assert.Equal(empty, value.Equals(EmptyValue.Instance));
            Assert.Equal(!empty, value.Contains(NumberValue.Create(2)));
            Assert.Equal(plain.Enumerate(context), value.Enumerate(context));

            foreach (var name in new[] { "size", "first", "last" })
            {
                Assert.Equal(await plain.GetValueAsync(name, context), await value.GetValueAsync(name, context));
            }

            foreach (var index in new[] { -1, 0, 1, 3 })
            {
                Assert.Equal(await plain.GetIndexAsync(NumberValue.Create(index), context),
                    await value.GetIndexAsync(NumberValue.Create(index), context));
            }

            using var writer = new StringWriter();
            await value.WriteToAsync(writer, NullEncoder.Default, CultureInfo.InvariantCulture);
            Assert.Equal(plain.ToStringValue(), writer.ToString());

            context.SetValue("values", value);
            var template = _parser.Parse("{{ values | json }}|{{ values | join: ',' }}");
            Assert.Equal(empty ? "[]|" : "[1,2,3]|1,2,3", await template.RenderAsync(context));
            Assert.Equal(1, source.EnumerationCount);

            if (!empty)
            {
                items[0] = 99;
                Assert.Equal(1m, value.Values[0].ToNumberValue());
            }
        }

        [Theory]
        [InlineData("{{ values | size }}|{{ values | first }}|{{ values | last }}|{{ values | slice: 1, 1 | first }}", "3|1|3|2")]
        [InlineData("{% for item in values %}{{ item }}{% endfor %}|{% for item in values %}{{ item }}{% endfor %}", "123|123")]
        [InlineData("{{ values | reverse | join: ',' }}|{{ values | sort | join: ',' }}|{{ values | concat: values | join: ',' }}", "3,2,1|1,2,3|1,2,3,1,2,3")]
        [InlineData("{% if values startswith 1 and values endswith 3 and values contains 2 %}yes{% endif %}", "yes")]
        [InlineData("{% include 'item' for values as item %}|{% render 'item' for values as item %}", "123|123")]
        [InlineData("{% tablerow item in values cols: 2 %}{{ item }}{% endtablerow %}", "<tr class=\"row1\">\n<td class=\"col1\">1</td><td class=\"col2\">2</td></tr>\n<tr class=\"row2\"><td class=\"col1\">3</td></tr>\n")]
        public async Task ShouldRetainArrayConsumersWithoutReenumerating(string template, string expected)
        {
            var options = new TemplateOptions { FileProvider = new MockFileProvider().Add("item.liquid", "{{ item }}") };
            var source = new SinglePassEnumerable(1, 2, 3);
            var context = new TemplateContext(options);
            context.SetValue("values", source);

            Assert.Equal(expected, await _parser.Parse(template).RenderAsync(context));
            Assert.Equal(1, source.EnumerationCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ShouldAllowOnlyRegisteredMembersIncludingEmptyEnumerables(bool empty)
        {
            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            var source = new SinglePassEnumerable(empty ? Array.Empty<int>() : new[] { 1, 2, 3 });
            var value = Assert.IsType<ArrayValue>(FluidValue.Create(source, options));

            Assert.Same(NilValue.Instance, await value.GetValueAsync("Label", context));
            options.MemberAccessStrategy.Register<SinglePassEnumerable>("Label", "AsyncLabel", "size", "first", "last");
            Assert.Equal("history", (await value.GetValueAsync("Label", context)).ToStringValue());
            Assert.Equal("async history", (await value.GetValueAsync("AsyncLabel", context)).ToStringValue());
            Assert.Same(NilValue.Instance, await value.GetValueAsync("Secret", context));
            Assert.Equal(empty ? 0m : 3m, (await value.GetValueAsync("size", context)).ToNumberValue());
            Assert.Equal(empty ? "" : "1", (await value.GetValueAsync("first", context)).ToStringValue());
            Assert.Equal(empty ? "" : "3", (await value.GetValueAsync("last", context)).ToStringValue());

            context.Undefined = name => new StringValue("undefined: " + name);
            Assert.Equal("undefined: Secret", (await value.GetValueAsync("Secret", context)).ToStringValue());
            options.StrictVariables = true;
            await Assert.ThrowsAsync<FluidException>(() => value.GetValueAsync("Secret", context).AsTask());
            Assert.Equal(1, source.EnumerationCount);
        }

        [Fact]
        public async Task ShouldUseConfiguredMemberAccessors()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.CamelCase;
            options.MemberAccessStrategy.Register<SinglePassEnumerable>("label");
            options.MemberAccessStrategy.Register<SinglePassEnumerable, string>("custom", async source =>
            {
                await Task.Yield();
                return source.Label + " custom";
            });
            var context = new TemplateContext(options);
            context.SetValue("values", new SinglePassEnumerable(1));

            Assert.Equal("history|history custom|",
                await _parser.Parse("{{ values.label }}|{{ values.custom }}|{{ values.Label }}").RenderAsync(context));
        }

        [Fact]
        public async Task ShouldKeepPlainArraysListsAndFluidValueEnumerablesUnchanged()
        {
            var options = new TemplateOptions { StrictVariables = true };
            options.MemberAccessStrategy.Register<NamedList>();
            options.MemberAccessStrategy.Register<FluidEnumerable>();
            options.MemberAccessStrategy.Register<int[]>();
            var context = new TemplateContext(options);
            var fluidItems = new FluidValue[] { NumberValue.Create(1), NumberValue.Create(2) };
            var direct = new ArrayValue(fluidItems);
            Assert.Same(fluidItems, direct.Values);

            foreach (var source in new object[]
            {
                new[] { 1, 2 }, new NamedList { 1, 2 }, fluidItems,
                new List<FluidValue>(fluidItems), new FluidEnumerable(fluidItems), direct
            })
            {
                var value = Assert.IsType<ArrayValue>(FluidValue.Create(source, options));
                Assert.Equal(new object[] { 1m, 2m }, Assert.IsType<object[]>(value.ToObjectValue()));
                Assert.Same(NilValue.Instance, await value.GetValueAsync("Label", context));
                Assert.Same(NilValue.Instance, await value.GetValueAsync("Length", context));
                Assert.Same(NilValue.Instance, await value.GetValueAsync("Count", context));
            }

            Assert.Same(direct, FluidValue.Create(direct, options));
            Assert.Same(ArrayValue.Empty, FluidValue.Create(Array.Empty<int>(), options));
            Assert.Same(ArrayValue.Empty, FluidValue.Create(new NamedList(), options));
            Assert.Same(ArrayValue.Empty, FluidValue.Create(Array.Empty<FluidValue>(), options));
            Assert.Same(NilValue.Instance, await new ArrayValue(null).GetValueAsync("Label", context));
        }

        [Fact]
        public async Task ShouldPreserveConverterAndDictionaryPrecedence()
        {
            var options = new TemplateOptions();
            var source = new SinglePassEnumerable(1, 2);
            var replacement = new StringValue("converted");
            options.ValueConverters.Add(value => ReferenceEquals(value, source) ? replacement : null);
            options.ValueConverters.Add(value => throw new InvalidOperationException("Later converters must not run."));
            Assert.Same(replacement, FluidValue.Create(source, options));
            Assert.Same(replacement, FluidValue.Create(replacement, options));
            Assert.Equal(0, source.EnumerationCount);

            options.ValueConverters.Clear();
            options.ValueConverters.Add(value => ReferenceEquals(value, source) ? new[] { 3, 4 } : null);
            var converted = Assert.IsType<ArrayValue>(FluidValue.Create(source, options));
            Assert.Equal(new object[] { 3m, 4m }, converted.ToObjectValue());
            Assert.Equal(0, source.EnumerationCount);
            Assert.Same(NilValue.Instance, await converted.GetValueAsync("Label", new TemplateContext(options)));

            options.ValueConverters.Clear();
            var replacementSource = new SinglePassEnumerable(5, 6);
            options.ValueConverters.Add(value => ReferenceEquals(value, source) ? replacementSource : null);
            options.MemberAccessStrategy.Register<SinglePassEnumerable>("Label");
            converted = Assert.IsType<ArrayValue>(FluidValue.Create(source, options));
            Assert.Equal(new object[] { 5m, 6m }, converted.ToObjectValue());
            Assert.Equal("history", (await converted.GetValueAsync("Label", new TemplateContext(options))).ToStringValue());
            Assert.Equal(0, source.EnumerationCount);
            Assert.Equal(1, replacementSource.EnumerationCount);

            var dictionary = Assert.IsType<DictionaryValue>(FluidValue.Create(new Dictionary<string, int> { ["Label"] = 42 }, options));
            Assert.Equal(42m, (await dictionary.GetValueAsync("Label", new TemplateContext(options))).ToNumberValue());
        }

        private sealed class SinglePassEnumerable : IEnumerable<int>
        {
            private readonly int[] _items;

            public SinglePassEnumerable(params int[] items) => _items = items;

            public int EnumerationCount { get; private set; }
            public string Label => "history";
            public string Secret => "hidden";
            public Task<string> AsyncLabel => Task.FromResult("async history");
            public int size => 999;
            public int first => 999;
            public int last => 999;

            public IEnumerator<int> GetEnumerator()
            {
                if (++EnumerationCount != 1)
                {
                    throw new InvalidOperationException("The source must only be enumerated once.");
                }

                return ((IEnumerable<int>)_items).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private sealed class NamedList : List<int>
        {
            public string Label => "list";
        }

        private sealed class FluidEnumerable : IEnumerable<FluidValue>
        {
            private readonly FluidValue[] _items;

            public FluidEnumerable(FluidValue[] items) => _items = items;
            public string Label => "fluid";
            public IEnumerator<FluidValue> GetEnumerator() => ((IEnumerable<FluidValue>)_items).GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
