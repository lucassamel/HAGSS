using HAGSS.Data.Entities;

namespace HAGSS.Models;

public sealed record SeatResponse(Guid Id, string Row, string Number, SeatStatus Status);
