using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fluid.ViewEngine
{
    public class FileProviderMapper : IFileProvider, ITemplateFileProvider
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

        public ValueTask<TemplateSourceInfo> GetFileInfoAsync(
            string subpath,
            TemplateContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = GetFileInfo(subpath);
            if (fileInfo == null || !fileInfo.Exists || fileInfo.IsDirectory)
            {
                return default;
            }

            return new ValueTask<TemplateSourceInfo>(
                new TemplateSourceInfo(
                    fileInfo.LastModified,
                    _ => new ValueTask<Stream>(fileInfo.CreateReadStream())));
        }

        public IChangeToken Watch(string filter)
        {
            var mappedFilter = _mappedFolder + filter;
            return _fileProvider.Watch(mappedFilter);
        }
    }
}
