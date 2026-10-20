using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Dispute.SubmissionDispute.Validators;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Dispute.SubmissionDispute.Commands;

public record ResolveSubmissionDisputeCommand(
    Guid DisputeId,
    Guid ModeratorId,
    string Resolution, // Dismiss, Disqualify, RequestReEvaluation, Other
    string? Note
) : IRequest<(bool Success, string Message, string[] Errors)>;

public class ResolveSubmissionDisputeCommandHandler
    : IRequestHandler<ResolveSubmissionDisputeCommand, (bool Success, string Message, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public ResolveSubmissionDisputeCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string Message, string[] Errors)> Handle(
        ResolveSubmissionDisputeCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new ResolveSubmissionDisputeCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, "Dữ liệu không hợp lệ.", validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var dispute = await _db.SubmissionDisputes
            .Include(d => d.Submission)
                .ThenInclude(s => s!.Artwork)
            .FirstOrDefaultAsync(d => d.Id == request.DisputeId, cancellationToken);

        if (dispute == null)
        {
            return (false, "Không tìm thấy khiếu nại bài dự thi.", ["Khiếu nại không tồn tại."]);
        }

        if (dispute.Status == "Resolved" || dispute.Status == "Rejected")
        {
            return (false, "Khiếu nại này đã được xử lý trước đó.", ["Khiếu nại đã đóng."]);
        }

        bool isDismissed = string.Equals(request.Resolution, "Dismiss", StringComparison.OrdinalIgnoreCase);

        dispute.Status = isDismissed ? "Rejected" : "Resolved";
        dispute.Resolution = request.Resolution.Trim();
        dispute.Note = request.Note?.Trim();
        dispute.ResolvedAt = DateTimeOffset.UtcNow;
        dispute.UpdatedAt = DateTimeOffset.UtcNow;

        // Nếu truất quyền thi đấu bài thi (Disqualify)
        if (string.Equals(request.Resolution, "Disqualify", StringComparison.OrdinalIgnoreCase) && dispute.Submission != null)
        {
            var submission = dispute.Submission;
            var now = DateTimeOffset.UtcNow;

            submission.IsDisqualified = true;
            submission.VoteCount = 0;
            submission.Score = null;
            submission.AdminNote = !string.IsNullOrWhiteSpace(request.Note)
                ? $"[Bị truất quyền do khiếu nại]: {request.Note}"
                : "[Bị truất quyền do khiếu nại]";

            // Dọn dẹp điểm chấm của BGK và lượt bình chọn cho bài thi bị truất quyền
            var criteriaScores = await _db.CriteriaScores
                .Where(cs => cs.SubmissionId == submission.Id)
                .ToListAsync(cancellationToken);
            if (criteriaScores.Count > 0)
            {
                _db.CriteriaScores.RemoveRange(criteriaScores);
            }

            var votes = await _db.EventVotes
                .Where(v => v.SubmissionId == submission.Id)
                .ToListAsync(cancellationToken);
            if (votes.Count > 0)
            {
                _db.EventVotes.RemoveRange(votes);
            }

            // Dọn dẹp tranh nếu tranh này được upload riêng cho bài thi này (không dùng ở bài khác, không có tương tác nào)
            var artwork = submission.Artwork ?? await _db.Artworks
                .FirstOrDefaultAsync(a => a.Id == submission.ArtworkId, cancellationToken);

            if (artwork != null)
            {
                var isUsedInOtherSubmissions = await _db.EventSubmissions
                    .AnyAsync(s => s.ArtworkId == artwork.Id && s.Id != submission.Id && !s.IsDisqualified, cancellationToken);

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
        }
        // Nếu yêu cầu Ban Giám Khảo phúc khảo / chấm lại (RequestReEvaluation)
        else if (string.Equals(request.Resolution, "RequestReEvaluation", StringComparison.OrdinalIgnoreCase) && dispute.Submission != null)
        {
            dispute.Submission.AdminNote = !string.IsNullOrWhiteSpace(request.Note)
                ? $"[Yêu cầu Ban Giám Khảo chấm lại]: {request.Note}"
                : "[Yêu cầu Ban Giám Khảo chấm lại do khiếu nại]";
        }

        await _db.SaveChangesAsync(cancellationToken);

        string message = isDismissed
            ? "Đã bác bỏ khiếu nại bài dự thi."
            : $"Đã xử lý khiếu nại bài dự thi theo phán quyết: {request.Resolution}.";

        return (true, message, Array.Empty<string>());
    }
}
