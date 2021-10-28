using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.FileProviders;

namespace Fluid.Tests.Mocks
{
    public class MockFileInfo : IFileInfo
    {
        public static readonly MockFileInfo Null = new MockFileInfo("", "") { _exists = false };

        private bool _exists = true;

        public MockFileInfo(string name, string content)
        {
            Name = name;
            Content = content;
        }

        public string Content { get; set; }
        public bool Exists => _exists;

        public bool IsDirectory => false;

        public DateTimeOffset LastModified => DateTimeOffset.MinValue;

        public long Length => -1;

        public string Name { get; }

        public string PhysicalPath => null;

        public Stream CreateReadStream()
        {
            var data = Encoding.UTF8.GetBytes(Content);
            return new MemoryStream(data);
        }
    }
}
