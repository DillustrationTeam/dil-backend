using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetMyInvitationsQuery(
    Guid UserId,
    InvitationStatus? Status = null,
    Guid? EventId = null,
    int Page = 1,
    int PageSize = 10
) : IRequest<(IReadOnlyList<InvitationDto> Items, int TotalCount)>;

public class GetMyInvitationsQueryHandler 
    : IRequestHandler<GetMyInvitationsQuery, (IReadOnlyList<InvitationDto> Items, int TotalCount)>
{
    private const int MaxPageSize = 50;
    private readonly IApplicationDbContext _db;

    public GetMyInvitationsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<InvitationDto> Items, int TotalCount)> Handle(
        GetMyInvitationsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, MaxPageSize);

        // Lọc lời mời gửi đến creator tương ứng với UserId (hoặc SentToCreatorId == UserId nếu truyền trực tiếp CreatorProfile.Id)
        var query = _db.Invitations
            .AsNoTracking()
            .Where(i => !i.IsDeleted && 
                        (i.SentToCreatorId == request.UserId || 
                         (i.SentToCreator != null && i.SentToCreator.UserId == request.UserId)));

        if (request.Status.HasValue)
        {
            query = query.Where(i => i.Status == request.Status.Value);
        }

        if (request.EventId.HasValue)
        {
            query = query.Where(i => i.EventId == request.EventId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvitationDto
            {
                Id = i.Id,
                EventId = i.EventId,
                EventTitle = i.Event != null ? i.Event.Title : null,
                EventBannerUrl = i.Event != null ? i.Event.BannerUrl : null,
                SentFromAdminId = i.SentFromAdminId,
                SentFromAdminName = i.SentFromAdmin != null ? i.SentFromAdmin.FullName : null,
                SentToCreatorId = i.SentToCreatorId,
                SentToCreatorName = i.SentToCreator != null 
                    ? (i.SentToCreator.User != null && !string.IsNullOrEmpty(i.SentToCreator.User.FullName) 
                        ? i.SentToCreator.User.FullName 
                        : i.SentToCreator.DisplayName) 
                    : null,
                SentToCreatorEmail = i.SentToCreator != null && i.SentToCreator.User != null 
                    ? i.SentToCreator.User.Email 
                    : null,
                SentToCreatorAvatarUrl = i.SentToCreator != null ? i.SentToCreator.BannerUrl : null,
                IsHeadJury = i.IsHeadJury,
                Status = i.Status.ToString(),
                CreatedAt = i.CreatedAt,
                RespondedAt = i.RespondedAt
            })
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
