using System;
using System.Globalization;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class ResourcesProviderTests
    {
        [Theory]
        [InlineData("Hello", null, "Hello")]
        [InlineData("Hello", "fr-FR", "Bonjour")]
        [InlineData("Hello", "pl-PL", "Cześć")]
        [InlineData("Hello", "de-DE", "Hello")] // No translations for German - fallback.
        [InlineData("Missing", null, null)]
        public async Task GetStringShouldSucceed(string key, string culture, string expected)
        {
            // Arrange
            var cultureInfo = String.IsNullOrEmpty(culture)
                    ? CultureInfo.InvariantCulture
                    : CultureInfo.CreateSpecificCulture(culture)
                ;            
            var rp = new ResxResourcesProvider("Fluid.Tests.SR", GetType().Assembly);

            // Act
            var result = await rp.GetString(key, cultureInfo);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}