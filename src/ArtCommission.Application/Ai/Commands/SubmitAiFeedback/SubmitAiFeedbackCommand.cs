using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Commands.SubmitAiFeedback;

/// <summary>
/// UC44 — POST /api/v1/ai/messages/{aiMessageId}/feedback
/// Người dùng đánh giá câu trả lời hữu ích / không, để cải thiện chất lượng bot.
///
/// Chỉ đánh giá được câu trả lời CỦA CHÍNH MÌNH (trong phiên của mình), và chỉ
/// đánh giá được tin của trợ lý — đánh giá câu hỏi của người dùng là vô nghĩa.
/// </summary>
public record SubmitAiFeedbackCommand(
    Guid UserId,
    Guid AiMessageId,
    bool Helpful,
    string? Note = null
) : IRequest<(bool Success, AiFeedbackDto? Data, string[] Errors)>;

public class SubmitAiFeedbackCommandHandler
    : IRequestHandler<SubmitAiFeedbackCommand, (bool, AiFeedbackDto?, string[])>
{
    /// <summary>Trần độ dài ghi chú đánh giá.</summary>
    private const int MaxNoteLength = 1000;

    private readonly IApplicationDbContext _db;

    public SubmitAiFeedbackCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, AiFeedbackDto?, string[])> Handle(
        SubmitAiFeedbackCommand request,
        CancellationToken cancellationToken)
    {
        if (request.AiMessageId == Guid.Empty)
        {
            return (false, null, ["Thiếu mã tin nhắn."]);
        }

        if (request.Note is { Length: > MaxNoteLength })
        {
            return (false, null, [$"Ghi chú tối đa {MaxNoteLength} ký tự."]);
        }

        var message = await _db.AiMessages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(
                m => m.Id == request.AiMessageId && !m.IsDeleted,
                cancellationToken);

        if (message is null)
        {
            return (false, null, ["Không tìm thấy tin nhắn."]);
        }

        if (message.Conversation is null || message.Conversation.UserId != request.UserId)
        {
            return (false, null, ["Bạn không có quyền đánh giá tin nhắn này."]);
        }

        if (message.Role != Domain.Enums.AiMessageRole.Assistant)
        {
            return (false, null, ["Chỉ đánh giá được câu trả lời của trợ lý ảo."]);
        }

        var now = DateTimeOffset.UtcNow;

        // Cho phép ĐỔI đánh giá: người dùng có thể bấm nhầm rồi sửa lại.
        message.FeedbackHelpful = request.Helpful;
        message.FeedbackNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        message.FeedbackAt = now;
        message.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, new AiFeedbackDto(message.Id, request.Helpful, message.FeedbackNote), []);
    }
}
