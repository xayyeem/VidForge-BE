using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoUploadSystem.Application.Interfaces;

namespace VideoUploadSystem.Infrastructure.Media
{
    public sealed class FfmpegVideoProcessingService : IVideoProcessingService
    {
        private readonly FfmpegOptions _options;
        public FfmpegVideoProcessingService(IOptions<FfmpegOptions> options)
        {
            _options = options.Value;
        }

        public async Task<VideoProcessingResult> ProcessAsync(Guid videoId, string storagePath, string? existingThumbnailPath,CancellationToken cancellationToken)
        {
            if (!File.Exists(storagePath))
            {
                throw new FileNotFoundException("Uploaded video file was not found.", storagePath);
            }
            if (!File.Exists(_options.FfmpegPath))
            {
                throw new FileNotFoundException(
                    "FFmpeg executable not found.",
                    _options.FfmpegPath);
            }

            if (!File.Exists(_options.FfprobePath))
            {
                throw new FileNotFoundException(
                    "FFprobe executable not found.",
                    _options.FfprobePath);
            }
            Directory.CreateDirectory(_options.ThumbnailOutputDirectory);
            var durationOutput = await GetVideoDurationAsync(storagePath, cancellationToken);
            if (!decimal.TryParse(durationOutput.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var durationSeconds) || durationSeconds <= 0)
            {
                throw new InvalidOperationException("Could not extract video duration.");
            }

            string thumbnailPath; //= await GenerateThumbnailAsync(videoId, storagePath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(existingThumbnailPath) && File.Exists(existingThumbnailPath))
            {
                thumbnailPath = existingThumbnailPath;
            }
            else
            {
                thumbnailPath = await GenerateThumbnailAsync(videoId, storagePath, cancellationToken);
            }

            return new VideoProcessingResult
            {
                DurationSeconds = durationSeconds,
                ThumbnailPath = thumbnailPath
            };
            // Implement the logic to process the video using FFmpeg and FFprobe
            // For example, you can use the FFmpeg and FFprobe command-line tools to extract video duration and generate a thumbnail
            // Example implementation (pseudo-code):
            // 1. Use FFprobe to get video duration
            // 2. Use FFmpeg to generate a thumbnail image
            // 3. Return the result with duration and thumbnail path
        }

        private static async Task<string> RunProcessAsync(string executablePath, IEnumerable<string> arguments, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = new Process
            {
                StartInfo = startInfo
            };

            if (!process.Start())
            {
                throw new InvalidOperationException($"Could not start {executablePath}");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();

            var stderrTask = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Process already exited.
                }
                throw;
            }
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"FFmpeg/FFprobe failed: {stderr}");
            }
            return stdout;
        }

        private async Task<string> GetVideoDurationAsync(string storagePath,CancellationToken cancellationToken)
        {
            return await RunProcessAsync(_options.FfprobePath,
                new[]
                {
            "-v", "error",
            "-show_entries", "format=duration",
            "-of", "default=noprint_wrappers=1:nokey=1",
            storagePath
                },
                cancellationToken);
        }

        private async Task<string> GenerateThumbnailAsync(Guid videoId, string storagePath, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(_options.ThumbnailOutputDirectory);
            var thumbnailPath = Path.Combine(_options.ThumbnailOutputDirectory, $"{videoId}.jpg");
            if (File.Exists(thumbnailPath))
            {
                return "";
            }
            await RunProcessAsync(_options.FfmpegPath,
            new[]
            {
            "-hide_banner",
            "-loglevel", "error",
            "-nostdin",
            "-y",
            "-i", storagePath,
            "-map", "0:v:0",
            "-frames:v", "1",
            "-vf", "scale=640:-1",
            "-threads", "1",
            thumbnailPath
                },
                cancellationToken);

            if (!File.Exists(thumbnailPath) || new FileInfo(thumbnailPath).Length == 0)
            {
                throw new InvalidOperationException("Thumbnail generation failed.");
            }

            return thumbnailPath;
        }

    }
}
