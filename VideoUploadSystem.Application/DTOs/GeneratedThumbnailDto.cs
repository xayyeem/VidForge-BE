using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoUploadSystem.Application.DTOs
{
    public sealed class GeneratedThumbnailDto
    {
        public Guid Id { get; init; }

        public string ThumbnailUrl { get; init; } = string.Empty;
    }
}
