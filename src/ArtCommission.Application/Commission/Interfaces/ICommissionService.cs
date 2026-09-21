using ArtCommission.Application.Commission.DTOs;

namespace ArtCommission.Application.Commission.Interfaces;

public interface ICommissionService
{
    Task<CommissionDto> CreateCommissionAsync(CreateCommissionRequest request, Guid clientId, CancellationToken cancellationToken = default);
    Task<(List<CommissionDto> Items, int TotalItems)> GetCommissionsAsync(string? status, string? role, Guid userId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<CommissionDetailDto?> GetCommissionByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<CommissionDto> RespondCommissionAsync(Guid id, RespondCommissionRequest request, Guid creatorId, CancellationToken cancellationToken = default);
    Task<CommissionDto> RespondToCounterofferAsync(Guid id, bool accept, Guid clientId, CancellationToken cancellationToken = default);
    Task<CommissionDto> DepositEscrowAsync(Guid id, Guid clientId, string paymentMethod, CancellationToken cancellationToken = default);
    Task<MilestoneDto> SubmitMilestoneWipAsync(Guid commissionId, Guid milestoneId, SubmitMilestoneRequest request, Guid creatorId, CancellationToken cancellationToken = default);
    Task<MilestoneDto> ApproveMilestoneAsync(Guid commissionId, Guid milestoneId, Guid clientId, CancellationToken cancellationToken = default);
    Task<MilestoneDto> RequestMilestoneRevisionAsync(Guid commissionId, Guid milestoneId, RequestRevisionRequest request, Guid clientId, CancellationToken cancellationToken = default);
    Task<CommissionDto> DeliverFinalWorkAsync(Guid id, string finalDeliverableUrl, Guid creatorId, CancellationToken cancellationToken = default);
    Task<(CommissionDto Commission, string DownloadPresignedUrl)> CompleteCommissionAsync(Guid id, Guid clientId, CancellationToken cancellationToken = default);
    Task<CommissionDto> CancelCommissionAsync(Guid id, string reason, Guid userId, CancellationToken cancellationToken = default);
    Task<DisputeDto> CreateDisputeAsync(Guid id, CreateDisputeRequest request, Guid raisedById, CancellationToken cancellationToken = default);
    Task<DisputeDto?> GetDisputeByCommissionIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<ReviewDto> CreateReviewAsync(Guid id, CreateReviewRequest request, Guid reviewerId, CancellationToken cancellationToken = default);
    Task<ReviewDto> ReplyReviewAsync(Guid id, string replyComment, Guid creatorId, CancellationToken cancellationToken = default);
}
