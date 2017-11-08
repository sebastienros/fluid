using System;
using System.Collections.Generic;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Fluid.Tests.Mocks
{
    public class MockFileProvider : IFileProvider
    {
        private Dictionary<string, IFileInfo> _files = new Dictionary<string, IFileInfo>(StringComparer.OrdinalIgnoreCase);

        public MockFileProvider()
        {
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            throw new NotImplementedException();
        }

        public IFileInfo GetFileInfo(string path)
        {
            _files.TryGetValue(path, out var result);

            return result;
        }

        public IChangeToken Watch(string filter)
        {
            throw new NotImplementedException();
        }

        public MockFileProvider AddTextFile(string path, string content)
        {
            _files.Add(path, new MockFileInfo(System.IO.Path.GetFileName(path), content));

            return this;
        }
    }
}
