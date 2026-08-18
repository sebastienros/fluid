using Fluid.Tests.Mocks;
using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class RenderFlushTests
    {
#if COMPILED
        private static readonly FluidParser _parser = new FluidParser().Compile();
#else
        private static readonly FluidParser _parser = new FluidParser();
#endif

        [Fact]
        public async Task PublicOutputRender_FlushesOnce()
        {
            var template = _parser.Parse("Hello");
            var output = new FlushTrackingOutput();

            await template.RenderAsync(output, NullEncoder.Default, new TemplateContext());

            Assert.Equal("Hello", output.ToString());
            Assert.Equal(1, output.FlushCount);
        }

        [Fact]
        public async Task RenderFor_FlushesOnlyAtOuterBoundary()
        {
            var fileProvider = new MockFileProvider()
                .Add("product.liquid", "{{ product }};");
            var context = new TemplateContext(new TemplateOptions { FileProvider = fileProvider });
            context.SetValue("products", new[] { 1, 2, 3 });
            var template = _parser.Parse("{% render 'product' for products as product %}");
            var output = new FlushTrackingOutput();

            await template.RenderAsync(output, NullEncoder.Default, context);

            Assert.Equal("1;2;3;", output.ToString());
            Assert.Equal(1, output.FlushCount);
        }

        [Fact]
        public async Task IncludeFor_FlushesOnlyAtOuterBoundary()
        {
            var fileProvider = new MockFileProvider()
                .Add("product.liquid", "{{ product }};");
            var context = new TemplateContext(new TemplateOptions { FileProvider = fileProvider });
            context.SetValue("products", new[] { 1, 2, 3 });
            var template = _parser.Parse("{% include 'product' for products as product %}");
            var output = new FlushTrackingOutput();

            await template.RenderAsync(output, NullEncoder.Default, context);

            Assert.Equal("1;2;3;", output.ToString());
            Assert.Equal(1, output.FlushCount);
        }

        [Fact]
        public async Task RenderFor_MaxOutputSizeIsCumulativeAcrossChildren()
        {
            var fileProvider = new MockFileProvider()
                .Add("product.liquid", "{{ product }}");
            var context = new TemplateContext(new TemplateOptions { FileProvider = fileProvider })
            {
                MaxOutputSize = 5
            };
            context.SetValue("products", new[] { 12, 34, 56 });
            var template = _parser.Parse("{% render 'product' for products as product %}");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => template.RenderAsync(new FlushTrackingOutput(), NullEncoder.Default, context).AsTask());
        }

        private sealed class FlushTrackingOutput : IFluidOutput
        {
            private char[] _buffer = new char[256];
            private int _index;

            public int FlushCount { get; private set; }

            public void Advance(int count) => _index += count;

            public Memory<char> GetMemory(int sizeHint = 0) => _buffer.AsMemory(_index);

            public Span<char> GetSpan(int sizeHint = 0) => _buffer.AsSpan(_index);

            public void Write(string value)
            {
                value.CopyTo(0, _buffer, _index, value.Length);
                _index += value.Length;
            }

            public void Write(char[] buffer, int index, int count)
            {
                buffer.AsSpan(index, count).CopyTo(_buffer.AsSpan(_index));
                _index += count;
            }

            public ValueTask FlushAsync()
            {
                FlushCount++;
                return default;
            }

            public override string ToString() => new string(_buffer, 0, _index);
        }
    }
}
