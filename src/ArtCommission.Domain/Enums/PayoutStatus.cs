namespace ArtCommission.Domain.Enums;

/// <summary>
/// Trạng thái yêu cầu rút tiền (UC49).
/// Pending  : Creator đã gửi, ví đã bị trừ ngay, chờ Admin duyệt
/// Processed: Admin đã chuyển khoản xong
/// Rejected : Admin từ chối, tiền đã được hoàn lại ví Creator
/// </summary>
public enum PayoutStatus
{
    Pending,
    Processed,
    Rejected
}

public static class PayoutStatusNames
{
    public const string Pending = nameof(PayoutStatus.Pending);
    public const string Processed = nameof(PayoutStatus.Processed);
    public const string Rejected = nameof(PayoutStatus.Rejected);

    public static readonly string[] All = [Pending, Processed, Rejected];
}
