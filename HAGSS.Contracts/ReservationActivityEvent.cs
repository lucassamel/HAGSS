namespace HAGSS.Contracts;

public sealed record ReservationActivityEvent(
    Guid Id,
    DateTime OccurredAtUtc,
    Guid EventId,
    Guid SeatId,
    string? SeatLabel,
    string CustomerEmail,
    string? TimeZoneId,
    string? ClientId,
    string Source,
    ActivityOutcome Outcome,
    int HttpStatusCode,
    Guid? ReservationId,
    string? Message);

public sealed record ActivityStatsSnapshot(
    long TotalEvents,
    long Accepted,
    long Conflicts,
    long LockTimeouts,
    long PaymentConfirmed,
    DateTime? LastEventAtUtc);
