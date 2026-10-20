using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoUploadSystem.Domain.Enums;

namespace VideoUploadSystem.Domain.Entities
{
    public class Video
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string OriginalFileName { get; set; } = string.Empty;

        public string StoragePath { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public decimal? DurationSeconds { get; set; }

        public string? ThumbnailPath { get; set; }

        public VideoStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
