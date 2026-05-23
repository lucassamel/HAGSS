namespace HAGSS.Services;

public interface ISeatSnapshotBroadcaster
{
    Task BroadcastAsync(Guid eventId, CancellationToken cancellationToken = default);
}
