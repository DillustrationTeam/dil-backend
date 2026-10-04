using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Validators;
using ArtCommission.Domain.Enums;
using MediatR;

namespace ArtCommission.Application.Event.Commands;

public record DeclineInvitationCommand(
    Guid InvitationId,
    Guid UserId
) : IRequest<(bool Success, InvitationDto? Data, string[] Errors)>;

public class DeclineInvitationCommandHandler
    : IRequestHandler<DeclineInvitationCommand, (bool Success, InvitationDto? Data, string[] Errors)>
{
    private readonly IMediator _mediator;

    public DeclineInvitationCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<(bool Success, InvitationDto? Data, string[] Errors)> Handle(
        DeclineInvitationCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new DeclineInvitationCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        return await _mediator.Send(
            new UpdateInvitationStatusCommand(request.InvitationId, request.UserId, InvitationStatus.Declined),
            cancellationToken);
    }
}
