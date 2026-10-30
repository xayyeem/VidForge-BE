using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoUploadSystem.Application.Interfaces
{
    public interface IVideoProcessingService
    {
        Task<VideoProcessingResult> ProcessAsync(Guid videoId, string storagePath, CancellationToken cancellationToken);
    }
    public sealed class VideoProcessingResult
    {
        public decimal DurationSeconds { get; init; }

        public string ThumbnailPath { get; init; } = string.Empty;
    }
}
