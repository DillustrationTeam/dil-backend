namespace ArtCommission.Application.Payment.Common;

using ArtCommission.Domain.Entities.Payment;

/// <summary>
/// Tiện ích che số tài khoản ngân hàng.
/// Nguyên tắc: KHÔNG bao giờ trả số tài khoản đầy đủ ra client.
/// </summary>
public static class BankAccountMasker
{
    /// <summary>Che số tài khoản, chỉ giữ 4 ký tự cuối. VD: "1234567890" => "******7890".</summary>
    public static string Mask(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            return string.Empty;
        }

        var value = accountNumber.Trim();

        if (value.Length <= 4)
        {
            return new string('*', value.Length);
        }

        return new string('*', value.Length - 4) + value[^4..];
    }

    public static BankAccountDto ToDto(BankAccount entity) => new(
        BankAccountId: entity.Id,
        BankName: entity.BankName,
        BankBin: entity.BankBin,
        BankCode: entity.BankCode,
        AccountNumberMasked: Mask(entity.AccountNumber),
        AccountHolder: entity.AccountHolder,
        IsDefault: entity.IsDefault,
        IsVerified: entity.IsVerified
    );

    /// <summary>Snapshot thông tin ngân hàng để lưu vào PayoutRequest (đề phòng Creator sửa/xoá TK sau).</summary>
    public static string BuildSnapshot(BankAccount entity) =>
        $"{entity.BankName}|{Mask(entity.AccountNumber)}|{entity.AccountHolder}";
}
