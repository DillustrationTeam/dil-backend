using System.Security.Claims;
using ArtCommission.API.Common;
using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers.v1;

[ApiController]
[Authorize]
[Route("api/v1/commissions")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public class CommissionsController : ControllerBase
{
    private readonly ICommissionService _commissionService;

    public CommissionsController(ICommissionService commissionService)
    {
        _commissionService = commissionService;
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdStr, out var id) && id != Guid.Empty) return id;
        throw new UnauthorizedAccessException("A valid user identity is required.");
    }

    /// <summary>
    /// POST /api/v1/commissions
    /// Client tạo và gửi Yêu cầu Đặt vẽ (Brief) mới
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> CreateCommission([FromBody] CreateCommissionRequest request, CancellationToken ct)
    {
        var clientId = GetCurrentUserId();
        if (request.CreatorId == Guid.Empty)
            throw new ArgumentException("CreatorId is required.");
        var result = await _commissionService.CreateCommissionAsync(request, clientId, ct);
        return Ok(new ApiResponse<CommissionDto>(result));
    }

    /// <summary>
    /// GET /api/v1/commissions
    /// Lấy danh sách các đơn Commission đặt vẽ
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCommissions([FromQuery] string? status, [FromQuery] string? role, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            throw new ArgumentException("Page must be at least 1 and pageSize must be between 1 and 100.");
        if (role is not null && !string.Equals(role, "Client", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(role, "Creator", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Role must be Client or Creator.");
        var userId = GetCurrentUserId();
        var (items, totalItems) = await _commissionService.GetCommissionsAsync(status, role, userId, page, pageSize, ct);
        var meta = new { page, pageSize, totalItems, totalPages = (int)Math.Ceiling(totalItems / (double)pageSize) };
        return Ok(new ApiResponse<List<CommissionDto>>(items, meta));
    }

    /// <summary>
    /// GET /api/v1/commissions/{id}
    /// Xem chi tiết nội dung Commission Brief & Milestones
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCommissionById(Guid id, CancellationToken ct)
    {
        var result = await _commissionService.GetCommissionByIdAsync(id, GetCurrentUserId(), ct);
if (result == null) return NotFound(ApiErrors.Create(404, "Not Found", "Không tìm thấy đơn vẽ.", HttpContext.TraceIdentifier));
        return Ok(new ApiResponse<CommissionDetailDto>(result));
    }

    /// <summary>
    /// PATCH /api/v1/commissions/{id}/respond
    /// Creator tiếp nhận Yêu cầu Đặt vẽ (Accept/Reject/Negotiate)
    /// </summary>
    [HttpPatch("{id:guid}/respond")]
    [Authorize(Roles = "Creator")]
    public async Task<IActionResult> RespondCommission(Guid id, [FromBody] RespondCommissionRequest request, CancellationToken ct)
    {
        var creatorId = GetCurrentUserId();
        var result = await _commissionService.RespondCommissionAsync(id, request, creatorId, ct);
        return Ok(new ApiResponse<CommissionDto>(result));
    }

    /// <summary>
    /// POST /api/v1/commissions/{id}/escrow/deposit
    /// Client đặt cọc nạp tiền ký quỹ Escrow
    /// </summary>
    [HttpPost("{id:guid}/escrow/deposit")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> DepositEscrow(Guid id, [FromQuery] string paymentMethod = "Wallet", CancellationToken ct = default)
    {
        var clientId = GetCurrentUserId();
        var result = await _commissionService.DepositEscrowAsync(id, clientId, paymentMethod, ct);
        return Ok(new ApiResponse<CommissionDto>(result));
    }

    /// <summary>
    /// POST /api/v1/commissions/{commissionId}/milestones/{milestoneId}/submit
    /// Creator nộp sản phẩm bản phác thảo/lineart (WIP)
    /// </summary>
    [HttpPost("{commissionId:guid}/milestones/{milestoneId:guid}/submit")]
    [Authorize(Roles = "Creator")]
    public async Task<IActionResult> SubmitMilestoneWip(Guid commissionId, Guid milestoneId, [FromBody] SubmitMilestoneRequest request, CancellationToken ct)
    {
        var creatorId = GetCurrentUserId();
        var result = await _commissionService.SubmitMilestoneWipAsync(commissionId, milestoneId, request, creatorId, ct);
        return Ok(new ApiResponse<MilestoneDto>(result));
    }

    /// <summary>
    /// PATCH /api/v1/commissions/{commissionId}/milestones/{milestoneId}/approve
    /// Client phê duyệt cột mốc sản phẩm đã nộp & giải ngân
    /// </summary>
    [HttpPatch("{commissionId:guid}/milestones/{milestoneId:guid}/approve")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> ApproveMilestone(Guid commissionId, Guid milestoneId, CancellationToken ct)
    {
        var clientId = GetCurrentUserId();
        var result = await _commissionService.ApproveMilestoneAsync(commissionId, milestoneId, clientId, ct);
        return Ok(new ApiResponse<MilestoneDto>(result));
    }

    /// <summary>
    /// POST /api/v1/commissions/{commissionId}/milestones/{milestoneId}/request-revision
    /// Client gửi yêu cầu sửa đổi (Revision) cho cột mốc tiến độ
    /// </summary>
    [HttpPost("{commissionId:guid}/milestones/{milestoneId:guid}/request-revision")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> RequestMilestoneRevision(Guid commissionId, Guid milestoneId, [FromBody] RequestRevisionRequest request, CancellationToken ct)
    {
        var clientId = GetCurrentUserId();
        var result = await _commissionService.RequestMilestoneRevisionAsync(commissionId, milestoneId, request, clientId, ct);
        return Ok(new ApiResponse<MilestoneDto>(result));
    }

    /// <summary>
    /// POST /api/v1/commissions/{id}/deliver
    /// Creator bàn giao sản phẩm gốc hoàn chỉnh file HD lên Cloud Storage
    /// </summary>
    [HttpPost("{id:guid}/deliver")]
    [Authorize(Roles = "Creator")]
    public async Task<IActionResult> DeliverFinalWork(Guid id, [FromQuery] string finalDeliverableUrl, CancellationToken ct)
    {
        var creatorId = GetCurrentUserId();
        var result = await _commissionService.DeliverFinalWorkAsync(id, finalDeliverableUrl, creatorId, ct);
        return Ok(new ApiResponse<CommissionDto>(result));
    }

    /// <summary>
    /// PATCH /api/v1/commissions/{id}/complete
    /// Client nghiệm thu sản phẩm cuối cùng, hoàn tất đơn hàng
    /// </summary>
    [HttpPatch("{id:guid}/complete")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> CompleteCommission(Guid id, CancellationToken ct)
    {
        var clientId = GetCurrentUserId();
        var (commission, presignedUrl) = await _commissionService.CompleteCommissionAsync(id, clientId, ct);
        var meta = new { downloadPresignedUrl = presignedUrl };
        return Ok(new ApiResponse<CommissionDto>(commission, meta));
    }

    /// <summary>
    /// POST /api/v1/commissions/{id}/cancel
    /// Hủy đơn hàng Commission
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelCommission(Guid id, [FromQuery] string reason, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _commissionService.CancelCommissionAsync(id, reason, userId, ct);
        return Ok(new ApiResponse<CommissionDto>(result));
    }

    /// <summary>
    /// POST /api/v1/commissions/{id}/disputes
    /// Mở tranh chấp/khiếu nại đơn hàng
    /// </summary>
    [HttpPost("{id:guid}/disputes")]
    public async Task<IActionResult> CreateDispute(Guid id, [FromBody] CreateDisputeRequest request, CancellationToken ct)
    {
        var raisedById = GetCurrentUserId();
        var result = await _commissionService.CreateDisputeAsync(id, request, raisedById, ct);
        return Ok(new ApiResponse<DisputeDto>(result));
    }

    /// <summary>
    /// GET /api/v1/commissions/{id}/disputes
    /// Xem thông tin chi tiết tranh chấp của đơn hàng
    /// </summary>
    [HttpGet("{id:guid}/disputes")]
    public async Task<IActionResult> GetDisputeByCommissionId(Guid id, CancellationToken ct)
    {
        var result = await _commissionService.GetDisputeByCommissionIdAsync(id, GetCurrentUserId(), ct);
if (result == null) return NotFound(ApiErrors.Create(404, "Not Found", "Chưa có tranh chấp cho đơn hàng này.", HttpContext.TraceIdentifier));
        return Ok(new ApiResponse<DisputeDto>(result));
    }

    /// <summary>
    /// POST /api/v1/commissions/{id}/reviews
    /// Client đánh giá số sao (1-5★) và nhận xét dịch vụ
    /// </summary>
    [HttpPost("{id:guid}/reviews")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> CreateReview(Guid id, [FromBody] CreateReviewRequest request, CancellationToken ct)
    {
        var reviewerId = GetCurrentUserId();
        var result = await _commissionService.CreateReviewAsync(id, request, reviewerId, ct);
        return Ok(new ApiResponse<ReviewDto>(result));
    }

    /// <summary>
    /// POST /api/v1/commissions/{id}/reviews/reply
    /// Creator đăng phản hồi đối với đánh giá của Client
    /// </summary>
    [HttpPost("{id:guid}/reviews/reply")]
    [Authorize(Roles = "Creator")]
    public async Task<IActionResult> ReplyReview(Guid id, [FromBody] ReplyReviewRequest request, CancellationToken ct)
    {
        var creatorId = GetCurrentUserId();
        var result = await _commissionService.ReplyReviewAsync(id, request.ReplyComment, creatorId, ct);
        return Ok(new ApiResponse<ReviewDto>(result));
    }
}
