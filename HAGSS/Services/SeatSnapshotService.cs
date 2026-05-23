using HAGSS.Contracts;
using HAGSS.Data;
using HAGSS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HAGSS.Services;

public interface ISeatSnapshotService
{
    Task<EventSeatsSnapshot?> GetSnapshotAsync(Guid eventId, CancellationToken cancellationToken = default);
}

public sealed class SeatSnapshotService(AppDbContext db) : ISeatSnapshotService
{
    public async Task<EventSeatsSnapshot?> GetSnapshotAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (!await db.Events.AnyAsync(e => e.Id == eventId, cancellationToken))
            return null;

        var seats = await db.Seats
            .AsNoTracking()
            .Where(s => s.EventId == eventId)
            .OrderBy(s => s.Row)
            .ThenBy(s => s.Number)
            .Select(s => new { s.Id, s.Row, s.Number, s.Status })
            .ToListAsync(cancellationToken);

        var available = seats
            .Where(s => s.Status == SeatStatus.Available)
            .Select(s => new SeatMonitorItem(s.Id, s.Row, s.Number, $"{s.Row}-{s.Number}"))
            .ToList();

        var confirmed = seats
            .Where(s => s.Status == SeatStatus.Sold)
            .Select(s => new SeatMonitorItem(s.Id, s.Row, s.Number, $"{s.Row}-{s.Number}"))
            .ToList();

        var reservedCount = seats.Count(s => s.Status == SeatStatus.Reserved);

        return new EventSeatsSnapshot(
            eventId,
            DateTime.UtcNow,
            available,
            confirmed,
            reservedCount,
            available.Count,
            confirmed.Count);
    }
}
