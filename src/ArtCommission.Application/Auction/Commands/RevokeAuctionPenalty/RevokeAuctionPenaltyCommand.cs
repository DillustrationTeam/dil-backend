using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Auction.Commands.RevokeAuctionPenalty;

/// <summary>
/// Ban quản trị THU HỒI một án phạt giao hàng đã áp sai.
///
/// LUẬT:
///   - Chỉ Admin gọi được (controller chặn theo role), và phải nêu lý do.
///   - Án đã thu hồi thì trả lại nguyên trạng (idempotent) — bấm 2 lần không ghi đè lý do cũ.
///   - Thu hồi KHÔNG xoá dòng: giữ nguyên vết (ai thu hồi, khi nào, vì sao) để đối soát.
///   - Hiệu lực tức thì: phần phụ phí và thời gian nghỉ bán do án này tạo ra không còn
///     được tính khi chốt phiên (surcharge lấy MAX trên các án Active; ban lấy theo BanUntil).
/// </summary>
public sealed record RevokeAuctionPenaltyCommand(
    Guid PenaltyId,
    Guid AdminUserId,
    string Reason
) : IRequest<(bool Success, AuctionPenaltyRevokeResultDto? Data, string[] Errors)>;

public sealed class RevokeAuctionPenaltyCommandHandler
    : IRequestHandler<RevokeAuctionPenaltyCommand, (bool, AuctionPenaltyRevokeResultDto?, string[])>
{
    private const int MaxReasonLength = 500;
    // Lý do 1–2 chữ ("sai", "ok") không đủ để đối soát về sau: mở lại hồ sơ phải hiểu được
    // vì sao án phạt bị thu hồi, nên siết mức tối thiểu ngay ở tầng nghiệp vụ.
    private const int MinReasonLength = 10;

    private readonly IApplicationDbContext _db;
    private readonly ILogger<RevokeAuctionPenaltyCommandHandler> _logger;

    public RevokeAuctionPenaltyCommandHandler(
        IApplicationDbContext db,
        ILogger<RevokeAuctionPenaltyCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(bool, AuctionPenaltyRevokeResultDto?, string[])> Handle(
        RevokeAuctionPenaltyCommand request,
        CancellationToken cancellationToken)
    {
        if (request.PenaltyId == Guid.Empty)
        {
            return (false, null, ["Thiếu mã án phạt."]);
        }

        if (request.AdminUserId == Guid.Empty)
        {
            return (false, null, ["Thiếu thông tin quản trị viên."]);
        }

        var reason = (request.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
        {
            return (false, null, ["Vui lòng nhập lý do thu hồi để lưu vào nhật ký."]);
        }

        if (reason.Length > MaxReasonLength)
        {
            return (false, null, [$"Lý do thu hồi tối đa {MaxReasonLength} ký tự."]);
        }

        if (reason.Length < MinReasonLength)
        {
            return (false, null,
                [$"Lý do thu hồi phải có ít nhất {MinReasonLength} ký tự để lưu vết kiểm toán."]);
        }

        var penalty = await _db.AuctionPenalties
            .FirstOrDefaultAsync(p => p.Id == request.PenaltyId && !p.IsDeleted, cancellationToken);

        if (penalty is null)
        {
            return (false, null, ["Không tìm thấy án phạt."]);
        }

        // Idempotent: đã thu hồi rồi thì trả nguyên trạng, không ghi đè lý do/thời điểm cũ.
        if (penalty.Status == PenaltyStatus.Revoked)
        {
            return (true, new AuctionPenaltyRevokeResultDto(
                penalty.Id, penalty.SellerId, penalty.StrikeLevel,
                penalty.Status.ToString(),
                penalty.RevokedAt ?? DateTimeOffset.UtcNow,
                penalty.RevokeReason), []);
        }

        var now = DateTimeOffset.UtcNow;
        penalty.Status = PenaltyStatus.Revoked;
        penalty.RevokedAt = now;
        penalty.RevokedByAdminId = request.AdminUserId;
        penalty.RevokeReason = reason;
        penalty.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Admin {AdminId} thu hồi án phạt {PenaltyId} (seller={SellerId}, mức {Level}). Lý do: {Reason}",
            request.AdminUserId, penalty.Id, penalty.SellerId, penalty.StrikeLevel, reason);

        return (true, new AuctionPenaltyRevokeResultDto(
            penalty.Id, penalty.SellerId, penalty.StrikeLevel,
            penalty.Status.ToString(), now, reason), []);
    }
}
