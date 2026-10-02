using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.LinkExternalLogin;

public record LinkExternalLoginCommand(Guid UserId, string Provider, string AccessToken) : IRequest<(bool Success, string[] Errors)>;

public class LinkExternalLoginCommandValidator : AbstractValidator<LinkExternalLoginCommand>
{
    public LinkExternalLoginCommandValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty().WithMessage("Access token is required.");
    }
}

public class LinkExternalLoginCommandHandler : IRequestHandler<LinkExternalLoginCommand, (bool Success, string[] Errors)>
{
    private readonly IGoogleUserInfoService _googleUserInfoService;
    private readonly IIdentityService _identityService;

    public LinkExternalLoginCommandHandler(IGoogleUserInfoService googleUserInfoService, IIdentityService identityService)
    {
        _googleUserInfoService = googleUserInfoService;
        _identityService = identityService;
    }

    public async Task<(bool Success, string[] Errors)> Handle(LinkExternalLoginCommand request, CancellationToken cancellationToken)
    {
        var validation = new LinkExternalLoginCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // Hiện chỉ hỗ trợ Google — các provider khác chưa có xác thực token tương ứng.
        if (!string.Equals(request.Provider, "Google", StringComparison.OrdinalIgnoreCase))
        {
            return (false, new[] { "Unsupported provider." });
        }

        var googleUser = await _googleUserInfoService.GetUserInfoAsync(request.AccessToken, cancellationToken);
        if (googleUser == null)
        {
            return (false, new[] { "Google verification failed. Please try again." });
        }

        return await _identityService.LinkExternalLoginAsync(request.UserId, "Google", googleUser.Sub, cancellationToken);
    }
}
