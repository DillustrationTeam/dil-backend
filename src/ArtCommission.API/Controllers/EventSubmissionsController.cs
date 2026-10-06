using ArtCommission.Application.Event.Commands;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Quản lý và tra cứu bài nộp sự kiện (Event Submissions).
/// </summary>
[Route("api/v1/event-submissions")]
public class EventSubmissionsController : ApiControllerBase
{
    /// <summary>
    /// Nộp bài dự thi sự kiện (Hỗ trợ nộp tranh có sẵn trong kho hoặc upload tranh mới).
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadSubmission(
        [FromQuery] Guid? eventId,
        [FromBody] UploadSubmissionDto dto,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var targetEventId = eventId ?? dto.EventId ?? Guid.Empty;
        var command = new UploadSubmissionCommand(
            EventId: targetEventId,
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
    /// Chỉnh sửa bài dự thi (Dành riêng cho Creator/Tác giả nộp bài, trong thời hạn nhận bài).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateSubmission(
        Guid id,
        [FromBody] UpdateSubmissionDto dto,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new UpdateSubmissionCommand(
            SubmissionId: id,
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
    /// Rút bài dự thi khỏi sự kiện (Dành riêng cho Creator/Tác giả nộp bài, chỉ thực hiện được trong thời hạn nhận bài).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> WithdrawSubmission(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new WithdrawSubmissionCommand(id, CurrentUserId);
        var (success, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "Đã rút bài dự thi khỏi sự kiện thành công." });
    }

    /// <summary>
    /// Lấy danh sách bài nộp sự kiện trên toàn hệ thống.
    /// Hỗ trợ lọc theo SubmitterId, Tiêu đề hoặc tên/username người nộp;
    /// Thứ tự sắp xếp luôn là giảm dần (descending) theo ngày nộp (mặc định), tiêu đề, số lượt vote, điểm số;
    /// và hỗ trợ phân trang.
    /// </summary>
    [HttpGet]
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
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubmissionById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSubmissionByIdQuery(id);
        var result = await Mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFoundEnvelope("Không tìm thấy bài nộp hoặc sự kiện đã bị xóa.");
        }

        return OkEnvelope(result);
    }

    /// <summary>
    /// Bình chọn cho một bài nộp sự kiện.
    /// </summary>
    [HttpPost("{id:guid}/vote")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Vote(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new ArtCommission.Application.Event.Commands.CreateVoteCommand(
            SubmissionId: id,
            VoterId: CurrentUserId
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Hủy bình chọn cho một bài nộp sự kiện.
    /// </summary>
    [HttpDelete("{id:guid}/vote")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Unvote(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new ArtCommission.Application.Event.Commands.DeleteVoteCommand(
            SubmissionId: id,
            VoterId: CurrentUserId
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Lấy danh sách lượt bình chọn của một bài nộp sự kiện.
    /// </summary>
    [HttpGet("{id:guid}/votes")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubmissionVotes(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ArtCommission.Application.Event.Queries.GetVotesBySubmissionQuery(
            SubmissionId: id,
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
    /// Chấm điểm bài dự thi sự kiện theo tiêu chí Rubric (Dành cho Giám khảo / Head Jury).
    /// </summary>
    [HttpPost("{id:guid}/score")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SubmitScore(
        Guid id,
        [FromBody] SubmitScoreDto dto,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new SubmitJuryScoreCommand(id, CurrentUserId, dto);
        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }
}


