using ArtCommission.Application.ArtistStudio.Commands.UploadArtwork;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Validators;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Commands;

public record UpdateSubmissionCommand(
    Guid SubmissionId,
    Guid UserId,
    string? Title = null,
    string? Description = null,
    Guid? ArtworkId = null,
    string? ImageUrl = null,
    string? ThumbnailUrl = null,
    bool IsAiGenerated = false,
    decimal? AiDetectionScore = null,
    IReadOnlyList<string>? Tags = null
) : IRequest<(bool Success, EventSubmissionDto? Data, string[] Errors)>;

public class UpdateSubmissionCommandHandler
    : IRequestHandler<UpdateSubmissionCommand, (bool Success, EventSubmissionDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;

    public UpdateSubmissionCommandHandler(IApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<(bool Success, EventSubmissionDto? Data, string[] Errors)> Handle(
        UpdateSubmissionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra tính hợp lệ của tham số
        var validator = new UpdateSubmissionCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // 2. Tìm bài dự thi
        var submission = await _db.EventSubmissions
            .Include(s => s.Event)
            .Include(s => s.Artwork)
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return (false, null, ["Không tìm thấy bài dự thi."]);
        }

        // 3. Quy tắc cốt lõi: Chỉ chính chủ Creator nộp bài mới có quyền chỉnh sửa
        if (submission.SubmitterId != request.UserId)
        {
            return (false, null, ["Bạn chỉ có quyền chỉnh sửa bài dự thi do chính mình nộp."]);
        }

        // 4. Kiểm tra sự kiện và thời hạn nộp bài
        var platformEvent = submission.Event ?? await _db.PlatformEvents
            .FirstOrDefaultAsync(e => e.Id == submission.EventId && !e.IsDeleted, cancellationToken);

        if (platformEvent == null || platformEvent.IsDeleted)
        {
            return (false, null, ["Sự kiện liên quan không tồn tại hoặc đã bị xóa."]);
        }

        var now = DateTimeOffset.UtcNow;
        if (now < platformEvent.SubmissionStartAt)
        {
            return (false, null, [$"Thời gian nhận bài thi chưa bắt đầu (Bắt đầu: {platformEvent.SubmissionStartAt:u})."]);
        }

        if (now > platformEvent.SubmissionEndAt)
        {
            return (false, null, [$"Thời hạn nộp bài đã kết thúc (Kết thúc lúc: {platformEvent.SubmissionEndAt:u}). Bạn không thể chỉnh sửa bài dự thi sau khi hết hạn."]);
        }

        var oldArtwork = submission.Artwork;

        // 5. Xử lý thay đổi tác phẩm
        // Trường hợp 5A: Thí sinh upload một tranh mới toanh từ máy lên thay thế
        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            var uploadArtworkCommand = new UploadArtworkCommand(
                UserId: request.UserId,
                Title: !string.IsNullOrWhiteSpace(request.Title) ? request.Title.Trim() : submission.Title,
                Description: request.Description ?? submission.Description,
                ImageUrl: request.ImageUrl.Trim(),
                ThumbnailUrl: request.ThumbnailUrl,
                IsAiGenerated: request.IsAiGenerated,
                AiDetectionScore: request.AiDetectionScore,
                Tags: request.Tags
            );

            var (artSuccess, artData, artErrors) = await _mediator.Send(uploadArtworkCommand, cancellationToken);
            if (!artSuccess || artData == null)
            {
                return (false, null, artErrors.Length > 0 ? artErrors : ["Không thể tải tác phẩm mới lên hệ thống."]);
            }

            submission.ArtworkId = artData.Id;
            submission.Artwork = null; // Reset để load lại theo Id mới
            submission.AiScanPassed = !request.IsAiGenerated && (request.AiDetectionScore == null || request.AiDetectionScore < 0.5m);

            // Cập nhật trạng thái duyệt cho tác phẩm mới thành Approved để hiển thị công khai trên trang tranh
            var newArt = await _db.Artworks.FirstOrDefaultAsync(a => a.Id == artData.Id, cancellationToken);
            if (newArt != null && newArt.ModerationStatus != "Approved")
            {
                newArt.ModerationStatus = "Approved";
            }
        }
        
        // Trường hợp 5B: Thí sinh chọn đổi sang một tác phẩm khác đã có sẵn trong kho của mình
        else if (request.ArtworkId.HasValue && request.ArtworkId.Value != submission.ArtworkId)
        {
            var newArtwork = await _db.Artworks
                .Include(a => a.CreatorProfile)
                .FirstOrDefaultAsync(a => a.Id == request.ArtworkId.Value && !a.IsDeleted, cancellationToken);

            if (newArtwork == null)
            {
                return (false, null, ["Tác phẩm thay thế không tồn tại hoặc đã bị xóa."]);
            }

            if (newArtwork.CreatorProfile == null || newArtwork.CreatorProfile.UserId != request.UserId)
            {
                return (false, null, ["Bạn chỉ có thể thay thế bằng tác phẩm thuộc quyền sở hữu của chính mình."]);
            }

            var isDuplicate = await _db.EventSubmissions
                .AnyAsync(s => s.EventId == submission.EventId && s.ArtworkId == newArtwork.Id && s.Id != submission.Id, cancellationToken);

            if (isDuplicate)
            {
                return (false, null, ["Tác phẩm này đã được sử dụng cho một bài dự thi khác trong sự kiện."]);
            }

            submission.ArtworkId = newArtwork.Id;
            submission.Artwork = newArtwork;
            submission.AiScanPassed = !newArtwork.IsAiGenerated && (newArtwork.AiDetectionScore == null || newArtwork.AiDetectionScore < 0.5m);
        }

        // 6. Dọn dẹp tranh cũ nếu tranh cũ là tranh upload từ máy (chỉ phục vụ bài thi này và không có tương tác nào khác)
        if (oldArtwork != null && oldArtwork.Id != submission.ArtworkId)
        {
            var isUsedInOtherSubmissions = await _db.EventSubmissions
                .AnyAsync(s => s.ArtworkId == oldArtwork.Id && s.Id != submission.Id, cancellationToken);

            var isInCollections = await _db.CollectionArtworks
                .AnyAsync(c => c.ArtworkId == oldArtwork.Id, cancellationToken);

            var hasFavorites = await _db.ArtworkFavorites
                .AnyAsync(f => f.ArtworkId == oldArtwork.Id, cancellationToken);

            var hasComments = await _db.ArtworkComments
                .AnyAsync(c => c.ArtworkId == oldArtwork.Id && !c.IsDeleted, cancellationToken);

            if (!isUsedInOtherSubmissions && !isInCollections && !hasFavorites && !hasComments)
            {
                oldArtwork.IsDeleted = true;
                oldArtwork.UpdatedAt = now;
            }
        }

        // 7. Cập nhật Tiêu đề và Mô tả nếu có truyền
        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            submission.Title = request.Title.Trim();
        }

        if (request.Description != null)
        {
            submission.Description = request.Description;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // 7. Chuẩn bị DTO phản hồi
        var submitterUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == submission.SubmitterId, cancellationToken);

        var artworkRecord = await _db.Artworks
            .FirstOrDefaultAsync(a => a.Id == submission.ArtworkId, cancellationToken);

        var resultDto = new EventSubmissionDto
        {
            Id = submission.Id,
            EventId = submission.EventId,
            EventTitle = platformEvent.Title,
            SubmitterId = submission.SubmitterId,
            SubmitterName = submitterUser?.FullName,
            SubmitterUsername = submitterUser?.UserName,
            SubmitterAvatarUrl = null,
            ArtworkId = submission.ArtworkId,
            Title = submission.Title,
            Description = submission.Description,
            ArtworkTitle = artworkRecord?.Title,
            ArtworkImageUrl = artworkRecord?.ImageUrl,
            ArtworkThumbnailUrl = artworkRecord?.ThumbnailUrl,
            AiScanPassed = submission.AiScanPassed,
            VoteCount = submission.VoteCount,
            Score = submission.Score,
            AdminNote = submission.AdminNote,
            SubmittedAt = submission.SubmittedAt
        };

        return (true, resultDto, Array.Empty<string>());
    }
}
