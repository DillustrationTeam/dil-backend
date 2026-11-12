using ArtCommission.Application.Commission.Interfaces;

namespace ArtCommission.API.BackgroundWorkers;

/// <summary>Auto-approves submitted milestones that have waited past the 72-hour client review window.</summary>
public sealed class MilestoneAutoAcceptJob
{
    private readonly ICommissionService _commissionService;

    public MilestoneAutoAcceptJob(ICommissionService commissionService)
    {
        _commissionService = commissionService;
    }

    public Task RunAsync(CancellationToken cancellationToken) =>
        _commissionService.ProcessMilestoneAutoAcceptDeadlinesAsync(cancellationToken);
}
