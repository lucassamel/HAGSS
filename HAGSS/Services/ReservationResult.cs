namespace HAGSS.Services;

public enum ReservationErrorCode
{
    None,
    SeatNotFound,
    SeatNotAvailable,
    LockNotAcquired,
    ConcurrencyConflict
}

public sealed record ReservationResult(
    bool Success,
    Guid? ReservationId,
    ReservationErrorCode ErrorCode,
    string? Message = null)
{
    public static ReservationResult Ok(Guid reservationId) =>
        new(true, reservationId, ReservationErrorCode.None);

    public static ReservationResult Fail(ReservationErrorCode code, string message) =>
        new(false, null, code, message);
}
