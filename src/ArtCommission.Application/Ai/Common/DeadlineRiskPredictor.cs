using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Common;

/// <summary>Kết quả tính điểm rủi ro trễ hạn của một đơn đặt vẽ.</summary>
public sealed record DeadlineRiskAssessment(
    Guid CommissionId,
    decimal RiskScore,
    DeadlineRiskLevel Level,
    DateTimeOffset? Deadline,
    int CurrentStage,
    string ModelVersion,
    /// <summary>Thời điểm nên gửi nhắc nhở kế tiếp.</summary>
    DateTimeOffset? NextRemindAt);

/// <summary>
/// Tính điểm rủi ro trễ hạn cho đơn đặt vẽ (UC46).
///
/// VÌ SAO dùng công thức tường minh thay vì gọi mô hình:
///   - Kết quả phải GIẢI THÍCH ĐƯỢC. Người dùng hỏi "vì sao đơn tôi bị đánh dấu rủi ro cao"
///     thì cần trả lời bằng các yếu tố cụ thể (còn 2 ngày, 3 mốc chưa xong, 4 lần sửa).
///   - Hệ thống chưa có dữ liệu huấn luyện; một mô hình chưa kiểm chứng sẽ tệ hơn
///     một công thức đọc được.
///   - <c>ModelVersion</c> được lưu cùng bản ghi nên khi đổi công thức vẫn so sánh được
///     kết quả cũ/mới — đó là điều kiện để sau này thay bằng mô hình thật.
/// </summary>
public interface IDeadlineRiskPredictor
{
    /// <summary>Phiên bản công thức hiện hành. Đổi công thức thì tăng số này.</summary>
    string ModelVersion { get; }

    Task<DeadlineRiskAssessment?> AssessAsync(
        Guid commissionId,
        CancellationToken cancellationToken = default);
}

public class DeadlineRiskPredictor : IDeadlineRiskPredictor
{
    /// <summary>Phiên bản công thức — ghi vào DeadlineReminder.ModelVersion.</summary>
    public const string CurrentModelVersion = "rules-v1";

    public string ModelVersion => CurrentModelVersion;

    private readonly IApplicationDbContext _db;

    public DeadlineRiskPredictor(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DeadlineRiskAssessment?> AssessAsync(
        Guid commissionId,
        CancellationToken cancellationToken = default)
    {
        var commission = await _db.Commissions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == commissionId && !c.IsDeleted, cancellationToken);

        if (commission is null)
        {
            return null;
        }

        var milestones = await _db.Milestones
            .AsNoTracking()
            .Where(m => m.CommissionId == commissionId && !m.IsDeleted)
            .Select(m => new { m.Status, m.RevisionCount })
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        var totalMilestones = milestones.Count;
        var approvedMilestones = milestones.Count(m => m.Status == MilestoneStatus.Approved);
        var unfinishedMilestones = totalMilestones - approvedMilestones;
        var totalRevisions = milestones.Sum(m => m.RevisionCount);

        var score = ComputeScore(
            commission.DeadlineAt,
            now,
            totalMilestones,
            unfinishedMilestones,
            totalRevisions);

        var level = AiAndRevenueEnumNames.RiskLevelFromScore(score);

        // Thời điểm nhắc kế tiếp: trước hạn 24h; nếu đã quá hạn thì nhắc ngay.
        DateTimeOffset? nextRemindAt = null;
        if (commission.DeadlineAt.HasValue)
        {
            var candidate = commission.DeadlineAt.Value.AddHours(-24);
            nextRemindAt = candidate <= now ? now : candidate;
        }

        return new DeadlineRiskAssessment(
            commission.Id,
            score,
            level,
            commission.DeadlineAt,
            commission.CurrentStage,
            CurrentModelVersion,
            nextRemindAt);
    }

    /// <summary>
    /// Công thức tính điểm, tách riêng để đọc và kiểm thử độc lập.
    ///
    /// Thang 0–100, cộng dồn từ bốn nhóm yếu tố rồi cắt trần:
    ///   1. Thời gian còn lại tới hạn  — yếu tố nặng nhất (0–65 điểm).
    ///   2. Số mốc chưa xong           — tối đa 20 điểm.
    ///   3. Số lần yêu cầu sửa         — tối đa 20 điểm (dấu hiệu scope phình ra).
    ///   4. Tiến độ tụt hậu so với thời gian — 10 điểm.
    /// </summary>
    internal static decimal ComputeScore(
        DateTimeOffset? deadline,
        DateTimeOffset now,
        int totalMilestones,
        int unfinishedMilestones,
        int totalRevisions)
    {
        // Không có hạn thì không thể trễ — trả mức nền thấp thay vì báo động giả.
        if (!deadline.HasValue)
        {
            return 10m;
        }

        var daysLeft = (deadline.Value - now).TotalDays;

        decimal score = daysLeft switch
        {
            <= 0d => 65m,
            <= 1d => 55m,
            <= 3d => 42m,
            <= 7d => 28m,
            <= 14d => 18m,
            _ => 8m
        };

        score += Math.Min(unfinishedMilestones * 4m, 20m);
        score += Math.Min(totalRevisions * 3m, 20m);

        // Tiến độ tụt hậu: còn nhiều mốc chưa xong mà thời gian đã gần hết.
        if (totalMilestones > 0 && daysLeft <= 7d)
        {
            var completedRatio = (decimal)(totalMilestones - unfinishedMilestones) / totalMilestones;
            if (completedRatio < 0.5m)
            {
                score += 10m;
            }
        }

        return Math.Clamp(Math.Round(score, 2, MidpointRounding.AwayFromZero), 0m, 100m);
    }
}
