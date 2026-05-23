using HAGSS.Contracts;

namespace HAGSS.Services;

public interface IReservationActivityPublisher
{
    ValueTask PublishAsync(ReservationActivityEvent activityEvent, CancellationToken cancellationToken = default);

    IReadOnlyList<ReservationActivityEvent> GetRecent(int count = 100);

    ActivityStatsSnapshot GetStats();
}
