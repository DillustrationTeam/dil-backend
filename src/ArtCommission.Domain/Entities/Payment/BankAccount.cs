using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Payment;

/// <summary>
/// Tài khoản ngân hàng nhận tiền của Creator (UC49 — /api/v1/bank-accounts).
/// Mỗi Creator có thể lưu nhiều tài khoản, trong đó tối đa 1 tài khoản mặc định.
/// </summary>
public class BankAccount : BaseEntity
{
    public Guid UserId { get; set; }

    public string BankName { get; set; } = string.Empty;

    /// <summary>
    /// Mã BIN ngân hàng (VD: 970422 = MB Bank).
    /// Cần khi gọi API chi hộ của payOS (`toBin`).
    /// </summary>
    public string? BankBin { get; set; }

    /// <summary>Mã ngắn ngân hàng (VCB, TCB, MB...).</summary>
    public string? BankCode { get; set; }

    /// <summary>Số tài khoản — lưu thô, CHE (masked) khi trả về client.</summary>
    public string AccountNumber { get; set; } = string.Empty;

    public string AccountHolder { get; set; } = string.Empty;

    /// <summary>Mỗi user chỉ được có 1 tài khoản mặc định (ràng buộc ở tầng DB).</summary>
    public bool IsDefault { get; set; }

    public bool IsVerified { get; set; }
}
