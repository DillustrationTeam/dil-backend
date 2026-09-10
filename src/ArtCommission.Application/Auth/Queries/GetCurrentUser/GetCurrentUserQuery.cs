using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;

namespace ArtCommission.Application.Auth.Queries.GetCurrentUser;

public record GetCurrentUserQuery(
    Guid UserId
) : IRequest<(bool Success, UserDto? User, string[] Roles, string[] Errors)>;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, (bool Success, UserDto? User, string[] Roles, string[] Errors)>
{
    private readonly IIdentityService _identityService;

    public GetCurrentUserQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        return _identityService.GetUserByIdAsync(request.UserId, cancellationToken);
    }
}
