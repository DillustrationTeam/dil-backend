using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Chat.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Chat;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Chat.Commands.SendMessage;

/// <summary>
/// UC43 — gửi tin nhắn vào phòng Workroom.
///
/// Được gọi từ HAI nơi: SignalR <c>ChatHub.SendMessage</c> và REST (dự phòng khi
/// WebSocket không kết nối được). Cùng một handler ⇒ cùng một luật nghiệp vụ,
/// không có chuyện gửi qua hub thì bỏ qua kiểm tra quyền.
/// </summary>
public record SendMessageCommand(
    Guid UserId,
    Guid RoomId,
    string Body,
    string? MessageType = null
) : IRequest<(bool Success, ChatMessageDto? Data, string[] Errors)>;

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    /// <summary>Trần độ dài một tin nhắn.</summary>
    public const int MaxBodyLength = 4000;

    public SendMessageCommandValidator()
    {
        RuleFor(x => x.RoomId).NotEmpty().WithMessage("Thiếu mã phòng chat.");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Nội dung tin nhắn không được để trống.")
            .MaximumLength(MaxBodyLength)
            .WithMessage($"Tin nhắn tối đa {MaxBodyLength} ký tự.");

        RuleFor(x => x.MessageType)
            .Must(MessageTypes.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.MessageType))
            .WithMessage("Loại tin nhắn không hợp lệ.");
    }
}

public class SendMessageCommandHandler
    : IRequestHandler<SendMessageCommand, (bool, ChatMessageDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IChatRoomService _chatRoomService;

    public SendMessageCommandHandler(
        IApplicationDbContext db,
        IChatRoomService chatRoomService)
    {
        _db = db;
        _chatRoomService = chatRoomService;
    }

    public async Task<(bool, ChatMessageDto?, string[])> Handle(
        SendMessageCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new SendMessageCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var (room, error) = await _chatRoomService.ResolveRoomAsync(
            request.RoomId, request.UserId, cancellationToken);

        if (room is null)
        {
            return (false, null, [error ?? "Không truy cập được phòng chat."]);
        }

        if (room.IsLocked)
        {
            return (false, null, ["Phòng chat đã bị khoá nên không gửi được tin nhắn mới."]);
        }

        var now = DateTimeOffset.UtcNow;

        var message = new Message
        {
            RoomId = room.Id,
            CommissionId = room.CommissionId,
            SenderId = request.UserId,
            MessageType = MessageTypes.Normalize(request.MessageType),
            Body = request.Body.Trim(),
            SentAt = now
        };

        _db.Messages.Add(message);

        room.LastMessageAt = now;
        room.UpdatedAt = now;

        // Người gửi coi như đã đọc tới thời điểm này — nếu không badge "chưa đọc"
        // sẽ hiện lại tin của chính mình.
        var senderMembership = await _db.ChatRoomMembers
            .FirstOrDefaultAsync(
                m => m.RoomId == room.Id && m.UserId == request.UserId,
                cancellationToken);

        if (senderMembership is not null)
        {
            senderMembership.LastReadAt = now;
            senderMembership.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var senderName = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == request.UserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var dto = new ChatMessageDto(
            MessageId: message.Id,
            RoomId: message.RoomId,
            SenderId: message.SenderId,
            SenderFullName: senderName,
            Body: message.Body,
            TranslatedBody: message.TranslatedBody,
            SourceLang: message.SourceLang,
            TargetLang: message.TargetLang,
            TranslationStatus: message.TranslationStatus.ToString(),
            MessageType: message.MessageType,
            SentAt: message.SentAt,
            IsRead: message.IsRead,
            Attachments: []);

        return (true, dto, []);
    }
}
