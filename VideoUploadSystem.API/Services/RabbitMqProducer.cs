//using RabbitMQ.Client;
//using System.Text;
//using System.Text.Json;
//using VideoUploadSystem.Application.Interfaces;

//namespace VideoUploadSystem.API.Services
//{
//    public class RabbitMqPublisher : IMessagePublisher
//    {
//        private readonly ConnectionFactory _factory;

//        public RabbitMqPublisher()
//        {
//            _factory = new ConnectionFactory()
//            {
//                HostName = "localhost",
//                Port = 5672,
//                UserName = "guest",
//                Password = "guest"
//            };
//        }

//        public async Task PublishAsync<T>(T message, string queueName)
//        {
//            await using var connection = await _factory.CreateConnectionAsync();
//            await using var channel = await connection.CreateChannelAsync();

//            await channel.QueueDeclareAsync(
//                queue: queueName,
//                durable: true,
//                exclusive: false,
//                autoDelete: false,
//                arguments: null
//            );

//            // Object ko JSON mein convert karo
//            var json = JsonSerializer.Serialize(message);
//            var body = Encoding.UTF8.GetBytes(json);

//            await channel.BasicPublishAsync(
//                exchange: string.Empty,
//                routingKey: queueName,
//                body: body
//            );

//            Console.WriteLine($"✅ Message published: {json}");
//        }
//    }
//}
