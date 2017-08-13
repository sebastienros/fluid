using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Values;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
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
                    FileProvider = new TestFileProvider("NonPartials")
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
                FileProvider = new TestFileProvider("Partials")
            };

            await new IncludeStatement(expression).WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("Partial Content", sw.ToString());
        }

        public class TestFileProvider : IFileProvider
        {
            private string _partialsFolderPath;

            public TestFileProvider(string path)
            {
                if (path != "Partials")
                {
                    throw new DirectoryNotFoundException();
                }

                _partialsFolderPath = path;
            }

            public IDirectoryContents GetDirectoryContents(string subpath)
            {
                throw new NotImplementedException();
            }

            public IFileInfo GetFileInfo(string subpath)
            {
                var path = Path.Combine(_partialsFolderPath, subpath);
                return new TestFileInfo(path);
            }

            public IChangeToken Watch(string filter)
            {
                throw new NotImplementedException();
            }
        }

        public class TestFileInfo : IFileInfo
        {
            public TestFileInfo(string name)
            {
                Name = name;
            }

            public bool Exists => true;

            public bool IsDirectory => false;

            public DateTimeOffset LastModified => DateTimeOffset.MinValue;

            public long Length => -1;

            public string Name { get; }

            public string PhysicalPath => null;

            public Stream CreateReadStream()
            {
                var data = Encoding.UTF8.GetBytes("Partial Content");
                return new MemoryStream(data);
            }
        }
    }
}