using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Fluid.Utils;
using Xunit;

namespace Fluid.Tests
{
    /// <summary>
    /// Integration tests for IFluidOutput implementations with realistic template rendering scenarios.
    /// These tests focus on issue #919 behavior and real-world usage patterns.
    /// </summary>
    public class FluidOutputIntegrationTests
    {
        private static readonly FluidParser _parser = new FluidParser();

        #region TextWriterFluidOutput Integration Tests

        [Fact]
        public async Task TextWriterFluidOutput_ComplexTemplate_RendersCorrectly()
        {
            var templateSource = @"
{% for item in items %}
  <div class=""item"">
    <h2>{{ item.name }}</h2>
    <p>{{ item.description }}</p>
    {% if item.active %}
      <span class=""active"">Active</span>
    {% endif %}
  </div>
{% endfor %}
";
            Assert.True(_parser.TryParse(templateSource, out var template, out _));

            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 256, leaveOpen: true))
            {
                var context = new TemplateContext();
                context.SetValue("items", new[]
                {
                    new { name = "Item 1", description = "First item", active = true },
                    new { name = "Item 2", description = "Second item", active = false },
                    new { name = "Item 3", description = "Third item", active = true }
                });

                await template.RenderAsync(output, HtmlEncoder.Default, context);
            }

