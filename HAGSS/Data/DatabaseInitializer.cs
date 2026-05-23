using HAGSS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HAGSS.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        await db.Database.MigrateAsync(cancellationToken);

        if (await db.Events.AnyAsync(cancellationToken))
            return;

        var sampleEvent = new Event
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Sample Concert",
            StartsAtUtc = DateTime.UtcNow.AddDays(30)
        };

        var seats = new List<Seat>();
        for (var row = 1; row <= 10; row++)
        {
            for (var number = 1; number <= 20; number++)
            {
                seats.Add(new Seat
                {
                    Id = Guid.NewGuid(),
                    EventId = sampleEvent.Id,
                    Row = row.ToString(),
                    Number = number.ToString("D2"),
                    Status = SeatStatus.Available
                });
            }
        }

        sampleEvent.Seats = seats;
        db.Events.Add(sampleEvent);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded sample event {EventId} with {SeatCount} seats", sampleEvent.Id, seats.Count);
    }
}
