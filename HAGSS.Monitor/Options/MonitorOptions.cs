namespace HAGSS.Monitor.Options;

public class MonitorOptions
{
    public const string SectionName = "Monitor";

    public string ApiBaseUrl { get; set; } = "http://localhost:8080";
    public Guid EventId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public int MaxDisplayedEvents { get; set; } = 200;
    public int SeatRefreshIntervalSeconds { get; set; } = 5;
}
