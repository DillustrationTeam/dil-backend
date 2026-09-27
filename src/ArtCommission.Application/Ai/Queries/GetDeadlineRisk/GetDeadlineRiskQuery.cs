using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Ai;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Queries.GetDeadlineRisk;

/// <summary>
/// UC46 — GET /api/v1/commissions/{commissionId}/deadline-risks
/// Điểm rủi ro trễ hạn của đơn đặt vẽ kèm danh sách nhắc nhở đã/đang lên lịch.
///
/// Quyền: chỉ client hoặc creator của đơn xem được.
/// </summary>
public record GetDeadlineRiskQuery(
    Guid UserId,
    Guid CommissionId
) : IRequest<(bool Success, DeadlineRiskDto? Data, string[] Errors)>;

public class GetDeadlineRiskQueryHandler
    : IRequestHandler<GetDeadlineRiskQuery, (bool, DeadlineRiskDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IDeadlineRiskPredictor _predictor;

    public GetDeadlineRiskQueryHandler(
        IApplicationDbContext db,
        IDeadlineRiskPredictor predictor)
    {
        _db = db;
        _predictor = predictor;
    }

    public async Task<(bool, DeadlineRiskDto?, string[])> Handle(
        GetDeadlineRiskQuery request,
        CancellationToken cancellationToken)
    {
        if (request.CommissionId == Guid.Empty)
        {
            return (false, null, ["Thiếu mã đơn đặt vẽ."]);
        }

        var commission = await _db.Commissions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == request.CommissionId && !c.IsDeleted,
                cancellationToken);

        if (commission is null)
        {
            return (false, null, ["Không tìm thấy đơn đặt vẽ."]);
        }

        var creatorProfileId = await _db.CreatorProfiles
            .AsNoTracking()
            .Where(p => p.UserId == request.UserId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (commission.ClientId != request.UserId && commission.CreatorId != request.UserId && (creatorProfileId == Guid.Empty || commission.CreatorId != creatorProfileId))
        {
            return (false, null, ["Bạn không có quyền xem tiến độ của đơn này."]);
        }

        var assessment = await _predictor.AssessAsync(request.CommissionId, cancellationToken);

        if (assessment is null)
        {
            return (false, null, ["Không tính được rủi ro cho đơn này."]);
        }

        // Chỉ lấy reminders ở dạng DTO phẳng, nhưng phải kèm PredictedAt — trước đây
        // endpoint này hardcode `PredictedAt: null` trong khi bảng có cột và lệnh predict
        // ghi giá trị thật. Trả null làm mất thông tin "dự đoán này tính lúc nào",
        // và FE không phân biệt được bản ghi cũ với bản ghi vừa tính lại.
        var reminders = await _db.DeadlineReminders
            .AsNoTracking()
            .Where(r => r.CommissionId == request.CommissionId && !r.IsDeleted)
            .OrderByDescending(r => r.RemindAt)
            .Select(r => new DeadlineReminderDto(
                r.Id,
                r.CommissionId,
                r.MilestoneId,
                r.RemindAt,
                r.Channel.ToString(),
                r.SentAt,
                r.RiskScore,
                r.IsManual))
            .ToListAsync(cancellationToken);

        // PredictedAt của bản dự đoán mới nhất — điểm rủi ro trả ở trên được tính
        // từ trạng thái hiện tại, nên mốc thời gian đi kèm phải là lần dự đoán gần nhất.
        var lastPredictedAt = await _db.DeadlineReminders
            .AsNoTracking()
            .Where(r => r.CommissionId == request.CommissionId
                        && !r.IsDeleted
                        && r.PredictedAt != null)
            .OrderByDescending(r => r.PredictedAt)
            .Select(r => r.PredictedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var dto = new DeadlineRiskDto(
            CommissionId: request.CommissionId,
            RiskScore: assessment.RiskScore,
            RiskLevel: AiAndRevenueEnumNames.ToContractName(assessment.Level),
            Deadline: assessment.Deadline,
            CurrentStage: assessment.CurrentStage,
            PredictedAt: lastPredictedAt,
            NextRemindAt: assessment.NextRemindAt,
            ModelVersion: assessment.ModelVersion,
            Reminders: reminders);

        return (true, dto, []);
    }
}
