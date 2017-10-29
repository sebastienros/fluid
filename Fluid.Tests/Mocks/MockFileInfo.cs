﻿using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.FileProviders;

namespace Fluid.Tests.Mocks
{
    public class MockFileInfo : IFileInfo
    {
        public MockFileInfo(string name)
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
            var data = Encoding.UTF8.GetBytes("{{ 'Partial Content' }}");
            return new MemoryStream(data);
        }
    }
}
