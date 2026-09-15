using ArtCommission.Application.Auth.Commands.ForgotPassword;
using ArtCommission.Application.Auth.Commands.Login;
using ArtCommission.Application.Auth.Commands.RefreshToken;
using ArtCommission.Application.Auth.Commands.Register;
using ArtCommission.Application.Auth.Commands.ResetPassword;
using ArtCommission.Application.Auth.Commands.RevokeToken;
using ArtCommission.Application.Auth.Queries.GetCurrentUser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

public class AuthController : ApiControllerBase
{
    /// <summary>
    /// Register a new user account (UC-01)
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken cancellationToken)
    {
        var (success, authResponse, errors) = await Mediator.Send(command, cancellationToken);
        if (!success || authResponse == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(authResponse);
    }

    /// <summary>
    /// Authenticate user and issue JWT Access + Refresh Tokens (UC-02)
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var commandWithIp = command with { ClientIp = clientIp };

        var (success, authResponse, errors) = await Mediator.Send(commandWithIp, cancellationToken);
        if (!success || authResponse == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(authResponse);
    }

    /// <summary>
    /// Refresh JWT Access Token using Refresh Token Rotation (UC-02)
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var commandWithIp = command with { ClientIp = clientIp };

        var (success, authResponse, errors) = await Mediator.Send(commandWithIp, cancellationToken);
        if (!success || authResponse == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(authResponse);
    }

    /// <summary>
    /// Revoke Refresh Token / Logout (UC-02)
    /// </summary>
    [HttpPost("revoke-token")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenCommand command, CancellationToken cancellationToken)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var commandWithIp = command with { ClientIp = clientIp };

        var success = await Mediator.Send(commandWithIp, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope("Invalid refresh token or token already revoked.");
        }

        return OkEnvelope(new { message = "Token revoked successfully." });
    }

    /// <summary>
    /// Send password reset token email (UC-03)
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        await Mediator.Send(command, cancellationToken);
        return OkEnvelope(new { message = "If the email is registered, a password reset token has been sent." });
    }

    /// <summary>
    /// Reset password using token (UC-03)
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var (success, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "Password reset successfully." });
    }

    /// <summary>
    /// Get current authenticated user profile & roles (UC-02)
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, user, roles, errors) = await Mediator.Send(new GetCurrentUserQuery(CurrentUserId), cancellationToken);
        if (!success || user == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { user, roles });
    }
}
