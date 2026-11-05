using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtTradeEntity = ArtCommission.Domain.Entities.Trade.ArtTrade;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Commands;

public sealed record ProposeTradeCommand(
    Guid InitiatorId,
    Guid ReceiverId,
    string Title,
    string? Description,
    string? RequestNote,
    int? DurationDays = 14
) : IRequest<(bool Success, ArtTradeDto? Data, string[] Errors)>;

public class ProposeTradeCommandValidator : AbstractValidator<ProposeTradeCommand>
{
    public ProposeTradeCommandValidator()
    {
        RuleFor(x => x.InitiatorId).NotEmpty().WithMessage("Thiếu thông tin người khởi tạo.");
        RuleFor(x => x.ReceiverId).NotEmpty().WithMessage("Thiếu thông tin người nhận đề nghị.");
        RuleFor(x => x)
            .Must(x => x.InitiatorId != x.ReceiverId)
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

    public ProposeTradeCommandHandler(IApplicationDbContext db, ITradeSlotService slotService)
    {
        _db = db;
        _slotService = slotService;
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

        // Kiểm tra đối phương có tồn tại không
        var receiverExists = await _db.Users
            .AnyAsync(u => u.Id == request.ReceiverId && !u.IsDeleted, cancellationToken);
        if (!receiverExists)
        {
            return (false, null, ["Không tìm thấy người nhận đề nghị trao đổi."]);
        }

        // Kiểm tra xem đã có lời mời pending giữa 2 người chưa (tránh spam)
        var pendingExists = await _db.ArtTrades.AnyAsync(t =>
            !t.IsDeleted &&
            t.InitiatorId == request.InitiatorId &&
            t.ReceiverId == request.ReceiverId &&
            t.Status == TradeStatus.Proposed, cancellationToken);
        if (pendingExists)
        {
            return (false, null, ["Bạn đã gửi một lời mời đang chờ người này phản hồi. Vui lòng chờ."]);
        }

        var durationDays = request.DurationDays ?? 14;
        var trade = new ArtTradeEntity
        {
            InitiatorId = request.InitiatorId,
            ReceiverId = request.ReceiverId,
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

        return (true, TradeMapper.ToDto(trade), []);
    }
}
