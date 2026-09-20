using ArtCommission.Application.Admin.DTOs;
using ArtCommission.Application.Admin.Queries;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Bảng điều khiển tổng quan hệ thống &amp; Các chỉ số tài chính KPIs (SCR-24 / UC32).
/// Dành cho Quản trị viên (Administrator) và Kiểm duyệt viên (Moderator).
/// </summary>
[Authorize(Roles = $"{UserRoleNames.Administrator},{UserRoleNames.Moderator}")]
[Route("api/v1/admin/dashboard")]
public class AdminDashboardController : ApiControllerBase
{
    /// <summary>
    /// Lấy toàn bộ chỉ số tổng quan hệ thống, KPIs tài chính, vận hành, biểu đồ doanh thu và giao dịch gần nhất (SCR-24 / UC32).
    /// </summary>
    /// <param name="days">Số ngày tính toán xu hướng biểu đồ (mặc định 30 ngày, tối đa 90 ngày).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(AdminDashboardOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOverview(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminDashboardOverviewQuery(days);
        var result = await Mediator.Send(query, cancellationToken);
        return OkEnvelope(result);
    }

    /// <summary>
    /// Lấy nhanh 4 thẻ thống kê tài chính KPIs chính (GMV, Escrow locked, Doanh thu sàn, Số dư ví hệ thống) (SCR-24 / UC32).
    /// </summary>
    [HttpGet("kpis")]
    [ProducesResponseType(typeof(AdminFinancialKpiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFinancialKpis(CancellationToken cancellationToken = default)
    {
        var query = new GetAdminFinancialKpisQuery();
        var result = await Mediator.Send(query, cancellationToken);
        return OkEnvelope(result);
    }
}
