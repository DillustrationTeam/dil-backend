using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetClientStatistics;

public record GetClientStatisticsQuery(Guid ClientId) : IRequest<(bool Success, ClientStatisticsDto? Data, string[] Errors)>;

public record ClientStatisticsDto(int CompletedCommissionCount, int DisputeCount);

public class GetClientStatisticsQueryHandler : IRequestHandler<GetClientStatisticsQuery, (bool Success, ClientStatisticsDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public GetClientStatisticsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, ClientStatisticsDto? Data, string[] Errors)> Handle(GetClientStatisticsQuery request, CancellationToken cancellationToken)
    {
        var completed = await _db.Commissions.CountAsync(
            x => x.ClientId == request.ClientId && !x.IsDeleted && x.Status == CommissionStatus.Completed,
            cancellationToken);

        var disputeCount = await _db.Disputes.CountAsync(
            x => x.Commission.ClientId == request.ClientId,
            cancellationToken);

        return (true, new ClientStatisticsDto(completed, disputeCount), Array.Empty<string>());
    }
}
