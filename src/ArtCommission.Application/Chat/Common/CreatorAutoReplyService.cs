using System.Globalization;
using System.Text;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Chat;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Chat.Common;

/// <summary>
/// Tự động phản hồi (Auto-reply & FAQ) cho phòng chat Commission khi Client nhắn tin,
/// dựa trên cấu hình <see cref="CreatorAutoReplySetting"/> và <see cref="CreatorFaq"/>.
/// </summary>
public interface ICreatorAutoReplyService
{
    Task<ChatMessageDto?> TryCreateAutoReplyAsync(
        Guid roomId,
        Guid senderUserId,
        string messageBody,
        string messageType,
        CancellationToken cancellationToken = default);
}

public class CreatorAutoReplyService : ICreatorAutoReplyService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "la", "gi", "co", "khong", "ko", "k", "ban", "minh", "toi", "cho", "hoi",
        "ve", "nha", "nhe", "a", "ah", "oi", "the", "nao", "sao", "vay", "duoc",
        "dc", "is", "are", "do", "you", "your", "the", "a", "an", "in", "on", "for", "how", "what"
    };

    private readonly IApplicationDbContext _db;

    public CreatorAutoReplyService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ChatMessageDto?> TryCreateAutoReplyAsync(
        Guid roomId,
        Guid senderUserId,
        string messageBody,
        string messageType,
        CancellationToken cancellationToken = default)
    {
        if (MessageTypes.Normalize(messageType) != MessageTypes.Text || string.IsNullOrWhiteSpace(messageBody))
        {
            return null;
        }

        var room = await _db.ChatRooms
            .FirstOrDefaultAsync(r => r.Id == roomId && !r.IsDeleted, cancellationToken);

        if (room is null || room.IsLocked || room.RoomType != ChatRoomType.Commission || !room.CommissionId.HasValue)
        {
            return null;
        }

        var commission = await _db.Commissions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == room.CommissionId.Value && !c.IsDeleted, cancellationToken);

        // Chỉ kích hoạt khi Client nhắn tin, tránh vòng lặp vô hạn khi Creator hoặc Auto-reply gửi tin.
        if (commission is null || commission.ClientId != senderUserId)
        {
            return null;
        }

        var creatorProfile = await _db.CreatorProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => (p.Id == commission.CreatorId || p.UserId == commission.CreatorId) && !p.IsDeleted,
                cancellationToken);

        if (creatorProfile is null)
        {
            return null;
        }

        var setting = await _db.CreatorAutoReplySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CreatorProfileId == creatorProfile.Id && !x.IsDeleted, cancellationToken);

        var faqs = await _db.CreatorFaqs
            .AsNoTracking()
            .Where(x => x.CreatorProfileId == creatorProfile.Id && !x.IsDeleted)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);

        string? replyBody = null;
        var matchedFaq = FindMatchingFaq(messageBody, faqs);
        if (matchedFaq is not null)
        {
            replyBody = $"[Auto-reply] {matchedFaq.Answer.Trim()}";
        }
        else if (setting is not null && setting.IsEnabled && !string.IsNullOrWhiteSpace(setting.BriefTemplate))
        {
            var hasCreatorMessage = await _db.Messages
                .AsNoTracking()
                .AnyAsync(
                    m => m.RoomId == room.Id && m.SenderId == creatorProfile.UserId && !m.IsDeleted,
                    cancellationToken);

            if (!hasCreatorMessage)
            {
                replyBody = $"[Auto-reply] {setting.BriefTemplate.Trim()}";
            }
        }

        if (string.IsNullOrWhiteSpace(replyBody))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var replyMessage = new Message
        {
            RoomId = room.Id,
            CommissionId = room.CommissionId,
            SenderId = creatorProfile.UserId,
            MessageType = MessageTypes.Text,
            Body = replyBody,
            SentAt = now
        };

        _db.Messages.Add(replyMessage);
        room.LastMessageAt = now;
        room.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        var creatorName = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == creatorProfile.UserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        return new ChatMessageDto(
            MessageId: replyMessage.Id,
            RoomId: replyMessage.RoomId,
            SenderId: replyMessage.SenderId,
            SenderFullName: creatorName,
            Body: replyMessage.Body,
            TranslatedBody: replyMessage.TranslatedBody,
            SourceLang: replyMessage.SourceLang,
            TargetLang: replyMessage.TargetLang,
            TranslationStatus: replyMessage.TranslationStatus.ToString(),
            MessageType: replyMessage.MessageType,
            SentAt: replyMessage.SentAt,
            IsRead: replyMessage.IsRead,
            Attachments: []);
    }

    private static CreatorFaq? FindMatchingFaq(string messageBody, IReadOnlyList<CreatorFaq> faqs)
    {
        if (faqs.Count == 0)
        {
            return null;
        }

        var normalizedMsg = NormalizeText(messageBody);
        if (string.IsNullOrWhiteSpace(normalizedMsg))
        {
            return null;
        }

        var msgTokens = Tokenize(normalizedMsg);
        CreatorFaq? bestFaq = null;
        double bestScore = 0;

        foreach (var faq in faqs)
        {
            var normalizedQuestion = NormalizeText(faq.Question);
            if (string.IsNullOrWhiteSpace(normalizedQuestion))
            {
                continue;
            }

            if (string.Equals(normalizedMsg, normalizedQuestion, StringComparison.Ordinal)
                || (normalizedQuestion.Length >= 3 && normalizedMsg.Contains(normalizedQuestion, StringComparison.Ordinal))
                || (normalizedMsg.Length >= 6 && normalizedQuestion.Contains(normalizedMsg, StringComparison.Ordinal)))
            {
                return faq;
            }

            var faqTokens = Tokenize(normalizedQuestion);
            if (faqTokens.Count == 0 || msgTokens.Count == 0)
            {
                continue;
            }

            if (faqTokens.Count == 1)
            {
                var singleToken = faqTokens.First();
                if (msgTokens.Contains(singleToken))
                {
                    return faq;
                }
                continue;
            }

            var overlap = faqTokens.Count(t => msgTokens.Contains(t));
            var score = (double)overlap / faqTokens.Count;
            if (score >= 0.6 && score > bestScore)
            {
                bestScore = score;
                bestFaq = faq;
            }
        }

        return bestFaq;
    }

    private static string NormalizeText(string input)
    {
        var formD = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        var prevSpace = false;

        foreach (var ch in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var mapped = ch == 'đ' ? 'd' : ch;
            if (char.IsLetterOrDigit(mapped))
            {
                sb.Append(mapped);
                prevSpace = false;
            }
            else if (!prevSpace && sb.Length > 0)
            {
                sb.Append(' ');
                prevSpace = true;
            }
        }

        return sb.ToString().Trim();
    }

    private static HashSet<string> Tokenize(string normalized)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length >= 2 && !StopWords.Contains(part))
            {
                tokens.Add(part);
            }
        }

        return tokens;
    }
}
