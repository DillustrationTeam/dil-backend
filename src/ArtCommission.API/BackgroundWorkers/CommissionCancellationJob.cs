using ArtCommission.Application.Commission.Interfaces;

namespace ArtCommission.API.BackgroundWorkers;

/// <summary>Processes expired cancellation requests and overdue grace periods idempotently.</summary>
public sealed class CommissionCancellationJob
{
    private readonly ICommissionService _commissionService;

    public CommissionCancellationJob(ICommissionService commissionService)
    {
        _commissionService = commissionService;
    }

    public Task RunAsync(CancellationToken cancellationToken) =>
        _commissionService.ProcessCancellationDeadlinesAsync(cancellationToken);
}
