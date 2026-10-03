using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Utils;
using Xunit;

namespace Fluid.Tests
{
    /// <summary>
    /// Tests for TextWriterFluidOutput to ensure proper buffering, flushing, and disposal behavior.
    /// Related to issue #919: TextWriterFluidOutput.Dispose() requires force sync
    /// </summary>
    public class TextWriterFluidOutputTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidArguments_CreatesInstance()
        {
            using var writer = new StringWriter();
            using var output = new TextWriterFluidOutput(writer, bufferSize: 1024);
            
            Assert.NotNull(output);
        }

        [Fact]
        public void Constructor_WithNullWriter_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new TextWriterFluidOutput(null!, bufferSize: 1024));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void Constructor_WithInvalidBufferSize_ThrowsArgumentOutOfRangeException(int bufferSize)
        {
            using var writer = new StringWriter();
            Assert.Throws<ArgumentOutOfRangeException>(() => new TextWriterFluidOutput(writer, bufferSize));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(16)]
        [InlineData(1024)]
        [InlineData(16 * 1024)]
        public void Constructor_WithValidBufferSizes_CreatesInstance(int bufferSize)
        {
            using var writer = new StringWriter();
            using var output = new TextWriterFluidOutput(writer, bufferSize);
            Assert.NotNull(output);
        }

        [Fact]
        public void Constructor_WithCustomArrayPool_UsesProvidedPool()
        {
            using var writer = new StringWriter();
            var customPool = ArrayPool<char>.Create();
            using var output = new TextWriterFluidOutput(writer, bufferSize: 1024, pool: customPool);
            
            Assert.NotNull(output);
        }

        #endregion

        #region Write String Tests

        [Fact]
        public async Task Write_String_WritesContent()
        {
            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                output.Write("Hello World");
                await output.FlushAsync();
            }
            
            Assert.Equal("Hello World", writer.ToString());
        }

        [Fact]
        public async Task Write_EmptyString_DoesNothing()
        {
            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                output.Write("");
                output.Write(string.Empty);
                await output.FlushAsync();
            }
            
            Assert.Equal("", writer.ToString());
        }

        [Fact]
        public async Task Write_NullString_DoesNothing()
        {
            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                output.Write((string)null!);
                await output.FlushAsync();
            }
            
            Assert.Equal("", writer.ToString());
        }

        [Fact]
        public async Task Write_MultipleStrings_ConcatenatesContent()
        {
            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                output.Write("Hello");
                output.Write(" ");
                output.Write("World");
                await output.FlushAsync();
            }
            
            Assert.Equal("Hello World", writer.ToString());
        }

        [Fact]
        public async Task Write_StringLargerThanBuffer_WritesDirect()
        {
            using var writer = new StringWriter();
            var largeString = new string('x', 2048);
            
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                output.Write(largeString);
                await output.FlushAsync();
            }
            
            Assert.Equal(largeString, writer.ToString());
        }

        [Fact]
        public async Task Write_StringExactBufferSize_FlushesBuffer()
        {
            using var writer = new StringWriter();
            var exactString = new string('a', 64);
            
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 64, leaveOpen: true))
            {
                output.Write(exactString);
                await output.FlushAsync();
            }
            
            Assert.Equal(exactString, writer.ToString());
        }

        #endregion

        #region Write Char Tests

        [Fact]
        public async Task Write_SingleChar_WritesContent()
        {
            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                output.Write("A");
                await output.FlushAsync();
            }
            
            Assert.Equal("A", writer.ToString());
        }

        [Fact]
        public async Task Write_MultipleChars_WritesAllContent()
        {
            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                output.Write("H");
                output.Write("e");
                output.Write("l");
                output.Write("l");
                output.Write("o");
                await output.FlushAsync();
            }
            
            Assert.Equal("Hello", writer.ToString());
        }

        [Fact]
        public async Task Write_CharsFillBuffer_FlushesAutomatically()
        {
            using var writer = new StringWriter();
            // Use very small buffer to trigger automatic flush
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 4, leaveOpen: true))
            {
                for (int i = 0; i < 10; i++)
                {
                    output.Write("x");
                }
                await output.FlushAsync();
            }
            
            Assert.Equal("xxxxxxxxxx", writer.ToString());
        }

        #endregion

        #region Write Char Array Tests

        [Fact]
        public async Task Write_CharArray_WritesContent()
        {
            using var writer = new StringWriter();
            var chars = "Hello World".ToCharArray();
            
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                output.Write(chars, 0, chars.Length);
                await output.FlushAsync();
            }
            
            Assert.Equal("Hello World", writer.ToString());
        }

        [Fact]
        public async Task Write_CharArraySubset_WritesPartialContent()
        {
            using var writer = new StringWriter();
            var chars = "Hello World".ToCharArray();
            
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                output.Write(chars, 6, 5); // "World"
                await output.FlushAsync();
            }
            
            Assert.Equal("World", writer.ToString());
        }

        [Fact]
        public async Task Write_EmptyCharArray_DoesNothing()
        {
            using var writer = new StringWriter();
            var chars = "Hello".ToCharArray();
            
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                output.Write(chars, 0, 0);
                await output.FlushAsync();
            }
            
            Assert.Equal("", writer.ToString());
        }

        [Fact]
        public void Write_NullCharArray_ThrowsArgumentNullException()
        {
            using var writer = new StringWriter();
            using var output = new TextWriterFluidOutput(writer, bufferSize: 1024);
            
            Assert.Throws<ArgumentNullException>(() => output.Write(null!, 0, 0));
        }

        [Fact]
        public async Task Write_CharArrayLargerThanBuffer_WritesDirect()
        {
            using var writer = new StringWriter();
            var chars = new string('y', 2048).ToCharArray();
            
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                output.Write(chars, 0, chars.Length);
                await output.FlushAsync();
            }
            
            Assert.Equal(new string('y', 2048), writer.ToString());
        }

        #endregion

        #region GetMemory and GetSpan Tests

        [Fact]
        public void GetMemory_ReturnsWritableMemory()
        {
            using var writer = new StringWriter();
            using var output = new TextWriterFluidOutput(writer, bufferSize: 1024);
            
            var memory = output.GetMemory(10);
            Assert.True(memory.Length >= 10);
        }

        [Fact]
        public void GetSpan_ReturnsWritableSpan()
        {
            using var writer = new StringWriter();
            using var output = new TextWriterFluidOutput(writer, bufferSize: 1024);
            
            var span = output.GetSpan(10);
            Assert.True(span.Length >= 10);
        }

        [Fact]
        public void GetMemory_WithZeroHint_ReturnsNonEmpty()
        {
            using var writer = new StringWriter();
            using var output = new TextWriterFluidOutput(writer, bufferSize: 1024);
            
            var memory = output.GetMemory(0);
            // With zero hint, returns available space
            Assert.True(memory.Length > 0);
        }

        [Fact]
        public void GetSpan_WithNegativeHint_ThrowsArgumentOutOfRangeException()
        {
            using var writer = new StringWriter();
            using var output = new TextWriterFluidOutput(writer, bufferSize: 1024);
            
            Assert.Throws<ArgumentOutOfRangeException>(() => output.GetSpan(-1));
        }

        [Fact]
        public void GetSpan_WithHintLargerThanBuffer_ThrowsArgumentOutOfRangeException()
        {
            using var writer = new StringWriter();
            // Create with small buffer size
            using var output = new TextWriterFluidOutput(writer, bufferSize: 64);
            
            // Request more than buffer can provide
            Assert.Throws<ArgumentOutOfRangeException>(() => output.GetSpan(128));
        }

        #endregion

        #region Advance Tests

        [Fact]
        public async Task Advance_WithValidCount_AdvancesPosition()
        {
            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                var span = output.GetSpan(5);
                "Hello".AsSpan().CopyTo(span);
                output.Advance(5);
                await output.FlushAsync();
            }
            
            Assert.Equal("Hello", writer.ToString());
        }

        [Fact]
        public void Advance_WithNegativeCount_ThrowsArgumentOutOfRangeException()
        {
            using var writer = new StringWriter();
            using var output = new TextWriterFluidOutput(writer, bufferSize: 1024);
            
            Assert.Throws<ArgumentOutOfRangeException>(() => output.Advance(-1));
        }

        [Fact]
        public async Task Advance_PastBufferSize_FlushesAutomatically()
        {
            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 64, leaveOpen: true))
            {
                // Fill buffer completely and trigger auto-flush
                var span = output.GetSpan(64);
                new string('z', 64).AsSpan().CopyTo(span);
                output.Advance(64);
                await output.FlushAsync();
            }
            
            Assert.Equal(new string('z', 64), writer.ToString());
        }

        #endregion

        #region FlushAsync Tests

        [Fact]
        public async Task FlushAsync_WithBufferedContent_WritesToWriter()
        {
            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                output.Write("Hello");
                
                // Content should not be in writer yet (buffered)
                Assert.Equal("", writer.ToString());
                
                await output.FlushAsync();
                
                // After flush, content should be visible
                Assert.Equal("Hello", writer.ToString());
            }
        }

        [Fact]
        public async Task FlushAsync_WithNoContent_ReturnsCompletedTask()
        {
            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                // Should not throw
                await output.FlushAsync();
            }
            
            Assert.Equal("", writer.ToString());
        }

        [Fact]
        public async Task FlushAsync_CalledMultipleTimes_DoesNotDuplicate()
        {
            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                output.Write("Test");
                await output.FlushAsync();
                await output.FlushAsync();
                await output.FlushAsync();
            }
            
            Assert.Equal("Test", writer.ToString());
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_FlushesBufferedContent()
        {
            using var writer = new StringWriter();
            
            var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true);
            output.Write("Hello");
            output.Dispose();
            
            Assert.Equal("Hello", writer.ToString());
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            using var writer = new StringWriter();
            var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true);
            
            output.Dispose();
            output.Dispose(); // Should not throw
        }

        [Fact]
        public void Dispose_WithLeaveOpenTrue_DoesNotDisposeWriter()
        {
            var writer = new StringWriter();
            var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true);
            output.Write("Test");
            output.Dispose();
            
            // Writer should still be usable
            writer.Write(" More");
            Assert.Equal("Test More", writer.ToString());
            
            writer.Dispose();
        }

        [Fact]
        public void Dispose_WithLeaveOpenFalse_DisposesWriter()
        {
            var writer = new TrackingStringWriter();
            var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: false);
            output.Write("Test");
            output.Dispose();
            
            Assert.True(writer.IsDisposed);
        }

        #endregion

        #region DisposeAsync Tests

        [Fact]
        public async Task DisposeAsync_FlushesBufferedContent()
        {
            using var writer = new StringWriter();
            
            var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true);
            output.Write("Hello");
            await output.DisposeAsync();
            
            Assert.Equal("Hello", writer.ToString());
        }

        [Fact]
        public async Task DisposeAsync_CalledMultipleTimes_DoesNotThrow()
        {
            using var writer = new StringWriter();
            var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true);
            
            await output.DisposeAsync();
            await output.DisposeAsync(); // Should not throw
        }

        [Fact]
        public async Task DisposeAsync_WithLeaveOpenTrue_DoesNotDisposeWriter()
        {
            var writer = new StringWriter();
            var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true);
            output.Write("Test");
            await output.DisposeAsync();
            
            // Writer should still be usable
            writer.Write(" More");
            Assert.Equal("Test More", writer.ToString());
            
            writer.Dispose();
        }

        [Fact]
        public async Task DisposeAsync_WithLeaveOpenFalse_DisposesWriter()
        {
            var writer = new TrackingStringWriter();
            var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: false);
            output.Write("Test");
            await output.DisposeAsync();
            
            Assert.True(writer.IsDisposed);
        }

        #endregion

        #region Issue #919: Sync Dispose with Async-Only Writer Tests

        /// <summary>
        /// Issue #919: When using ASP.NET Core with Kestrel (AllowSynchronousIO = false),
        /// synchronous write operations throw InvalidOperationException.
        /// This test simulates that scenario.
        /// </summary>
        [Fact]
        public async Task DisposeAsync_WithAsyncOnlyWriter_CompletesSuccessfully()
        {
            var asyncOnlyWriter = new AsyncOnlyTextWriter();
            
            var output = new TextWriterFluidOutput(asyncOnlyWriter, bufferSize: 1024, leaveOpen: true);
            output.Write("Hello World");
            
            // DisposeAsync should use FlushAsync internally and not trigger sync write
            await output.DisposeAsync();
            
            Assert.Equal("Hello World", asyncOnlyWriter.ToString());
            Assert.Equal(0, asyncOnlyWriter.SyncWriteCount);
            Assert.True(asyncOnlyWriter.AsyncWriteCount > 0);
        }

        [Fact]
        public void Write_LargeString_DefaultAllowSynchronousIO_UsesSyncWrite()
        {
            var trackingWriter = new TrackingTextWriter();
            var output = new TextWriterFluidOutput(trackingWriter, bufferSize: 16, leaveOpen: true);

            output.Write(new string('x', 128));
            output.Dispose();

            Assert.True(trackingWriter.SyncWriteCount > 0);
        }

        [Fact]
        public async Task Write_LargeString_WithAllowSynchronousIODisabled_UsesAsyncWrite()
        {
            var strictWriter = new StrictAsyncOnlyTextWriter();
            await using var output = new TextWriterFluidOutput(strictWriter, bufferSize: 16, leaveOpen: true, allowSynchronousIO: false);

            output.Write(new string('x', 128));
            await output.FlushAsync();

            Assert.Equal(new string('x', 128), strictWriter.ToString());
        }

        /// <summary>
        /// Tests that when content is buffered and sync Dispose is called,
        /// it performs a synchronous flush.
        /// </summary>
        [Fact]
        public void Dispose_WithBufferedContent_PerformsSyncFlush()
        {
            var trackingWriter = new TrackingTextWriter();
            
            var output = new TextWriterFluidOutput(trackingWriter, bufferSize: 1024, leaveOpen: true);
            output.Write("Hello");
            output.Dispose();
            
            Assert.Equal("Hello", trackingWriter.ToString());
            Assert.True(trackingWriter.SyncWriteCount > 0);
        }

        /// <summary>
        /// Tests the workaround scenario: explicitly call FlushAsync before sync Dispose
        /// to avoid sync write operations.
        /// </summary>
        [Fact]
        public async Task FlushAsync_ThenDispose_AvoidsExtraSyncWrite()
        {
            var trackingWriter = new TrackingTextWriter();
            
            var output = new TextWriterFluidOutput(trackingWriter, bufferSize: 1024, leaveOpen: true);
            output.Write("Hello");
            
            // Flush async first
            await output.FlushAsync();
            var syncWritesAfterFlush = trackingWriter.SyncWriteCount;
            
            // Then dispose - should not trigger another sync write since buffer is empty
            output.Dispose();
            
            Assert.Equal("Hello", trackingWriter.ToString());
            Assert.Equal(syncWritesAfterFlush, trackingWriter.SyncWriteCount);
        }

        /// <summary>
        /// Tests the 'await using' pattern ensures async disposal
        /// </summary>
        [Fact]
        public async Task AwaitUsing_Pattern_UsesAsyncDisposal()
        {
            var asyncOnlyWriter = new AsyncOnlyTextWriter();
            
            await using (var output = new TextWriterFluidOutput(asyncOnlyWriter, bufferSize: 1024, leaveOpen: true))
            {
                output.Write("Template content");
                await output.FlushAsync();
            }
            
            Assert.Equal("Template content", asyncOnlyWriter.ToString());
        }

        #endregion

        #region Integration Tests with Template Rendering

        [Fact]
        public async Task RenderTemplate_ToTextWriterFluidOutput_ProducesCorrectOutput()
        {
            var parser = new FluidParser();
            Assert.True(parser.TryParse("Hello {{ name }}", out var template, out _));
            
            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                var context = new TemplateContext();
                context.SetValue("name", "World");
                
                await template.RenderAsync(output, NullEncoder.Default, context);
                await output.FlushAsync();
            }
            
            Assert.Equal("Hello World", writer.ToString());
        }

        [Fact]
        public async Task RenderTemplate_WithLargeOutput_HandlesBuffering()
        {
            var parser = new FluidParser();
            // Create a template that produces output larger than buffer
            var templateSource = "{% for i in (1..100) %}ABCDEFGHIJ{% endfor %}";
            Assert.True(parser.TryParse(templateSource, out var template, out _));
            
            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 64, leaveOpen: true))
            {
                await template.RenderAsync(output, NullEncoder.Default, new TemplateContext());
                await output.FlushAsync();
            }
            
            var expected = string.Concat(Enumerable.Repeat("ABCDEFGHIJ", 100));
            Assert.Equal(expected, writer.ToString());
        }

        [Fact]
        public async Task RenderTemplate_WithEncoding_ProducesEncodedOutput()
        {
            var parser = new FluidParser();
            Assert.True(parser.TryParse("{{ content }}", out var template, out _));
            
            using var writer = new StringWriter();
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                var context = new TemplateContext();
                context.SetValue("content", "<script>alert('xss')</script>");
                
                await template.RenderAsync(output, HtmlEncoder.Default, context);
                await output.FlushAsync();
            }
            
            Assert.Equal("&lt;script&gt;alert(&#x27;xss&#x27;)&lt;/script&gt;", writer.ToString());
        }

        #endregion

        #region Edge Cases and Boundary Conditions

        [Fact]
        public async Task Write_ContentExactlyFillingBuffer_FlushesCorrectly()
        {
            using var writer = new StringWriter();
            const int bufferSize = 16;
            var content = new string('a', bufferSize);
            
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: bufferSize, leaveOpen: true))
            {
                output.Write(content);
                await output.FlushAsync();
            }
            
            Assert.Equal(content, writer.ToString());
        }

        [Fact]
        public async Task Write_ContentSpanningMultipleBuffers_FlushesCorrectly()
        {
            using var writer = new StringWriter();
            const int bufferSize = 16;
            var content = new string('b', bufferSize * 3 + 5); // 53 chars with 16-byte buffer
            
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: bufferSize, leaveOpen: true))
            {
                output.Write(content);
                await output.FlushAsync();
            }
            
            Assert.Equal(content, writer.ToString());
        }

        [Fact]
        public async Task Write_MixedContentSizes_FlushesCorrectly()
        {
            using var writer = new StringWriter();
            
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 32, leaveOpen: true))
            {
                output.Write("Small");
                output.Write(new string('m', 100)); // Large, direct write
                output.Write("Tiny");
                output.Write("!");
                await output.FlushAsync();
            }
            
            Assert.Equal("Small" + new string('m', 100) + "Tiny!", writer.ToString());
        }

        [Fact]
        public async Task MinimalBufferSize_StillWorks()
        {
            using var writer = new StringWriter();
            
            // Use minimum possible buffer size
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1, leaveOpen: true))
            {
                output.Write("Hello");
                await output.FlushAsync();
            }
            
            Assert.Equal("Hello", writer.ToString());
        }

        [Fact]
        public async Task UnicodeContent_HandledCorrectly()
        {
            using var writer = new StringWriter();
            var unicodeContent = "Hello 世界 🌍 مرحبا";
            
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                output.Write(unicodeContent);
                await output.FlushAsync();
            }
            
            Assert.Equal(unicodeContent, writer.ToString());
        }

        [Fact]
        public async Task NewlineCharacters_HandledCorrectly()
        {
            using var writer = new StringWriter();
            var contentWithNewlines = "Line1\nLine2\r\nLine3\rLine4";
            
            await using (var output = new TextWriterFluidOutput(writer, bufferSize: 1024, leaveOpen: true))
            {
                output.Write(contentWithNewlines);
                await output.FlushAsync();
            }
            
            Assert.Equal(contentWithNewlines, writer.ToString());
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// A TextWriter that tracks whether it was disposed
        /// </summary>
        private class TrackingStringWriter : StringWriter
        {
            public bool IsDisposed { get; private set; }

            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// A TextWriter that tracks sync vs async write calls
        /// </summary>
        private class TrackingTextWriter : StringWriter
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
        /// A TextWriter that throws on synchronous writes, simulating ASP.NET Core Kestrel
        /// with AllowSynchronousIO = false
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
                // In real Kestrel scenario, this would throw:
                // throw new InvalidOperationException("Synchronous operations are disallowed.");
                _sb.Append(value);
            }

            public override void Write(char[] buffer, int index, int count)
            {
                SyncWriteCount++;
                // In real Kestrel scenario, this would throw:
                // throw new InvalidOperationException("Synchronous operations are disallowed.");
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
        /// A TextWriter that throws InvalidOperationException on any synchronous write,
        /// exactly mimicking Kestrel's behavior when AllowSynchronousIO = false
        /// </summary>
        private class StrictAsyncOnlyTextWriter : TextWriter
        {
            private readonly StringBuilder _sb = new();
            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                throw new InvalidOperationException("Synchronous operations are disallowed. Call WriteAsync or set AllowSynchronousIO to true instead.");
            }

            public override void Write(char[] buffer, int index, int count)
            {
                throw new InvalidOperationException("Synchronous operations are disallowed. Call WriteAsync or set AllowSynchronousIO to true instead.");
            }

            public override void Write(string value)
            {
                throw new InvalidOperationException("Synchronous operations are disallowed. Call WriteAsync or set AllowSynchronousIO to true instead.");
            }

            public override Task WriteAsync(char value)
            {
                _sb.Append(value);
                return Task.CompletedTask;
            }

            public override Task WriteAsync(char[] buffer, int index, int count)
            {
                _sb.Append(buffer, index, count);
                return Task.CompletedTask;
            }

            public override Task WriteAsync(string value)
            {
                _sb.Append(value);
                return Task.CompletedTask;
            }

            public override string ToString() => _sb.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Tests specifically for the sync dispose issue (#919) when using strict async-only writers.
    /// These tests document the current behavior and expected behavior after fix.
    /// </summary>
    public class TextWriterFluidOutputSyncIssueTests
    {
        /// <summary>
        /// Documents current behavior: sync Dispose with buffered content fails 
        /// when writer doesn't allow sync operations.
        /// This test will PASS after issue #919 is fixed (Dispose won't throw).
        /// Until then, it documents that sync Dispose triggers sync write.
        /// </summary>
        [Fact]
        public void SyncDispose_WithStrictAsyncWriter_DocumentsCurrentBehavior()
        {
            var strictWriter = new StrictAsyncOnlyTextWriter();
            var output = new TextWriterFluidOutput(strictWriter, bufferSize: 1024, leaveOpen: true);
            output.Write("Hello");
            
            // This documents that sync Dispose currently performs sync write
            // After fix, this should either:
            // 1. Not throw (use Task.Wait() internally or similar)
            // 2. Or the sync Dispose pattern should be avoided entirely
            var exception = Record.Exception(() => output.Dispose());
            
            // Current behavior: throws InvalidOperationException from sync write
            // After fix: should not throw
            Assert.NotNull(exception);
            Assert.IsType<InvalidOperationException>(exception);
        }

        /// <summary>
        /// Demonstrates the recommended workaround: use DisposeAsync instead of Dispose
        /// </summary>
        [Fact]
        public async Task AsyncDispose_WithStrictAsyncWriter_WorksCorrectly()
        {
            var strictWriter = new StrictAsyncOnlyTextWriter();
            var output = new TextWriterFluidOutput(strictWriter, bufferSize: 1024, leaveOpen: true);
            output.Write("Hello");
            
            // DisposeAsync uses FlushAsync internally, so it works
            await output.DisposeAsync();
            
            Assert.Equal("Hello", strictWriter.ToString());
        }

        /// <summary>
        /// Demonstrates that using 'await using' is the safe pattern
        /// </summary>
        [Fact]
        public async Task AwaitUsing_WithStrictAsyncWriter_IsRecommendedPattern()
        {
            var strictWriter = new StrictAsyncOnlyTextWriter();
            
            await using (var output = new TextWriterFluidOutput(strictWriter, bufferSize: 1024, leaveOpen: true))
            {
                output.Write("Hello World");
            }
            
            Assert.Equal("Hello World", strictWriter.ToString());
        }

        /// <summary>
        /// Tests that when there's no buffered content, sync Dispose doesn't cause issues
        /// </summary>
        [Fact]
        public void SyncDispose_WithNoBufferedContent_DoesNotThrow()
        {
            var strictWriter = new StrictAsyncOnlyTextWriter();
            var output = new TextWriterFluidOutput(strictWriter, bufferSize: 1024, leaveOpen: true);
            
            // No content written, so Dispose shouldn't need to flush
            output.Dispose();
            
            Assert.Equal("", strictWriter.ToString());
        }

        /// <summary>
        /// Tests the pattern of pre-flushing before sync dispose
        /// </summary>
        [Fact]
        public async Task PreFlush_ThenSyncDispose_WorksWithStrictWriter()
        {
            var strictWriter = new StrictAsyncOnlyTextWriter();
            var output = new TextWriterFluidOutput(strictWriter, bufferSize: 1024, leaveOpen: true);
            output.Write("Hello");
            
            // Flush async first to empty the buffer
            await output.FlushAsync();
            
            // Now sync Dispose won't need to write anything
            output.Dispose();
            
            Assert.Equal("Hello", strictWriter.ToString());
        }

        /// <summary>
        /// A TextWriter that throws on any synchronous write operation
        /// </summary>
        private class StrictAsyncOnlyTextWriter : TextWriter
        {
            private readonly StringBuilder _sb = new();
            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                throw new InvalidOperationException("Synchronous operations are disallowed.");
            }

            public override void Write(char[] buffer, int index, int count)
            {
                throw new InvalidOperationException("Synchronous operations are disallowed.");
            }

            public override void Write(string value)
            {
                throw new InvalidOperationException("Synchronous operations are disallowed.");
            }

            public override Task WriteAsync(char value)
            {
                _sb.Append(value);
                return Task.CompletedTask;
            }

            public override Task WriteAsync(char[] buffer, int index, int count)
            {
                _sb.Append(buffer, index, count);
                return Task.CompletedTask;
            }

            public override Task WriteAsync(string value)
            {
                _sb.Append(value);
                return Task.CompletedTask;
            }

            public override string ToString() => _sb.ToString();
        }
    }
}
