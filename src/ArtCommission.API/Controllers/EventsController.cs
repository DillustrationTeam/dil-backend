using ArtCommission.Application.Event.Commands;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Queries;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Quản lý sự kiện / cuộc thi vẽ (Platform Events) và gửi lời mời giám khảo cho sự kiện.
/// </summary>
[Route("api/v1/events")]
public class EventsController : ApiControllerBase
{
    private const string AdminOrModRoles = $"{UserRoleNames.Administrator},{UserRoleNames.Moderator}";

    /// <summary>
    /// Lấy danh sách sự kiện (Công khai).
    /// Hỗ trợ tìm kiếm theo tiêu đề/từ khóa, lọc theo trạng thái (Draft, Open, Judging, Ended),
    /// số lượng bài nộp tối thiểu/tối đa, sắp xếp và phân trang.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? title = null,
        [FromQuery] string? search = null,
        [FromQuery] EventStatus? status = null,
        [FromQuery] int? minSubmissions = null,
        [FromQuery] int? maxSubmissions = null,
        [FromQuery] bool? isFeatured = null,
        [FromQuery] string? sortBy = "latest",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetEventsQuery(
            Title: title,
            Search: search,
            Status: status,
            MinSubmissions: minSubmissions,
            MaxSubmissions: maxSubmissions,
            IsFeatured: isFeatured,
            SortBy: sortBy,
            Page: page,
            PageSize: pageSize
        );

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
    /// Lấy thống kê tổng quan của các sự kiện trên toàn hệ thống (Tổng giải thưởng, số sự kiện đang mở, tổng bài nộp).
    /// </summary>
    [HttpGet("stats")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEventStats(CancellationToken cancellationToken = default)
    {
        var query = new GetEventStatsQuery();
        var result = await Mediator.Send(query, cancellationToken);
        return OkEnvelope(result);
    }

    /// <summary>
    /// Lấy chi tiết một sự kiện theo Id (Công khai).
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetEventByIdQuery(id);
        var result = await Mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFoundEnvelope("Không tìm thấy sự kiện hoặc sự kiện đã bị xóa.");
        }

