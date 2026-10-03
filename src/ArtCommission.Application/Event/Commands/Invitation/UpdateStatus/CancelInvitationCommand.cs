using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Validators;
using ArtCommission.Domain.Enums;
using MediatR;

namespace ArtCommission.Application.Event.Commands;

public record CancelInvitationCommand(
    Guid InvitationId,
    Guid AdminId
) : IRequest<(bool Success, InvitationDto? Data, string[] Errors)>;

public class CancelInvitationCommandHandler
    : IRequestHandler<CancelInvitationCommand, (bool Success, InvitationDto? Data, string[] Errors)>
{
    private readonly IMediator _mediator;

    public CancelInvitationCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<(bool Success, InvitationDto? Data, string[] Errors)> Handle(
        CancelInvitationCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new CancelInvitationCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        return await _mediator.Send(
            new UpdateInvitationStatusCommand(request.InvitationId, request.AdminId, InvitationStatus.Canceled, IsAdmin: true),
            cancellationToken);
    }
}
