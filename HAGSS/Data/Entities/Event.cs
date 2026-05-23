namespace HAGSS.Data.Entities;

public class Event
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public ICollection<Seat> Seats { get; set; } = [];
}
