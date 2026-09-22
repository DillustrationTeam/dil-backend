using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.CreatorApplication.Commands;

public record WithdrawCreatorApplicationCommand(
    Guid ApplicantId
) : IRequest<(bool Success, string[] Errors)>;

public class WithdrawCreatorApplicationCommandHandler
    : IRequestHandler<WithdrawCreatorApplicationCommand, (bool Success, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public WithdrawCreatorApplicationCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string[] Errors)> Handle(
        WithdrawCreatorApplicationCommand request,
        CancellationToken cancellationToken)
    {
        var application = await _db.CreatorApplications
            .Where(c => c.ApplicantId == request.ApplicantId
                && !c.IsDeleted
                && (c.Status == ApplicationStatus.Pending || c.Status == ApplicationStatus.AdditionalProofRequested))
            .OrderByDescending(c => c.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (application == null)
        {
            return (false, new[] { "No withdrawable application found." });
        }

        application.IsDeleted = true;
        application.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>());
    }
}
