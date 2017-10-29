using System;
using System.IO;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Fluid.Tests.Mocks
{
    public class MockFileProvider : IFileProvider
    {
        private string _partialsFolderPath;

        public MockFileProvider(string path)
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
            return new MockFileInfo(path);
        }

        public IChangeToken Watch(string filter)
        {
            // Makes the test happy with IMemoryCache 
            return NullChangeToken.Singleton;
        }
    }
}
