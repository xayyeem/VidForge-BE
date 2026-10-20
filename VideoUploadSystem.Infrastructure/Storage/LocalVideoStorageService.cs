using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoUploadSystem.Application.Interfaces;

namespace VideoUploadSystem.Infrastructure.Storage
{
    public class LocalVideoStorageService : IVideoStorageService
    {
        private readonly string _storage;
        public LocalVideoStorageService(IConfiguration configuration)
        {
            _storage = configuration["Storage:VideoPath"] ?? "Storage/Videos";
            _storage = Path.GetFullPath(_storage);
            Directory.CreateDirectory(_storage);
        }

        public async Task<string> SaveAsync(Stream stream, string FileName, CancellationToken cancellationToken)
        {
            var filePath = Path.Combine(_storage, FileName);
            await using (var fileStream = new FileStream(filePath,FileMode.Create,FileAccess.Write,FileShare.None, bufferSize:1024*64, useAsync:true))
            {
                await stream.CopyToAsync(fileStream, cancellationToken);
            }
            return filePath;
        }

        public async Task DeleteAsync(string FilePath, CancellationToken cancellationToken = default)
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
            await Task.CompletedTask;
        }
    }
}
