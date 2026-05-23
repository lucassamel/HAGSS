namespace HAGSS.Data.Entities;

public class Seat
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public required string Row { get; set; }
    public required string Number { get; set; }
    public SeatStatus Status { get; set; } = SeatStatus.Available;
    public uint RowVersion { get; set; }
    public Event? Event { get; set; }
}
