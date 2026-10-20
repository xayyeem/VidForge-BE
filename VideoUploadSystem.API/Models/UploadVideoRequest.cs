namespace VideoUploadSystem.API.Models
{
    public class UploadVideoRequest
    {
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public IFormFile File { get; set; } = default!;
    }
}
