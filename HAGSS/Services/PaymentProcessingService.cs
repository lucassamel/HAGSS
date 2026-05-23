using HAGSS.Contracts;
using HAGSS.Data;
using HAGSS.Data.Entities;
using HAGSS.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;

namespace HAGSS.Services;

public interface IPaymentProcessingService
{
    Task ProcessAsync(PaymentMessage message, CancellationToken cancellationToken);
}

public sealed class PaymentProcessingService(
    AppDbContext db,
    IReservationActivityPublisher activityPublisher,
    ILogger<PaymentProcessingService> logger) : IPaymentProcessingService
{
    private static readonly ResiliencePipeline PaymentPipeline = CreatePaymentPipeline();

    public async Task ProcessAsync(PaymentMessage message, CancellationToken cancellationToken)
    {
        await PaymentPipeline.ExecuteAsync(token => ProcessCoreAsync(message, token), cancellationToken);
    }

    private async ValueTask ProcessCoreAsync(PaymentMessage message, CancellationToken cancellationToken)
    {
        var reservation = await db.Reservations
            .Include(r => r.Seat)
            .FirstOrDefaultAsync(r => r.Id == message.ReservationId, cancellationToken);

        if (reservation is null)
        {
            logger.LogWarning("Reservation {ReservationId} not found for payment processing", message.ReservationId);
            return;
        }

        if (reservation.Status is ReservationStatus.Confirmed or ReservationStatus.Cancelled)
            return;

        // Simulated payment gateway latency and occasional transient failures.
        await Task.Delay(Random.Shared.Next(50, 200), cancellationToken);
        if (Random.Shared.NextDouble() < 0.05)
            throw new InvalidOperationException("Simulated payment gateway timeout.");

        reservation.Status = ReservationStatus.Confirmed;
        reservation.ConfirmedAtUtc = DateTime.UtcNow;

        if (reservation.Seat is not null)
            reservation.Seat.Status = SeatStatus.Sold;

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Payment confirmed for reservation {ReservationId}", message.ReservationId);

        var seatLabel = reservation.Seat is null ? null : $"{reservation.Seat.Row}-{reservation.Seat.Number}";
        await activityPublisher.PublishAsync(new ReservationActivityEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            message.EventId,
            message.SeatId,
            seatLabel,
            message.CustomerEmail,
            null,
            null,
            "payment-worker",
            ActivityOutcome.PaymentConfirmed,
            StatusCodes.Status200OK,
            message.ReservationId,
            "Payment confirmed; seat marked as sold."), cancellationToken);
    }

    private static ResiliencePipeline CreatePaymentPipeline()
    {
        var retry = new RetryStrategyOptions
        {
            MaxRetryAttempts = 6,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>()
        };

        return new ResiliencePipelineBuilder()
            .AddRetry(retry)
            .Build();
    }
}
