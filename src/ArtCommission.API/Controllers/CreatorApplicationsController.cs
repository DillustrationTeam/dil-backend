using ArtCommission.Application.CreatorApplication.Commands;
using ArtCommission.Application.CreatorApplication.DTOs;
using ArtCommission.Application.CreatorApplication.Queries;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Quản lý việc nộp và duyệt hồ sơ trở thành Creator (UC29).
/// </summary>
[Authorize]
[Route("api/v1/creator-applications")]
public class CreatorApplicationsController : ApiControllerBase
{
    private const string ModOrAdminRoles = $"{UserRoleNames.Moderator},{UserRoleNames.Administrator}";

    /// <summary>
    /// Nộp đơn đăng ký trở thành Creator (Dành cho User/Client).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SubmitApplication(
        [FromBody] SubmitCreatorApplicationDto request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new SubmitCreatorApplicationCommand(
            ApplicantId: CurrentUserId,
            PortfolioLinks: request.PortfolioLinks,
            SocialLinks: request.SocialLinks,
            IdProofUrl: request.IdProofUrl
        );

        var (success, applicationId, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { applicationId });
    }

    /// <summary>
    /// Lấy danh sách các đơn đăng ký (Dành cho Moderator và Administrator).
    /// Hỗ trợ lọc theo trạng thái (Pending, Approved, Rejected) và phân trang.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = ModOrAdminRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetApplications(
        [FromQuery] ApplicationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetCreatorApplicationsQuery(status, page, pageSize);
        var (items, totalCount) = await Mediator.Send(query, cancellationToken);

        return OkEnvelope(items, new
        {
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    /// <summary>
    /// Xem chi tiết một đơn đăng ký theo Id (Dành cho Moderator và Administrator).
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = ModOrAdminRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApplicationById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetCreatorApplicationByIdQuery(id);
        var application = await Mediator.Send(query, cancellationToken);

        if (application == null)
        {
            return NotFoundEnvelope("Creator application not found.");
        }

        return OkEnvelope(application);
    }

    /// <summary>
    /// Phê duyệt hoặc Từ chối đơn đăng ký (Dành cho Moderator và Administrator).
    /// Khi duyệt thành công, hệ thống tự động cấp role Creator và khởi tạo CreatorProfile.
    /// </summary>
    [HttpPut("{id:guid}/review")]
    [Authorize(Roles = ModOrAdminRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReviewApplication(
        Guid id,
        [FromBody] ReviewCreatorApplicationDto request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new ReviewCreatorApplicationCommand(
            ApplicationId: id,
            ModeratorId: CurrentUserId,
            Status: request.Status,
            ReviewNote: request.ReviewNote
        );

        var (success, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "Application reviewed successfully." });
    }
}
