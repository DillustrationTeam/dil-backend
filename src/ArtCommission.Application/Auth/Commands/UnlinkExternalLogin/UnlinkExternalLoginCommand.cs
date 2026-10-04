using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.UnlinkExternalLogin;

public record UnlinkExternalLoginCommand(Guid UserId, string Provider) : IRequest<(bool Success, string[] Errors)>;

public class UnlinkExternalLoginCommandValidator : AbstractValidator<UnlinkExternalLoginCommand>
{
    public UnlinkExternalLoginCommandValidator()
    {
        RuleFor(x => x.Provider).NotEmpty().WithMessage("Provider is required.");
    }
}

public class UnlinkExternalLoginCommandHandler : IRequestHandler<UnlinkExternalLoginCommand, (bool Success, string[] Errors)>
{
    private readonly IIdentityService _identityService;

    public UnlinkExternalLoginCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<(bool Success, string[] Errors)> Handle(UnlinkExternalLoginCommand request, CancellationToken cancellationToken)
    {
        var validation = new UnlinkExternalLoginCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        return await _identityService.UnlinkExternalLoginAsync(request.UserId, request.Provider, cancellationToken);
    }
}
