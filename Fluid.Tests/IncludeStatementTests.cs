using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Fluid.Ast;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class IncludeStatementTests
    {
        [Fact]
        public async Task IncludeSatement_ShouldThrowsFileNotFoundException_IfTheFileProviderIsNotPresent()
        {
            var stmt = new IncludeStatement(
                new LiteralExpression(new StringValue("_Partial.liquid"))
            );
            var sw = new StringWriter();

            await Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await stmt.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext())
            );
        }

        [Fact]
        public async Task IncludeSatement_ShouldThrowsDirectoryNotFoundException_IfThePartialsFolderNotExist()
        {
            var stmt = new IncludeStatement(
                new LiteralExpression(new StringValue("_Partial.liquid"))
            );      
            await Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
            {
                var sw = new StringWriter();
                var context = new TemplateContext
                {
                    FileProvider = new TestFileProvider("NonPartials")
                };
                await stmt.WriteToAsync(sw, HtmlEncoder.Default, context);
            });
        }

        [Fact]
        public async Task IncludeSatement_ShouldLoadPartial_IfThePartialsFolderExist()
        {
            var stmt = new IncludeStatement(
                new LiteralExpression(new StringValue("_Partial.liquid"))
            );
            var sw = new StringWriter();
            var context = new TemplateContext
            {
                FileProvider = new TestFileProvider("Partials")
            };
            await stmt.WriteToAsync(sw, HtmlEncoder.Default, context);

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

            public bool Exists => false;

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
