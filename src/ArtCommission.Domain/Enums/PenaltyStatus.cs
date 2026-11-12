namespace ArtCommission.Domain.Enums;

/// <summary>
/// Lifecycle of a seller penalty that was created after an auction delivery dispute
/// was confirmed in favour of the buyer.
/// </summary>
public enum PenaltyStatus
{
    /// <summary>Penalty is being served: active surcharge or active selling suspension.</summary>
    Active = 0,

    /// <summary>Surcharge was applied for the whole session window, penalty is finished.</summary>
    Consumed = 1,

    /// <summary>Administrator revoked the penalty, it no longer has any effect.</summary>
    Revoked = 2,
}
