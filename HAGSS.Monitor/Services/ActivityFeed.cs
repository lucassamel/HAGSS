using HAGSS.Contracts;

namespace HAGSS.Monitor.Services;

public sealed class ActivityFeed
{
    private readonly object _lock = new();
    private readonly LinkedList<ReservationActivityEvent> _events = new();
    private ActivityStatsSnapshot _stats = new(0, 0, 0, 0, 0, null);
    private string _connectionState = "Disconnected";

    public event Action? Changed;

    public string ConnectionState
    {
        get { lock (_lock) return _connectionState; }
    }

    public ActivityStatsSnapshot Stats
    {
        get { lock (_lock) return _stats; }
    }

    public IReadOnlyList<ReservationActivityEvent> Events
    {
        get
        {
            lock (_lock)
                return _events.ToList();
        }
    }

    public void SetConnectionState(string state)
    {
        lock (_lock)
            _connectionState = state;
        Changed?.Invoke();
    }

    public void AddEvent(ReservationActivityEvent activityEvent, int maxEvents)
    {
        lock (_lock)
        {
            _events.AddFirst(activityEvent);
            while (_events.Count > maxEvents)
                _events.RemoveLast();
        }

        Changed?.Invoke();
    }

    public void UpdateStats(ActivityStatsSnapshot stats)
    {
        lock (_lock)
            _stats = stats;
        Changed?.Invoke();
    }

    public void ReplaceSnapshot(IEnumerable<ReservationActivityEvent> events, ActivityStatsSnapshot stats, int maxEvents)
    {
        lock (_lock)
        {
            _events.Clear();
            foreach (var item in events.Take(maxEvents))
                _events.AddLast(item);
            _stats = stats;
        }

        Changed?.Invoke();
    }
}
