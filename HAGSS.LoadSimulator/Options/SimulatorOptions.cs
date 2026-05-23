namespace HAGSS.LoadSimulator.Options;

public class SimulatorOptions
{
    public const string SectionName = "Simulator";

    public string ApiBaseUrl { get; set; } = "http://localhost:8080";
    public Guid EventId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public int MinUsers { get; set; } = 1;
    public int MaxUsers { get; set; } = 1000;
    public int MinDelayMs { get; set; } = 0;
    public int MaxDelayMs { get; set; } = 2000;
    public int HotSeatPoolSize { get; set; } = 8;
    public double HotSeatProbability { get; set; } = 0.75;
    public int RoundIntervalSeconds { get; set; } = 45;
    public int StartupDelaySeconds { get; set; } = 10;
    public int MaxParallelRequests { get; set; } = 200;
}
