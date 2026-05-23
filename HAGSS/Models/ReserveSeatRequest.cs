using System.ComponentModel.DataAnnotations;

namespace HAGSS.Models;

public sealed record ReserveSeatRequest(
    [property: Required, EmailAddress] string CustomerEmail,
    string? TimeZoneId = null,
    string? ClientId = null);
