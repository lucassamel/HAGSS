namespace HAGSS.Options;

public class ReservationOptions
{
    public const string SectionName = "Reservation";

    public decimal DefaultSeatPrice { get; set; } = 99.99m;
    public int LockExpirySeconds { get; set; } = 10;
    public int LockWaitSeconds { get; set; } = 5;
}
