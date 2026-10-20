using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoUploadSystem.Application.Interfaces
{
    public interface IVideoStorageService
    {
        Task<string> SaveAsync(Stream stream, string FileName, CancellationToken cancellationToken);
        Task DeleteAsync(string FilePath, CancellationToken cancellationToken);
    }
}
