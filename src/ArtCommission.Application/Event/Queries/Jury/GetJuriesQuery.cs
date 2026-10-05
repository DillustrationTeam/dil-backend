using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetJuriesQuery(
    Guid? EventId = null,
    string? Role = null,
    bool? IsHeadJury = null,
    string? Name = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<(IReadOnlyList<JuryDto> Items, int TotalCount)>;

public class GetJuriesQueryHandler
    : IRequestHandler<GetJuriesQuery, (IReadOnlyList<JuryDto> Items, int TotalCount)>
{
    private readonly IApplicationDbContext _db;

    public GetJuriesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<JuryDto> Items, int TotalCount)> Handle(
        GetJuriesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _db.Juries
            .AsNoTracking()
            .Include(j => j.Event)
            .Include(j => j.Creator)
                .ThenInclude(c => c!.User)
            .Where(j => !j.IsDeleted && (j.Event == null || !j.Event.IsDeleted));

        if (request.EventId.HasValue && request.EventId.Value != Guid.Empty)
        {
            query = query.Where(j => j.EventId == request.EventId.Value);
        }

        // Filter theo role: HeadJury vs Jury / Member
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var roleNormalized = request.Role.Trim().ToLowerInvariant();
            if (roleNormalized is "head" or "headjury" or "head_jury" or "leader" or "truongban" or "true")
            {
                query = query.Where(j => j.IsHeadJury);
            }
            else if (roleNormalized is "jury" or "member" or "regular" or "thanhvien" or "false")
            {
                query = query.Where(j => !j.IsHeadJury);
            }
        }

        if (request.IsHeadJury.HasValue)
        {
            query = query.Where(j => j.IsHeadJury == request.IsHeadJury.Value);
        }

        // Search theo name: Tìm kiếm theo tên người dùng, display name hoặc username
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var nameKeyword = request.Name.Trim().ToLower();
            query = query.Where(j =>
                (j.Creator != null && j.Creator.DisplayName.ToLower().Contains(nameKeyword)) ||
                (j.Creator != null && j.Creator.User != null && j.Creator.User.FullName != null && j.Creator.User.FullName.ToLower().Contains(nameKeyword)) ||
                (j.Creator != null && j.Creator.User != null && j.Creator.User.UserName != null && j.Creator.User.UserName.ToLower().Contains(nameKeyword))
            );
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim().ToLower();
            query = query.Where(j =>
                (j.Creator != null && j.Creator.DisplayName.ToLower().Contains(keyword)) ||
                (j.Creator != null && j.Creator.User != null && j.Creator.User.FullName != null && j.Creator.User.FullName.ToLower().Contains(keyword)) ||
                (j.Creator != null && j.Creator.User != null && j.Creator.User.UserName != null && j.Creator.User.UserName.ToLower().Contains(keyword)) ||
                (j.Event != null && j.Event.Title.ToLower().Contains(keyword))
            );
        }


        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(j => j.IsHeadJury)
            .ThenByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JuryDto
            {
                Id = j.Id,
                EventId = j.EventId,
                EventTitle = j.Event != null ? j.Event.Title : null,
                CreatorId = j.CreatorId,
                CreatorName = j.Creator != null && j.Creator.User != null ? j.Creator.User.FullName : null,
                CreatorDisplayName = j.Creator != null ? j.Creator.DisplayName : null,
                CreatorAvatarUrl = null,
                CreatorBio = j.Creator != null ? j.Creator.Bio : null,
                IsHeadJury = j.IsHeadJury,
                CreatedAt = j.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
