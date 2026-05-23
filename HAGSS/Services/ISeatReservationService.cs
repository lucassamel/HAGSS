namespace HAGSS.Services;

public interface ISeatReservationService
{
    Task<ReservationResult> ReserveAsync(
        Guid eventId,
        Guid seatId,
        string customerEmail,
        string? timeZoneId = null,
        string? clientId = null,
        string source = "api",
        CancellationToken cancellationToken = default);
}
