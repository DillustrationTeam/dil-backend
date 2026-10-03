using ArtCommission.Application.Common.Interfaces;
using MediatR;

namespace ArtCommission.Application.Auth.Queries.Get2FAStatus;

public record Get2FAStatusQuery(Guid UserId) : IRequest<bool>;

public class Get2FAStatusQueryHandler : IRequestHandler<Get2FAStatusQuery, bool>
{
    private readonly IIdentityService _identityService;

    public Get2FAStatusQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<bool> Handle(Get2FAStatusQuery request, CancellationToken cancellationToken)
    {
        return await _identityService.IsTwoFactorEnabledAsync(request.UserId, cancellationToken);
    }
}
