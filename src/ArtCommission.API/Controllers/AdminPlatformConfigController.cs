using ArtCommission.Application.Payment.PlatformConfig.Commands;
using ArtCommission.Application.Payment.PlatformConfig.DTOs;
using ArtCommission.Application.Payment.PlatformConfig.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Quản lý cấu hình tỷ lệ phí sàn và các chính sách vận hành hệ thống (SCR-18 / UC33).
/// Dành riêng cho Administrator.
/// </summary>
[Authorize(Roles = "Administrator")]
[Route("api/v1/admin/platform-config")]
public class AdminPlatformConfigController : ApiControllerBase
{
    /// <summary>
    /// Lấy cấu hình tỷ lệ phí sàn và chính sách vận hành hiện tại (SCR-18).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PlatformFeePolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPlatformFeePolicy(CancellationToken cancellationToken)
    {
        var query = new GetPlatformFeePolicyQuery();
        var result = await Mediator.Send(query, cancellationToken);

        return OkEnvelope(result);
    }

    /// <summary>
    /// Cập nhật tỷ lệ phí sàn (5.0% - 15.0%) và các thông số chính sách Escrow/Revision/URL (SCR-18).
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(PlatformFeePolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdatePlatformFeePolicy(
        [FromBody] UpdatePlatformFeePolicyRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePlatformFeePolicyCommand(
            PlatformFeePercent: request.PlatformFeePercent,
            MilestoneAutoApprovalDays: request.MilestoneAutoApprovalDays,
            DefaultFreeRevisionLimit: request.DefaultFreeRevisionLimit,
            PresignedUrlExpirationMinutes: request.PresignedUrlExpirationMinutes,
            AdminId: CurrentUserId
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data, new { message = "Platform Fee & Policy Updated Successfully!" });
    }
}
