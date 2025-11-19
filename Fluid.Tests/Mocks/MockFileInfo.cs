using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.FileProviders;

namespace Fluid.Tests.Mocks
{
    public class MockFileInfo : IFileInfo
    {
        public static readonly MockFileInfo Null = new MockFileInfo("", "") { Exists = false };

        public MockFileInfo(string name, string content)
        {
            Name = Path.GetFileName(name);
            PhysicalPath = name.Replace('\\', Path.PathSeparator).Replace('/', Path.PathSeparator);
            Content = content;
            Exists = true;
        }

        public string Content { get; set; }

        public bool Exists { get; set; }

        public bool IsDirectory => false;

        public DateTimeOffset LastModified { get; set; } = DateTimeOffset.MinValue;

        public long Length => -1;

        public string Name { get; }

        public string PhysicalPath { get; }

        public bool Accessed { get; set; }

        public Stream CreateReadStream()
        {
            Accessed = true;
            var data = Encoding.UTF8.GetBytes(Content);
            return new MemoryStream(data);
        }
    }
}
