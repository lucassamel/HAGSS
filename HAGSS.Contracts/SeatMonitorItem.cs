namespace HAGSS.Contracts;

public sealed record SeatMonitorItem(
    Guid Id,
    string Row,
    string Number,
    string Label);
