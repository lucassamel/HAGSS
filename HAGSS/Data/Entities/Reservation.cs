namespace HAGSS.Data.Entities;

public class Reservation
{
    public Guid Id { get; set; }
    public Guid SeatId { get; set; }
    public Guid EventId { get; set; }
    public required string CustomerEmail { get; set; }
    public decimal Amount { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.PendingPayment;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public Seat? Seat { get; set; }
}
