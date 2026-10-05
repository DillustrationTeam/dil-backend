using System.Text.RegularExpressions;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetUsernameAvailability;

/// <summary>Kiểm tra 1 username có thể dùng được không — hợp lệ định dạng, không nằm trong danh sách
/// cấm, và chưa có ai khác dùng (trừ chính CurrentUserId, để gõ lại đúng username hiện tại vẫn hợp lệ).</summary>
public record GetUsernameAvailabilityQuery(Guid CurrentUserId, string Username) : IRequest<bool>;

public class GetUsernameAvailabilityQueryHandler : IRequestHandler<GetUsernameAvailabilityQuery, bool>
{
    private static readonly Regex UsernameFormat = new("^[a-zA-Z0-9_.]{3,30}$", RegexOptions.Compiled);

    private readonly IApplicationDbContext _db;

    public GetUsernameAvailabilityQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> Handle(GetUsernameAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var normalized = request.Username.Trim().ToLowerInvariant();

        if (!UsernameFormat.IsMatch(normalized) || ReservedUsernames.Set.Contains(normalized))
        {
            return false;
        }

        var taken = await _db.ClientProfiles.AnyAsync(
            x => x.Username == normalized && x.UserId != request.CurrentUserId && !x.IsDeleted,
            cancellationToken);

        return !taken;
    }
}
