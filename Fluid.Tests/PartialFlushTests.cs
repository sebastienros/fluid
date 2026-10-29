using Fluid.Tests.Mocks;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class PartialFlushTests
{
    private static readonly FluidParser _parser = new FluidParser();

    private sealed class FlushCountingOutput : IFluidOutput
    {
        private readonly StringBuilder _builder = new();
        private char[] _buffer = new char[256];

        public int Flushes { get; private set; }

        public void Advance(int count) => _builder.Append(_buffer, 0, count);

        public Memory<char> GetMemory(int sizeHint = 0) => GetBuffer(sizeHint);

        public Span<char> GetSpan(int sizeHint = 0) => GetBuffer(sizeHint);

        public void Write(string value) => _builder.Append(value);

        public void Write(char[] buffer, int index, int count) => _builder.Append(buffer, index, count);

        public ValueTask FlushAsync()
        {
            Flushes++;
            return default;
        }

        public override string ToString() => _builder.ToString();

        private char[] GetBuffer(int sizeHint)
        {
            if (sizeHint > _buffer.Length)
            {
                _buffer = new char[sizeHint];
            }

            return _buffer;
        }
    }

    private static TemplateOptions CreateOptions(Func<TemplateOptionsBuilder, TemplateOptionsBuilder> configure = null)
    {
        var fileProvider = new MockFileProvider();
        fileProvider.Add("item.liquid", "[{{ i }}]");
        fileProvider.Add("outer.liquid", "<{% render 'item', i: i %}>");

        var builder = new TemplateOptionsBuilder().WithFileProvider(fileProvider);

        return (configure?.Invoke(builder) ?? builder).Build();
    }

    [Theory]
    [InlineData("{% for i in (1..3) %}{% render 'item', i: i %}{% endfor %}", "[1][2][3]")]
    [InlineData("{% for i in (1..3) %}{% include 'item' %}{% endfor %}", "[1][2][3]")]
    [InlineData("{% for i in (1..2) %}{% render 'outer', i: i %}{% endfor %}", "<[1]><[2]>")]
    [InlineData("{% for i in (1..2) %}{% include 'outer' %}{% endfor %}", "<[1]><[2]>")]
    public async Task PartialsLeaveTheFlushToTheOutermostRender(string source, string expected)
    {
        var output = new FlushCountingOutput();

        await _parser.Parse(source).RenderAsync(output, NullEncoder.Default, new TemplateContext(CreateOptions()));

        Assert.Equal(expected, output.ToString());
        Assert.Equal(1, output.Flushes);
    }

    [Fact]
    public async Task PartialsLeaveTheFlushToTheOutermostRenderWhenTheOutputIsLimited()
    {
        var output = new FlushCountingOutput();
        var options = CreateOptions(builder => builder.WithMaxOutputSize(1000));
        var template = _parser.Parse("{% for i in (1..3) %}{% render 'item', i: i %}{% include 'item' %}{% endfor %}");

        await template.RenderAsync(output, NullEncoder.Default, new TemplateContext(options));

        Assert.Equal("[1][1][2][2][3][3]", output.ToString());
        Assert.Equal(1, output.Flushes);
    }

    [Fact]
    public async Task ARenderIntoAnotherOutputIsStillFlushed()
    {
        // Rendering a second template from inside the first, into an output of its own: nothing
        // else will flush that output, so the inner render has to.
        var inner = new FlushCountingOutput();
        var innerTemplate = _parser.Parse("inner");

        var options = CreateOptions(builder => builder.AddFilter("render_inner", async (input, arguments, context) =>
        {
            await innerTemplate.RenderAsync(inner, NullEncoder.Default, context);
            return input;
        }));

        var output = new FlushCountingOutput();
        await _parser.Parse("{{ 'outer' | render_inner }}").RenderAsync(output, NullEncoder.Default, new TemplateContext(options));

        Assert.Equal("outer", output.ToString());
        Assert.Equal("inner", inner.ToString());
        Assert.Equal(1, inner.Flushes);
        Assert.Equal(1, output.Flushes);
    }

    [Theory]
    [InlineData("{% for i in (1..3) %}{% render 'item', i: i %}{% endfor %}")]
    [InlineData("{% for i in (1..3) %}{% include 'item' %}{% endfor %}")]
    public async Task EverythingReachesATextWriter(string source)
    {
        using var writer = new StringWriter();

        await _parser.Parse(source).RenderAsync(writer, new TemplateContext(CreateOptions()));

        Assert.Equal("[1][2][3]", writer.ToString());
    }

    [Fact]
    public async Task AContextCanBeReusedAfterAFailedRender()
    {
        var options = CreateOptions();
        var context = new TemplateContext(options);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _parser.Parse("{% render 'item', i: 1 %}{% render 'missing' %}").RenderAsync(new FlushCountingOutput(), NullEncoder.Default, context).AsTask());

        var output = new FlushCountingOutput();
        await _parser.Parse("{% render 'item', i: 2 %}").RenderAsync(output, NullEncoder.Default, context);

        Assert.Equal("[2]", output.ToString());
        Assert.Equal(1, output.Flushes);
    }
}
