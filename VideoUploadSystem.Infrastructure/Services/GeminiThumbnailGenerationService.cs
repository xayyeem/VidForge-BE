using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VideoUploadSystem.Application.DTOs;
using VideoUploadSystem.Application.Interfaces;
using VideoUploadSystem.Infrastructure.Configuration;

namespace VideoUploadSystem.Infrastructure.Services
{
    public sealed class GeminiThumbnailGenerationService
    : IThumbnailGenerationService
    {
        private readonly HttpClient _httpClient;
        private readonly GeminiOptions _options;
        private readonly string _outputDirectory;

        public GeminiThumbnailGenerationService(HttpClient httpClient, IOptions<GeminiOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;

            _outputDirectory = Path.GetFullPath(_options.ThumbnailOutputDirectory, AppContext.BaseDirectory);

            Directory.CreateDirectory(_outputDirectory);
        }

        public async Task<GenerateThumbnailResponse> GenerateAsync(GenerateThumbnailRequest request, CancellationToken cancellationToken = default)
        {
            var generatedThumbnails = new List<GeneratedThumbnailDto>();
            for (int i = 0; i < _options.NumberOfImages; i++)
            {
                var thumbnail = await GenerateSingleImageAsync(request.Prompt, cancellationToken);

                generatedThumbnails.Add(thumbnail);
            }

            return new GenerateThumbnailResponse
            {
                Data = generatedThumbnails
            };
        }

        private async Task<GeneratedThumbnailDto> GenerateSingleImageAsync(string prompt, CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                contents = new[]
                {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
                generationConfig = new
                {
                    responseModalities = new[] { "IMAGE" },
                    imageConfig = new
                    {
                        aspectRatio = "16:9"
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var endpoint = $"v1/models/{_options.Model}:generateContent";

            using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Gemini API failed ({(int)response.StatusCode}): {responseJson}");
            }

            using var document = JsonDocument.Parse(responseJson);

            if (!document.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("Gemini did not return any image candidates.");
            }

            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("content", out var candidateContent) ||
                    !candidateContent.TryGetProperty("parts", out var parts))
                {
                    continue;
                }

                foreach (var part in parts.EnumerateArray())
                {
                    if (!part.TryGetProperty("inlineData", out var inlineData))
                        continue;

                    var base64Data = inlineData.GetProperty("data").GetString();
                    var mimeType = inlineData.GetProperty("mimeType").GetString();

                    if (string.IsNullOrWhiteSpace(base64Data))
                        continue;

                    var extension = mimeType switch
                    {
                        "image/png" => ".png",
                        "image/jpeg" => ".jpg",
                        "image/webp" => ".webp",
                        _ => throw new InvalidOperationException(
                            $"Unsupported image type: {mimeType}")
                    };

                    var id = Guid.NewGuid();
                    var filePath = Path.Combine(
                        _outputDirectory,
                        $"{id}{extension}");

                    var imageBytes = Convert.FromBase64String(base64Data);

                    await File.WriteAllBytesAsync(
                        filePath,
                        imageBytes,
                        cancellationToken);

                    return new GeneratedThumbnailDto
                    {
                        Id = id,
                        ThumbnailUrl = $"/api/thumbnails/temp/{id}"
                    };
                }
            }

            throw new InvalidOperationException(
                "Gemini response did not contain image data.");
        }
    }
}
