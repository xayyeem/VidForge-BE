using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VideoUploadSystem.Application.DTOs;
using VideoUploadSystem.Application.Interfaces;
using VideoUploadSystem.Infrastructure.Configuration;

namespace VideoUploadSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ThumbnailsController: ControllerBase
    {
        private readonly IThumbnailGenerationService _thumbnailService;
        private readonly string _outputDirectory;
        public ThumbnailsController(IThumbnailGenerationService thumbnail, IOptions<GeminiOptions> options)
        {
            _thumbnailService = thumbnail;
            _outputDirectory = options.Value.ThumbnailOutputDirectory;
        }
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateThumbnail([FromBody] GenerateThumbnailRequest request, CancellationToken cancellationToken)
        {
            var thumbnailResult = await _thumbnailService.GenerateAsync(request, cancellationToken);
            return Ok(thumbnailResult);
        }
    }
}
