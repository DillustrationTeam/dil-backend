using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.Disable2FA;

public record Disable2FACommand(Guid UserId, string Password) : IRequest<(bool Success, string[] Errors)>;

public class Disable2FACommandValidator : AbstractValidator<Disable2FACommand>
{
    public Disable2FACommandValidator()
    {
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
    }
}

public class Disable2FACommandHandler : IRequestHandler<Disable2FACommand, (bool Success, string[] Errors)>
{
    private readonly IIdentityService _identityService;

    public Disable2FACommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<(bool Success, string[] Errors)> Handle(Disable2FACommand request, CancellationToken cancellationToken)
    {
        var validation = new Disable2FACommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var passwordValid = await _identityService.VerifyPasswordAsync(request.UserId, request.Password, cancellationToken);
        if (!passwordValid)
        {
            return (false, new[] { "Incorrect password." });
        }

        return await _identityService.Disable2FAAsync(request.UserId, cancellationToken);
    }
}
