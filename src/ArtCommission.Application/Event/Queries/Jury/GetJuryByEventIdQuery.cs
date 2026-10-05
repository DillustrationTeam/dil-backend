using ArtCommission.Application.Event.DTOs;
using MediatR;

namespace ArtCommission.Application.Event.Queries;

public record GetJuryByEventIdQuery(
    Guid EventId,
    string? Role = null,
    bool? IsHeadJury = null,
    string? Name = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<(IReadOnlyList<JuryDto> Items, int TotalCount)>;

public class GetJuryByEventIdQueryHandler
    : IRequestHandler<GetJuryByEventIdQuery, (IReadOnlyList<JuryDto> Items, int TotalCount)>
{
    private readonly IMediator _mediator;

    public GetJuryByEventIdQueryHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public Task<(IReadOnlyList<JuryDto> Items, int TotalCount)> Handle(
        GetJuryByEventIdQuery request,
        CancellationToken cancellationToken)
    {
        return _mediator.Send(new GetJuriesQuery(
            EventId: request.EventId,
            Role: request.Role,
            IsHeadJury: request.IsHeadJury,
            Name: request.Name,
            Search: request.Search,
            Page: request.Page,
            PageSize: request.PageSize
        ), cancellationToken);
    }
}

