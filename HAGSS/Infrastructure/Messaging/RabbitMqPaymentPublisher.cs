using System.Text;
using System.Text.Json;
using HAGSS.Options;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;

namespace HAGSS.Infrastructure.Messaging;

public sealed class RabbitMqPaymentPublisher(
    IConnection connection,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqPaymentPublisher> logger) : IPaymentPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ResiliencePipeline _publishPipeline = CreatePublishPipeline(logger);
    private IChannel? _channel;
    private readonly SemaphoreSlim _channelLock = new(1, 1);

    public async Task PublishAsync(PaymentMessage message, CancellationToken cancellationToken)
    {
        await _publishPipeline.ExecuteAsync(async token =>
        {
            var channel = await GetOrCreateChannelAsync(token);
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOptions));
            var props = new BasicProperties { Persistent = true, ContentType = "application/json" };

            await channel.BasicPublishAsync(
                options.Value.PaymentExchange,
                options.Value.PaymentRoutingKey,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: token);
        }, cancellationToken);
    }

    private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
                return _channel;

            _channel?.Dispose();
            _channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            return _channel;
        }
        finally
        {
            _channelLock.Release();
        }
    }

    private static ResiliencePipeline CreatePublishPipeline(ILogger logger)
    {
        var retry = new RetryStrategyOptions
        {
            MaxRetryAttempts = 5,
            Delay = TimeSpan.FromMilliseconds(200),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            OnRetry = args =>
            {
                logger.LogWarning(
                    args.Outcome.Exception,
                    "Retrying RabbitMQ publish (attempt {Attempt})",
                    args.AttemptNumber);
                return default;
            }
        };

        return new ResiliencePipelineBuilder()
            .AddRetry(retry)
            .Build();
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.CloseAsync();
        _channelLock.Dispose();
    }
}
