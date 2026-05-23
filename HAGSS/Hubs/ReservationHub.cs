using Microsoft.AspNetCore.SignalR;

namespace HAGSS.Hubs;

public sealed class ReservationHub : Hub
{
    public const string Path = "/hubs/reservations";
}
