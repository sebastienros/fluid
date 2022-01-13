using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Fluid.Tests.Mocks
{
    public class MockFileProvider : IFileProvider
    {
        private Dictionary<string, MockFileInfo> _files = new Dictionary<string, MockFileInfo>();
        private readonly bool _caseSensitive;

        public MockFileProvider(bool caseSensitive = false)
        {
            _caseSensitive = caseSensitive;
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            throw new NotImplementedException();
        }

        public IFileInfo GetFileInfo(string path)
        {
            path = NormalizePath(path);

            if (_files.ContainsKey(path))
            {
                return _files[path];
            }
            else
            {
                return MockFileInfo.Null;
            }
        }

        public MockFileProvider Add(string path, string content)
        {
            path = NormalizePath(path);

            _files[path] = new MockFileInfo(path, content);
            return this;
        }

        public IChangeToken Watch(string filter)
        {
            return NullChangeToken.Singleton;
        }

        private string NormalizePath(string path)
        {
            path = path.Replace('\\', '/');
            path = path.Replace('/', Path.DirectorySeparatorChar);
            path = String.Join(Path.DirectorySeparatorChar, path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));

            if (!_caseSensitive)
            {
                return path.ToLowerInvariant();
            }

            return path;
        }
    }
}
