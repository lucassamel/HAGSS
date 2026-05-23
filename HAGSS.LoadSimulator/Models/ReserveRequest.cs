namespace HAGSS.LoadSimulator.Models;

public sealed record ReserveRequest(string CustomerEmail, string? TimeZoneId, string? ClientId);
