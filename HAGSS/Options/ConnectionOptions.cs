namespace HAGSS.Options;

public class ConnectionOptions
{
    public const string SectionName = "ConnectionStrings";

    public string Postgres { get; set; } = string.Empty;
    public string Redis { get; set; } = string.Empty;
    public string RabbitMq { get; set; } = string.Empty;
}
