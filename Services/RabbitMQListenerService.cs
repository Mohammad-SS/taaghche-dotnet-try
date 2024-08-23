using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

public class RabbitMQListenerService : BackgroundService
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _redisCache;

    public RabbitMQListenerService(
        IConnectionFactory connectionFactory,
        IMemoryCache memoryCache,
        IDistributedCache redisCache)
    {
        _connectionFactory = connectionFactory;
        _memoryCache = memoryCache;
        _redisCache = redisCache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "bookQueue",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var bookId = Encoding.UTF8.GetString(body);
            Console.WriteLine($"book is HERE !");

            var cacheKey = $"book_{bookId}";

            _memoryCache.Remove(cacheKey);

            await _redisCache.RemoveAsync(cacheKey);
        };

        channel.BasicConsume(queue: "bookQueue",
            autoAck: true,
            consumer: consumer);

        await Task.CompletedTask;
    }
}