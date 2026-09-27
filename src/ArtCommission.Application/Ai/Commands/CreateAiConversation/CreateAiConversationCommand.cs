using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Ai;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Commands.CreateAiConversation;

/// <summary>
/// UC44 — POST /api/v1/ai/conversations
/// Mở một phiên hỏi đáp mới với chatbot, có thể gắn ngữ cảnh đơn hàng/tranh.
/// </summary>
public record CreateAiConversationCommand(
    Guid UserId,
    string? Topic,
    string? ContextType,
    Guid? ContextId
) : IRequest<(bool Success, AiConversationDto? Data, string[] Errors)>;

public class CreateAiConversationCommandValidator : AbstractValidator<CreateAiConversationCommand>
{
    public CreateAiConversationCommandValidator()
    {
        RuleFor(x => x.Topic)
            .MaximumLength(300).WithMessage("Chủ đề tối đa 300 ký tự.");

        RuleFor(x => x.ContextType)
            .Must(BeValidContextType)
            .When(x => !string.IsNullOrWhiteSpace(x.ContextType))
            .WithMessage("Loại ngữ cảnh không hợp lệ (General / Commission / Artwork).");

        // Ngữ cảnh cụ thể bắt buộc phải kèm contextId, nếu không phiên sẽ trỏ vào hư không.
        RuleFor(x => x.ContextId)
            .NotNull()
            .When(x => !string.IsNullOrWhiteSpace(x.ContextType)
                       && !string.Equals(x.ContextType, nameof(AiContextType.General), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Cần truyền mã đối tượng ngữ cảnh.");
    }

    private static bool BeValidContextType(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || Enum.TryParse<AiContextType>(value, ignoreCase: true, out _);
}

public class CreateAiConversationCommandHandler
    : IRequestHandler<CreateAiConversationCommand, (bool, AiConversationDto?, string[])>
{
    private readonly IApplicationDbContext _db;

    public CreateAiConversationCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, AiConversationDto?, string[])> Handle(
        CreateAiConversationCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new CreateAiConversationCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        if (request.UserId == Guid.Empty)
        {
            return (false, null, ["Bạn cần đăng nhập để dùng trợ lý ảo."]);
        }

        var contextType = AiContextType.General;
        if (!string.IsNullOrWhiteSpace(request.ContextType)
            && Enum.TryParse<AiContextType>(request.ContextType, ignoreCase: true, out var parsed))
        {
            contextType = parsed;
        }

        // Ngữ cảnh Commission: chỉ cho tạo khi user thực sự là người trong đơn.
        if (contextType == AiContextType.Commission && request.ContextId.HasValue)
        {
            var isParticipant = await _db.Commissions
                .AsNoTracking()
                .AnyAsync(
                    c => c.Id == request.ContextId.Value
                         && !c.IsDeleted
                         && (c.ClientId == request.UserId || c.CreatorId == request.UserId),
                    cancellationToken);

            if (!isParticipant)
            {
                return (false, null, ["Bạn không phải thành viên của đơn đặt vẽ này."]);
            }
        }

        var conversation = new AiConversation
        {
            UserId = request.UserId,
            Topic = string.IsNullOrWhiteSpace(request.Topic)
                ? "Cuộc trò chuyện mới"
                : request.Topic.Trim(),
            ContextType = contextType,
            ContextId = contextType == AiContextType.General ? null : request.ContextId,
            LastMessageAt = null,
            MessageCount = 0
        };

        _db.AiConversations.Add(conversation);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = new AiConversationDto(
            conversation.Id,
            conversation.Topic,
            conversation.ContextType.ToString(),
            conversation.ContextId,
            conversation.CreatedAt,
            conversation.LastMessageAt,
            conversation.MessageCount);

        return (true, dto, []);
    }
}
