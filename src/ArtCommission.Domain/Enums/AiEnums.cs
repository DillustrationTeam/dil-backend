namespace ArtCommission.Domain.Enums;

/// <summary>
/// Loại ngữ cảnh gắn vào một phiên hội thoại AI (UC44).
/// Quyết định handler nạp thêm dữ liệu gì làm context cho prompt.
/// </summary>
public enum AiContextType
{
    /// <summary>Không gắn ngữ cảnh — hỏi đáp chung về nền tảng.</summary>
    General,

    /// <summary>Gắn một đơn đặt vẽ: trạng thái, milestone, escrow, deadline.</summary>
    Commission,

    /// <summary>Gắn một tranh: tiêu đề, phong cách, trạng thái kiểm duyệt.</summary>
    Artwork
}

/// <summary>Vai trò của một tin nhắn trong hội thoại AI.</summary>
public enum AiMessageRole
{
    User,
    Assistant,
    System
}

/// <summary>Kênh gửi nhắc nhở deadline (UC46).</summary>
public enum ReminderChannel
{
    InApp,
    Email,
    Push
}

/// <summary>
/// Mức rủi ro trễ hạn, suy ra từ <c>DeadlineReminder.RiskScore</c>.
/// Dùng cho nhãn màu trên UI và để quyết định có gửi cảnh báo hay không.
/// </summary>
public enum DeadlineRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>Chu kỳ tổng hợp của một bản ghi chốt doanh thu (UC51).</summary>
public enum RevenueSnapshotScope
{
    Daily,
    Weekly,
    Monthly
}

/// <summary>Nguồn phát sinh doanh thu, dùng cho breakdown.</summary>
public enum RevenueSource
{
    Commission,
    Auction
}

/// <summary>
/// Chuyển đổi enum AI/doanh thu sang chuỗi.
///
/// Tách làm hai họ hàm có chủ đích:
///   - <c>ToContractName</c>  : chuỗi trả ra API (lowercase/snake theo hợp đồng FE).
///   - <c>ToStorageName</c>   : chuỗi ghi xuống cột DB (tên enum, ổn định khi đổi API).
/// Trộn hai họ này là nguyên nhân lệch dữ liệu khi FE đổi format.
/// </summary>
public static class AiAndRevenueEnumNames
{
    public static string ToContractName(AiMessageRole role) => role switch
    {
        AiMessageRole.Assistant => "assistant",
        AiMessageRole.System => "system",
        _ => "user"
    };

    public static string ToContractName(ReminderChannel channel) => channel switch
    {
        ReminderChannel.Email => "email",
        ReminderChannel.Push => "push",
        _ => "in_app"
    };

    public static string ToContractName(RevenueSnapshotScope scope) => scope switch
    {
        RevenueSnapshotScope.Weekly => "weekly",
        RevenueSnapshotScope.Monthly => "monthly",
        _ => "daily"
    };

    public static string ToContractName(RevenueSource source) =>
        source == RevenueSource.Auction ? "auction" : "commission";

    public static string ToContractName(DeadlineRiskLevel level) => level switch
    {
        DeadlineRiskLevel.Critical => "critical",
        DeadlineRiskLevel.High => "high",
        DeadlineRiskLevel.Medium => "medium",
        _ => "low"
    };

    public static string ToStorageName(AiMessageRole role) => role.ToString();

    public static string ToStorageName(ReminderChannel channel) => channel.ToString();

    public static string ToStorageName(RevenueSnapshotScope scope) => scope.ToString();

    public static string ToStorageName(RevenueSource source) => source.ToString();

    /// <summary>Đọc enum từ cột string; giá trị lạ thì trả mặc định thay vì ném lỗi.</summary>
    public static AiMessageRole ParseRole(string? value) =>
        Enum.TryParse<AiMessageRole>(value, ignoreCase: true, out var parsed) ? parsed : AiMessageRole.User;

    public static ReminderChannel ParseChannel(string? value) =>
        Enum.TryParse<ReminderChannel>(value, ignoreCase: true, out var parsed) ? parsed : ReminderChannel.InApp;

    public static RevenueSnapshotScope ParseScope(string? value) =>
        Enum.TryParse<RevenueSnapshotScope>(value, ignoreCase: true, out var parsed)
            ? parsed
            : RevenueSnapshotScope.Daily;

    public static RevenueSource ParseSource(string? value) =>
        Enum.TryParse<RevenueSource>(value, ignoreCase: true, out var parsed)
            ? parsed
            : RevenueSource.Commission;

    /// <summary>Suy ra mức rủi ro từ điểm 0–100 theo ngưỡng đã chốt.</summary>
    public static DeadlineRiskLevel RiskLevelFromScore(decimal riskScore) => riskScore switch
    {
        >= 80m => DeadlineRiskLevel.Critical,
        >= 60m => DeadlineRiskLevel.High,
        >= 35m => DeadlineRiskLevel.Medium,
        _ => DeadlineRiskLevel.Low
    };
}
