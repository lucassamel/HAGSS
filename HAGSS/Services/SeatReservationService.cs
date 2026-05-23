using HAGSS.Data;
using HAGSS.Data.Entities;
using HAGSS.Infrastructure.Messaging;
using HAGSS.Infrastructure.Redis;
using HAGSS.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HAGSS.Services;

public sealed class SeatReservationService(
    AppDbContext db,
    IRedisDistributedLock distributedLock,
    IPaymentPublisher paymentPublisher,
    IOptions<ReservationOptions> reservationOptions,
    ILogger<SeatReservationService> logger) : ISeatReservationService
{
    public async Task<ReservationResult> ReserveAsync(
        Guid eventId,
        Guid seatId,
        string customerEmail,
        CancellationToken cancellationToken)
    {
        var options = reservationOptions.Value;
        var lockKey = $"seat:{eventId}:{seatId}";
        var lockHandle = await distributedLock.TryAcquireAsync(
            lockKey,
            TimeSpan.FromSeconds(options.LockExpirySeconds),
            TimeSpan.FromSeconds(options.LockWaitSeconds),
            cancellationToken);

        if (lockHandle is null)
        {
            logger.LogWarning("Could not acquire distributed lock for seat {SeatId}", seatId);
            return ReservationResult.Fail(ReservationErrorCode.LockNotAcquired, "Seat is being reserved by another request. Try again.");
        }

        await using (lockHandle)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var seat = await db.Seats
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == seatId && s.EventId == eventId, cancellationToken);

            if (seat is null)
                return ReservationResult.Fail(ReservationErrorCode.SeatNotFound, "Seat not found for this event.");

            if (seat.Status != SeatStatus.Available)
                return ReservationResult.Fail(ReservationErrorCode.SeatNotAvailable, "Seat is no longer available.");

            var rowsUpdated = await db.Seats
                .Where(s => s.Id == seatId && s.EventId == eventId && s.Status == SeatStatus.Available)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(s => s.Status, SeatStatus.Reserved),
                    cancellationToken);

            if (rowsUpdated == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ReservationResult.Fail(ReservationErrorCode.ConcurrencyConflict, "Seat was reserved by another transaction.");
            }

            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                SeatId = seatId,
                EventId = eventId,
                CustomerEmail = customerEmail,
                Amount = options.DefaultSeatPrice,
                Status = ReservationStatus.PendingPayment,
                CreatedAtUtc = DateTime.UtcNow
            };

            db.Reservations.Add(reservation);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogWarning(ex, "Unique constraint prevented double booking for seat {SeatId}", seatId);
                return ReservationResult.Fail(ReservationErrorCode.ConcurrencyConflict, "Seat was already reserved.");
            }

            await paymentPublisher.PublishAsync(
                new PaymentMessage(reservation.Id, seatId, eventId, customerEmail, reservation.Amount),
                cancellationToken);

            logger.LogInformation("Reservation {ReservationId} created for seat {SeatId}", reservation.Id, seatId);
            return ReservationResult.Ok(reservation.Id);
        }
    }
}
