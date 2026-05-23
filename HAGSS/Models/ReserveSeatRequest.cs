using System.ComponentModel.DataAnnotations;

namespace HAGSS.Models;

public sealed record ReserveSeatRequest(
    [property: Required, EmailAddress] string CustomerEmail);
