using HAGSS.Contracts;
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
    IReservationActivityPublisher activityPublisher,
    IOptions<ReservationOptions> reservationOptions,
    ILogger<SeatReservationService> logger) : ISeatReservationService
{
    public async Task<ReservationResult> ReserveAsync(
        Guid eventId,
        Guid seatId,
        string customerEmail,
        string? timeZoneId = null,
        string? clientId = null,
        string source = "api",
        CancellationToken cancellationToken = default)
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
            var lockFail = ReservationResult.Fail(ReservationErrorCode.LockNotAcquired, "Seat is being reserved by another request. Try again.");
            await PublishAsync(eventId, seatId, null, customerEmail, timeZoneId, clientId, source, lockFail, StatusCodes.Status429TooManyRequests, cancellationToken);
            return lockFail;
        }

        await using (lockHandle)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var seat = await db.Seats
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == seatId && s.EventId == eventId, cancellationToken);

            if (seat is null)
            {
                var notFound = ReservationResult.Fail(ReservationErrorCode.SeatNotFound, "Seat not found for this event.");
                await PublishAsync(eventId, seatId, null, customerEmail, timeZoneId, clientId, source, notFound, StatusCodes.Status404NotFound, cancellationToken);
                return notFound;
            }

            if (seat.Status != SeatStatus.Available)
            {
                var unavailable = ReservationResult.Fail(ReservationErrorCode.SeatNotAvailable, "Seat is no longer available.");
                await PublishAsync(eventId, seatId, $"{seat.Row}-{seat.Number}", customerEmail, timeZoneId, clientId, source, unavailable, StatusCodes.Status409Conflict, cancellationToken);
                return unavailable;
            }

            var rowsUpdated = await db.Seats
                .Where(s => s.Id == seatId && s.EventId == eventId && s.Status == SeatStatus.Available)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(s => s.Status, SeatStatus.Reserved),
                    cancellationToken);

            if (rowsUpdated == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                var conflict = ReservationResult.Fail(ReservationErrorCode.ConcurrencyConflict, "Seat was reserved by another transaction.");
                await PublishAsync(eventId, seatId, $"{seat.Row}-{seat.Number}", customerEmail, timeZoneId, clientId, source, conflict, StatusCodes.Status409Conflict, cancellationToken);
                return conflict;
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
                var constraintFail = ReservationResult.Fail(ReservationErrorCode.ConcurrencyConflict, "Seat was already reserved.");
                await PublishAsync(eventId, seatId, $"{seat.Row}-{seat.Number}", customerEmail, timeZoneId, clientId, source, constraintFail, StatusCodes.Status409Conflict, cancellationToken);
                return constraintFail;
            }

            await paymentPublisher.PublishAsync(
                new PaymentMessage(reservation.Id, seatId, eventId, customerEmail, reservation.Amount),
                cancellationToken);

            logger.LogInformation("Reservation {ReservationId} created for seat {SeatId}", reservation.Id, seatId);
            var success = ReservationResult.Ok(reservation.Id);
            await PublishAsync(eventId, seatId, $"{seat.Row}-{seat.Number}", customerEmail, timeZoneId, clientId, source, success, StatusCodes.Status202Accepted, cancellationToken);
            return success;
        }
    }

    private async Task PublishAsync(
        Guid eventId,
        Guid seatId,
        string? seatLabel,
        string customerEmail,
        string? timeZoneId,
        string? clientId,
        string source,
        ReservationResult result,
        int httpStatus,
        CancellationToken cancellationToken)
    {
        var activity = new ReservationActivityEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            eventId,
            seatId,
            seatLabel,
            customerEmail,
            timeZoneId,
            clientId,
            source,
            MapOutcome(result.ErrorCode),
            httpStatus,
            result.ReservationId,
            result.Message);

        await activityPublisher.PublishAsync(activity, cancellationToken);
    }

    private static ActivityOutcome MapOutcome(ReservationErrorCode code) => code switch
    {
        ReservationErrorCode.None => ActivityOutcome.Accepted,
        ReservationErrorCode.SeatNotFound => ActivityOutcome.SeatNotFound,
        ReservationErrorCode.SeatNotAvailable => ActivityOutcome.SeatNotAvailable,
        ReservationErrorCode.LockNotAcquired => ActivityOutcome.LockNotAcquired,
        ReservationErrorCode.ConcurrencyConflict => ActivityOutcome.ConcurrencyConflict,
        _ => ActivityOutcome.BadRequest
    };
}
