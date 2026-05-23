using System.Collections.Concurrent;
using HAGSS.Contracts;
using HAGSS.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace HAGSS.Services;

public sealed class ReservationActivityPublisher(IHubContext<ReservationHub> hub) : IReservationActivityPublisher
{
    private const int MaxRecentEvents = 500;
    private readonly ConcurrentQueue<ReservationActivityEvent> _recent = new();
    private long _totalEvents;
    private long _accepted;
    private long _conflicts;
    private long _lockTimeouts;
    private long _paymentConfirmed;
    private DateTime? _lastEventAtUtc;

    public async ValueTask PublishAsync(ReservationActivityEvent activityEvent, CancellationToken cancellationToken = default)
    {
        _recent.Enqueue(activityEvent);
        while (_recent.Count > MaxRecentEvents && _recent.TryDequeue(out _))
        {
        }

        Interlocked.Increment(ref _totalEvents);
        _lastEventAtUtc = activityEvent.OccurredAtUtc;

        switch (activityEvent.Outcome)
        {
            case ActivityOutcome.Accepted:
                Interlocked.Increment(ref _accepted);
                break;
            case ActivityOutcome.ConcurrencyConflict:
            case ActivityOutcome.SeatNotAvailable:
                Interlocked.Increment(ref _conflicts);
                break;
            case ActivityOutcome.LockNotAcquired:
                Interlocked.Increment(ref _lockTimeouts);
                break;
            case ActivityOutcome.PaymentConfirmed:
                Interlocked.Increment(ref _paymentConfirmed);
                break;
        }

        await hub.Clients.All.SendAsync("ActivityReceived", activityEvent, cancellationToken);
        await hub.Clients.All.SendAsync("StatsUpdated", GetStats(), cancellationToken);
    }

    public IReadOnlyList<ReservationActivityEvent> GetRecent(int count = 100) =>
        _recent.Reverse().Take(count).ToList();

    public ActivityStatsSnapshot GetStats() => new(
        Interlocked.Read(ref _totalEvents),
        Interlocked.Read(ref _accepted),
        Interlocked.Read(ref _conflicts),
        Interlocked.Read(ref _lockTimeouts),
        Interlocked.Read(ref _paymentConfirmed),
        _lastEventAtUtc);
}
