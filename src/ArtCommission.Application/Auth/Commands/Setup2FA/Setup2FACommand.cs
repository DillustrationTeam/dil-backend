using ArtCommission.Application.Common.Interfaces;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.Setup2FA;

public record Setup2FACommand(Guid UserId) : IRequest<(bool Success, string SharedKey, string AuthenticatorUri, string[] Errors)>;

public class Setup2FACommandHandler : IRequestHandler<Setup2FACommand, (bool Success, string SharedKey, string AuthenticatorUri, string[] Errors)>
{
    private readonly IIdentityService _identityService;

    public Setup2FACommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<(bool Success, string SharedKey, string AuthenticatorUri, string[] Errors)> Handle(Setup2FACommand request, CancellationToken cancellationToken)
    {
        return await _identityService.Setup2FAAsync(request.UserId, cancellationToken);
    }
}
