using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.CreatorApplication.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.CreatorApplication.Queries;

/// <summary>
/// Lấy đơn đăng ký Creator MỚI NHẤT của chính người dùng đang đăng nhập
/// (để hiển thị trạng thái/lý do trên trang /become-creator).
/// Trả null nếu chưa từng nộp đơn nào — không phải lỗi.
/// </summary>
public record GetMyCreatorApplicationQuery(
    Guid ApplicantId
) : IRequest<CreatorApplicationResponseDto?>;

public class GetMyCreatorApplicationQueryHandler
    : IRequestHandler<GetMyCreatorApplicationQuery, CreatorApplicationResponseDto?>
{
    private readonly IApplicationDbContext _db;

    public GetMyCreatorApplicationQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CreatorApplicationResponseDto?> Handle(
        GetMyCreatorApplicationQuery request,
        CancellationToken cancellationToken)
    {
        return await _db.CreatorApplications
            .AsNoTracking()
            .Include(c => c.Applicant)
            .Include(c => c.ReviewedByMod)
            .Where(c => c.ApplicantId == request.ApplicantId && !c.IsDeleted)
            .OrderByDescending(c => c.SubmittedAt)
            .Select(c => new CreatorApplicationResponseDto
            {
                Id = c.Id,
                ApplicantName = c.Applicant != null ? c.Applicant.FullName : string.Empty,
                ApplicantUsername = c.Applicant != null ? (c.Applicant.UserName ?? string.Empty) : string.Empty,
                ApplicantEmail = c.Applicant != null ? (c.Applicant.Email ?? string.Empty) : string.Empty,
                PrimaryStyle = c.PrimaryStyle,
                PortfolioLinks = c.PortfolioLinks,
                SpeedpaintVideoUrl = c.SpeedpaintVideoUrl,
                SocialLinks = c.SocialLinks,
                IdProofUrl = c.IdProofUrl,
                IdProofBackUrl = c.IdProofBackUrl,
                IsNationalIdVerified = c.IsNationalIdVerified,
                Status = c.Status.ToString(),
                ReviewedByModId = c.ReviewedByModId,
                ReviewedByModName = c.ReviewedByMod != null ? c.ReviewedByMod.FullName : null,
                ReviewNote = c.ReviewNote,
                SubmittedAt = c.SubmittedAt,
                ReviewedAt = c.ReviewedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
