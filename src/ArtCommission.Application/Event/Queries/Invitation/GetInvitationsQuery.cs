using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetInvitationsQuery(
    Guid? EventId = null,
    InvitationStatus? Status = null,
    Guid? SentToCreatorId = null,
    Guid? SentFromAdminId = null,
    bool? IsHeadJury = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 10
) : IRequest<(IReadOnlyList<InvitationDto> Items, int TotalCount)>;

public class GetInvitationsQueryHandler 
    : IRequestHandler<GetInvitationsQuery, (IReadOnlyList<InvitationDto> Items, int TotalCount)>
{
    private const int MaxPageSize = 50;
    private readonly IApplicationDbContext _db;

    public GetInvitationsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<InvitationDto> Items, int TotalCount)> Handle(
        GetInvitationsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, MaxPageSize);

        var query = _db.Invitations
            .AsNoTracking()
            .Where(i => !i.IsDeleted);

        if (request.EventId.HasValue)
        {
            query = query.Where(i => i.EventId == request.EventId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(i => i.Status == request.Status.Value);
        }

        if (request.SentToCreatorId.HasValue)
        {
            query = query.Where(i => i.SentToCreatorId == request.SentToCreatorId.Value);
        }

        if (request.SentFromAdminId.HasValue)
        {
            query = query.Where(i => i.SentFromAdminId == request.SentFromAdminId.Value);
        }

        if (request.IsHeadJury.HasValue)
        {
            query = query.Where(i => i.IsHeadJury == request.IsHeadJury.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim().ToLower();
            query = query.Where(i =>
                (i.Event != null && i.Event.Title.ToLower().Contains(keyword)) ||
                (i.SentToCreator != null && i.SentToCreator.DisplayName.ToLower().Contains(keyword)) ||
                (i.SentToCreator != null && i.SentToCreator.User != null && i.SentToCreator.User.Email != null && i.SentToCreator.User.Email.ToLower().Contains(keyword)) ||
                (i.SentToCreator != null && i.SentToCreator.User != null && !string.IsNullOrEmpty(i.SentToCreator.User.FullName) && i.SentToCreator.User.FullName.ToLower().Contains(keyword))
            );
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
