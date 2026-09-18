using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Payment.Common;

public static class VoucherScopeParser
{
    /// <summary>
    /// Đọc mảng chuỗi (<c>["Commission","Deposit"]</c>) thành cờ bit <see cref="VoucherScope"/>.
    /// Không phân biệt hoa thường. Bỏ qua phần tử rỗng.
    /// </summary>
    public static bool TryParse(IEnumerable<string>? values, out VoucherScope scope, out string[] errors)
    {
        scope = VoucherScope.None;
        var invalid = new List<string>();

        foreach (var raw in values ?? [])
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (Enum.TryParse<VoucherScope>(raw.Trim(), ignoreCase: true, out var parsed)
                && parsed != VoucherScope.None)
            {
                scope |= parsed;
            }
            else
            {
                invalid.Add(raw.Trim());
            }
        }

        if (invalid.Count > 0)
        {
            errors = [$"Phạm vi áp dụng không hợp lệ: {string.Join(", ", invalid)}. Hợp lệ: {string.Join(", ", VoucherScopeNames.All)}."];
            return false;
        }

        errors = [];
        return true;
    }

    /// <summary>Đổi cờ bit thành mảng chuỗi để trả ra API.</summary>
    public static string[] ToNames(VoucherScope scope)
    {
        var names = new List<string>(3);

        if (scope.HasFlag(VoucherScope.Commission))
        {
            names.Add(VoucherScopeNames.Commission);
        }

        if (scope.HasFlag(VoucherScope.Auction))
        {
            names.Add(VoucherScopeNames.Auction);
        }

        if (scope.HasFlag(VoucherScope.Deposit))
        {
            names.Add(VoucherScopeNames.Deposit);
        }

        return [.. names];
    }

    /// <summary>Đổi 1 chuỗi refType của giao dịch thành cờ để so với <c>Voucher.Scope</c>.</summary>
    public static VoucherScope FromRefType(string? refType) =>
        Enum.TryParse<VoucherScope>(refType?.Trim(), ignoreCase: true, out var scope)
            ? scope
            : VoucherScope.None;

    public static string Describe(VoucherScope scope)
    {
        var names = ToNames(scope);
        return names.Length == 0 ? "Không giới hạn" : string.Join(", ", names);
    }
}
