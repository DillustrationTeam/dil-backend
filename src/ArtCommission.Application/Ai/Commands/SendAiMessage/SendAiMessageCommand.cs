using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Ai;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Ai.Commands.SendAiMessage;

/// <summary>
/// UC44 — POST /api/v1/ai/conversations/{conversationId}/messages
/// Người dùng gửi câu hỏi, AI trả lời dựa trên ngữ cảnh đơn vẽ / tranh.
///
/// THỨ TỰ THỰC HIỆN có chủ đích:
///   1. Lưu tin nhắn của người dùng TRƯỚC khi gọi AI. Nếu gọi AI lỗi, câu hỏi vẫn
///      còn trong lịch sử — người dùng không phải gõ lại và không mất mạch hội thoại.
///   2. Gọi AI với ngữ cảnh nạp mới.
///   3. Lưu câu trả lời khi thành công; khi lỗi thì trả lỗi rõ ràng nhưng KHÔNG
///      xoá câu hỏi đã lưu.
/// </summary>
public record SendAiMessageCommand(
    Guid UserId,
    Guid ConversationId,
    string Content
) : IRequest<(bool Success, SendAiMessageResultDto? Data, string[] Errors)>;

public class SendAiMessageCommandValidator : AbstractValidator<SendAiMessageCommand>
{
    /// <summary>Trần độ dài câu hỏi — chặn chi phí token ngoài kiểm soát.</summary>
    public const int MaxContentLength = 4000;

    public SendAiMessageCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty().WithMessage("Thiếu mã phiên hội thoại.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Vui lòng nhập câu hỏi.")
            .MaximumLength(MaxContentLength)
            .WithMessage($"Câu hỏi tối đa {MaxContentLength} ký tự.");
    }
}

public class SendAiMessageCommandHandler
    : IRequestHandler<SendAiMessageCommand, (bool, SendAiMessageResultDto?, string[])>
{
    /// <summary>Số lượt hội thoại cũ đưa vào prompt — đủ để giữ mạch mà không phình token.</summary>
    private const int MaxHistoryTurns = 10;

    private readonly IApplicationDbContext _db;
    private readonly IAiChatClient _aiChatClient;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly ILogger<SendAiMessageCommandHandler> _logger;

    public SendAiMessageCommandHandler(
        IApplicationDbContext db,
        IAiChatClient aiChatClient,
        IAiContextBuilder contextBuilder,
        ILogger<SendAiMessageCommandHandler> logger)
    {
        _db = db;
        _aiChatClient = aiChatClient;
        _contextBuilder = contextBuilder;
        _logger = logger;
    }

    public async Task<(bool, SendAiMessageResultDto?, string[])> Handle(
        SendAiMessageCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new SendAiMessageCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var conversation = await _db.AiConversations
            .FirstOrDefaultAsync(
                c => c.Id == request.ConversationId && !c.IsDeleted,
                cancellationToken);

        if (conversation is null)
        {
            return (false, null, ["Không tìm thấy phiên hội thoại."]);
        }

        // Chỉ chủ phiên mới đọc/ghi được nội dung — nếu không, người khác đoán được
        // conversationId là đọc hết hội thoại riêng.
        if (conversation.UserId != request.UserId)
        {
            return (false, null, ["Bạn không có quyền truy cập phiên hội thoại này."]);
        }

        if (conversation.IsArchived)
        {
            return (false, null, ["Phiên hội thoại đã được lưu trữ, không gửi thêm được."]);
        }

        var now = DateTimeOffset.UtcNow;

        // Bước 1: lưu câu hỏi trước khi gọi AI.
        var userMessage = new AiMessage
        {
            ConversationId = conversation.Id,
            Role = AiMessageRole.User,
            Content = request.Content.Trim(),
            CreatedAt = now
        };

        _db.AiMessages.Add(userMessage);
        conversation.LastMessageAt = now;
        conversation.MessageCount += 1;
        conversation.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        // Lịch sử gần nhất, KHÔNG gồm câu hỏi vừa lưu.
        var history = await _db.AiMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id
                        && !m.IsDeleted
                        && m.Id != userMessage.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(MaxHistoryTurns)
            .Select(m => new { m.Role, m.Content })
            .ToListAsync(cancellationToken);

        // Đảo lại thành thứ tự thời gian tăng dần — mô hình cần đọc theo trình tự.
        history.Reverse();

        var historyTurns = history
            .Select(m => new AiChatTurn(
                AiAndRevenueEnumNames.ToContractName(m.Role),
                m.Content))
            .ToList();

        // Bước 2: nạp ngữ cảnh MỚI mỗi lượt.
        var systemInstruction = await _contextBuilder.BuildSystemInstructionAsync(
            request.UserId,
            conversation.ContextType,
            conversation.ContextId,
            cancellationToken);

        var completion = await _aiChatClient.CompleteAsync(
            systemInstruction,
            historyTurns,
            userMessage.Content,
            cancellationToken);

        if (!completion.Success || string.IsNullOrWhiteSpace(completion.Content))
        {
            _logger.LogWarning(
                "Chatbot trả lời thất bại cho phiên {ConversationId}: {Error}",
                conversation.Id, completion.Error);

            return (false, null,
            [
                "Trợ lý ảo đang bận hoặc chưa được cấu hình. Câu hỏi của bạn đã được lưu, vui lòng thử lại."
            ]);
        }

        // Bước 3: lưu câu trả lời.
        var assistantMessage = new AiMessage
        {
            ConversationId = conversation.Id,
            Role = AiMessageRole.Assistant,
            Content = completion.Content,
            TokenCount = completion.TokenCount,
            ModelVersion = completion.ModelVersion,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.AiMessages.Add(assistantMessage);
        conversation.LastMessageAt = assistantMessage.CreatedAt;
        conversation.MessageCount += 1;
        conversation.UpdatedAt = assistantMessage.CreatedAt;

        await _db.SaveChangesAsync(cancellationToken);

        var result = new SendAiMessageResultDto(
            ToDto(userMessage),
            ToDto(assistantMessage));

        return (true, result, []);
    }

    private static AiMessageDto ToDto(AiMessage message) => new(
        AiMessageId: message.Id,
        ConversationId: message.ConversationId,
        Role: AiAndRevenueEnumNames.ToContractName(message.Role),
        Content: message.Content,
        TokenCount: message.TokenCount,
        Helpful: message.FeedbackHelpful,
        Note: message.FeedbackNote,
        ModelVersion: message.ModelVersion,
        CreatedAt: message.CreatedAt);
}
