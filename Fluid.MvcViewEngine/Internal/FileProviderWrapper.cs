﻿using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.IO;

namespace Fluid.MvcViewEngine.Internal
{
    public class FileProviderWrapper : IFileProvider
    {
        private readonly IFileProvider _fileProvider;
        private readonly string _partialsFolder;

        public FileProviderWrapper(IFileProvider fileProvider)
        {
            _fileProvider = fileProvider;
        }

        public FileProviderWrapper(IFileProvider fileProvider, string partialsFolder = "Partials")
        {
            _fileProvider = fileProvider;
            _partialsFolder = partialsFolder;
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            var path = Path.Combine(_partialsFolder, subpath);
            return _fileProvider.GetDirectoryContents(path);
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            var path = Path.Combine(_partialsFolder, subpath);
            return _fileProvider.GetFileInfo(path);
        }

        public IChangeToken Watch(string filter)
        {
            return _fileProvider.Watch(filter);
        }
    }
}
