using HAGSS.Contracts;

namespace HAGSS.Monitor.Services;

public sealed class ActivityFeed
{
    private readonly object _lock = new();
    private readonly LinkedList<ReservationActivityEvent> _events = new();
    private ActivityStatsSnapshot _stats = new(0, 0, 0, 0, 0, null);
    private EventSeatsSnapshot? _seats;
    private string _connectionState = "Disconnected";
    private long _version;

    public event Action? Changed;

    public long Version
    {
        get { lock (_lock) return _version; }
    }

    public string ConnectionState
    {
        get { lock (_lock) return _connectionState; }
    }

    public ActivityStatsSnapshot Stats
    {
        get { lock (_lock) return _stats; }
    }

    public EventSeatsSnapshot? Seats
    {
        get { lock (_lock) return _seats; }
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
        NotifyChanged();
    }

    public void AddEvent(ReservationActivityEvent activityEvent, int maxEvents)
    {
        lock (_lock)
        {
            _events.AddFirst(activityEvent);
            while (_events.Count > maxEvents)
                _events.RemoveLast();
            _version++;
        }

        NotifyChanged();
    }

    public void UpdateStats(ActivityStatsSnapshot stats)
    {
        lock (_lock)
        {
            _stats = stats;
            _version++;
        }

        NotifyChanged();
    }

    public void UpdateSeats(EventSeatsSnapshot snapshot)
    {
        lock (_lock)
        {
            _seats = snapshot;
            _version++;
        }

        NotifyChanged();
    }

    public void ReplaceSnapshot(
        IEnumerable<ReservationActivityEvent> events,
        ActivityStatsSnapshot stats,
        EventSeatsSnapshot? seats,
        int maxEvents)
    {
        lock (_lock)
        {
            _events.Clear();
            foreach (var item in events.Take(maxEvents))
                _events.AddLast(item);
            _stats = stats;
            _seats = seats;
            _version++;
        }

        NotifyChanged();
    }

    private void NotifyChanged() => Changed?.Invoke();
}
