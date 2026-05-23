using HAGSS.Contracts;
using HAGSS.Monitor.Options;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace HAGSS.Monitor.Services;

public sealed class ApiActivityListener(
    ActivityFeed feed,
    IOptions<MonitorOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<ApiActivityListener> logger) : BackgroundService
{
    private HubConnection? _connection;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var apiBaseUrl = options.Value.ApiBaseUrl.TrimEnd('/');
        var hubUrl = $"{apiBaseUrl}/hubs/reservations";

        await LoadInitialSnapshotAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                feed.SetConnectionState("Connecting");
                _connection = new HubConnectionBuilder()
                    .WithUrl(hubUrl)
                    .WithAutomaticReconnect([
                        TimeSpan.Zero,
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(10),
                        TimeSpan.FromSeconds(30)])
                    .Build();

                _connection.Reconnecting += _ =>
                {
                    feed.SetConnectionState("Reconnecting");
                    return Task.CompletedTask;
                };

                _connection.Reconnected += async _ =>
                {
                    feed.SetConnectionState("Connected");
                    await LoadInitialSnapshotAsync(stoppingToken);
                };

                _connection.Closed += _ =>
                {
                    feed.SetConnectionState("Disconnected");
                    return Task.CompletedTask;
                };

                _connection.On<ReservationActivityEvent>("ActivityReceived", activity =>
                {
                    feed.AddEvent(activity, options.Value.MaxDisplayedEvents);
                    return Task.CompletedTask;
                });

                _connection.On<ActivityStatsSnapshot>("StatsUpdated", stats =>
                {
                    feed.UpdateStats(stats);
                    return Task.CompletedTask;
                });

                _connection.On<EventSeatsSnapshot>("SeatsUpdated", snapshot =>
                {
                    feed.UpdateSeats(snapshot);
                    return Task.CompletedTask;
                });

                await _connection.StartAsync(stoppingToken);
                feed.SetConnectionState("Connected");
                logger.LogInformation("Connected to API activity hub at {HubUrl}", hubUrl);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                feed.SetConnectionState("Disconnected");
                logger.LogWarning(ex, "Activity hub connection lost; retrying in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task LoadInitialSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("hagss-api");
            var eventId = options.Value.EventId;

            var recentTask = client.GetFromJsonAsync<List<ReservationActivityEvent>>(
                $"/api/activity/recent?count={options.Value.MaxDisplayedEvents}",
                cancellationToken);

            var statsTask = client.GetFromJsonAsync<ActivityStatsSnapshot>(
                "/api/activity/stats",
                cancellationToken);

            var seatsTask = client.GetFromJsonAsync<EventSeatsSnapshot>(
                $"/api/events/{eventId}/seats/snapshot",
                cancellationToken);

            await Task.WhenAll(recentTask, statsTask, seatsTask);

            var recent = await recentTask;
            var stats = await statsTask;
            var seats = await seatsTask;

            if (recent is not null && stats is not null)
            {
                feed.ReplaceSnapshot(
                    recent.OrderByDescending(e => e.OccurredAtUtc),
                    stats,
                    seats,
                    options.Value.MaxDisplayedEvents);
            }
            else if (seats is not null)
            {
                feed.UpdateSeats(seats);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not load initial activity snapshot");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
