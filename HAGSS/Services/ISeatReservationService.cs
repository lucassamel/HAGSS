namespace HAGSS.Services;

public interface ISeatReservationService
{
    Task<ReservationResult> ReserveAsync(
        Guid eventId,
        Guid seatId,
        string customerEmail,
        CancellationToken cancellationToken);
}
