using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Validators;
using ArtCommission.Domain.Enums;
using MediatR;

namespace ArtCommission.Application.Event.Commands;

public record AcceptInvitationCommand(
    Guid InvitationId,
    Guid UserId
) : IRequest<(bool Success, InvitationDto? Data, string[] Errors)>;

public class AcceptInvitationCommandHandler
    : IRequestHandler<AcceptInvitationCommand, (bool Success, InvitationDto? Data, string[] Errors)>
{
    private readonly IMediator _mediator;

    public AcceptInvitationCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<(bool Success, InvitationDto? Data, string[] Errors)> Handle(
        AcceptInvitationCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new AcceptInvitationCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        return await _mediator.Send(
            new UpdateInvitationStatusCommand(request.InvitationId, request.UserId, InvitationStatus.Accepted),
            cancellationToken);
    }
}
