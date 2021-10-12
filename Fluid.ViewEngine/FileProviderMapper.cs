using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.IO;

namespace Fluid.ViewEngine
{
    public class FileProviderMapper : IFileProvider
    {
        private readonly IFileProvider _fileProvider;
        private readonly string _mappedFolder;

        public FileProviderMapper(IFileProvider fileProvider, string mappedFolder)
        {
            _fileProvider = fileProvider;
            _mappedFolder = mappedFolder;

            if (!_mappedFolder.EndsWith("/") || _mappedFolder.EndsWith("\\"))
            {
                _mappedFolder = _mappedFolder + Path.DirectorySeparatorChar;
            }
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            var path = _mappedFolder + subpath;
            return _fileProvider.GetDirectoryContents(path);
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            var path = _mappedFolder + subpath;
            return _fileProvider.GetFileInfo(path);
        }

        public IChangeToken Watch(string filter)
        {
            var mappedFilter = _mappedFolder + filter;
            return _fileProvider.Watch(mappedFilter);
        }
    }
}
