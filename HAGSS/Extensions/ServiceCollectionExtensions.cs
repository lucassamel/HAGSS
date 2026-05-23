using HAGSS.Data;
using HAGSS.Hubs;
using HAGSS.Infrastructure.Messaging;
using HAGSS.Infrastructure.Redis;
using HAGSS.Options;
using HAGSS.Services;
using HAGSS.Workers;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace HAGSS.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHagssInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connections = configuration.GetSection(ConnectionOptions.SectionName).Get<ConnectionOptions>()
            ?? throw new InvalidOperationException("Connection strings are not configured.");

        services.Configure<ReservationOptions>(configuration.GetSection(ReservationOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));

        services.AddSignalR();
        services.AddSingleton<IReservationActivityPublisher, ReservationActivityPublisher>();
        services.AddSingleton<ISeatSnapshotBroadcaster, SeatSnapshotBroadcaster>();
        services.AddScoped<ISeatSnapshotService, SeatSnapshotService>();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connections.Postgres));

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(connections.Redis));

        services.AddSingleton<IRedisDistributedLock, RedisDistributedLock>();

        services.AddSingleton<IConnection>(_ =>
        {
            var factory = new ConnectionFactory { Uri = new Uri(connections.RabbitMq) };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        services.AddSingleton<IPaymentPublisher, RabbitMqPaymentPublisher>();
        services.AddScoped<ISeatReservationService, SeatReservationService>();
        services.AddScoped<IPaymentProcessingService, PaymentProcessingService>();

        services.AddHostedService<RabbitMqTopologyInitializer>();
        services.AddHostedService<PaymentProcessingConsumer>();

        services.AddHealthChecks()
            .AddNpgSql(connections.Postgres, name: "postgresql")
            .AddRedis(connections.Redis, name: "redis")
            .AddRabbitMQ(sp => sp.GetRequiredService<IConnection>(), name: "rabbitmq");

        return services;
    }
}
