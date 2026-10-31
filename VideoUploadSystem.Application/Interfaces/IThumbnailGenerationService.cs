using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoUploadSystem.Application.DTOs;

namespace VideoUploadSystem.Application.Interfaces
{
    public interface IThumbnailGenerationService
    {
        Task<GenerateThumbnailResponse> GenerateAsync(GenerateThumbnailRequest request, CancellationToken cancellationToken = default);

    }
}
