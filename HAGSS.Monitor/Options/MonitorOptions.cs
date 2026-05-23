namespace HAGSS.Monitor.Options;

public class MonitorOptions
{
    public const string SectionName = "Monitor";

    public string ApiBaseUrl { get; set; } = "http://localhost:8080";
    public int MaxDisplayedEvents { get; set; } = 200;
}
