namespace VideoUploadSystem.API.Models
{
    public class UploadVideoRequest
    {
        public string Title { get; set; } = string.Empty;
        // Manual thumbnail
        public IFormFile? Thumbnail { get; set; }

        // AI generated thumbnail
        public Guid? GeneratedThumbnailId { get; set; }

        public string? Description { get; set; }

        public IFormFile File { get; set; } = default!;
    }
}
