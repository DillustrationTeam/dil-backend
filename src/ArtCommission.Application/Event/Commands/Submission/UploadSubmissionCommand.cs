using ArtCommission.Application.ArtistStudio.Commands.UploadArtwork;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Validators;
using ArtCommission.Domain.Entities.Event;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Commands;

public record UploadSubmissionCommand(
    Guid EventId,
    Guid SubmitterId,
    Guid? ArtworkId = null,
    string? Title = null,
    string? Description = null,
    string? ImageUrl = null,
    string? ThumbnailUrl = null,
    bool IsAiGenerated = false,
    decimal? AiDetectionScore = null,
    IReadOnlyList<string>? Tags = null
) : IRequest<(bool Success, EventSubmissionDto? Data, string[] Errors)>;

public class UploadSubmissionCommandHandler
    : IRequestHandler<UploadSubmissionCommand, (bool Success, EventSubmissionDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;

    public UploadSubmissionCommandHandler(IApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<(bool Success, EventSubmissionDto? Data, string[] Errors)> Handle(
        UploadSubmissionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate định dạng dữ liệu đầu vào
        var validator = new UploadSubmissionCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // 2. Kiểm tra sự kiện
        var platformEvent = await _db.PlatformEvents
            .FirstOrDefaultAsync(e => e.Id == request.EventId && !e.IsDeleted, cancellationToken);

        if (platformEvent == null)
        {
            return (false, null, ["Sự kiện không tồn tại hoặc đã bị xóa."]);
        }

        if (platformEvent.Status == EventStatus.Draft)
        {
            return (false, null, ["Không thể nộp bài vào sự kiện đang ở trạng thái Bản nháp (Draft)."]);
        }

        if (platformEvent.Status == EventStatus.Ended)
        {
            return (false, null, ["Không thể nộp bài vào sự kiện đã kết thúc."]);
        }

        // 3. Kiểm tra Timeline nộp bài
        var now = DateTimeOffset.UtcNow;
        if (now < platformEvent.SubmissionStartAt)
        {
            return (false, null, [$"Thời gian nhận bài thi chưa bắt đầu (Bắt đầu lúc: {platformEvent.SubmissionStartAt:u})."]);
        }

        if (now > platformEvent.SubmissionEndAt)
        {
            return (false, null, [$"Thời gian nhận bài thi đã kết thúc (Kết thúc lúc: {platformEvent.SubmissionEndAt:u})."]);
        }

        // 4. Kiểm tra quyền Creator (Chỉ Creator mới có thể tham gia nộp bài dự thi sự kiện)
        var creatorProfile = await _db.CreatorProfiles
            .FirstOrDefaultAsync(cp => cp.UserId == request.SubmitterId && !cp.IsDeleted, cancellationToken);

        if (creatorProfile == null)
        {
            return (false, null, ["Chỉ Creator (Họa sĩ) mới có thể tham gia nộp bài dự thi sự kiện. Vui lòng thiết lập hồ sơ Creator để tham gia."]);
        }

        // 4a. Kiểm tra Creator có phải Jury của sự kiện này không
        var isJuryForEvent = await _db.Juries
            .AnyAsync(j => j.EventId == request.EventId && j.CreatorId == creatorProfile.Id && !j.IsDeleted, cancellationToken);

        if (isJuryForEvent)
        {
            return (false, null, ["Thành viên Ban giám khảo (Jury) của sự kiện không được tham gia nộp bài dự thi trong chính sự kiện đó."]);
        }

        // 4b. Kiểm tra quy tắc 1 Creator chỉ được nộp duy nhất 1 bài dự thi cho mỗi sự kiện
        var hasExistingSubmission = await _db.EventSubmissions
            .AnyAsync(s => s.EventId == request.EventId && s.SubmitterId == request.SubmitterId, cancellationToken);

        if (hasExistingSubmission)
        {
            return (false, null, ["Mỗi Creator chỉ được nộp một bài dự thi cho mỗi sự kiện. Bạn đã có bài nộp trong sự kiện này, vui lòng chỉnh sửa bài dự thi hiện có."]);
        }

        Guid targetArtworkId;
        string submissionTitle;
        string? submissionDesc = request.Description;
        bool aiScanPassed;

        // 5. Xử lý tác phẩm (Artwork)
        if (request.ArtworkId.HasValue)
        {
            // Case A: Chọn tranh có sẵn trong kho
            var artwork = await _db.Artworks
                .Include(a => a.CreatorProfile)
                .FirstOrDefaultAsync(a => a.Id == request.ArtworkId.Value && !a.IsDeleted, cancellationToken);

            if (artwork == null)
            {
                return (false, null, ["Tác phẩm không tồn tại hoặc đã bị xóa."]);
            }

            if (artwork.CreatorProfile == null || artwork.CreatorProfile.UserId != request.SubmitterId)
            {
                return (false, null, ["Bạn chỉ có thể nộp bài bằng tác phẩm do chính mình tạo ra."]);
            }

            var isAlreadySubmitted = await _db.EventSubmissions
                .AnyAsync(s => s.EventId == request.EventId && s.ArtworkId == artwork.Id, cancellationToken);

            if (isAlreadySubmitted)
            {
                return (false, null, ["Tác phẩm này đã được nộp vào sự kiện trước đó."]);
            }

            targetArtworkId = artwork.Id;
            submissionTitle = !string.IsNullOrWhiteSpace(request.Title) ? request.Title.Trim() : artwork.Title;
            submissionDesc ??= artwork.Description;
            aiScanPassed = !artwork.IsAiGenerated && (artwork.AiDetectionScore == null || artwork.AiDetectionScore < 0.5m);
        }
        else
        {
            // Case B: Upload tranh mới toanh
            var uploadArtworkCommand = new UploadArtworkCommand(
                UserId: request.SubmitterId,
                Title: request.Title!.Trim(),
                Description: request.Description,
                ImageUrl: request.ImageUrl!,
                ThumbnailUrl: request.ThumbnailUrl,
                IsAiGenerated: request.IsAiGenerated,
                AiDetectionScore: request.AiDetectionScore,
                Tags: request.Tags
            );

            var (artSuccess, artData, artErrors) = await _mediator.Send(uploadArtworkCommand, cancellationToken);
            if (!artSuccess || artData == null)
            {
                return (false, null, artErrors.Length > 0 ? artErrors : ["Không thể tải tác phẩm lên hệ thống."]);
            }

            targetArtworkId = artData.Id;
            submissionTitle = request.Title!.Trim();
            aiScanPassed = !request.IsAiGenerated && (request.AiDetectionScore == null || request.AiDetectionScore < 0.5m);

            // Tác phẩm upload phục vụ cuộc thi -> đặt ModerationStatus = "Approved" để hiển thị công khai trên trang tranh
            var createdArtwork = await _db.Artworks.FirstOrDefaultAsync(a => a.Id == targetArtworkId, cancellationToken);
            if (createdArtwork != null && createdArtwork.ModerationStatus != "Approved")
            {
                createdArtwork.ModerationStatus = "Approved";
            }
        }

        // 5. Tạo bản ghi EventSubmission
        var submission = new EventSubmission
        {
            Id = Guid.NewGuid(),
            EventId = request.EventId,
            SubmitterId = request.SubmitterId,
            ArtworkId = targetArtworkId,
            Title = submissionTitle,
            Description = submissionDesc,
            AiScanPassed = aiScanPassed,
            VoteCount = 0,
            Score = null,
            SubmittedAt = now
        };

        _db.EventSubmissions.Add(submission);
        await _db.SaveChangesAsync(cancellationToken);

        // 6. Truy vấn thông tin để chuẩn bị DTO phản hồi
        var submitterUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.SubmitterId, cancellationToken);

        var artworkRecord = await _db.Artworks
            .FirstOrDefaultAsync(a => a.Id == targetArtworkId, cancellationToken);

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
