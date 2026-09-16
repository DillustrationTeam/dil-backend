namespace ArtCommission.Domain.Enums;

public enum EscrowStatus
{
    Pending = 1,
    Deposited = 2,
    PartialReleased = 3,
    Released = 4,
    Refunded = 5,
    Disputed = 6
}
