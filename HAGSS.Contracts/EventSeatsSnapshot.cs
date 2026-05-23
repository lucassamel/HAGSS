namespace HAGSS.Contracts;

public sealed record EventSeatsSnapshot(
    Guid EventId,
    DateTime UpdatedAtUtc,
    IReadOnlyList<SeatMonitorItem> Available,
    IReadOnlyList<SeatMonitorItem> Confirmed,
    int ReservedCount,
    int AvailableCount,
    int ConfirmedCount);
