namespace HAGSS.Contracts;

public enum ActivityOutcome
{
    Accepted = 0,
    SeatNotAvailable = 1,
    ConcurrencyConflict = 2,
    LockNotAcquired = 3,
    SeatNotFound = 4,
    BadRequest = 5,
    PaymentConfirmed = 6,
    PaymentFailed = 7,
    ClientError = 8
}
