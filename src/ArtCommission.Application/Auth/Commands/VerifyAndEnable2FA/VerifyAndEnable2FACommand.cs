using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.VerifyAndEnable2FA;

public record VerifyAndEnable2FACommand(Guid UserId, string Code) : IRequest<(bool Success, string[] RecoveryCodes, string[] Errors)>;

public class VerifyAndEnable2FACommandValidator : AbstractValidator<VerifyAndEnable2FACommand>
{
    public VerifyAndEnable2FACommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Verification code is required.");
    }
}

public class VerifyAndEnable2FACommandHandler : IRequestHandler<VerifyAndEnable2FACommand, (bool Success, string[] RecoveryCodes, string[] Errors)>
{
    private readonly IIdentityService _identityService;

    public VerifyAndEnable2FACommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<(bool Success, string[] RecoveryCodes, string[] Errors)> Handle(VerifyAndEnable2FACommand request, CancellationToken cancellationToken)
    {
        var validation = new VerifyAndEnable2FACommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, Array.Empty<string>(), validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        return await _identityService.VerifyAndEnable2FAAsync(request.UserId, request.Code, cancellationToken);
    }
}
