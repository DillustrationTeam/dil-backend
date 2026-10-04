using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetInvitationByIdQuery(
    Guid InvitationId
) : IRequest<InvitationDto?>;

public class GetInvitationByIdQueryHandler 
    : IRequestHandler<GetInvitationByIdQuery, InvitationDto?>
{
    private readonly IApplicationDbContext _db;

    public GetInvitationByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<InvitationDto?> Handle(
        GetInvitationByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _db.Invitations
            .AsNoTracking()
            .Where(i => i.Id == request.InvitationId && !i.IsDeleted)
            .Select(i => new InvitationDto
            {
                Id = i.Id,
                EventId = i.EventId,
                EventTitle = i.Event != null ? i.Event.Title : null,
                EventBannerUrl = i.Event != null ? i.Event.BannerUrl : null,
                SentFromAdminId = i.SentFromAdminId,
                SentFromAdminName = i.SentFromAdmin != null ? i.SentFromAdmin.FullName : null,
                SentToCreatorId = i.SentToCreatorId,
                SentToCreatorName = i.SentToCreator != null 
                    ? (i.SentToCreator.User != null && !string.IsNullOrEmpty(i.SentToCreator.User.FullName) 
                        ? i.SentToCreator.User.FullName 
                        : i.SentToCreator.DisplayName) 
                    : null,
                SentToCreatorEmail = i.SentToCreator != null && i.SentToCreator.User != null 
                    ? i.SentToCreator.User.Email 
                    : null,
                SentToCreatorAvatarUrl = i.SentToCreator != null ? i.SentToCreator.BannerUrl : null,
                IsHeadJury = i.IsHeadJury,
                Status = i.Status.ToString(),
                CreatedAt = i.CreatedAt,
                RespondedAt = i.RespondedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
