using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auth.Commands.DeactivateAccount;

public record DeactivateAccountCommand(Guid UserId, string Password) : IRequest<(bool Success, string[] Errors)>;

public class DeactivateAccountCommandValidator : AbstractValidator<DeactivateAccountCommand>
{
    public DeactivateAccountCommandValidator()
    {
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
    }
}

public class DeactivateAccountCommandHandler : IRequestHandler<DeactivateAccountCommand, (bool Success, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public DeactivateAccountCommandHandler(IApplicationDbContext db, IIdentityService identityService, IJwtTokenGenerator jwtTokenGenerator)
    {
        _db = db;
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<(bool Success, string[] Errors)> Handle(DeactivateAccountCommand request, CancellationToken cancellationToken)
    {
        var validation = new DeactivateAccountCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // 1. Không còn commission nào đang chạy (chưa Completed/Cancelled) liên quan tới user này (dù là Client hay Creator).
        var hasActiveCommission = await _db.Commissions.AnyAsync(
            c => (c.ClientId == request.UserId || c.CreatorId == request.UserId)
                && c.Status != CommissionStatus.Completed
                && c.Status != CommissionStatus.Cancelled,
            cancellationToken);
        if (hasActiveCommission)
        {
            return (false, new[] { "You still have an active commission in progress. Please complete or cancel it first." });
        }

        // 2. Ví phải về 0 (cả số dư khả dụng lẫn số dư đang bị giữ).
        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == request.UserId, cancellationToken);
        if (wallet is not null && (wallet.Balance != 0 || wallet.LockedBalance != 0))
        {
            return (false, new[] { "Your wallet still has funds. Please withdraw or settle your balance first." });
        }

        // 3. Không còn dispute nào chưa giải quyết liên quan tới user này.
        var hasUnresolvedDispute = await _db.Disputes.AnyAsync(
            d => d.Status != "Resolved" && (d.RaisedById == request.UserId
                || d.Commission.ClientId == request.UserId
                || d.Commission.CreatorId == request.UserId),
            cancellationToken);
        if (hasUnresolvedDispute)
        {
            return (false, new[] { "You have an unresolved dispute. Please wait until it is resolved first." });
        }

        // 4. Xác thực mật khẩu thật (bắt buộc dù FE đã có bước gõ "DELETE" để xác nhận).
        var isPasswordValid = await _identityService.VerifyPasswordAsync(request.UserId, request.Password, cancellationToken);
        if (!isPasswordValid)
        {
            return (false, new[] { "Incorrect password." });
        }

        var (success, errors) = await _identityService.DeactivateAccountAsync(request.UserId, cancellationToken);
        if (!success)
        {
            return (false, errors);
        }

        await _jwtTokenGenerator.RevokeAllTokensExceptAsync(request.UserId, exceptSessionId: null, cancellationToken);

        return (true, Array.Empty<string>());
    }
}
