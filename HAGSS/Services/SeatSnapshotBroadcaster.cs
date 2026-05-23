using HAGSS.Contracts;
using HAGSS.Data;
using HAGSS.Data.Entities;
using HAGSS.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HAGSS.Services;

public sealed class SeatSnapshotBroadcaster(
    IServiceScopeFactory scopeFactory,
    IHubContext<ReservationHub> hub) : ISeatSnapshotBroadcaster
{
    public async Task BroadcastAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var seats = await db.Seats
            .AsNoTracking()
            .Where(s => s.EventId == eventId)
            .OrderBy(s => s.Row)
            .ThenBy(s => s.Number)
            .Select(s => new { s.Id, s.Row, s.Number, s.Status })
            .ToListAsync(cancellationToken);

        var available = seats
            .Where(s => s.Status == SeatStatus.Available)
            .Select(s => ToItem(s.Id, s.Row, s.Number))
            .ToList();

        var confirmed = seats
            .Where(s => s.Status == SeatStatus.Sold)
            .Select(s => ToItem(s.Id, s.Row, s.Number))
            .ToList();

        var reservedCount = seats.Count(s => s.Status == SeatStatus.Reserved);

        var snapshot = new EventSeatsSnapshot(
            eventId,
            DateTime.UtcNow,
            available,
            confirmed,
            reservedCount,
            available.Count,
            confirmed.Count);

        await hub.Clients.All.SendAsync("SeatsUpdated", snapshot, cancellationToken);
    }

    private static SeatMonitorItem ToItem(Guid id, string row, string number) =>
        new(id, row, number, $"{row}-{number}");
}
