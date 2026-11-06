using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Common.Interfaces;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtTradeEntity = ArtCommission.Domain.Entities.Trade.ArtTrade;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Commands;

public sealed record ProposeTradeCommand(
    Guid InitiatorId,
    Guid? ReceiverId,
    string Title,
    string? Description = null,
    string? RequestNote = null,
    int? DurationDays = 14,
    string? ReceiverDisplayName = null
) : IRequest<(bool Success, ArtTradeDto? Data, string[] Errors)>;

public class ProposeTradeCommandValidator : AbstractValidator<ProposeTradeCommand>
{
    public ProposeTradeCommandValidator()
    {
        RuleFor(x => x.InitiatorId).NotEmpty().WithMessage("Thiếu thông tin người khởi tạo.");
        RuleFor(x => x)
            .Must(x => (x.ReceiverId.HasValue && x.ReceiverId.Value != Guid.Empty) || !string.IsNullOrWhiteSpace(x.ReceiverDisplayName))
            .WithMessage("Thiếu thông tin người nhận đề nghị (cần ID hoặc Tên hiển thị của nghệ sĩ).");
        RuleFor(x => x)
            .Must(x => !x.ReceiverId.HasValue || x.InitiatorId != x.ReceiverId.Value)
            .WithMessage("Không thể tự trao đổi tranh với chính mình.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề cuộc trao đổi không được để trống.")
            .MaximumLength(200).WithMessage("Tiêu đề không được vượt quá 200 ký tự.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Mô tả không được vượt quá 2000 ký tự.");

        RuleFor(x => x.RequestNote)
            .MaximumLength(2000).WithMessage("Yêu cầu vẽ không được vượt quá 2000 ký tự.");

        RuleFor(x => x.DurationDays)
            .InclusiveBetween(1, 60).When(x => x.DurationDays.HasValue)
            .WithMessage("Thời hạn hoàn thành tranh phải từ 1 đến 60 ngày.");
    }
}

