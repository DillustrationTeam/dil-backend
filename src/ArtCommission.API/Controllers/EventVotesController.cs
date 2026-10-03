using ArtCommission.Application.Event.Commands;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Quản lý bình chọn bài dự thi sự kiện (Event Votes).
/// </summary>
[Route("api/v1/event-votes")]
public class EventVotesController : ApiControllerBase
{
    /// <summary>
    /// Lấy danh sách bình chọn trên toàn hệ thống (Hỗ trợ lọc theo EventId, SubmissionId, VoterId và phân trang).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVotes(
        [FromQuery] Guid? eventId = null,
        [FromQuery] Guid? submissionId = null,
        [FromQuery] Guid? voterId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetVotesQuery(
            EventId: eventId,
            SubmissionId: submissionId,
            VoterId: voterId,
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
    /// Lấy danh sách các bài thi mà người dùng hiện tại đã bình chọn.
    /// </summary>
    [HttpGet("my-votes")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyVotes(
        [FromQuery] Guid? eventId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var query = new GetMyVotesQuery(
            VoterId: CurrentUserId,
            EventId: eventId,
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
    /// Lấy danh sách bình chọn của một bài dự thi cụ thể.
    /// </summary>
    [HttpGet("by-submission/{submissionId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVotesBySubmission(
        Guid submissionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetVotesBySubmissionQuery(
            SubmissionId: submissionId,
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
    /// Lấy danh sách bình chọn của một sự kiện cụ thể.
    /// </summary>
    [HttpGet("by-event/{eventId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVotesByEvent(
        Guid eventId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetVotesByEventQuery(
            EventId: eventId,
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
    /// Bình chọn cho một bài dự thi sự kiện (Yêu cầu đăng nhập).
    /// Ràng buộc: Mỗi người dùng chỉ được vote cho 1 bài dự thi 1 lần, và tổng số vote trong sự kiện không vượt quá MaxVote.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateVoteFromBody(
        [FromBody] CreateVoteRequest request,
        CancellationToken cancellationToken = default)
    {
        return await HandleVote(request.SubmissionId, cancellationToken);
    }

    /// <summary>
    /// Bình chọn cho một bài dự thi sự kiện theo submissionId trong URL (Yêu cầu đăng nhập).
    /// </summary>
    [HttpPost("{submissionId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateVote(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        return await HandleVote(submissionId, cancellationToken);
    }

    private async Task<IActionResult> HandleVote(Guid submissionId, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new CreateVoteCommand(
            SubmissionId: submissionId,
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
    /// Hủy bình chọn đã thực hiện cho bài dự thi (Yêu cầu đăng nhập).
    /// </summary>
    [HttpDelete("{submissionId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteVote(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new DeleteVoteCommand(
            SubmissionId: submissionId,
            VoterId: CurrentUserId
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }
}

public sealed record CreateVoteRequest(Guid SubmissionId);
