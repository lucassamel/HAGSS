using HAGSS.Contracts;
using HAGSS.Monitor.Options;
using Microsoft.Extensions.Options;

namespace HAGSS.Monitor.Services;

/// <summary>
/// Fallback polling so seat lists stay in sync if a SignalR message is missed.
/// </summary>
public sealed class SeatSnapshotRefreshWorker(
    ActivityFeed feed,
    IHttpClientFactory httpClientFactory,
    IOptions<MonitorOptions> options,
    ILogger<SeatSnapshotRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(2, options.Value.SeatRefreshIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(interval, stoppingToken);

            if (feed.ConnectionState != "Connected")
                continue;

            try
            {
                var client = httpClientFactory.CreateClient("hagss-api");
                var eventId = options.Value.EventId;
                var snapshot = await client.GetFromJsonAsync<EventSeatsSnapshot>(
                    $"/api/events/{eventId}/seats/snapshot",
                    stoppingToken);

                if (snapshot is not null)
                    feed.UpdateSeats(snapshot);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Seat snapshot refresh failed");
            }
        }
    }
}
