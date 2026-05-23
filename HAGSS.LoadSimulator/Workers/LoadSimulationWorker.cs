using System.Net.Http.Json;
using System.Text.Json;
using HAGSS.LoadSimulator.Models;
using HAGSS.LoadSimulator.Options;
using Microsoft.Extensions.Options;

namespace HAGSS.LoadSimulator.Workers;

public sealed class LoadSimulationWorker(
    IHttpClientFactory httpClientFactory,
    IOptions<SimulatorOptions> options,
    ILogger<LoadSimulationWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] TimeZoneIds =
    [
        "America/New_York",
        "America/Sao_Paulo",
        "Europe/London",
        "Europe/Paris",
        "Asia/Tokyo",
        "Asia/Dubai",
        "Australia/Sydney",
        "Pacific/Auckland",
        "America/Los_Angeles",
        "Africa/Johannesburg"
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        logger.LogInformation(
            "Load simulator starting. API={Api}, Event={EventId}, users {Min}-{Max}",
            config.ApiBaseUrl,
            config.EventId,
            config.MinUsers,
            config.MaxUsers);

        await Task.Delay(TimeSpan.FromSeconds(config.StartupDelaySeconds), stoppingToken);
        await WaitForApiAsync(stoppingToken);

        var round = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            round++;
            try
            {
                await RunRoundAsync(round, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Simulation round {Round} failed", round);
            }

            await Task.Delay(TimeSpan.FromSeconds(config.RoundIntervalSeconds), stoppingToken);
        }
    }

    private async Task RunRoundAsync(int round, CancellationToken cancellationToken)
    {
        var config = options.Value;
        var client = httpClientFactory.CreateClient("hagss-api");
        var seats = await FetchSeatsAsync(client, config.EventId, cancellationToken);

        if (seats.Count == 0)
        {
            logger.LogWarning("No seats returned for event {EventId}", config.EventId);
            return;
        }

        var available = seats.Where(s => s.Status == 0).ToList();
        if (available.Count == 0)
        {
            logger.LogWarning("Round {Round}: no available seats left", round);
            return;
        }

        var userCount = Random.Shared.Next(config.MinUsers, config.MaxUsers + 1);
        var hotSeats = available.OrderBy(_ => Random.Shared.Next()).Take(config.HotSeatPoolSize).ToList();
        var semaphore = new SemaphoreSlim(config.MaxParallelRequests);

        logger.LogInformation(
            "Round {Round}: launching {UserCount} users across {TimeZoneCount} timezones ({Available} seats available, {Hot} hot seats)",
            round,
            userCount,
            TimeZoneIds.Length,
            available.Count,
            hotSeats.Count);

        var tasks = Enumerable.Range(1, userCount).Select(async userIndex =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var timeZoneId = TimeZoneIds[Random.Shared.Next(TimeZoneIds.Length)];
                var delay = Random.Shared.Next(config.MinDelayMs, config.MaxDelayMs + 1);
                if (delay > 0)
                    await Task.Delay(delay, cancellationToken);

                var seat = PickSeat(available, hotSeats, config.HotSeatProbability);
                var email = $"user-{round:D4}-{userIndex:D5}@sim.hagss.local";
                var clientId = $"sim-{round}-{userIndex}";

                await TryReserveAsync(client, config.EventId, seat.Id, email, timeZoneId, clientId, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        logger.LogInformation("Round {Round} completed", round);
    }

    private static SeatDto PickSeat(List<SeatDto> available, List<SeatDto> hotSeats, double hotProbability)
    {
        if (hotSeats.Count > 0 && Random.Shared.NextDouble() < hotProbability)
            return hotSeats[Random.Shared.Next(hotSeats.Count)];

        return available[Random.Shared.Next(available.Count)];
    }

    private async Task TryReserveAsync(
        HttpClient client,
        Guid eventId,
        Guid seatId,
        string email,
        string timeZoneId,
        string clientId,
        CancellationToken cancellationToken)
    {
        var url = $"/api/events/{eventId}/seats/{seatId}/reserve";
        var body = new ReserveRequest(email, timeZoneId, clientId);

        try
        {
            using var response = await client.PostAsJsonAsync(url, body, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode && (int)response.StatusCode != 429)
                logger.LogDebug("Reserve {Email} seat {SeatId} -> {Status}", email, seatId, response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Reserve failed for {Email}", email);
        }
    }

    private async Task<List<SeatDto>> FetchSeatsAsync(HttpClient client, Guid eventId, CancellationToken cancellationToken)
    {
        var seats = await client.GetFromJsonAsync<List<SeatDto>>($"/api/events/{eventId}/seats", JsonOptions, cancellationToken);
        return seats ?? [];
    }

    private async Task WaitForApiAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("hagss-api");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await client.GetAsync("/health", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("API is healthy");
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Waiting for API...");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
    }
}
