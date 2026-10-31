using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoUploadSystem.Application.DTOs
{
    public sealed class GenerateThumbnailResponse
    {
        public List<GeneratedThumbnailDto> Data { get; init; } = new();
    }
}
