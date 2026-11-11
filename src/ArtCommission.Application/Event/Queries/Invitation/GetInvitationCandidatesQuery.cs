using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public sealed record InvitationCandidateDto
{
    public Guid UserId { get; init; }
    public Guid? CreatorId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? FullName { get; init; }
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public string? AvatarUrl { get; init; }
    public List<string> Roles { get; init; } = new();
    public bool HasCreatorProfile { get; init; }
    public bool IsAlreadyJury { get; init; }
    public bool HasPendingInvitation { get; init; }
    public bool CanInvite { get; init; }
    public string? IneligibleReason { get; init; }
}

public record GetInvitationCandidatesQuery(
    Guid EventId,
    string? Search = null,
    string RoleFilter = "All",
    int Page = 1,
    int PageSize = 20
) : IRequest<(IReadOnlyList<InvitationCandidateDto> Items, int TotalCount)>;

public class GetInvitationCandidatesQueryHandler
    : IRequestHandler<GetInvitationCandidatesQuery, (IReadOnlyList<InvitationCandidateDto> Items, int TotalCount)>
{
    private const int MaxPageSize = 50;
    private readonly IApplicationDbContext _db;

    public GetInvitationCandidatesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<InvitationCandidateDto> Items, int TotalCount)> Handle(
        GetInvitationCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, MaxPageSize);
        var now = DateTimeOffset.UtcNow;

        var roles = await _db.Set<IdentityRole<Guid>>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var roleNameById = roles
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .ToDictionary(r => r.Id, r => r.Name!);

        var creatorRoleId = roles.FirstOrDefault(r => r.Name == UserRoleNames.Creator)?.Id ?? Guid.Empty;
        var clientRoleId = roles.FirstOrDefault(r => r.Name == UserRoleNames.Client)?.Id ?? Guid.Empty;

        var creatorUserIdsQuery = _db.Set<IdentityUserRole<Guid>>()
            .Where(ur => ur.RoleId == creatorRoleId)
            .Select(ur => ur.UserId);

        var clientUserIdsQuery = _db.Set<IdentityUserRole<Guid>>()
            .Where(ur => ur.RoleId == clientRoleId)
            .Select(ur => ur.UserId);

        var activeJuryCreatorIdsQuery = _db.Juries
            .Where(j => j.EventId == request.EventId && !j.IsDeleted)
            .Select(j => j.CreatorId);

        var pendingInviteCreatorIdsQuery = _db.Invitations
            .Where(i => i.EventId == request.EventId && i.Status == InvitationStatus.Pending && !i.IsDeleted)
            .Select(i => i.SentToCreatorId);

        var usersQuery = _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted && (u.LockoutEnd == null || u.LockoutEnd <= now))
            .Where(u => creatorUserIdsQuery.Contains(u.Id) || clientUserIdsQuery.Contains(u.Id));

        var roleFilter = string.IsNullOrWhiteSpace(request.RoleFilter)
            ? "All"
            : request.RoleFilter.Trim();

        if (roleFilter.Equals("Creator", StringComparison.OrdinalIgnoreCase))
        {
            usersQuery = usersQuery.Where(u =>
                creatorUserIdsQuery.Contains(u.Id) &&
                _db.CreatorProfiles.Any(c => !c.IsDeleted && c.UserId == u.Id));
        }
        else if (roleFilter.Equals("Eligible", StringComparison.OrdinalIgnoreCase))
        {
            usersQuery = usersQuery.Where(u =>
                creatorUserIdsQuery.Contains(u.Id) &&
                _db.CreatorProfiles.Any(c =>
                    !c.IsDeleted &&
                    c.UserId == u.Id &&
                    !activeJuryCreatorIdsQuery.Contains(c.Id) &&
                    !pendingInviteCreatorIdsQuery.Contains(c.Id)));
        }
        else if (roleFilter.Equals("Client", StringComparison.OrdinalIgnoreCase))
        {
            usersQuery = usersQuery.Where(u =>
                clientUserIdsQuery.Contains(u.Id) &&
                !creatorUserIdsQuery.Contains(u.Id));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim().ToLower();
            usersQuery = usersQuery.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(keyword)) ||
                (u.FullName != null && u.FullName.ToLower().Contains(keyword)) ||
                (u.UserName != null && u.UserName.ToLower().Contains(keyword)) ||
                _db.CreatorProfiles.Any(c =>
                    !c.IsDeleted &&
                    c.UserId == u.Id &&
                    c.DisplayName.ToLower().Contains(keyword)));
        }

        var totalCount = await usersQuery.CountAsync(cancellationToken);

        var pagedUsers = await usersQuery
            .OrderByDescending(u => creatorUserIdsQuery.Contains(u.Id))
            .ThenBy(u => u.FullName ?? u.Email ?? u.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.UserName,
                u.Email,
                u.AvatarUrl
            })
            .ToListAsync(cancellationToken);

        if (pagedUsers.Count == 0)
        {
            return (Array.Empty<InvitationCandidateDto>(), totalCount);
        }

        var pageUserIds = pagedUsers.Select(u => u.Id).ToList();

        var userRoles = await _db.Set<IdentityUserRole<Guid>>()
            .AsNoTracking()
            .Where(ur => pageUserIds.Contains(ur.UserId))
            .ToListAsync(cancellationToken);

        var rolesByUser = userRoles
            .GroupBy(ur => ur.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(ur => roleNameById.GetValueOrDefault(ur.RoleId))
                      .Where(n => !string.IsNullOrWhiteSpace(n))
                      .Cast<string>()
                      .Distinct()
                      .ToList());

        var creatorProfiles = await _db.CreatorProfiles
            .AsNoTracking()
            .Where(c => !c.IsDeleted && pageUserIds.Contains(c.UserId))
            .ToListAsync(cancellationToken);

        var profileByUser = creatorProfiles
            .GroupBy(c => c.UserId)
            .ToDictionary(g => g.Key, g => g.First());

        var creatorIds = creatorProfiles.Select(c => c.Id).ToList();

        var existingJuryCreatorIds = creatorIds.Count == 0
            ? new HashSet<Guid>()
            : (await _db.Juries
                .AsNoTracking()
                .Where(j => j.EventId == request.EventId && !j.IsDeleted && creatorIds.Contains(j.CreatorId))
                .Select(j => j.CreatorId)
                .ToListAsync(cancellationToken)).ToHashSet();

        var pendingInviteCreatorSet = creatorIds.Count == 0
            ? new HashSet<Guid>()
            : (await _db.Invitations
                .AsNoTracking()
                .Where(i => i.EventId == request.EventId && i.Status == InvitationStatus.Pending && !i.IsDeleted && creatorIds.Contains(i.SentToCreatorId))
                .Select(i => i.SentToCreatorId)
                .ToListAsync(cancellationToken)).ToHashSet();

        var items = pagedUsers.Select(u =>
        {
            var userRoleList = rolesByUser.GetValueOrDefault(u.Id) ?? new List<string>();
            profileByUser.TryGetValue(u.Id, out var creatorProfile);

            var hasCreatorRole = userRoleList.Contains(UserRoleNames.Creator, StringComparer.OrdinalIgnoreCase);
            var hasCreatorProfile = creatorProfile != null;
            var isAlreadyJury = creatorProfile != null && existingJuryCreatorIds.Contains(creatorProfile.Id);
            var hasPendingInvite = creatorProfile != null && pendingInviteCreatorSet.Contains(creatorProfile.Id);

            string? ineligibleReason = null;
            if (!hasCreatorRole || !hasCreatorProfile)
            {
                ineligibleReason = "Tài khoản này chưa có vai trò/hồ sơ Creator nên chưa thể làm giám khảo.";
            }
            else if (isAlreadyJury)
            {
                ineligibleReason = "Creator này hiện đã là thành viên ban giám khảo của sự kiện.";
            }
            else if (hasPendingInvite)
            {
                ineligibleReason = "Creator này đang có lời mời chờ phản hồi trong sự kiện.";
            }

            var displayName = !string.IsNullOrWhiteSpace(creatorProfile?.DisplayName)
                ? creatorProfile!.DisplayName
                : (!string.IsNullOrWhiteSpace(u.FullName) ? u.FullName! : (u.UserName ?? u.Email ?? "User"));

            return new InvitationCandidateDto
            {
                UserId = u.Id,
                CreatorId = creatorProfile?.Id,
                DisplayName = displayName,
                FullName = u.FullName,
                UserName = u.UserName,
                Email = u.Email,
                AvatarUrl = u.AvatarUrl,
                Roles = userRoleList,
                HasCreatorProfile = hasCreatorProfile,
                IsAlreadyJury = isAlreadyJury,
                HasPendingInvitation = hasPendingInvite,
                CanInvite = ineligibleReason == null,
                IneligibleReason = ineligibleReason
            };
        }).ToList();

        return (items, totalCount);
    }
}

