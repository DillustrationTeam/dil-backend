using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Ai;

/// <summary>
/// Một lần nhắc nhở tiến độ/ deadline cho một đơn đặt vẽ (UC46).
///
/// <see cref="RiskScore"/>, <see cref="PredictedAt"/>, <see cref="ModelVersion"/> là
/// yêu cầu schema bổ sung: không có chúng thì endpoint
/// <c>POST /commissions/{id}/deadline-risks/predict</c> không lưu được kết quả dự đoán,
/// và không so sánh được độ chính xác giữa các phiên bản mô hình.
///
/// Bất biến: <see cref="RiskScore"/> nằm trong [0, 100].
/// </summary>
public class DeadlineReminder : BaseEntity
{
    public Guid CommissionId { get; set; }

    /// <summary>Mốc công việc được nhắc tới. Null nếu nhắc ở cấp đơn.</summary>
    public Guid? MilestoneId { get; set; }

    /// <summary>Thời điểm hệ thống sẽ/đã gửi nhắc nhở.</summary>
    public DateTimeOffset RemindAt { get; set; }

    public ReminderChannel Channel { get; set; } = ReminderChannel.InApp;

    /// <summary>Đã gửi thành công. Null = chưa gửi.</summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Điểm rủi ro trễ hạn tại thời điểm tính, thang 0–100.</summary>
    public decimal RiskScore { get; set; }

    /// <summary>Thời điểm chạy dự đoán sinh ra bản ghi này.</summary>
    public DateTimeOffset? PredictedAt { get; set; }

    /// <summary>Phiên bản mô hình/quy tắc tính điểm — phục vụ so sánh chất lượng.</summary>
    public string? ModelVersion { get; set; }

    /// <summary>Lý do gửi lỗi — phục vụ retry.</summary>
    public string? FailureReason { get; set; }

    /// <summary>Nhắc thủ công do người dùng tạo (khác nhắc do hệ thống tính).</summary>
    public bool IsManual { get; set; }
}
