using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.Validators;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Commands;

public record WithdrawSubmissionCommand(
    Guid SubmissionId,
    Guid UserId
) : IRequest<(bool Success, string[] Errors)>;

public class WithdrawSubmissionCommandHandler
    : IRequestHandler<WithdrawSubmissionCommand, (bool Success, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public WithdrawSubmissionCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string[] Errors)> Handle(
        WithdrawSubmissionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra tính hợp lệ của tham số
        var validator = new WithdrawSubmissionCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // 2. Tìm bài dự thi
        var submission = await _db.EventSubmissions
            .Include(s => s.Event)
            .Include(s => s.Artwork)
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return (false, ["Không tìm thấy bài dự thi."]);
        }

        // 3. Quy tắc bắt buộc: Chỉ chính chủ Creator nộp bài mới có quyền rút bài thi
        if (submission.SubmitterId != request.UserId)
        {
            return (false, ["Bạn chỉ có quyền rút bài dự thi do chính mình nộp."]);
        }

        // 4. Kiểm tra sự kiện và thời hạn rút bài
        var platformEvent = submission.Event ?? await _db.PlatformEvents
            .FirstOrDefaultAsync(e => e.Id == submission.EventId && !e.IsDeleted, cancellationToken);

        if (platformEvent == null || platformEvent.IsDeleted)
        {
            return (false, ["Sự kiện liên quan không tồn tại hoặc đã bị xóa."]);
        }

        var now = DateTimeOffset.UtcNow;
        if (now > platformEvent.SubmissionEndAt)
        {
            return (false, [$"Thời hạn nộp bài đã kết thúc (Kết thúc lúc: {platformEvent.SubmissionEndAt:u}). Bạn không thể rút bài dự thi sau khi cuộc thi đã chuyển sang giai đoạn chấm thi hoặc bình chọn."]);
        }

        // 5. Dọn dẹp tranh nếu tranh này được upload riêng cho bài thi này (không dùng ở bài khác, không có tương tác nào)
        var artwork = submission.Artwork ?? await _db.Artworks
            .FirstOrDefaultAsync(a => a.Id == submission.ArtworkId, cancellationToken);

        if (artwork != null)
        {
            var isUsedInOtherSubmissions = await _db.EventSubmissions
                .AnyAsync(s => s.ArtworkId == artwork.Id && s.Id != submission.Id, cancellationToken);

            var isInCollections = await _db.CollectionArtworks
                .AnyAsync(c => c.ArtworkId == artwork.Id, cancellationToken);

            var hasFavorites = await _db.ArtworkFavorites
                .AnyAsync(f => f.ArtworkId == artwork.Id, cancellationToken);

            var hasComments = await _db.ArtworkComments
                .AnyAsync(c => c.ArtworkId == artwork.Id && !c.IsDeleted, cancellationToken);

            if (!isUsedInOtherSubmissions && !isInCollections && !hasFavorites && !hasComments)
            {
                artwork.IsDeleted = true;
                artwork.UpdatedAt = now;
            }
        }

        // 6. Xóa bài nộp khỏi sự kiện
        _db.EventSubmissions.Remove(submission);
        await _db.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>());
    }
}
