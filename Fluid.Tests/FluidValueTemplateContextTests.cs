using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class FluidValueTemplateContextTests
    {
        [Fact]
        public void ToBooleanValue_WithContext_ShouldWork()
        {
            // Arrange
            var context = new TemplateContext();
            var value = BooleanValue.True;

            // Act
            var result = value.ToBooleanValue(context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ToNumberValue_WithContext_ShouldWork()
        {
            // Arrange
            var context = new TemplateContext();
            var value = NumberValue.Create(42);

            // Act
            var result = value.ToNumberValue(context);

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public void ToStringValue_WithContext_ShouldWork()
        {
            // Arrange
            var context = new TemplateContext();
            var value = new StringValue("Hello");

            // Act
            var result = value.ToStringValue(context);

            // Assert
            Assert.Equal("Hello", result);
        }

        [Fact]
        public void ToObjectValue_WithContext_ShouldWork()
        {
            // Arrange
            var context = new TemplateContext();
            var value = NumberValue.Create(42);

            // Act
            var result = value.ToObjectValue(context);

            // Assert
            Assert.Equal(42m, result);
        }

        [Fact]
        public void CustomFluidValue_CanOverrideMethods_WithContext()
        {
            // Arrange
            var context = new TemplateContext();
            context.SetValue("multiplier", 2);
            var value = new CustomFluidValue(5);

            // Act
            var result = value.ToNumberValue(context);

            // Assert - Custom implementation should use context
            Assert.Equal(10, result); // 5 * 2
        }

        [Fact]
        public void CustomFluidValue_WithNullContext_ShouldFallbackToDefault()
        {
            // Arrange
            var value = new CustomFluidValue(5);

            // Act
            var result = value.ToNumberValue(null);

            // Assert - Without context, should return base value
            Assert.Equal(5, result);
        }

        /// <summary>
        /// Example custom FluidValue that uses TemplateContext for conversion
        /// </summary>
        private class CustomFluidValue : FluidValue
        {
            private readonly decimal _value;

            public CustomFluidValue(decimal value)
            {
                _value = value;
            }

            public override FluidValues Type => FluidValues.Number;

            public override bool Equals(FluidValue other) => false;

            public override bool ToBooleanValue() => _value != 0;

            public override decimal ToNumberValue() => _value;

            public override string ToStringValue() => _value.ToString();

            public override object ToObjectValue() => _value;

            // Override to use TemplateContext
            public override decimal ToNumberValue(TemplateContext context)
            {
                if (context != null)
                {
                    // Example: multiply by a context value
                    var multiplier = context.GetValue("multiplier");
                    if (multiplier != null && multiplier.Type == FluidValues.Number)
                    {
                        return _value * multiplier.ToNumberValue();
                    }
                }

                return base.ToNumberValue(context);
            }
        }
    }
}
