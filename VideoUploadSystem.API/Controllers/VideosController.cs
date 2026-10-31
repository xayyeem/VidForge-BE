using CloudinaryDotNet;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;
using System.Threading;
using VideoUploadSystem.API.Models;
using VideoUploadSystem.Application.Interfaces;
using VideoUploadSystem.Application.Messages;
using VideoUploadSystem.Domain.Entities;
using VideoUploadSystem.Domain.Enums;
using VideoUploadSystem.Infrastructure.Configuration;
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
        private readonly string _generatedThumbnailDirectory;
        public VideosController(ApplicationDbContext context, IVideoStorageService storage, RabbitMqPublisher publisher, IOptions<GeminiOptions> geminiOptions)
        {
            _context = context;
            _storage = storage;
            _publisher = publisher;
            _generatedThumbnailDirectory = Path.GetFullPath(geminiOptions.Value.ThumbnailOutputDirectory,AppContext.BaseDirectory);
        }

        [HttpDelete]
        [Route("DeleteVideos")]
        public async Task<IActionResult> Delete([FromQuery] Guid VideoId,CancellationToken cancellationToken = default)
        {
            var video = await _context.Videos.FirstOrDefaultAsync(v => v.Id == VideoId, cancellationToken);

            if (video == null)
            {
                return NotFound("Video not found.");
            }

            // Delete physical video file
            if (System.IO.File.Exists(video.StoragePath))
            {
                System.IO.File.Delete(video.StoragePath);
            }

            // Delete thumbnail file
            if (!string.IsNullOrWhiteSpace(video.ThumbnailPath) && System.IO.File.Exists(video.ThumbnailPath))
            {
                System.IO.File.Delete(video.ThumbnailPath);
            }

            _context.Videos.Remove(video);

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                message = "Video deleted successfully.",
                videoId = VideoId
            });
        }

        [HttpGet]
        [Route("getvideos")]
        public async Task<IActionResult> Get([FromQuery] string sort = "NewestFirst", [FromQuery] string? search = null, CancellationToken cancellationToken = default)
        {
            IQueryable<Video> query = _context.Videos.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(v =>
                    v.Title.Contains(search));
            }
            switch (sort)
            {
                case nameof(SortingEnum.NewestFirst):
                    query = query.OrderByDescending(v => v.CreatedAt);
                    break;

                case nameof(SortingEnum.OldestFirst):
                    query = query.OrderBy(v => v.CreatedAt);
                    break;

                case nameof(SortingEnum.Name):
                    query = query.OrderBy(v => v.Title);
                    break;

                default:
                    query = query.OrderByDescending(v => v.CreatedAt);
                    break;
            }

            var videos = await query
                .Select(v => new VideoResponseDto
                {
                    Id = v.Id,
                    Title = v.Title,
                    Description = v.Description,
                    OriginalFileName = v.OriginalFileName,
                    ContentType = v.ContentType,
                    FileSize = v.FileSize,
                    DurationSeconds = v.DurationSeconds,
                    Status = v.Status,
                    CreatedAt = v.CreatedAt,

                    ThumbnailUrl = !string.IsNullOrEmpty(v.ThumbnailPath) ? $"/api/videos/{v.Id}/thumbnail" : null,

                    StreamUrl = $"/api/videos/{v.Id}/stream"
                })
                .ToListAsync(cancellationToken);

            return Ok(videos);
        }


        private async Task<string?> SaveSelectedThumbnailAsync(UploadVideoRequest request, Guid videoId, CancellationToken cancellationToken)
        {
            // Manual thumbnail has priority.
            if (request.Thumbnail != null && request.Thumbnail.Length > 0)
            {
                var extension = Path.GetExtension(request.Thumbnail.FileName).ToLowerInvariant();

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

                if (!allowedExtensions.Contains(extension))
                    throw new InvalidOperationException("Invalid thumbnail format.");
                var fileName = $"{videoId}_thumbnail{extension}";
                await using var stream = request.Thumbnail.OpenReadStream();
                return await _storage.SaveAsync(stream,fileName, cancellationToken);
            }

            // AI-generated thumbnail
            if (request.GeneratedThumbnailId.HasValue)
            {
                if (!Directory.Exists(_generatedThumbnailDirectory))
                    throw new FileNotFoundException("Generated thumbnail directory not found.");
                var thumbnailId = request.GeneratedThumbnailId.Value;
                var filePath = Directory.EnumerateFiles(_generatedThumbnailDirectory,$"{thumbnailId}.*").FirstOrDefault();
                if (filePath == null)
                    throw new FileNotFoundException("Generated thumbnail not found.");
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                if (!allowedExtensions.Contains(extension))
                    throw new InvalidOperationException("Invalid generated thumbnail format.");
                var fileName = $"{videoId}_thumbnail{extension}";
                await using var stream = System.IO.File.OpenRead(filePath);
                var permanentPath = await _storage.SaveAsync(stream, fileName, cancellationToken);

                // Remove temporary image after permanent save succeeds.
                System.IO.File.Delete(filePath);
                return permanentPath;
            }
            return null;
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
                    video.ThumbnailPath = await SaveSelectedThumbnailAsync(request, videoId, cancellationToken);
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

        [HttpGet("{id:guid}/thumbnail")]
        public async Task<IActionResult> GetThumbnail(Guid id, CancellationToken cancellationToken)
        {
            var video = await _context.Videos
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

            if (video == null)
                return NotFound("Video not found.");

            //if (video.Status != VideoStatus.Ready)
            //    return Conflict("Video is not ready yet.");

            if (string.IsNullOrWhiteSpace(video.ThumbnailPath) || !System.IO.File.Exists(video.ThumbnailPath))
            {
                return NotFound("Thumbnail not found.");
            }

            return PhysicalFile(video.ThumbnailPath, "image/jpeg");
        }

        [HttpGet("{id:guid}/stream")]
        public async Task<IActionResult> StreamVideo(Guid id, CancellationToken cancellationToken)
        {
            var video = await _context.Videos
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

            if (video == null)
                return NotFound("Video not found.");

            if (video.Status != VideoStatus.Ready)
                return Conflict("Video is not ready for streaming.");

            if (string.IsNullOrWhiteSpace(video.StoragePath) || !System.IO.File.Exists(video.StoragePath))
            {
                return NotFound("Video file not found.");
            }

            var contentType = string.IsNullOrWhiteSpace(video.ContentType)
                ? "application/octet-stream"
                : video.ContentType;

            return PhysicalFile(video.StoragePath, contentType, enableRangeProcessing: true);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetVideoById(Guid id, CancellationToken cancellationToken)
        {
            var video = await _context.Videos
                .AsNoTracking()
                .Where(v => v.Id == id)
                .Select(v => new VideoResponseDto
                {
                    Id = v.Id,
                    Title = v.Title,
                    Description = v.Description,
                    OriginalFileName = v.OriginalFileName,
                    ContentType = v.ContentType,
                    FileSize = v.FileSize,
                    DurationSeconds = v.DurationSeconds,
                    Status = v.Status,
                    CreatedAt = v.CreatedAt,
                    ThumbnailUrl = v.Status == VideoStatus.Ready
                        ? $"/api/videos/{v.Id}/thumbnail"
                        : null,
                    StreamUrl = $"/api/videos/{v.Id}/stream"
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (video == null)
                return NotFound("Video not found.");

            return Ok(video);
        }
    }
}
