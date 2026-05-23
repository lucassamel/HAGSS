using HAGSS.Services;

namespace HAGSS.Endpoints;

public static class ActivityEndpoints
{
    public static void MapActivityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/activity").WithTags("Activity");

        group.MapGet("/recent", (IReservationActivityPublisher publisher, int? count) =>
            Results.Ok(publisher.GetRecent(count ?? 100)))
            .WithName("GetRecentActivity");

        group.MapGet("/stats", (IReservationActivityPublisher publisher) =>
            Results.Ok(publisher.GetStats()))
            .WithName("GetActivityStats");
    }
}
