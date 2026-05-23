using HAGSS.Options;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace HAGSS.Infrastructure.Messaging;

public sealed class RabbitMqTopologyInitializer(
    IConnection connection,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqTopologyInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var cfg = options.Value;

        await channel.ExchangeDeclareAsync(cfg.PaymentExchange, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(cfg.PaymentQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(cfg.PaymentQueue, cfg.PaymentExchange, cfg.PaymentRoutingKey, cancellationToken: cancellationToken);

        logger.LogInformation("RabbitMQ topology initialized for queue {Queue}", cfg.PaymentQueue);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
