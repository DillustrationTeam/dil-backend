namespace ArtCommission.Application.Payment.Common;

/// <summary>Tài khoản ngân hàng nhận tiền của Creator (UC49).</summary>
public record BankAccountDto(
    Guid BankAccountId,
    string BankName,
    string? BankBin,
    string? BankCode,
    /// <summary>Số tài khoản ĐÃ CHE — chỉ lộ 4 số cuối.</summary>
    string AccountNumberMasked,
    string AccountHolder,
    bool IsDefault,
    bool IsVerified
);

/// <summary>Yêu cầu rút tiền (UC49).</summary>
public record PayoutRequestDto(
    Guid PayoutRequestId,
    decimal Amount,
    string PayoutStatus,
    string? PayoutNote,
    Guid? ProcessedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt,
    string? TransactionRef,
    string? RejectReason,
    BankAccountDto? BankAccount
);
