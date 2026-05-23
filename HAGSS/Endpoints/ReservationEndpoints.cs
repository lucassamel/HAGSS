using HAGSS.Data;
using HAGSS.Models;
using HAGSS.Services;
using Microsoft.EntityFrameworkCore;

namespace HAGSS.Endpoints;

public static class ReservationEndpoints
{
    public static void MapReservationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").WithTags("Reservations");

        group.MapGet("/events", async (AppDbContext db, CancellationToken ct) =>
        {
            var events = await db.Events
                .AsNoTracking()
                .OrderBy(e => e.StartsAtUtc)
                .Select(e => new EventResponse(e.Id, e.Name, e.StartsAtUtc))
                .ToListAsync(ct);
            return Results.Ok(events);
        })
        .WithName("ListEvents");

        group.MapGet("/events/{eventId:guid}/seats", async (Guid eventId, AppDbContext db, CancellationToken ct) =>
        {
            var seats = await db.Seats
                .AsNoTracking()
                .Where(s => s.EventId == eventId)
                .OrderBy(s => s.Row)
                .ThenBy(s => s.Number)
                .Select(s => new SeatResponse(s.Id, s.Row, s.Number, s.Status))
                .ToListAsync(ct);

            return seats.Count == 0 ? Results.NotFound() : Results.Ok(seats);
        })
        .WithName("ListSeats");

        group.MapPost("/events/{eventId:guid}/seats/{seatId:guid}/reserve", async (
            Guid eventId,
            Guid seatId,
            ReserveSeatRequest request,
            ISeatReservationService reservationService,
            CancellationToken ct) =>
        {
            var result = await reservationService.ReserveAsync(eventId, seatId, request.CustomerEmail, ct);

            return result.ErrorCode switch
            {
                ReservationErrorCode.None when result.ReservationId.HasValue =>
                    Results.Accepted($"/api/reservations/{result.ReservationId}", new ReserveSeatResponse(
                        result.ReservationId.Value,
                        "Reservation created. Payment is being processed asynchronously.")),

                ReservationErrorCode.SeatNotFound => Results.NotFound(new { result.Message }),
                ReservationErrorCode.SeatNotAvailable => Results.Conflict(new { result.Message }),
                ReservationErrorCode.LockNotAcquired => Results.Json(
                    new { result.Message },
                    statusCode: StatusCodes.Status429TooManyRequests),
                ReservationErrorCode.ConcurrencyConflict => Results.Conflict(new { result.Message }),
                _ => Results.BadRequest(new { result.Message })
            };
        })
        .WithName("ReserveSeat");

        group.MapGet("/reservations/{reservationId:guid}", async (Guid reservationId, AppDbContext db, CancellationToken ct) =>
        {
            var reservation = await db.Reservations
                .AsNoTracking()
                .Where(r => r.Id == reservationId)
                .Select(r => new
                {
                    r.Id,
                    r.EventId,
                    r.SeatId,
                    r.CustomerEmail,
                    r.Amount,
                    r.Status,
                    r.CreatedAtUtc,
                    r.ConfirmedAtUtc
                })
                .FirstOrDefaultAsync(ct);

            return reservation is null ? Results.NotFound() : Results.Ok(reservation);
        })
        .WithName("GetReservation");
    }
}
