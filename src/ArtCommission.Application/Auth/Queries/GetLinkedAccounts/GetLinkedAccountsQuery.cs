using ArtCommission.Application.Common.Interfaces;
using MediatR;

namespace ArtCommission.Application.Auth.Queries.GetLinkedAccounts;

public record GetLinkedAccountsQuery(Guid UserId) : IRequest<LinkedAccountsDto>;

public record LinkedAccountsDto(bool HasPassword, IReadOnlyList<string> LinkedProviders);

public class GetLinkedAccountsQueryHandler : IRequestHandler<GetLinkedAccountsQuery, LinkedAccountsDto>
{
    private readonly IIdentityService _identityService;

    public GetLinkedAccountsQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<LinkedAccountsDto> Handle(GetLinkedAccountsQuery request, CancellationToken cancellationToken)
    {
        var (hasPassword, linkedProviders) = await _identityService.GetLinkedAccountsAsync(request.UserId, cancellationToken);
        return new LinkedAccountsDto(hasPassword, linkedProviders);
    }
}
