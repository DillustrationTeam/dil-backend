namespace ArtCommission.Application.Payment.Common;

/// <summary>
/// Yêu cầu tạo lệnh chi hộ (Payout) qua payOS.
/// </summary>
public record PayOsPayoutRequest(
    string ReferenceId,
    long Amount,
    string Description,
    string ToBin,
    string ToAccountNumber
);

/// <summary>
/// Kết quả gọi lệnh chi hộ qua payOS.
/// </summary>
public record PayOsPayoutResult(
    bool Success,
    string? TransactionRef,
    string? ErrorCode,
    string? Message
);

/// <summary>
/// Service giao tiếp với Kênh chi (Disbursement) của payOS.
/// </summary>
public interface IPayOsPayoutService
{
    Task<PayOsPayoutResult> CreatePayoutAsync(PayOsPayoutRequest request, CancellationToken cancellationToken = default);
}
