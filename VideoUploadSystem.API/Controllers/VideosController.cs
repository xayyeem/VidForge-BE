using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using VideoUploadSystem.API.Models;
using VideoUploadSystem.Application.Interfaces;
using VideoUploadSystem.Application.Messages;
using VideoUploadSystem.Domain.Entities;
using VideoUploadSystem.Domain.Enums;
using VideoUploadSystem.Infrastructure.Data;
using VideoUploadSystem.Infrastructure.Messaging;

namespace VideoUploadSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VideosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IVideoStorageService _storage;
        private readonly RabbitMqPublisher _publisher;
        private const long MaxFileSize = 2L * 1024 * 1024 * 1024;
        private static readonly string[] AllowedExtensions =
        [
            ".mp4",
                ".mov",
                ".webm",
                ".mkv"
        ];

        public VideosController(ApplicationDbContext context, IVideoStorageService storage, RabbitMqPublisher publisher)
        {
            _context = context;
            _storage = storage;
            _publisher = publisher;
        }
        [HttpGet]
        [Route("getvideos")]
        public IActionResult Get([FromQuery] string sort = "NewestFirst")
        {
            var videos = _context.Videos.ToList();

            switch (sort)
            {
                case nameof(SortingEnum.NewestFirst):
                    videos = videos.OrderByDescending(v => v.CreatedAt).ToList();
                    break;

                case nameof(SortingEnum.OldestFirst):
                    videos = videos.OrderBy(v => v.CreatedAt).ToList();
                    break;

                case nameof(SortingEnum.Name):
                    videos = videos.OrderBy(v => v.Title).ToList();
                    break;

                default:
                    videos = videos.OrderByDescending(v => v.CreatedAt).ToList();
                    break;
            }

            return Ok(videos);
        }
        [HttpPost]
        [Route("upload")]
        [RequestSizeLimit(MaxFileSize)]
        public async Task<IActionResult> Upload([FromForm] UploadVideoRequest request, CancellationToken cancellationToken)
        {
            if(request.File == null || request.File.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }
            if(request.File.Length > MaxFileSize)
            {
                return BadRequest("File size exceeds the maximum limit of 2GB.");
            }
            var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                return BadRequest("Invalid file type. Only MP4, MOV, WEBM, and MKV files are allowed.");
            }
            if (string.IsNullOrEmpty(request.Title))
            {
                return BadRequest("Title is required.");
            }
            Guid videoId = Guid.NewGuid();
            string fileName = $"{videoId}{extension}";
            VideoStatus status = VideoStatus.Uploading;
            Video video = new Video
            {
                Id = videoId,
                Title = request.Title,
                Description = request.Description?.Trim(),
                OriginalFileName = request.File.FileName,
                ContentType = request.File.ContentType,
                FileSize = request.File.Length,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            try
            {
                await using (var stream = request.File.OpenReadStream())
                {
                    var storagePath = await _storage.SaveAsync(stream, fileName, cancellationToken);
                    video.StoragePath = storagePath;
                    video.Status = VideoStatus.Uploaded;
                    _context.Videos.Add(video);
                    await _context.SaveChangesAsync(cancellationToken);
                    var message = new VideoUploadedMessage
                    {
                        VideoId = video.Id,
                        StoragePath = video.StoragePath,
                        FileName = fileName
                    };

                    await _publisher.PublishAsync(message, "video-processing");
                    return Ok(new { video.Id, video.Title, video.Description, video.OriginalFileName, video.StoragePath, video.ContentType, video.FileSize, video.Status, video.CreatedAt });
                }
            }
            catch(Exception ex)
            {
                video.Status = VideoStatus.Failed;

                return StatusCode(
                    StatusCodes.Status500InternalServerError, "Video upload failed.");
            }
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok("API working");
        }

        [HttpPost("send")]
        public async Task<IActionResult> Send()
        {
            var message = new VideoUploadedMessage
            {
                VideoId = Guid.NewGuid()
            };
            await _publisher.PublishAsync(message, "video-processing");

            return Ok("Message sent");
        }
    }
}
