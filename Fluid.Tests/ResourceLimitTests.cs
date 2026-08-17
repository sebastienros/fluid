using Fluid.Values;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class ResourceLimitTests
    {
        [Fact]
        public async Task MaxOutputSizeLimitsStringRendering()
        {
            var template = new FluidParser().Parse("12345");
            var context = new TemplateContext { MaxOutputSize = 4 };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => template.RenderAsync(context).AsTask());

            Assert.Contains("maximum template output size", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task MaxOutputSizeLimitsTextWriterRendering()
        {
            var template = new FluidParser().Parse("12345");
            var context = new TemplateContext { MaxOutputSize = 4 };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => template.RenderAsync(new StringWriter(), context).AsTask());
        }

        [Fact]
        public async Task MaxOutputSizeAppliesInsideLessRestrictiveOutput()
        {
            var template = new FluidParser().Parse("12345");
            var context = new TemplateContext { MaxOutputSize = 4 };
            await using var writerOutput = new Fluid.Utils.TextWriterFluidOutput(
                new StringWriter(),
                bufferSize: 16);
            var output = Fluid.Utils.LimitedFluidOutput.Create(writerOutput, 100);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => template.RenderAsync(output, System.Text.Encodings.Web.HtmlEncoder.Default, context).AsTask());
        }

        [Fact]
        public async Task MaxOutputSizeLimitsCapturedContent()
        {
            var template = new FluidParser().Parse("{% capture value %}12345{% endcapture %}");
            var context = new TemplateContext { MaxOutputSize = 4 };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => template.RenderAsync(context).AsTask());
        }

        [Fact]
        public async Task MaxOutputSizeRejectsStringConcatenationBeforeAllocation()
        {
            var template = new FluidParser().Parse("{% assign value = '1234' %}{% assign value = value | append: value %}");
            var context = new TemplateContext { MaxOutputSize = 7 };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => template.RenderAsync(context).AsTask());
        }

        [Fact]
        public async Task MaxOutputSizeRejectsEmptyPatternReplacementBeforeAllocation()
        {
            var template = new FluidParser().Parse("{{ '' | replace: '', '1234' }}");
            var context = new TemplateContext { MaxOutputSize = 4 };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => template.RenderAsync(context).AsTask());
        }

        [Fact]
        public async Task MaxOutputSizeRejectsBase64BeforeAllocation()
        {
            var template = new FluidParser().Parse("{{ '1234' | base64_encode }}");
            var context = new TemplateContext { MaxOutputSize = 7 };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => template.RenderAsync(context).AsTask());
        }

        [Fact]
        public async Task ExactStringGrowthWithinLimitIsAllowed()
        {
            var template = new FluidParser().Parse("{{ 'abcdef' | newline_to_br }}");
            var context = new TemplateContext { MaxOutputSize = 6 };

            Assert.Equal("abcdef", await template.RenderAsync(context));
        }

        [Fact]
        public async Task MaxCollectionSizeLimitsRangesBeforeAllocation()
        {
            var template = new FluidParser().Parse("{{ (1..5) | size }}");
            var context = new TemplateContext { MaxCollectionSize = 4 };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => template.RenderAsync(context).AsTask());
        }

        [Fact]
        public async Task MaxCollectionSizeLimitsSplitBeforeMaterialization()
        {
            var template = new FluidParser().Parse("{{ 'a,b,c' | split: ',' | size }}");
            var context = new TemplateContext { MaxCollectionSize = 2 };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => template.RenderAsync(context).AsTask());
        }

        [Fact]
        public async Task MaxCollectionSizeLimitsArraySlice()
        {
            var template = new FluidParser().Parse("{{ values | slice: 0, 3 | size }}");
            var context = new TemplateContext(new
            {
                values = new[] { 1, 2, 3, 4, 5 }
            })
            {
                MaxCollectionSize = 2
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => template.RenderAsync(context).AsTask());
        }

        [Fact]
        public async Task MaxStepsAppliesToArrayFilterEnumeration()
        {
            var template = new FluidParser().Parse("{{ values | join: ',' }}");
            var context = new TemplateContext(new
            {
                values = new[] { 1, 2, 3, 4, 5 }
            })
            {
                MaxSteps = 2
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => template.RenderAsync(context).AsTask());
        }

        [Fact]
        public void CircularArrayStringificationThrows()
        {
            var values = new List<FluidValue>();
            var array = new ArrayValue(values);
            values.Add(array);

            var exception = Assert.Throws<InvalidOperationException>(() => array.ToStringValue());

            Assert.Contains("circular", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CircularArrayHashingThrows()
        {
            var values = new List<FluidValue>();
            var array = new ArrayValue(values);
            values.Add(array);

            Assert.Throws<InvalidOperationException>(() => array.GetHashCode());
        }

        [Fact]
        public void AcyclicSharedArrayEqualityDoesNotLookRecursive()
        {
            var child = new ArrayValue([]);
            var middle = new ArrayValue([child]);
            var parent = new ArrayValue([middle]);

            Assert.False(parent.Equals(middle));
        }
    }
}