            var result = writer.ToString();
            Assert.Contains("Item 1", result);
            Assert.Contains("Item 2", result);
            Assert.Contains("Item 3", result);
            Assert.Contains("<span class=\"active\">Active</span>", result);
        }

        [Fact]
        public async Task TextWriterFluidOutput_NestedLoops_HandlesBufferingCorrectly()
        {
            var templateSource = @"{% for i in (1..5) %}{% for j in (1..10) %}[{{ i }},{{ j }}]{% endfor %}
{% endfor %}";
            Assert.True(_parser.TryParse(templateSource, out var template, out _));

            using var writer = new StringWriter();
            // Use small buffer to test frequent flushing
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 32, leaveOpen: true))
            {
                await template.RenderAsync(output, NullEncoder.Default, new TemplateContext());
            }

            var result = writer.ToString();
            Assert.Contains("[1,1]", result);
            Assert.Contains("[5,10]", result);
        }

        [Fact]
        public async Task TextWriterFluidOutput_LargeVariableContent_HandlesDirectWrite()
        {
            var templateSource = "{{ content }}";
            Assert.True(_parser.TryParse(templateSource, out var template, out _));

            var largeContent = new string('x', 10_000);
            using var writer = new StringWriter();
            // Use buffer smaller than content to test direct write path
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                var context = new TemplateContext();
                context.SetValue("content", largeContent);
                await template.RenderAsync(output, NullEncoder.Default, context);
            }

            Assert.Equal(largeContent, writer.ToString());
        }

        #endregion

        #region Issue #919: Async Writer Compatibility Tests

        [Fact]
        public async Task AsyncOnlyWriter_WithAwaitUsing_WorksCorrectly()
        {
            var asyncWriter = new AsyncOnlyTextWriter();
            var templateSource = "Hello {{ name }}!";
            Assert.True(_parser.TryParse(templateSource, out var template, out _));

            await using (var output = new TextWriterFluidOutput(asyncWriter, bufferSize: 1024, leaveOpen: true))
            {
                var context = new TemplateContext();
                context.SetValue("name", "World");
                await template.RenderAsync(output, NullEncoder.Default, context);
            }

            Assert.Equal("Hello World!", asyncWriter.ToString());
            Assert.Equal(0, asyncWriter.SyncWriteCount);
        }

        [Fact]
        public async Task AsyncOnlyWriter_WithExplicitFlushAsync_WorksCorrectly()
        {
            var asyncWriter = new AsyncOnlyTextWriter();

            var output = new TextWriterFluidOutput(asyncWriter, bufferSize: 1024, leaveOpen: true);
            output.Write("Test content");

            // Explicit async flush
            await output.FlushAsync();

            // Then dispose async to avoid sync write
            await output.DisposeAsync();

            Assert.Equal("Test content", asyncWriter.ToString());
            Assert.Equal(0, asyncWriter.SyncWriteCount);
        }

        [Fact]
        public async Task AsyncOnlyWriter_MultipleFlushes_AllAsync()
        {
            var asyncWriter = new AsyncOnlyTextWriter();

            await using (var output = new TextWriterFluidOutput(asyncWriter, bufferSize: 16, leaveOpen: true))
            {
                // Write multiple chunks to trigger multiple flushes
                for (int i = 0; i < 10; i++)
                {
                    output.Write($"Chunk{i}__");
                    await output.FlushAsync();
                }
            }

            Assert.Equal(0, asyncWriter.SyncWriteCount);
            Assert.True(asyncWriter.AsyncWriteCount >= 10);
        }

        [Fact]
        public void SyncDispose_WithBufferedContent_TriggersSync()
        {
            var trackingWriter = new SyncTrackingTextWriter();

            var output = new TextWriterFluidOutput(trackingWriter, bufferSize: 1024, leaveOpen: true);
            output.Write("Content to flush");
            output.Dispose();

            Assert.True(trackingWriter.SyncWriteCount > 0);
            Assert.Equal("Content to flush", trackingWriter.ToString());
        }

        [Fact]
        public async Task AsyncDispose_WithBufferedContent_TriggersAsync()
        {
            var trackingWriter = new SyncTrackingTextWriter();

            var output = new TextWriterFluidOutput(trackingWriter, bufferSize: 1024, leaveOpen: true);
            output.Write("Content to flush");
            await output.DisposeAsync();

            Assert.True(trackingWriter.AsyncWriteCount > 0);
            Assert.Equal("Content to flush", trackingWriter.ToString());
        }

        #endregion

        #region Concurrent Access Tests

        [Fact]
        public async Task MultipleTemplates_SequentialRendering_Isolated()
        {
            var template1Source = "Template1: {{ value }}";
            var template2Source = "Template2: {{ value }}";
            Assert.True(_parser.TryParse(template1Source, out var template1, out _));
            Assert.True(_parser.TryParse(template2Source, out var template2, out _));

            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 256, leaveOpen: true))
            {
                var context1 = new TemplateContext();
                context1.SetValue("value", "A");
                await template1.RenderAsync(output, NullEncoder.Default, context1);

                output.Write(" | ");

                var context2 = new TemplateContext();
                context2.SetValue("value", "B");
                await template2.RenderAsync(output, NullEncoder.Default, context2);
            }

            Assert.Equal("Template1: A | Template2: B", writer.ToString());
        }

        #endregion

        #region Error Recovery Tests

        [Fact]
        public async Task DisposalAfterPartialWrite_ReleasesResources()
        {
            using var writer = new StringWriter();
            var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true);

            output.Write("Partial content");

            // Simulate some work then dispose
            await Task.Delay(1);
            await output.DisposeAsync();

            // Writer should still be usable since leaveOpen = true
            writer.Write(" more");
            Assert.Equal("Partial content more", writer.ToString());
        }

        [Fact]
        public async Task EmptyTemplate_NoOutputProduced()
        {
            var templateSource = "";
            Assert.True(_parser.TryParse(templateSource, out var template, out _));

            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 256, leaveOpen: true))
            {
                await template.RenderAsync(output, NullEncoder.Default, new TemplateContext());
            }

            Assert.Equal("", writer.ToString());
        }

        [Fact]
        public async Task WhitespaceOnlyTemplate_OutputsWhitespace()
        {
            var templateSource = "   \n\t\r\n   ";
            Assert.True(_parser.TryParse(templateSource, out var template, out _));

            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 256, leaveOpen: true))
            {
                await template.RenderAsync(output, NullEncoder.Default, new TemplateContext());
            }

            Assert.Equal("   \n\t\r\n   ", writer.ToString());
        }

        #endregion

        #region Encoding Integration Tests

        [Fact]
        public async Task HtmlEncoding_EscapesSpecialCharacters()
        {
            var templateSource = "{{ content }}";
            Assert.True(_parser.TryParse(templateSource, out var template, out _));

            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 256, leaveOpen: true))
            {
                var context = new TemplateContext();
                context.SetValue("content", "<script>alert('XSS')</script>");
                await template.RenderAsync(output, HtmlEncoder.Default, context);
            }

            var result = writer.ToString();
            Assert.DoesNotContain("<script>", result);
            Assert.Contains("&lt;script&gt;", result);
        }

        [Fact]
        public async Task RawFilter_BypassesEncoding()
        {
            var templateSource = "{{ content | raw }}";
            Assert.True(_parser.TryParse(templateSource, out var template, out _));

            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 256, leaveOpen: true))
            {
                var context = new TemplateContext();
                context.SetValue("content", "<b>Bold</b>");
                await template.RenderAsync(output, HtmlEncoder.Default, context);
            }

            Assert.Equal("<b>Bold</b>", writer.ToString());
        }

        #endregion

        #region Real-World Pattern Tests

        [Fact]
        public async Task WebServerPattern_StreamResponse()
        {
            // Simulates streaming a template response to a web client
            var templateSource = @"<!DOCTYPE html>
<html>
<head><title>{{ title }}</title></head>
<body>
  <h1>{{ title }}</h1>
  <ul>
  {% for item in items %}
    <li>{{ item }}</li>
  {% endfor %}
  </ul>
</body>
</html>";
            Assert.True(_parser.TryParse(templateSource, out var template, out _));

            // Simulate a response stream with async writes
            var responseStream = new AsyncMemoryStream();
            await using (var streamWriter = new StreamWriter(responseStream, Encoding.UTF8, leaveOpen: true))
            await using (var output = new TextWriterFluidOutput(streamWriter, bufferSize: 512, leaveOpen: true))
            {
                var context = new TemplateContext();
                context.SetValue("title", "Test Page");
                context.SetValue("items", Enumerable.Range(1, 10).Select(i => $"Item {i}"));
                await template.RenderAsync(output, HtmlEncoder.Default, context);
            }

            responseStream.Position = 0;
            using var reader = new StreamReader(responseStream);
            var html = await reader.ReadToEndAsync();

            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("<title>Test Page</title>", html);
            Assert.Contains("<li>Item 1</li>", html);
            Assert.Contains("<li>Item 10</li>", html);
        }

        [Fact]
        public async Task EmailTemplatePattern_BufferedOutput()
        {
            // Simulates generating an email body that needs to be captured as a string
            var templateSource = @"Dear {{ recipient.name }},

Thank you for your order #{{ order.id }}.

Items:
{% for item in order.items %}
- {{ item.name }}: ${{ item.price }}
{% endfor %}

Total: ${{ order.total }}

Best regards,
The Team";
            Assert.True(_parser.TryParse(templateSource, out var template, out _));

            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                var context = new TemplateContext();
                context.SetValue("recipient", new { name = "John Doe" });
                context.SetValue("order", new
                {
                    id = "12345",
                    items = new[]
                    {
                        new { name = "Widget", price = "9.99" },
                        new { name = "Gadget", price = "19.99" }
                    },
                    total = "29.98"
                });
                await template.RenderAsync(output, NullEncoder.Default, context);
            }

            var emailBody = writer.ToString();
            Assert.Contains("Dear John Doe", emailBody);
            Assert.Contains("order #12345", emailBody);
            Assert.Contains("Widget: $9.99", emailBody);
            Assert.Contains("Total: $29.98", emailBody);
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// A TextWriter that simulates async-only behavior (like Kestrel with AllowSynchronousIO = false)
        /// </summary>
        private class AsyncOnlyTextWriter : TextWriter
        {
            private readonly StringBuilder _sb = new();
            public int SyncWriteCount { get; private set; }
            public int AsyncWriteCount { get; private set; }

            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                SyncWriteCount++;
                _sb.Append(value);
            }

            public override void Write(char[] buffer, int index, int count)
            {
                SyncWriteCount++;
                _sb.Append(buffer, index, count);
            }

            public override void Write(string value)
            {
                SyncWriteCount++;
                _sb.Append(value);
            }

            public override Task WriteAsync(char value)
            {
                AsyncWriteCount++;
                _sb.Append(value);
                return Task.CompletedTask;
            }

            public override Task WriteAsync(char[] buffer, int index, int count)
            {
                AsyncWriteCount++;
                _sb.Append(buffer, index, count);
                return Task.CompletedTask;
            }

            public override Task WriteAsync(string value)
            {
                AsyncWriteCount++;
                _sb.Append(value);
                return Task.CompletedTask;
            }

            public override string ToString() => _sb.ToString();
        }

        /// <summary>
        /// A TextWriter that tracks sync vs async write calls
        /// </summary>
        private class SyncTrackingTextWriter : StringWriter
        {
            public int SyncWriteCount { get; private set; }
            public int AsyncWriteCount { get; private set; }

            public override void Write(char[] buffer, int index, int count)
            {
                SyncWriteCount++;
                base.Write(buffer, index, count);
            }

            public override void Write(string value)
            {
                SyncWriteCount++;
                base.Write(value);
            }

            public override Task WriteAsync(char[] buffer, int index, int count)
            {
                AsyncWriteCount++;
                return base.WriteAsync(buffer, index, count);
            }

            public override Task WriteAsync(string value)
            {
                AsyncWriteCount++;
                return base.WriteAsync(value);
            }
        }

        /// <summary>
        /// A MemoryStream wrapper that supports async operations
        /// </summary>
        private class AsyncMemoryStream : MemoryStream
        {
            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await base.WriteAsync(buffer, offset, count, cancellationToken);
            }

            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                await base.WriteAsync(buffer, cancellationToken);
            }
        }

        #endregion
    }
}
