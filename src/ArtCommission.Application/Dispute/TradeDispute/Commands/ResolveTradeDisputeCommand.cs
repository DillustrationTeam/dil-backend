using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Dispute.TradeDispute.DTOs;
using ArtCommission.Application.Dispute.TradeDispute.Validators;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Dispute.TradeDispute.Commands;

public sealed record ResolveTradeDisputeCommand(
    Guid DisputeId,
    Guid AdminId,
    string Resolution,
    string? AdminNote = null
) : IRequest<(bool Success, string Message, string[] Errors)>;

public class ResolveTradeDisputeCommandHandler
    : IRequestHandler<ResolveTradeDisputeCommand, (bool Success, string Message, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly ITradeSlotService _slotService;

    public ResolveTradeDisputeCommandHandler(IApplicationDbContext db, ITradeSlotService slotService)
    {
        _db = db;
        _slotService = slotService;
    }

    public async Task<(bool Success, string Message, string[] Errors)> Handle(
        ResolveTradeDisputeCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new ResolveTradeDisputeCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, "Dữ liệu không hợp lệ.", validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var dispute = await _db.TradeDisputes
            .Include(d => d.ArtTrade)
            .FirstOrDefaultAsync(d => d.Id == request.DisputeId && !d.IsDeleted, cancellationToken);

        if (dispute == null)
        {
            return (false, "Không tìm thấy hồ sơ khiếu nại.", ["Khiếu nại không tồn tại."]);
        }

        if (dispute.Status == "Resolved" || dispute.Status == "Dismissed")
        {
            return (false, "Khiếu nại này đã được xử lý trước đó.", ["Không thể xử lý lại khiếu nại đã đóng."]);
        }

        var trade = dispute.ArtTrade;
        var now = DateTimeOffset.UtcNow;
        dispute.Resolution = request.Resolution;
        dispute.AdminNote = request.AdminNote?.Trim();
        dispute.ResolvedAt = now;

        switch (request.Resolution)
        {
            case TradeDisputeResolutions.FaultInitiator:
                // Lỗi thuộc về Initiator -> Huỷ trade, phạt đóng băng slot Initiator 30 ngày
                trade.Status = TradeStatus.Cancelled;
                trade.CancelledAt = now;
                trade.CancellationReason = $"Huỷ do vi phạm (Phán quyết Admin): {request.AdminNote}";
                trade.UpdatedAt = now;

                await _slotService.FreezeSlotAsync(
                    trade.InitiatorId,
                    trade.Id,
                    days: 30,
                    reason: $"Bị phạt do vi phạm khiếu nại trade #{trade.Id}: {request.AdminNote}",
                    cancellationToken);

                dispute.Status = "Resolved";
                break;

            case TradeDisputeResolutions.FaultReceiver:
                // Lỗi thuộc về Receiver -> Huỷ trade, phạt đóng băng slot Receiver 30 ngày
                trade.Status = TradeStatus.Cancelled;
                trade.CancelledAt = now;
                trade.CancellationReason = $"Huỷ do vi phạm (Phán quyết Admin): {request.AdminNote}";
                trade.UpdatedAt = now;

                await _slotService.FreezeSlotAsync(
                    trade.ReceiverId,
                    trade.Id,
                    days: 30,
                    reason: $"Bị phạt do vi phạm khiếu nại trade #{trade.Id}: {request.AdminNote}",
                    cancellationToken);

                dispute.Status = "Resolved";
                break;

            case TradeDisputeResolutions.MutualCancel:
                // Đồng thuận huỷ hoà -> Huỷ trade, giải phóng slot cho cả 2 bên (không ai bị phạt)
                trade.Status = TradeStatus.Cancelled;
                trade.CancelledAt = now;
                trade.CancellationReason = $"Huỷ hoà theo phán quyết Admin: {request.AdminNote}";
                trade.UpdatedAt = now;

                dispute.Status = "Resolved";
                break;

            case TradeDisputeResolutions.Dismissed:
                // Bác bỏ khiếu nại vô căn cứ -> Khôi phục lại trạng thái BothSubmitted để tiếp tục
                trade.Status = TradeStatus.BothSubmitted;
                trade.ConfirmationDeadlineAt = now.AddHours(72);
                trade.UpdatedAt = now;

                dispute.Status = "Dismissed";
                break;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (true, $"Đã giải quyết khiếu nại thành công theo phán quyết {request.Resolution}.", []);
    }
}

