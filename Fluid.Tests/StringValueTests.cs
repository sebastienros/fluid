using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class StringValueTests
    {
        [Theory]
        [InlineData(null, "", true)]
        [InlineData("", "", true)]
        [InlineData("Foo", "Foo", false)]
        public void CreateStringValue(string value, string expected, bool isEmpty)
        {
            // Arrange & Act
            var stringValue = new StringValue(value);

            // Assert
            Assert.Equal(expected, stringValue.ToStringValue());
            Assert.Equal(isEmpty, stringValue.Equals(StringValue.Empty));
        }

        [Fact]
        public void StringValueCreateNullShouldReturnEmpty()
        {
            var stringValue = new StringValue(null);

            // Assert
            Assert.Equal(StringValue.Empty, stringValue);
        }
    }
}
