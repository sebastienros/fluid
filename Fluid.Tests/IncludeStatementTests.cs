using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Values;
using Fluid.Tests.Mocks;
using Xunit;

namespace Fluid.Tests
{
    public class IncludeStatementTests
    {
        [Fact]
        public async Task IncludeSatement_ShouldThrowFileNotFoundException_IfTheFileProviderIsNotPresent()
        {
            var expression = new LiteralExpression(new StringValue("_Partial.liquid"));
            var sw = new StringWriter();

            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                new IncludeStatement(expression).WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext())
            );
        }

        [Fact]
        public async Task IncludeSatement_ShouldThrowDirectoryNotFoundException_IfThePartialsFolderNotExist()
        {
            var expression = new LiteralExpression(new StringValue("_Partial.liquid"));

            await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            {
                var sw = new StringWriter();
                var context = new TemplateContext
                {
                    FileProvider = new MockFileProvider("NonPartials")
                };

                return new IncludeStatement(expression).WriteToAsync(sw, HtmlEncoder.Default, context);
            });
        }

        [Fact]
        public async Task IncludeSatement_ShouldLoadPartial_IfThePartialsFolderExist()
        {
            var expression = new LiteralExpression(new StringValue("_Partial.liquid"));

            var sw = new StringWriter();
            var context = new TemplateContext
            {
                FileProvider = new MockFileProvider("Partials")
            };

            await new IncludeStatement(expression).WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("Partial Content", sw.ToString());
        }
    }
}