public class ProposeTradeCommandHandler : IRequestHandler<ProposeTradeCommand, (bool Success, ArtTradeDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly ITradeSlotService _slotService;
    private readonly INotificationPublisher _notifPublisher;

    public ProposeTradeCommandHandler(
        IApplicationDbContext db,
        ITradeSlotService slotService,
        INotificationPublisher notifPublisher)
    {
        _db = db;
        _slotService = slotService;
        _notifPublisher = notifPublisher;
    }

    public async Task<(bool Success, ArtTradeDto? Data, string[] Errors)> Handle(ProposeTradeCommand request, CancellationToken cancellationToken)
    {
        var validator = new ProposeTradeCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // Kiểm tra slot của người khởi tạo
        var (canTrade, _, reason) = await _slotService.CheckTradeSlotAvailableAsync(request.InitiatorId, cancellationToken);
        if (!canTrade)
        {
            return (false, null, [reason ?? "Bạn không đủ Slot để tạo lời mời trao đổi mới."]);
        }

        // Kiểm tra người khởi tạo có phải là Creator không (chỉ Creator mới có quyền trao đổi tranh)
        var initiatorIsCreator = await _db.CreatorProfiles
            .AnyAsync(cp => cp.UserId == request.InitiatorId && !cp.IsDeleted, cancellationToken);
        if (!initiatorIsCreator)
        {
            return (false, null, ["Tính năng Trao đổi tranh (Art Trade) chỉ dành riêng cho Creator (họa sĩ)."]);
        }

        // Phân giải đối phương qua ReceiverId hoặc ReceiverDisplayName
        Guid resolvedReceiverId = Guid.Empty;
        CreatorProfile? receiverProfile = null;

        if (request.ReceiverId.HasValue && request.ReceiverId.Value != Guid.Empty)
        {
            receiverProfile = await _db.CreatorProfiles
                .Include(cp => cp.User)
                .FirstOrDefaultAsync(cp => (cp.UserId == request.ReceiverId.Value || cp.Id == request.ReceiverId.Value) && !cp.IsDeleted, cancellationToken);

            if (receiverProfile != null)
            {
                resolvedReceiverId = receiverProfile.UserId;
            }
            else
            {
                var userExists = await _db.Users.AnyAsync(u => u.Id == request.ReceiverId.Value && !u.IsDeleted, cancellationToken);
                if (userExists)
                {
                    resolvedReceiverId = request.ReceiverId.Value;
                }
            }
        }

        if (resolvedReceiverId == Guid.Empty && !string.IsNullOrWhiteSpace(request.ReceiverDisplayName))
        {
            var trimmedName = request.ReceiverDisplayName.Trim();
            receiverProfile = await _db.CreatorProfiles
                .Include(cp => cp.User)
                .FirstOrDefaultAsync(cp => !cp.IsDeleted && (
                    cp.DisplayName.ToLower() == trimmedName.ToLower() ||
                    (cp.User != null && cp.User.UserName != null && cp.User.UserName.ToLower() == trimmedName.ToLower())
                ), cancellationToken);

            if (receiverProfile != null)
            {
                resolvedReceiverId = receiverProfile.UserId;
            }
        }

        if (resolvedReceiverId == Guid.Empty)
        {
            return (false, null, ["Không tìm thấy người nhận đề nghị trao đổi."]);
        }

        if (resolvedReceiverId == request.InitiatorId)
        {
            return (false, null, ["Không thể tự trao đổi tranh với chính mình."]);
        }

        // Kiểm tra đối phương có phải là Creator không
        var receiverIsCreator = receiverProfile != null || await _db.CreatorProfiles
            .AnyAsync(cp => cp.UserId == resolvedReceiverId && !cp.IsDeleted, cancellationToken);
        if (!receiverIsCreator)
        {
            return (false, null, ["Đối phương phải là một Creator (họa sĩ) để có thể trao đổi tranh."]);
        }

        // Kiểm tra xem đã có lời mời pending giữa 2 người chưa (tránh spam)
        var pendingExists = await _db.ArtTrades.AnyAsync(t =>
            !t.IsDeleted &&
            t.InitiatorId == request.InitiatorId &&
            t.ReceiverId == resolvedReceiverId &&
            t.Status == TradeStatus.Proposed, cancellationToken);
        if (pendingExists)
        {
            return (false, null, ["Bạn đã gửi một lời mời đang chờ người này phản hồi. Vui lòng chờ."]);
        }

        var durationDays = request.DurationDays ?? 14;
        var trade = new ArtTradeEntity
        {
            InitiatorId = request.InitiatorId,
            ReceiverId = resolvedReceiverId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            InitiatorRequestNote = request.RequestNote?.Trim(),
            Status = TradeStatus.Proposed,
            SubmissionDeadlineAt = DateTimeOffset.UtcNow.AddDays(durationDays)
        };

        _db.ArtTrades.Add(trade);
        await _db.SaveChangesAsync(cancellationToken);

        // Load navigation properties for DTO mapping
        await _db.Entry(trade).Reference(t => t.Initiator).LoadAsync(cancellationToken);
        await _db.Entry(trade).Reference(t => t.Receiver).LoadAsync(cancellationToken);

        try
        {
            var senderName = trade.Initiator?.FullName ?? "Một họa sĩ";
            await _notifPublisher.PublishAsync(
                userId: trade.ReceiverId,
                notificationType: NotificationType.ArtTradeProposed,
                title: "Lời mời trao đổi tác phẩm mới",
                body: $"Họa sĩ {senderName} đã gửi cho bạn đề nghị trao đổi tranh: \"{trade.Title}\".",
                refType: "ArtTrade",
                refId: trade.Id,
                dedupKey: $"ArtTradeProposed:{trade.Id}:{trade.ReceiverId}",
                cancellationToken: cancellationToken
            );
        }
        catch
        {
            // Bỏ qua lỗi gửi thông báo để không ảnh hưởng giao dịch
        }

        return (true, ArtTradeDto.FromEntity(trade), []);
    }
}
