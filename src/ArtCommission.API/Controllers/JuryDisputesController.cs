using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Dispute.SubmissionDispute.Commands;
using ArtCommission.Application.Dispute.SubmissionDispute.DTOs;
using ArtCommission.Application.Dispute.SubmissionDispute.Queries;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Quản lý khiếu nại bài thi sự kiện dành cho Ban giám khảo (Jury &amp; Head Jury).
/// </summary>
[Authorize]
[Route("api/v1/jury/disputes")]
[Route("api/v1/juries/disputes")]
public class JuryDisputesController : ApiControllerBase
{
    private readonly IApplicationDbContext _db;

    public JuryDisputesController(IApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lấy danh sách khiếu nại bài thi thuộc các sự kiện mà giám khảo đang tham gia chấm điểm.
    /// Hỗ trợ lọc theo EventId, SubmissionId, Status, Category và phân trang.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SubmissionDisputeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetJuryDisputes(
        [FromQuery] Guid? eventId = null,
        [FromQuery] Guid? submissionId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        // Lấy danh sách EventId mà người dùng làm giám khảo
        var juryEventIds = await GetJuryEventIdsAsync(CurrentUserId, cancellationToken);
        bool isAdminOrMod = User.IsInRole(UserRoleNames.Administrator) || User.IsInRole(UserRoleNames.Moderator);

        if (!isAdminOrMod && juryEventIds.Count == 0)
        {
            return OkEnvelope(Array.Empty<SubmissionDisputeDto>(), new
            {
                page,
                pageSize,
                totalCount = 0,
                totalPages = 0
            });
        }

        // Nếu giám khảo truyền eventId cụ thể, kiểm tra quyền giám khảo trên event đó
        if (eventId.HasValue)
        {
            if (!isAdminOrMod && !juryEventIds.Contains(eventId.Value))
            {
                return Forbid();
            }
        }

        var query = new GetSubmissionDisputesQuery(
            EventId: eventId,
            SubmissionId: submissionId,
            Status: status,
            PageNumber: page,
            PageSize: pageSize
        );

        var (items, totalCount) = await Mediator.Send(query, cancellationToken);

        // Nếu không lọc eventId cụ thể và không phải admin, chỉ trả về các bài thi thuộc event mà user làm jury
        if (!isAdminOrMod && !eventId.HasValue)
        {
            items = items.Where(i => juryEventIds.Contains(i.EventId)).ToList();
            totalCount = items.Count;
        }

        return OkEnvelope(items, new
        {
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    /// <summary>
    /// Giám khảo xem chi tiết một vụ khiếu nại bài thi của sự kiện.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SubmissionDisputeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJuryDisputeDetail(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var query = new GetSubmissionDisputeByIdQuery(id);
        var result = await Mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFoundEnvelope("Không tìm thấy vụ khiếu nại bài thi yêu cầu.");
        }

        // Kiểm tra giám khảo có thuộc sự kiện này không
        bool hasAccess = await IsUserJuryOfEventAsync(result.EventId, CurrentUserId, cancellationToken);
        if (!hasAccess)
        {
            return Forbid();
        }

        return OkEnvelope(result);
    }

    /// <summary>
    /// Giám khảo chủ động lập khiếu nại / báo cáo vi phạm đối với một bài dự thi (ví dụ: phát hiện AI Art, đạo nhái, hack vote).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateJuryDispute(
        [FromBody] CreateSubmissionDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var submission = await _db.EventSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return NotFoundEnvelope("Không tìm thấy bài dự thi yêu cầu.");
        }

        // Kiểm tra quyền giám khảo trên sự kiện này
        bool hasAccess = await IsUserJuryOfEventAsync(submission.EventId, CurrentUserId, cancellationToken);
        if (!hasAccess)
        {
            return Forbid();
        }

        var command = new CreateSubmissionDisputeCommand(
            SubmissionId: request.SubmissionId,
            RaisedById: CurrentUserId,
            Reason: request.Reason,
            Description: request.Description,
            EvidenceUrls: request.EvidenceUrls,
            Priority: request.Priority
        );

        var (success, message, disputeId, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message, disputeId });
    }

    /// <summary>
    /// Trưởng ban giám khảo (Head Jury) hoặc Quản trị viên xử lý khiếu nại bài dự thi (Dismiss, Disqualify, RequestReEvaluation).
    /// </summary>
    [HttpPost("{id:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveJuryDispute(
        Guid id,
        [FromBody] ResolveSubmissionDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var dispute = await _db.SubmissionDisputes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (dispute == null)
        {
            return NotFoundEnvelope("Không tìm thấy vụ khiếu nại bài thi yêu cầu.");
        }

        // Chỉ Trưởng ban giám khảo (Head Jury) hoặc Admin/Mod mới có quyền phân xử khiếu nại
        bool isHeadJuryOrAdmin = await IsUserHeadJuryOfEventAsync(dispute.EventId, CurrentUserId, cancellationToken);
        if (!isHeadJuryOrAdmin)
        {
            return Forbid();
        }

        var command = new ResolveSubmissionDisputeCommand(
            DisputeId: id,
            ModeratorId: CurrentUserId,
            Resolution: request.Resolution,
            Note: request.Note
        );

        var (success, message, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message });
    }

    #region Helper Methods
    private async Task<List<Guid>> GetJuryEventIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _db.Juries
            .AsNoTracking()
            .Where(j => !j.IsDeleted && (j.CreatorId == userId || (j.Creator != null && j.Creator.UserId == userId)))
            .Select(j => j.EventId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> IsUserJuryOfEventAsync(Guid eventId, Guid userId, CancellationToken cancellationToken)
    {
        if (User.IsInRole(UserRoleNames.Administrator) || User.IsInRole(UserRoleNames.Moderator))
        {
            return true;
        }

        return await _db.Juries
            .AsNoTracking()
            .AnyAsync(j =>
                j.EventId == eventId &&
                !j.IsDeleted &&
                (j.CreatorId == userId || (j.Creator != null && j.Creator.UserId == userId)),
                cancellationToken);
    }

    private async Task<bool> IsUserHeadJuryOfEventAsync(Guid eventId, Guid userId, CancellationToken cancellationToken)
    {
        if (User.IsInRole(UserRoleNames.Administrator) || User.IsInRole(UserRoleNames.Moderator))
        {
            return true;
        }

        return await _db.Juries
            .AsNoTracking()
            .AnyAsync(j =>
                j.EventId == eventId &&
                j.IsHeadJury &&
                !j.IsDeleted &&
                (j.CreatorId == userId || (j.Creator != null && j.Creator.UserId == userId)),
                cancellationToken);
    }
    #endregion
}
