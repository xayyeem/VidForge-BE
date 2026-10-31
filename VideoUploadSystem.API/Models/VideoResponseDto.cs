using VideoUploadSystem.Domain.Enums;

namespace VideoUploadSystem.API.Models
{
    public class VideoResponseDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string OriginalFileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public decimal? DurationSeconds { get; set; }

        public VideoStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? ThumbnailUrl { get; set; }

        public string StreamUrl { get; set; } = string.Empty;
    }
}