        return OkEnvelope(result);
    }

    /// <summary>
    /// Lấy bảng xếp hạng (Leaderboard) của sự kiện.
    /// Hỗ trợ xếp hạng theo số lượt bình chọn (votes) hoặc điểm ban giám khảo (score) và phân trang.
    /// </summary>
    [HttpGet("{id:guid}/leaderboard")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventLeaderboard(
        Guid id,
        [FromQuery] string? sortBy = "votes",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetEventLeaderboardQuery(
            EventId: id,
            SortBy: sortBy,
            Page: page,
            PageSize: pageSize
        );

        var (items, totalCount, eventTitle) = await Mediator.Send(query, cancellationToken);
        if (eventTitle == null)
        {
            return NotFoundEnvelope("Không tìm thấy sự kiện hoặc sự kiện đã bị xóa.");
        }

        return OkEnvelope(items, new
        {
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            eventId = id,
            eventTitle,
            sortBy
        });
    }


    /// <summary>
    /// Tạo mới một sự kiện (Dành cho Quản trị viên &amp; Kiểm duyệt viên).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AdminOrModRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateEvent(
        [FromBody] EventRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new CreateEventCommand(
            Title: dto.Title,
            Description: dto.Description,
            BannerUrl: dto.BannerUrl,
            Rules: dto.Rules,
            Prize: dto.Prize,
            MaxVote: dto.MaxVote,
            Status: dto.Status,
            SubmissionStartAt: dto.SubmissionStartAt,
            SubmissionEndAt: dto.SubmissionEndAt,
            JudgingStartAt: dto.JudgingStartAt,
            JudgingEndAt: dto.JudgingEndAt,
            VotingStartAt: dto.VotingStartAt,
            VotingEndAt: dto.VotingEndAt,
            ResultAnnouncementAt: dto.ResultAnnouncementAt,
            AdminId: CurrentUserId,
            IsFeatured: dto.IsFeatured
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Cập nhật thông tin sự kiện (Dành cho Quản trị viên &amp; Kiểm duyệt viên).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = AdminOrModRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateEvent(
        Guid id,
        [FromBody] EventRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new UpdateEventCommand(
            EventId: id,
            Title: dto.Title,
            Description: dto.Description,
            BannerUrl: dto.BannerUrl,
            Rules: dto.Rules,
            Prize: dto.Prize,
            MaxVote: dto.MaxVote,
            Status: dto.Status,
            SubmissionStartAt: dto.SubmissionStartAt,
            SubmissionEndAt: dto.SubmissionEndAt,
            JudgingStartAt: dto.JudgingStartAt,
            JudgingEndAt: dto.JudgingEndAt,
            VotingStartAt: dto.VotingStartAt,
            VotingEndAt: dto.VotingEndAt,
            ResultAnnouncementAt: dto.ResultAnnouncementAt,
            AdminId: CurrentUserId,
            IsFeatured: dto.IsFeatured
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Xóa sự kiện (Soft delete) (Dành cho Quản trị viên &amp; Kiểm duyệt viên).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AdminOrModRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteEvent(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new DeleteEventCommand(
            EventId: id,
            AdminId: CurrentUserId
        );

        var (success, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "Sự kiện đã được xóa thành công." });
    }

    /// <summary>
    /// Gửi lời mời làm giám khảo cho một sự kiện (Dành cho Quản trị viên &amp; Kiểm duyệt viên).
    /// </summary>
    [HttpPost("{id:guid}/invitations")]
    [Authorize(Roles = AdminOrModRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InviteJury(
        Guid id,
        [FromBody] InviteJuryDto dto,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new SendInvitationCommand(
            eventId: id,
            adminId: CurrentUserId,
            dto: dto
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Lấy danh sách các lời mời ban giám khảo trong sự kiện (Dành cho Quản trị viên &amp; Kiểm duyệt viên).
    /// </summary>
    [HttpGet("{id:guid}/invitations")]
    [Authorize(Roles = AdminOrModRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEventInvitations(
        Guid id,
        [FromQuery] InvitationStatus? status = null,
        [FromQuery] bool? isHeadJury = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetInvitationsQuery(
            EventId: id,
            Status: status,
            IsHeadJury: isHeadJury,
            Page: page,
            PageSize: pageSize
        );

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
    /// Lấy danh sách bài nộp của một sự kiện (Công khai).
    /// Hỗ trợ tìm kiếm theo tiêu đề, người nộp (tên hoặc username), lọc AI scan, sắp xếp (ngày, tiêu đề, vote count, score) và phân trang.
    /// </summary>
    [HttpGet("{id:guid}/submissions")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEventSubmissions(
        Guid id,
        [FromQuery] string? title = null,
        [FromQuery] string? submitterName = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? aiScanPassed = null,
        [FromQuery] string? sortBy = "date",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSubmissionsByEventIdQuery(
            EventId: id,
            Title: title,
            SubmitterName: submitterName,
            Search: search,
            AiScanPassed: aiScanPassed,
            SortBy: sortBy,
            Page: page,
            PageSize: pageSize
        );

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
    /// Nộp bài dự thi cho sự kiện (Hỗ trợ chọn tranh có sẵn hoặc upload tranh mới).
    /// </summary>
    [HttpPost("{id:guid}/submissions")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadEventSubmission(
        Guid id,
        [FromBody] UploadSubmissionDto dto,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new UploadSubmissionCommand(
            EventId: id,
            SubmitterId: CurrentUserId,
            ArtworkId: dto.ArtworkId,
            Title: dto.Title,
            Description: dto.Description,
            ImageUrl: dto.ImageUrl,
            ThumbnailUrl: dto.ThumbnailUrl,
            IsAiGenerated: dto.IsAiGenerated,
            AiDetectionScore: dto.AiDetectionScore,
            Tags: dto.Tags
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Chỉnh sửa bài dự thi sự kiện (Dành cho Creator cập nhật bài thi của mình).
    /// </summary>
    [HttpPut("{id:guid}/submissions/{submissionId:guid}")]
    [HttpPut("submissions/{submissionId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateEventSubmission(
        Guid? id,
        Guid submissionId,
        [FromBody] UpdateSubmissionDto dto,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new UpdateSubmissionCommand(
            SubmissionId: submissionId,
            UserId: CurrentUserId,
            Title: dto.Title,
            Description: dto.Description,
            ArtworkId: dto.ArtworkId,
            ImageUrl: dto.ImageUrl,
            ThumbnailUrl: dto.ThumbnailUrl,
            IsAiGenerated: dto.IsAiGenerated,
            AiDetectionScore: dto.AiDetectionScore,
            Tags: dto.Tags
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Lấy danh sách bài nộp sự kiện trên toàn hệ thống (Công khai).
    /// </summary>
    [HttpGet("submissions")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubmissions(
        [FromQuery] Guid? submitterId = null,
        [FromQuery] string? title = null,
        [FromQuery] string? submitterName = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? aiScanPassed = null,
        [FromQuery] string? sortBy = "date",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSubmissionsQuery(
            SubmitterId: submitterId,
            Title: title,
            SubmitterName: submitterName,
            Search: search,
            AiScanPassed: aiScanPassed,
            SortBy: sortBy,
            Page: page,
            PageSize: pageSize
        );

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
    /// Lấy chi tiết một bài nộp sự kiện theo Id (Công khai).
    /// </summary>
    [HttpGet("submissions/{submissionId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubmissionById(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSubmissionByIdQuery(submissionId);
        var result = await Mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFoundEnvelope("Không tìm thấy bài nộp hoặc sự kiện đã bị xóa.");
        }

        return OkEnvelope(result);
    }

    /// <summary>
    /// Lấy danh sách ban giám khảo của sự kiện (Hỗ trợ lọc theo Role, IsHeadJury, tìm kiếm theo Name/Search).
    /// </summary>
    [HttpGet("{id:guid}/juries")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEventJuries(
        Guid id,
        [FromQuery] string? role = null,
        [FromQuery] bool? isHeadJury = null,
        [FromQuery] string? name = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetJuryByEventIdQuery(
            EventId: id,
            Role: role,
            IsHeadJury: isHeadJury,
            Name: name,
            Search: search,
            Page: page,
            PageSize: pageSize
        );

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
    /// Thêm giám khảo vào sự kiện (Dành cho Quản trị viên &amp; Kiểm duyệt viên).
    /// Điều kiện bắt buộc: Giám khảo được thêm phải có vai trò CREATOR.
    /// </summary>
    [HttpPost("{id:guid}/juries")]
    [Authorize(Roles = AdminOrModRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddEventJury(
        Guid id,
        [FromBody] AddJuryDto dto,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new AddJuryCommand(
            EventId: id,
            CreatorId: dto.CreatorId,
            AdminId: CurrentUserId,
            IsHeadJury: dto.IsHeadJury
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Xóa giám khảo khỏi sự kiện (Dành cho Quản trị viên &amp; Kiểm duyệt viên).
    /// </summary>
    [HttpDelete("{id:guid}/juries/{creatorId:guid}")]
    [Authorize(Roles = AdminOrModRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveEventJury(
        Guid id,
        Guid creatorId,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = RemoveJuryCommand.ByEventAndCreator(id, creatorId, CurrentUserId);

        var (success, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "Đã xóa giám khảo khỏi sự kiện thành công." });
    }

    /// <summary>
    /// Lấy danh sách tiêu chí chấm điểm (Rubric) của sự kiện.
    /// </summary>
    [HttpGet("{id:guid}/criteria")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEventCriteria(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetEventCriteriaQuery(id);
        var result = await Mediator.Send(query, cancellationToken);
        return OkEnvelope(result);
    }
}

