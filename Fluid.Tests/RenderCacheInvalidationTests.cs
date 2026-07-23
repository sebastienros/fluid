using Fluid.Accessors;
using Fluid.Values;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    /// <summary>
    /// Rendering caches member accessors and filter delegates per call site, keyed on the options they
    /// were resolved from. These tests pin the invalidation: a template parsed once and rendered many
    /// times must observe registrations made between renders, and must not leak a resolution from one
    /// set of options into another.
    /// </summary>
    public class RenderCacheInvalidationTests
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

        private sealed class Other
        {
            public string Name { get; set; }
        }

        [Fact]
        public async Task AccessorRegisteredBetweenRendersIsPickedUp()
        {
            _parser.TryParse("{{ p.Name }}", out var template, out var error);
            Assert.Null(error);

            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register(typeof(Model), "Name", new DelegateAccessor((o, n) => "first"));

            var context = new TemplateContext(options).SetValue("p", new Model { Name = "ignored" });
            Assert.Equal("first", await template.RenderAsync(context));

            // Re-registering must invalidate whatever the first render cached for this call site.
            options.MemberAccessStrategy.Register(typeof(Model), "Name", new DelegateAccessor((o, n) => "second"));

            context = new TemplateContext(options).SetValue("p", new Model { Name = "ignored" });
            Assert.Equal("second", await template.RenderAsync(context));
        }

        [Fact]
        public async Task DerivedStrategyOverridingGetAccessorIsNeverCached()
        {
            // A subclass may resolve accessors from a source this library can't see, so it must be
            // consulted on every access rather than have its first answer cached forever.
            _parser.TryParse("{{ p.Name }}", out var template, out var error);
            Assert.Null(error);

            var strategy = new ToggleStrategy();
            var options = new TemplateOptions { MemberAccessStrategy = strategy };

            var context = new TemplateContext(options).SetValue("p", new Model());
            Assert.Equal("A", await template.RenderAsync(context));

            strategy.Flip = true;

            context = new TemplateContext(options).SetValue("p", new Model());
            Assert.Equal("B", await template.RenderAsync(context));

            // The strategy must be consulted afresh for each render rather than answered from a cache.
            Assert.Equal(2, strategy.GetAccessorCalls);
        }

        private sealed class ToggleStrategy : DefaultMemberAccessStrategy
        {
            public bool Flip;

            public int GetAccessorCalls;

            public override IMemberAccessor GetAccessor(Type type, string name, StringComparer stringComparer)
            {
                GetAccessorCalls++;

                // The two accessors must be distinct instances that capture the answer at resolution
                // time. A single accessor reading a mutable field would return the new value even when
                // the accessor itself was cached, so the test would pass with the opt-out removed.
                return Flip
                    ? new DelegateAccessor((o, n) => "B")
                    : new DelegateAccessor((o, n) => "A");
            }
        }

        [Fact]
        public async Task SameTemplateAlternatingBetweenOptionsResolvesEachIndependently()
        {
            _parser.TryParse("{{ p.Name }}", out var template, out var error);
            Assert.Null(error);

            var first = new TemplateOptions();
            first.MemberAccessStrategy.Register(typeof(Model), "Name", new DelegateAccessor((o, n) => "one"));

            var second = new TemplateOptions();
            second.MemberAccessStrategy.Register(typeof(Model), "Name", new DelegateAccessor((o, n) => "two"));

            for (var i = 0; i < 3; i++)
            {
                Assert.Equal("one", await template.RenderAsync(new TemplateContext(first).SetValue("p", new Model())));
                Assert.Equal("two", await template.RenderAsync(new TemplateContext(second).SetValue("p", new Model())));
            }
        }

        [Fact]
        public async Task SameCallSiteHandlesAlternatingRuntimeTypes()
        {
            // One call site seeing a polymorphic collection must re-resolve per type, not reuse the
            // accessor of whichever type it happened to see first.
            _parser.TryParse("{% for p in items %}{{ p.Name }};{% endfor %}", out var template, out var error);
            Assert.Null(error);

            var options = new TemplateOptions();
            var context = new TemplateContext(options)
                .SetValue("items", new object[]
                {
                    new Model { Name = "m1" },
                    new Other { Name = "o1" },
                    new Model { Name = "m2" },
                    new Other { Name = "o2" },
                });

            Assert.Equal("m1;o1;m2;o2;", await template.RenderAsync(context));
        }

        [Fact]
        public async Task FilterRegisteredBetweenRendersIsPickedUp()
        {
            _parser.TryParse("{{ 'x' | mark }}", out var template, out var error);
            Assert.Null(error);

            var options = new TemplateOptions();

            // Not registered yet: non-strict filters pass the input through.
            Assert.Equal("x", await template.RenderAsync(new TemplateContext(options)));

            options.Filters.AddFilter("mark", (input, args, ctx) => new StringValue(input.ToStringValue() + "!"));
            Assert.Equal("x!", await template.RenderAsync(new TemplateContext(options)));

            // Replacing the delegate must also invalidate.
            options.Filters.AddFilter("mark", (input, args, ctx) => new StringValue(input.ToStringValue() + "?"));
            Assert.Equal("x?", await template.RenderAsync(new TemplateContext(options)));

            options.Filters.Remove("mark");
            Assert.Equal("x", await template.RenderAsync(new TemplateContext(options)));
        }

        [Fact]
        public async Task StrictFiltersStillThrowsAfterAFilterIsRemoved()
        {
            _parser.TryParse("{{ 'x' | mark }}", out var template, out var error);
            Assert.Null(error);

            var options = new TemplateOptions { StrictFilters = true };
            options.Filters.AddFilter("mark", (input, args, ctx) => new StringValue("ok"));

            Assert.Equal("ok", await template.RenderAsync(new TemplateContext(options)));

            options.Filters.Remove("mark");

            await Assert.ThrowsAsync<FluidException>(() => template.RenderAsync(new TemplateContext(options)).AsTask());
        }

        [Fact]
        public async Task SameTemplateAlternatingBetweenFilterCollections()
        {
            _parser.TryParse("{{ 'x' | mark }}", out var template, out var error);
            Assert.Null(error);

            var first = new TemplateOptions();
            first.Filters.AddFilter("mark", (input, args, ctx) => new StringValue("one"));

            var second = new TemplateOptions();
            second.Filters.AddFilter("mark", (input, args, ctx) => new StringValue("two"));

            for (var i = 0; i < 3; i++)
            {
                Assert.Equal("one", await template.RenderAsync(new TemplateContext(first)));
                Assert.Equal("two", await template.RenderAsync(new TemplateContext(second)));
            }
        }

        [Theory]
        [InlineData("ar-SA")]
        [InlineData("fa-IR")]
        [InlineData("hi-IN")]
        [InlineData("de-DE")]
        public async Task InternedNumbersRenderIdenticallyAcrossCultures(string culture)
        {
            // Small non-negative integers render from precomputed text instead of formatting the
            // decimal, which is only sound if that text is what every culture would have produced.
            // 1023 is the last interned value and 1024 the first non-interned one, so they must agree.
            _parser.TryParse("{{ a }}|{{ b }}|{{ c }}", out var template, out var error);
            Assert.Null(error);

            var cultureInfo = CultureInfo.GetCultureInfo(culture);
            var options = new TemplateOptions { CultureInfo = cultureInfo };
            var context = new TemplateContext(options)
                .SetValue("a", NumberValue.Create(0m))
                .SetValue("b", NumberValue.Create(1023m))
                .SetValue("c", NumberValue.Create(1024m));

            Assert.Equal("0|1023|1024", await template.RenderAsync(context));
        }

        [Theory]
        [InlineData("ar-SA")]
        [InlineData("fa-IR")]
        [InlineData("de-DE")]
        public async Task NegativeNumbersStillFormatPerCulture(string culture)
        {
            // Interning only covers non-negative whole numbers, so negatives must keep going through
            // culture-aware formatting (Arabic locales use their own minus sign and direction marks).
            _parser.TryParse("{{ a }}", out var template, out var error);
            Assert.Null(error);

            var cultureInfo = CultureInfo.GetCultureInfo(culture);
            var options = new TemplateOptions { CultureInfo = cultureInfo };
            var context = new TemplateContext(options).SetValue("a", NumberValue.Create(-1m));

            Assert.Equal((-1m).ToString(cultureInfo), await template.RenderAsync(context));
        }

        [Fact]
        public async Task AlternatingOptionsStopsAllocatingCacheEntries()
        {
            // A template rendered against two sets of options misses its per-call-site caches every
            // time. Rather than allocate a replacement entry per access forever, both caches give up
            // and fall back to resolving directly -- so a long run must not keep growing the heap.
            _parser.TryParse("{% for p in items %}{{ p.Name }}{{ 'x' | mark }};{% endfor %}", out var template, out var error);
            Assert.Null(error);

            var first = new TemplateOptions();
            first.Filters.AddFilter("mark", (input, args, ctx) => new StringValue("1"));

            var second = new TemplateOptions();
            second.Filters.AddFilter("mark", (input, args, ctx) => new StringValue("2"));

            var items = new List<Model>();
            for (var i = 0; i < 50; i++)
            {
                items.Add(new Model { Name = "n" });
            }

            async Task RenderBoth()
            {
                await template.RenderAsync(new TemplateContext(first).SetValue("items", items));
                await template.RenderAsync(new TemplateContext(second).SetValue("items", items));
            }

            // Warm up past the give-up threshold, then measure.
            for (var i = 0; i < 20; i++)
            {
                await RenderBoth();
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 20; i++)
            {
                await RenderBoth();
            }
            var settled = GC.GetAllocatedBytesForCurrentThread() - before;

            before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 40; i++)
            {
                await RenderBoth();
            }
            var doubled = GC.GetAllocatedBytesForCurrentThread() - before;

            // Allocation should track the render count, not grow per access from cache churn.
            Assert.InRange((double)doubled / settled, 1.5, 2.5);
        }

        [Fact]
        public void SettingANullNameThrowsAtTheOffendingCall()
        {
            // The dictionary this replaced rejected a null key; losing that deferred the failure to an
            // unrelated later call.
            var scope = new Scope();

            Assert.Throws<ArgumentNullException>(() => scope.SetOwnValue(null, StringValue.Empty));
            Assert.Throws<ArgumentNullException>(() => scope.SetValue(null, StringValue.Empty));
        }

        [Fact]
        public async Task UndefinedFilterUnderStrictFiltersFaultsTheTaskRatherThanThrowingSynchronously()
        {
            // Callers may start a render and await it later; the error has to arrive on the task.
            _parser.TryParse("{{ 'x' | nope }}", out var template, out var error);
            Assert.Null(error);

            var options = new TemplateOptions { StrictFilters = true };

            // Second evaluation takes the cached fast path, which is where the throw would escape.
            for (var i = 0; i < 2; i++)
            {
                var task = template.RenderAsync(new TemplateContext(options));
                await Assert.ThrowsAsync<FluidException>(async () => await task);
            }
        }

        [Fact]
        public async Task FilterThrowingSynchronouslyFaultsTheTask()
        {
            _parser.TryParse("{{ 'x' | boom }}", out var template, out var error);
            Assert.Null(error);

            var options = new TemplateOptions();
            options.Filters.AddFilter("boom", (input, args, ctx) => throw new InvalidOperationException("boom"));

            for (var i = 0; i < 2; i++)
            {
                var task = template.RenderAsync(new TemplateContext(options));
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
            }
        }

        [Fact]
        public void InterningPreservesDecimalScale()
        {
            // Liquid keeps the scale of a number as part of its identity (1.0 * 2.0 == 2.00), so a
            // scaled value must never be replaced by an interned whole number.
            Assert.Equal("5", NumberValue.Create(5m).ToStringValue());
            Assert.Equal("5.0", NumberValue.Create(5.0m).ToStringValue());
            Assert.Equal("0.0", NumberValue.Create(0.0m).ToStringValue());
            Assert.Equal("1023.00", NumberValue.Create(1023.00m).ToStringValue());
        }
    }
}
