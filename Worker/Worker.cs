
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using VideoUploadSystem.Application.Interfaces;
using VideoUploadSystem.Application.Messages;
using VideoUploadSystem.Domain.Enums;
using VideoUploadSystem.Infrastructure.Data;

namespace VideoUploadSystem.Worker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        private const string QueueName = "video-processing";

        public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "guest",
                Password = "guest"
            };
            await using var connection = await factory.CreateConnectionAsync();

            await using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // Ek samay par ek message process karenge.
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, args) =>
            {
                try
                {
                    var body = args.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);

                    var message =JsonSerializer.Deserialize<VideoUploadedMessage>(json);

                    if (message == null || message.VideoId == Guid.Empty)
                    {
                        _logger.LogWarning("Invalid video message received.");
                        await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false);
                        return;
                    }

                    _logger.LogInformation("Video message received: {VideoId}", message.VideoId);

                    _logger.LogInformation("File: {FileName}, Path: {StoragePath}", message.FileName, message.StoragePath);

                    // Har message ke liye alag DbContext scope.
                    using var scope = _scopeFactory.CreateScope();

                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var video = await dbContext.Videos.FirstOrDefaultAsync(v => v.Id == message.VideoId,stoppingToken);

                    if (video == null)
                    {
                        _logger.LogWarning("Video not found in database: {VideoId}", message.VideoId);

                        // Unknown VideoId ko repeatedly requeue
                        // nahi kar rahe.
                        await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false);
                        return;
                    }

                    // Sirf Uploaded video ko processing mein le jayenge.
                    if (video.Status != VideoStatus.Uploaded)
                    {
                        _logger.LogWarning("Video {VideoId} has status {Status}. " + "Expected Uploaded. Message will be acknowledged.", video.Id, video.Status);

                        await channel.BasicAckAsync(args.DeliveryTag, multiple: false);

                        return;
                    }

                    video.Status = VideoStatus.Processing;
                    video.UpdatedAt = DateTime.UtcNow;

                    await dbContext.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("Video status updated to Processing: {VideoId}", video.Id);

                    // TODO:
                    // Yahan FFmpeg processing, duration extraction
                    // aur thumbnail generation add karenge.
                    var videoProcessor = scope.ServiceProvider.GetRequiredService<IVideoProcessingService>();

                    try
                    {
                        // Video ki duration aur thumbnail generate karo.
                        var existingThumbnailPath = video.ThumbnailPath;
                        var result = await videoProcessor.ProcessAsync(video.Id, video.StoragePath, existingThumbnailPath, stoppingToken);

                        video.DurationSeconds = result.DurationSeconds;
                        if (string.IsNullOrEmpty(result.ThumbnailPath))
                        {

                        }
                        video.ThumbnailPath = result.ThumbnailPath;
                        video.Status = VideoStatus.Ready;
                        video.UpdatedAt = DateTime.UtcNow;

                        await dbContext.SaveChangesAsync(stoppingToken);

                        _logger.LogInformation("Video {VideoId} is Ready. Duration: {Duration}s, Thumbnail: {ThumbnailPath}", video.Id, result.DurationSeconds, result.ThumbnailPath);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Video processing failed for {VideoId}", video.Id);

                        video.Status = VideoStatus.Failed;
                        video.UpdatedAt = DateTime.UtcNow;

                        await dbContext.SaveChangesAsync(stoppingToken);

                        _logger.LogWarning("Video {VideoId} marked as Failed.", video.Id);
                    }

                    // Processing complete ya failure database mein save hone ke baad ACK.
                    await channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                    // Is milestone mein processing start hone ka
                    // status save hone ke baad message ACK kar rahe hain.
                    //await channel.BasicAckAsync(args.DeliveryTag,multiple: false);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Worker is stopping.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing RabbitMQ message.");

                    // Abhi simple failure handling hai.
                    // Production mein retry/dead-letter policy
                    // configure karna zaroori hai.
                    try
                    {
                        await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false);
                    }
                    catch (Exception nackException)
                    {
                        _logger.LogError(nackException, "Failed to reject RabbitMQ message.");
                    }
                }
            };

            await channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer);

            _logger.LogInformation("Video processing worker is listening on queue {QueueName}.", QueueName);

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Video processing worker stopped.");
            }
        }
    }
}
