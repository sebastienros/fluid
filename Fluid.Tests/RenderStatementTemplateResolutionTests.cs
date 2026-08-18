using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class RenderStatementTemplateResolutionTests
    {
#if COMPILED
        private static FluidParser CreateParser() => new FluidParser().Compile();
#else
        private static FluidParser CreateParser() => new FluidParser();
#endif

        [Fact]
        public async Task VersionedProviderSkipsResolutionAfterCacheHit()
        {
            var provider = new VersionedProvider().Set("item.liquid", "x");
            var options = new TemplateOptions { FileProvider = provider };
            var template = CreateParser().Parse("{% render 'item' %}");

            Assert.Equal("x", await template.RenderAsync(new TemplateContext(options)));
            Assert.Equal("x", await template.RenderAsync(new TemplateContext(options)));

            Assert.Equal(2, provider.Calls);
        }

        [Fact]
        public async Task NullTemplateCacheDisablesResolutionCache()
        {
            var provider = new VersionedProvider().Set("item.liquid", "x");
            var options = new TemplateOptions
            {
                FileProvider = provider,
                TemplateCache = null
            };
            var template = CreateParser().Parse("{% render 'item' %}");

            Assert.Equal("x", await template.RenderAsync(new TemplateContext(options)));
            Assert.Equal("x", await template.RenderAsync(new TemplateContext(options)));

            Assert.Equal(4, provider.Calls);
            Assert.Equal(2, provider.Reads);
        }

        [Fact]
        public async Task UnversionedProviderDisablesResolutionCache()
        {
            var inner = new VersionedProvider().Set("item.liquid", "x");
            var provider = new UnversionedProvider(inner);
            var options = new TemplateOptions { FileProvider = provider };
            var template = CreateParser().Parse("{% render 'item' %}");

            Assert.Equal("x", await template.RenderAsync(new TemplateContext(options)));
            Assert.Equal("x", await template.RenderAsync(new TemplateContext(options)));

            Assert.Equal(4, inner.Calls);
            Assert.Equal(1, inner.Reads);
        }

        [Fact]
        public async Task VersionChangeWithSameLastModifiedPreservesTemplateCacheSemantics()
        {
            var lastModified = DateTimeOffset.UnixEpoch.AddDays(1);
            var provider = new VersionedProvider().Set("item.liquid", "old", lastModified);
            var options = new TemplateOptions { FileProvider = provider };
            var template = CreateParser().Parse("{% render 'item' %}");

            Assert.Equal("old", await template.RenderAsync(new TemplateContext(options)));

            provider.Set("item.liquid", "new", lastModified);

            Assert.Equal("old", await template.RenderAsync(new TemplateContext(options)));
            Assert.Equal("old", await template.RenderAsync(new TemplateContext(options)));
            Assert.Equal(4, provider.Calls);
            Assert.Equal(1, provider.Reads);
        }

        [Fact]
        public async Task ChangedLastModifiedReloadsChangedContent()
        {
            var provider = new VersionedProvider().Set("item.liquid", "old");
            var options = new TemplateOptions { FileProvider = provider };
            var template = CreateParser().Parse("{% render 'item' %}");

            Assert.Equal("old", await template.RenderAsync(new TemplateContext(options)));

            provider.Set("item.liquid", "new");

            Assert.Equal("new", await template.RenderAsync(new TemplateContext(options)));
            Assert.Equal("new", await template.RenderAsync(new TemplateContext(options)));
            Assert.Equal(4, provider.Calls);
            Assert.Equal(2, provider.Reads);
        }

        [Fact]
        public async Task DefaultExtensionFallbackAndIdentifierAreCached()
        {
            var provider = new VersionedProvider().Set("folder/item.liquid", "{{ item }}");
            var options = new TemplateOptions { FileProvider = provider };
            var template = CreateParser().Parse("{% render 'folder/item' with 'value' %}");

            Assert.Equal("value", await template.RenderAsync(new TemplateContext(options)));
            Assert.Equal("value", await template.RenderAsync(new TemplateContext(options)));

            Assert.Equal(2, provider.Calls);
            Assert.Equal(["folder/item", "folder/item.liquid"], provider.RequestedPaths);
        }

        [Fact]
        public async Task MultiPartDefaultExtensionPreservesResolvedIdentifier()
        {
            var provider = new VersionedProvider().Set("item.partial.liquid", "ignored");
            var options = new TemplateOptions
            {
                FileProvider = provider,
                DefaultFileExtension = ".partial.liquid",
                TemplateParsed = (_, _) => new ContextValueTemplate("item.partial")
            };
            var template = CreateParser().Parse("{% render 'item' with 'value' %}");

            Assert.Equal("value", await template.RenderAsync(new TemplateContext(options)));
            Assert.Equal("value", await template.RenderAsync(new TemplateContext(options)));
            Assert.Equal(2, provider.Calls);
        }

        [Fact]
        public async Task ContextSpecificResolutionKeysDoNotLeakTemplates()
        {
            var provider = new ContextualProvider();
            var options = new TemplateOptions { FileProvider = provider };
            var template = CreateParser().Parse("{% render 'item' %}");
            var first = new TemplateContext(options).SetValue("tenant", "first");
            var second = new TemplateContext(options).SetValue("tenant", "second");

            for (var i = 0; i < 3; i++)
            {
                Assert.Equal("first", await template.RenderAsync(first));
                Assert.Equal("second", await template.RenderAsync(second));
            }

            Assert.Equal(4, provider.Calls);
        }

        [Fact]
        public async Task DistinctOptionsProvidersAndCachesStayIsolated()
        {
            var firstProvider = new VersionedProvider().Set("item.liquid", "first");
            var secondProvider = new VersionedProvider().Set("item.liquid", "second");
            var firstOptions = new TemplateOptions
            {
                FileProvider = firstProvider,
                TemplateCache = new TrackingTemplateCache()
            };
            var secondOptions = new TemplateOptions
            {
                FileProvider = secondProvider,
                TemplateCache = new TrackingTemplateCache()
            };
            var template = CreateParser().Parse("{% render 'item' %}");

            for (var i = 0; i < 3; i++)
            {
                Assert.Equal("first", await template.RenderAsync(new TemplateContext(firstOptions)));
                Assert.Equal("second", await template.RenderAsync(new TemplateContext(secondOptions)));
            }

            Assert.Equal(2, firstProvider.Calls);
            Assert.Equal(2, secondProvider.Calls);
        }

        [Fact]
        public async Task CustomTemplateCacheMissIsObserved()
        {
            var provider = new VersionedProvider().Set("item.liquid", "x");
            var cache = new TrackingTemplateCache();
            var options = new TemplateOptions { FileProvider = provider, TemplateCache = cache };
            var template = CreateParser().Parse("{% render 'item' %}");

            Assert.Equal("x", await template.RenderAsync(new TemplateContext(options)));
            cache.Enabled = false;
            Assert.Equal("x", await template.RenderAsync(new TemplateContext(options)));

            Assert.Equal(4, provider.Calls);
            Assert.Equal(2, provider.Reads);
        }

        [Fact]
        public async Task ConcurrentRendersUsePublishedImmutableResolution()
        {
            var provider = new VersionedProvider().Set("item.liquid", "x");
            var options = new TemplateOptions { FileProvider = provider };
            var template = CreateParser().Parse("{% render 'item' %}");

            Assert.Equal("x", await template.RenderAsync(new TemplateContext(options)));

            var renders = new Task<string>[100];
            for (var i = 0; i < renders.Length; i++)
            {
                renders[i] = template.RenderAsync(new TemplateContext(options)).AsTask();
            }

            var results = await Task.WhenAll(renders);
            Assert.All(results, result => Assert.Equal("x", result));
            Assert.Equal(2, provider.Calls);
        }

        [Fact]
        public async Task ParseFailureIsNotCached()
        {
            var provider = new VersionedProvider().Set("item.liquid", "{% if %}");
            var options = new TemplateOptions { FileProvider = provider };
            var template = CreateParser().Parse("{% render 'item' %}");

            await Assert.ThrowsAsync<ParseException>(
                () => template.RenderAsync(new TemplateContext(options)).AsTask());
            await Assert.ThrowsAsync<ParseException>(
                () => template.RenderAsync(new TemplateContext(options)).AsTask());
            Assert.Equal(4, provider.Calls);

            provider.Set("item.liquid", "valid");

            Assert.Equal("valid", await template.RenderAsync(new TemplateContext(options)));
            Assert.Equal(6, provider.Calls);
        }

        [Fact]
        public async Task TemplateParsedTransformationIsCached()
        {
            var provider = new VersionedProvider().Set("item.liquid", "original");
            var callbackCalls = 0;
            var options = new TemplateOptions
            {
                FileProvider = provider,
                TemplateParsed = (_, _) =>
                {
                    callbackCalls++;
                    return new TextTemplate("transformed");
                }
            };
            var template = CreateParser().Parse("{% render 'item' %}");

            Assert.Equal("transformed", await template.RenderAsync(new TemplateContext(options)));
            Assert.Equal("transformed", await template.RenderAsync(new TemplateContext(options)));

            Assert.Equal(1, callbackCalls);
            Assert.Equal(2, provider.Calls);
        }

        [Fact]
        public async Task CancellationIsCheckedBeforeWarmCache()
        {
            var provider = new VersionedProvider().Set("item.liquid", "x");
            var options = new TemplateOptions { FileProvider = provider };
            var template = CreateParser().Parse("{% render 'item' %}");

            Assert.Equal("x", await template.RenderAsync(new TemplateContext(options)));

            using var source = new CancellationTokenSource();
            source.Cancel();
            var context = new TemplateContext(options) { CancellationToken = source.Token };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => template.RenderAsync(context).AsTask());
            Assert.Equal(2, provider.Calls);
        }

        [Fact]
        public async Task ProviderExceptionsAreNotCachedOrHidden()
        {
            var provider = new VersionedProvider { ThrowOnGet = true };
            var options = new TemplateOptions { FileProvider = provider };
            var template = CreateParser().Parse("{% render 'item' %}");

            for (var i = 0; i < 2; i++)
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => template.RenderAsync(new TemplateContext(options)).AsTask());
                Assert.Equal("provider", exception.Message);
            }

            Assert.Equal(2, provider.Calls);

            provider.ThrowOnGet = false;
            provider.Set("item.liquid", "x");
            Assert.Equal("x", await template.RenderAsync(new TemplateContext(options)));

            provider.ThrowOnVersion = true;
            var versionException = await Assert.ThrowsAsync<InvalidOperationException>(
                () => template.RenderAsync(new TemplateContext(options)).AsTask());
            Assert.Equal("version", versionException.Message);
        }

        [Fact]
        public async Task SeparateParserInstancesKeepStatementCachesIndependent()
        {
            var firstProvider = new VersionedProvider().Set("item.liquid", "first");
            var secondProvider = new VersionedProvider().Set("item.liquid", "second");
            var first = CreateParser().Parse("{% render 'item' %}");
            var second = CreateParser().Parse("{% render 'item' %}");

            Assert.Equal(
                "first",
                await first.RenderAsync(new TemplateContext(new TemplateOptions { FileProvider = firstProvider })));
            Assert.Equal(
                "second",
                await second.RenderAsync(new TemplateContext(new TemplateOptions { FileProvider = secondProvider })));
        }

        private sealed class VersionedProvider : IVersionedTemplateFileProvider
        {
            private readonly ConcurrentDictionary<string, Entry> _files = new(StringComparer.Ordinal);
            private readonly ConcurrentQueue<string> _requestedPaths = new();
            private long _lastModified;
            private long _version;
            private int _calls;
            private int _reads;

            public bool ThrowOnGet { get; set; }
            public bool ThrowOnVersion { get; set; }
            public int Calls => Volatile.Read(ref _calls);
            public int Reads => Volatile.Read(ref _reads);
            public string[] RequestedPaths => _requestedPaths.ToArray();

            public long Version
            {
                get
                {
                    if (ThrowOnVersion)
                    {
                        throw new InvalidOperationException("version");
                    }

                    return Interlocked.Read(ref _version);
                }
            }

            public object GetTemplateResolutionCacheKey(TemplateContext context) => this;

            public VersionedProvider Set(string path, string content, DateTimeOffset? lastModified = null)
            {
                _files[path] = new Entry(
                    Encoding.UTF8.GetBytes(content),
                    lastModified ?? DateTimeOffset.UnixEpoch.AddTicks(Interlocked.Increment(ref _lastModified)));
                Interlocked.Increment(ref _version);
                return this;
            }

            public ValueTask<TemplateSourceInfo> GetFileInfoAsync(
                string subpath,
                TemplateContext context,
                CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _calls);
                _requestedPaths.Enqueue(subpath);
                cancellationToken.ThrowIfCancellationRequested();

                if (ThrowOnGet)
                {
                    throw new InvalidOperationException("provider");
                }

                if (!_files.TryGetValue(subpath, out var entry))
                {
                    return default;
                }

                return new ValueTask<TemplateSourceInfo>(
                    new TemplateSourceInfo(entry.LastModified, _ =>
                    {
                        Interlocked.Increment(ref _reads);
                        return new ValueTask<Stream>(new MemoryStream(entry.Content, writable: false));
                    }));
            }

            private sealed record Entry(byte[] Content, DateTimeOffset LastModified);
        }

        private sealed class UnversionedProvider : ITemplateFileProvider
        {
            private readonly VersionedProvider _inner;

            public UnversionedProvider(VersionedProvider inner)
            {
                _inner = inner;
            }

            public ValueTask<TemplateSourceInfo> GetFileInfoAsync(
                string subpath,
                TemplateContext context,
                CancellationToken cancellationToken) =>
                _inner.GetFileInfoAsync(subpath, context, cancellationToken);
        }

        private sealed class ContextualProvider : IVersionedTemplateFileProvider
        {
            private readonly object _firstKey = new();
            private readonly object _secondKey = new();
            private int _calls;

            public long Version => 0;
            public int Calls => Volatile.Read(ref _calls);

            public object GetTemplateResolutionCacheKey(TemplateContext context) =>
                context.GetValue("tenant").ToStringValue() == "first" ? _firstKey : _secondKey;

            public ValueTask<TemplateSourceInfo> GetFileInfoAsync(
                string subpath,
                TemplateContext context,
                CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _calls);
                cancellationToken.ThrowIfCancellationRequested();

                if (subpath != "item.liquid")
                {
                    return default;
                }

                var tenant = context.GetValue("tenant").ToStringValue();
                var content = Encoding.UTF8.GetBytes(tenant);
                return new ValueTask<TemplateSourceInfo>(
                    new TemplateSourceInfo(
                        DateTimeOffset.UnixEpoch,
                        _ => new ValueTask<Stream>(new MemoryStream(content, writable: false)),
                        cacheKey: tenant + ":" + subpath));
            }
        }

        private sealed class TrackingTemplateCache : ITemplateCache
        {
            private readonly ConcurrentDictionary<string, Entry> _templates = new(StringComparer.Ordinal);

            public bool Enabled { get; set; } = true;

            public bool TryGetTemplate(
                string subpath,
                DateTimeOffset lastModified,
                out IFluidTemplate template)
            {
                if (Enabled &&
                    _templates.TryGetValue(subpath, out var entry) &&
                    entry.LastModified >= lastModified)
                {
                    template = entry.Template;
                    return true;
                }

                template = null;
                return false;
            }

            public void SetTemplate(string subpath, DateTimeOffset lastModified, IFluidTemplate template)
            {
                if (Enabled)
                {
                    _templates[subpath] = new Entry(lastModified, template);
                }
            }

            private sealed record Entry(DateTimeOffset LastModified, IFluidTemplate Template);
        }

        private sealed class TextTemplate : IFluidTemplate
        {
            private readonly string _text;

            public TextTemplate(string text)
            {
                _text = text;
            }

            public ValueTask RenderAsync(
                IFluidOutput output,
                TextEncoder encoder,
                TemplateContext context)
            {
                output.Write(_text);
                return default;
            }

        }

        private sealed class ContextValueTemplate : IFluidTemplate
        {
            private readonly string _name;

            public ContextValueTemplate(string name)
            {
                _name = name;
            }

            public ValueTask RenderAsync(
                IFluidOutput output,
                TextEncoder encoder,
                TemplateContext context)
            {
                output.Write(context.GetValue(_name).ToStringValue());
                return default;
            }
        }
    }
}
