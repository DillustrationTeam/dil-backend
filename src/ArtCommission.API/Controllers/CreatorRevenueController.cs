using ArtCommission.Application.Revenue.Commands.RebuildRevenueSnapshot;
using ArtCommission.Application.Revenue.Queries.GetRevenueBreakdown;
using ArtCommission.Application.Revenue.Queries.GetRevenueSnapshots;
using ArtCommission.Application.Revenue.Queries.GetRevenueSummary;
using ArtCommission.Application.Revenue.Queries.GetRevenueTimeseries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>Thân request chốt lại doanh thu.</summary>
public sealed record RebuildRevenueSnapshotRequest(
    string Scope,
    DateOnly? SnapshotDate = null,
    Guid? CreatorId = null);

/// <summary>
/// UC51 — Creator Revenue Analytics (5 endpoint).
///
/// Bốn endpoint đầu phục vụ Creator xem doanh thu của CHÍNH MÌNH — không có tham số
/// creatorId vì lấy từ token, nên không thể xem số của người khác bằng cách đổi URL.
/// Endpoint <c>rebuild</c> cho phép Administrator chốt lại số của một Creator cụ thể.
/// </summary>
[Authorize]
[Route("api/v1/creator/revenue")]
public class CreatorRevenueController : ApiControllerBase
{
    /// <summary>Thẻ số liệu tổng quan doanh thu của Creator (UC51).</summary>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, meta, errors) = await Mediator.Send(
            new GetRevenueSummaryQuery(CurrentUserId, from, to),
            cancellationToken);

        return success ? OkEnvelope(data, meta) : BadRequestEnvelope(errors);
    }

    /// <summary>Chuỗi doanh thu theo ngày/tuần/tháng để vẽ biểu đồ (UC51).</summary>
    /// <remarks>
    /// <c>granularity</c> để trống thì mặc định "day". Phải khai nullable: ASP.NET Core
    /// coi tham số query kiểu string không-nullable là BẮT BUỘC và trả 400 khi client
    /// không truyền — trong khi đây là tham số tuỳ chọn có giá trị mặc định.
    /// </remarks>
    [HttpGet("timeseries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTimeseries(
        [FromQuery] string? granularity,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, meta, errors) = await Mediator.Send(
            new GetRevenueTimeseriesQuery(
                CurrentUserId,
                string.IsNullOrWhiteSpace(granularity) ? "day" : granularity,
                from,
                to),
            cancellationToken);

        return success ? OkEnvelope(data, meta) : BadRequestEnvelope(errors);
    }

    /// <summary>Phân rã doanh thu theo nguồn (đơn đặt vẽ / đấu giá) (UC51).</summary>
    /// <remarks><c>groupBy</c> để trống thì mặc định "source".</remarks>
    [HttpGet("breakdown")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBreakdown(
        [FromQuery] string? groupBy,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, meta, errors) = await Mediator.Send(
            new GetRevenueBreakdownQuery(
                CurrentUserId,
                string.IsNullOrWhiteSpace(groupBy) ? "source" : groupBy,
                from,
                to),
            cancellationToken);

        return success ? OkEnvelope(data, meta) : BadRequestEnvelope(errors);
    }

    /// <summary>Các bản ghi chốt doanh thu định kỳ, phục vụ đối soát (UC51).</summary>
    [HttpGet("snapshots")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSnapshots(
        [FromQuery] string? scope,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? cursor,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, meta, errors) = await Mediator.Send(
            new GetRevenueSnapshotsQuery(CurrentUserId, scope, from, to, cursor, limit),
            cancellationToken);

        return success ? OkEnvelope(data, meta) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Tính lại bản chốt doanh thu khi phát hiện lệch số (UC51).
    /// </summary>
    /// <remarks>
    /// Quyền: <c>Administrator</c> — theo <c>.agents/03-backend-rules.md</c> mục 10,
    /// endpoint Admin phải khai <c>[Authorize(Roles = "Administrator")]</c>.
    ///
    /// VÌ SAO siết lại: trước đây chỉ có <c>[Authorize]</c> và handler cho phép người
    /// dùng thường tự chốt lại số của mình. Tuy thao tác này không tạo ra tiền (chỉ đọc
    /// lại sổ cái), nó GHI đè bản ghi đối soát — để người dùng tự sửa số liệu đối soát
    /// của chính mình làm mất ý nghĩa của snapshot như một bằng chứng độc lập.
    /// <c>Admin</c> vẫn chốt được cho một Creator cụ thể qua <c>creatorId</c>.
    /// </remarks>
    [HttpPost("snapshots/rebuild")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RebuildSnapshot(
        [FromBody] RebuildRevenueSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new RebuildRevenueSnapshotCommand(
                CurrentUserId,
                User.IsInRole("Administrator"),
                request.CreatorId,
                request.Scope,
                request.SnapshotDate),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }
}
