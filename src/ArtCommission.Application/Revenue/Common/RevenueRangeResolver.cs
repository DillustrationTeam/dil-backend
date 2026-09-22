namespace ArtCommission.Application.Revenue.Common;

/// <summary>
/// Chuẩn hoá khoảng thời gian dùng chung cho các endpoint doanh thu (UC51).
///
/// VÌ SAO cần lớp riêng: quy tắc "mặc định 30 ngày", "từ phải trước đến", "chặn khoảng
/// quá dài" phải GIỐNG NHAU ở mọi endpoint. Nếu mỗi endpoint tự xử lý, cùng một
/// dashboard sẽ cho ra số liệu lệch nhau giữa các thẻ vì mỗi thẻ hiểu mặc định một kiểu.
/// </summary>
public static class RevenueRangeResolver
{
    /// <summary>Khoảng mặc định khi client không truyền gì.</summary>
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(30);

    /// <summary>Trần khoảng truy vấn — chặn truy vấn quét toàn bộ sổ cái.</summary>
    private static readonly TimeSpan MaxWindow = TimeSpan.FromDays(366 * 3);

    /// <summary>
    /// Trả về khoảng đã chuẩn hoá, hoặc <c>Error</c> nếu khoảng không hợp lệ.
    /// Khi có lỗi, <c>From</c>/<c>To</c> là giá trị mặc định và không được dùng.
    /// </summary>
    public static (DateTimeOffset From, DateTimeOffset To, string? Error) Resolve(
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var now = DateTimeOffset.UtcNow;

        var resolvedTo = to ?? now;
        var resolvedFrom = from ?? resolvedTo - DefaultWindow;

        if (resolvedFrom >= resolvedTo)
        {
            return (default, default, "Thời điểm bắt đầu phải trước thời điểm kết thúc.");
        }

        if (resolvedTo - resolvedFrom > MaxWindow)
        {
            return (default, default, "Khoảng thời gian truy vấn tối đa là 3 năm.");
        }

        return (resolvedFrom, resolvedTo, null);
    }
}
