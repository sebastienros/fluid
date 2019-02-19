using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;

namespace Fluid.Tests.Mocks
{
    public class MockFileProvider : IFileProvider
    {
        private Dictionary<string, MockFileInfo> _files = new Dictionary<string, MockFileInfo>();

        public MockFileProvider()
        {
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            throw new NotImplementedException();
        }

        public IFileInfo GetFileInfo(string path)
        {
            if (_files.ContainsKey(path))
            {
                return _files[path];
            }
            else
            {
                return null;
            }
        }

        public MockFileProvider Add(string path, string content)
        {
            _files[path] = new MockFileInfo(path, content);
            return this;
        }

        public IChangeToken Watch(string filter)
        {
            throw new NotImplementedException();
        }
    }
}
