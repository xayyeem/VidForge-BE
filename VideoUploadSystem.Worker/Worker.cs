using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VideoUploadSystem.Application.Messages;

namespace VideoUploadSystem.Worker
{
    public class Worker : BackgroundService
    {
        private const string QueueName = "video-processing";

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "guest",
                Password = "guest"
            };

            await using var connection =
                await factory.CreateConnectionAsync();

            await using var channel =
                await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, args) =>
            {
                var body = args.Body.ToArray();

                var json = Encoding.UTF8.GetString(body);

                var message = JsonSerializer.Deserialize<VideoUploadedMessage>(json);

                Console.WriteLine(
                    $"Video received: {message?.VideoId}");

                Console.WriteLine(
                    $"File: {message?.FileName}");

                Console.WriteLine(
                    $"Path: {message?.StoragePath}");

                await channel.BasicAckAsync(
                    args.DeliveryTag,
                    multiple: false);
            };

            await channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer);

            Console.WriteLine(
                "Video processing worker is listening...");

            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
    }
