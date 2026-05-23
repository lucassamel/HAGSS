namespace HAGSS.Infrastructure.Messaging;

public sealed record PaymentMessage(
    Guid ReservationId,
    Guid SeatId,
    Guid EventId,
    string CustomerEmail,
    decimal Amount);
