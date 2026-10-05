using ArtCommission.Application.Auth.Commands.ChangeEmail;
using ArtCommission.Application.Auth.Commands.CompleteAccountPasswordSetup;
using ArtCommission.Application.Auth.Commands.CompleteGoogleRegistration;
using ArtCommission.Application.Auth.Commands.ChangeMyPassword;
using ArtCommission.Application.Auth.Commands.ConfirmVerificationCode;
using ArtCommission.Application.Auth.Commands.DeactivateAccount;
using ArtCommission.Application.Auth.Commands.Disable2FA;
using ArtCommission.Application.Auth.Commands.ForgotPassword;
using ArtCommission.Application.Auth.Commands.GoogleAuth;
using ArtCommission.Application.Auth.Commands.Login;
using ArtCommission.Application.Auth.Commands.RefreshToken;
using ArtCommission.Application.Auth.Commands.Register;
using ArtCommission.Application.Auth.Commands.ResetPassword;
using ArtCommission.Application.Auth.Commands.RevokeOtherSessions;
using ArtCommission.Application.Auth.Commands.RevokeSession;
using ArtCommission.Application.Auth.Commands.RevokeToken;
using ArtCommission.Application.Auth.Commands.SendVerificationCode;
using ArtCommission.Application.Auth.Commands.LinkExternalLogin;
using ArtCommission.Application.Auth.Commands.Setup2FA;
using ArtCommission.Application.Auth.Commands.UnlinkExternalLogin;
using ArtCommission.Application.Auth.Commands.UpdateProfile;
using ArtCommission.Application.Auth.Commands.VerifyAndEnable2FA;
using ArtCommission.Application.Auth.Commands.VerifyTwoFactorLogin;
using ArtCommission.Application.Auth.Queries.Get2FAStatus;
using ArtCommission.Application.Auth.Queries.GetCurrentUser;
using ArtCommission.Application.Auth.Queries.GetLinkedAccounts;
using ArtCommission.Application.Auth.Queries.GetMySessions;
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
        var commandWithUserAgent = command with { UserAgent = HttpContext.Request.Headers.UserAgent.ToString() };
        var (success, authResponse, errors) = await Mediator.Send(commandWithUserAgent, cancellationToken);
        if (!success || authResponse == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(authResponse);
    }

    /// <summary>
    /// Gửi mã OTP xác minh email trước khi đăng ký (đăng ký bằng email/password)
    /// </summary>
    [HttpPost("send-verification-code")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendVerificationCode([FromBody] SendVerificationCodeCommand command, CancellationToken cancellationToken)
    {
        var (success, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "Verification code sent." });
    }

    /// <summary>
    /// Xác nhận mã OTP đã gửi, trả về vé xác minh (verificationTicket) để dùng khi Register
    /// </summary>
    [HttpPost("confirm-verification-code")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmVerificationCode([FromBody] ConfirmVerificationCodeCommand command, CancellationToken cancellationToken)
    {
        var (success, verificationTicket, errors) = await Mediator.Send(command, cancellationToken);
        if (!success || verificationTicket == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { verificationTicket });
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
        var commandWithIp = command with { ClientIp = clientIp, UserAgent = HttpContext.Request.Headers.UserAgent.ToString() };

        var (success, authResponse, requiresTwoFactor, twoFactorTicket, twoFactorEmail, errors) = await Mediator.Send(commandWithIp, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        if (requiresTwoFactor)
        {
            return OkEnvelope(new { requiresTwoFactor = true, twoFactorTicket, email = twoFactorEmail });
        }

        if (authResponse == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(authResponse);
    }

    /// <summary>
    /// Authenticate via Google OAuth access token, or signal that this is a new account needing
    /// username + password before it can be created (custom button + implicit flow)
    /// </summary>
    [HttpPost("google")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthCommand command, CancellationToken cancellationToken)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var commandWithIp = command with { ClientIp = clientIp, UserAgent = HttpContext.Request.Headers.UserAgent.ToString() };

        var (success, authResponse, requiresTwoFactor, twoFactorTicket, twoFactorEmail, requiresRegistration, registrationTicket, requiresPasswordSetup, passwordSetupTicket, suggestedEmail, suggestedFullName, errors) = await Mediator.Send(commandWithIp, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        if (requiresTwoFactor)
        {
            return OkEnvelope(new { requiresTwoFactor = true, twoFactorTicket, email = twoFactorEmail });
        }

        if (requiresRegistration)
        {
            return OkEnvelope(new
            {
                requiresRegistration = true,
                registrationTicket,
                suggestedEmail,
                suggestedFullName
            });
        }

        if (requiresPasswordSetup)
        {
            return OkEnvelope(new
            {
                requiresPasswordSetup = true,
                passwordSetupTicket,
                email = suggestedEmail,
                fullName = suggestedFullName
            });
        }

        if (authResponse == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(authResponse);
    }

    /// <summary>
    /// Hoàn tất đăng ký tài khoản mới qua Google: nhận registrationTicket từ /auth/google kèm
    /// username + password user vừa điền, chỉ lúc này tài khoản mới thực sự được tạo.
    /// </summary>
    [HttpPost("google/complete-registration")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteGoogleRegistration([FromBody] CompleteGoogleRegistrationCommand command, CancellationToken cancellationToken)
    {
        var commandWithUserAgent = command with { UserAgent = HttpContext.Request.Headers.UserAgent.ToString() };
        var (success, authResponse, errors) = await Mediator.Send(commandWithUserAgent, cancellationToken);
        if (!success || authResponse == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(authResponse);
    }

    /// <summary>
    /// Thêm username + password lần đầu cho một tài khoản ĐÃ TỒN TẠI nhưng chưa có mật khẩu (vd tài khoản
    /// Google tạo trước khi tính năng này ra đời) — nhận passwordSetupTicket từ /auth/google hoặc
    /// /login/verify-2fa, chỉ sau khi form này submit thành công mới phát JWT (mới được vào home).
    /// </summary>
    [HttpPost("complete-account-setup")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteAccountPasswordSetup([FromBody] CompleteAccountPasswordSetupCommand command, CancellationToken cancellationToken)
    {
        var commandWithUserAgent = command with { UserAgent = HttpContext.Request.Headers.UserAgent.ToString() };
        var (success, authResponse, errors) = await Mediator.Send(commandWithUserAgent, cancellationToken);
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
        var commandWithIp = command with { ClientIp = clientIp, UserAgent = HttpContext.Request.Headers.UserAgent.ToString() };

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

    public record UpdateProfileRequest(string? FullName, string? Bio, string? AvatarUrl, string? CoverUrl, List<string>? SocialLinks);

    /// <summary>
    /// Update current authenticated user's profile settings (avatar, cover, bio, social links, display name)
    /// </summary>
    [HttpPut("me/profile")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, user, errors) = await Mediator.Send(
            new UpdateProfileCommand(CurrentUserId, request.FullName, request.Bio, request.AvatarUrl, request.CoverUrl, request.SocialLinks),
            cancellationToken);

        if (!success || user == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(user);
    }

    /// <summary>Gửi OTP tới email HIỆN TẠI — chỉ dùng cho tài khoản không có mật khẩu (Google-only) để xác thực lại danh tính trước khi đổi email.</summary>
    [HttpPost("me/change-email/request-reauth")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestEmailChangeReauth(CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, errors) = await Mediator.Send(new RequestEmailChangeReauthCommand(CurrentUserId), cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "Verification code sent to current email." });
    }

    public record RequestChangeEmailRequest(string NewEmail, string? CurrentPassword, string? TwoFactorCode, string? ReauthCode);

    /// <summary>Gửi mã OTP tới email mới để bắt đầu đổi email — yêu cầu xác thực lại danh tính trước.</summary>
    [HttpPost("me/change-email/request")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestChangeEmail([FromBody] RequestChangeEmailRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, errors) = await Mediator.Send(
            new RequestChangeEmailCommand(CurrentUserId, request.NewEmail, request.CurrentPassword, request.TwoFactorCode, request.ReauthCode),
            cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "Verification code sent to new email." });
    }

    public record ConfirmChangeEmailRequest(string NewEmail, string Code, Guid? CurrentSessionId);

    /// <summary>Xác nhận mã OTP, hoàn tất đổi email — email cũ sẽ nhận thông báo bảo mật, các phiên khác bị đăng xuất.</summary>
    [HttpPost("me/change-email/confirm")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmChangeEmail([FromBody] ConfirmChangeEmailRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, user, errors) = await Mediator.Send(
            new ConfirmChangeEmailCommand(CurrentUserId, request.NewEmail, request.Code, request.CurrentSessionId), cancellationToken);
        if (!success || user == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(user);
    }

    /// <summary>Gửi mã OTP tới email hiện tại của user — bắt buộc trước khi đổi/đặt mật khẩu.</summary>
    [HttpPost("me/change-password/request-otp")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestChangePasswordOtp(CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, errors) = await Mediator.Send(new RequestChangePasswordOtpCommand(CurrentUserId), cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "Verification code sent." });
    }

    public record ChangeMyPasswordRequest(string? CurrentPassword, string NewPassword, string Code, Guid? CurrentSessionId);

    /// <summary>Đổi mật khẩu (tài khoản có mật khẩu) hoặc đặt mật khẩu lần đầu (tài khoản chỉ có Google) — yêu cầu mã OTP đã gửi, thu hồi mọi phiên khác sau khi thành công.</summary>
    [HttpPost("me/change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangeMyPassword([FromBody] ChangeMyPasswordRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, errors) = await Mediator.Send(
            new ChangeMyPasswordCommand(CurrentUserId, request.CurrentPassword, request.NewPassword, request.Code, request.CurrentSessionId),
            cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "Password updated successfully." });
    }

    /// <summary>Danh sách external login (vd Google) đã liên kết + tài khoản đã có mật khẩu chưa.</summary>
    [HttpGet("me/linked-accounts")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLinkedAccounts(CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var result = await Mediator.Send(new GetLinkedAccountsQuery(CurrentUserId), cancellationToken);
        return OkEnvelope(result);
    }

    /// <summary>Gỡ liên kết 1 external login provider (chặn nếu đây là cách đăng nhập duy nhất).</summary>
    [HttpDelete("me/linked-accounts/{provider}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnlinkExternalLogin(string provider, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, errors) = await Mediator.Send(new UnlinkExternalLoginCommand(CurrentUserId, provider), cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "Unlinked successfully." });
    }

    public record LinkGoogleAccountRequest(string AccessToken);

    /// <summary>Liên kết tài khoản Google vào tài khoản đang đăng nhập (chặn nếu Google đó đã gắn với user khác).</summary>
    [HttpPost("me/linked-accounts/google")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LinkGoogleAccount([FromBody] LinkGoogleAccountRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, errors) = await Mediator.Send(new LinkExternalLoginCommand(CurrentUserId, "Google", request.AccessToken), cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "Linked successfully." });
    }

    /// <summary>Danh sách phiên đăng nhập (refresh token) đang hoạt động.</summary>
    [HttpGet("me/sessions")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySessions([FromQuery] Guid? currentSessionId, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var result = await Mediator.Send(new GetMySessionsQuery(CurrentUserId, currentSessionId), cancellationToken);
        return OkEnvelope(result);
    }

    /// <summary>Đăng xuất 1 thiết bị/phiên cụ thể.</summary>
    [HttpDelete("me/sessions/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RevokeSession(Guid id, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, errors) = await Mediator.Send(new RevokeSessionCommand(CurrentUserId, id), cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "Session revoked." });
    }

    /// <summary>Đăng xuất tất cả thiết bị khác, giữ lại đúng phiên hiện tại (currentSessionId).</summary>
    [HttpDelete("me/sessions")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeOtherSessions([FromQuery] Guid? currentSessionId, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var count = await Mediator.Send(new RevokeOtherSessionsCommand(CurrentUserId, currentSessionId), cancellationToken);
        return OkEnvelope(new { revokedCount = count });
    }

    public record DeactivateAccountRequest(string Password);

    /// <summary>Vô hiệu hóa tài khoản (soft-delete) — chặn nếu còn commission đang chạy / còn tiền trong ví / còn dispute chưa giải quyết.</summary>
    [HttpPost("me/deactivate")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeactivateAccount([FromBody] DeactivateAccountRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, errors) = await Mediator.Send(new DeactivateAccountCommand(CurrentUserId, request.Password), cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "Account deactivated." });
    }

    /// <summary>Lấy trạng thái 2FA (đã bật hay chưa) của tài khoản đang đăng nhập.</summary>
    [HttpGet("me/2fa/status")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get2FAStatus(CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var enabled = await Mediator.Send(new Get2FAStatusQuery(CurrentUserId), cancellationToken);
        return OkEnvelope(new { enabled });
    }

    /// <summary>Sinh authenticator key + QR URI để bắt đầu bật 2FA — chưa bật thật, chỉ bật sau khi xác minh đúng mã ở bước tiếp theo.</summary>
    [HttpPost("me/2fa/setup")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Setup2FA(CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, sharedKey, authenticatorUri, errors) = await Mediator.Send(new Setup2FACommand(CurrentUserId), cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { sharedKey, authenticatorUri });
    }

    public record Verify2FARequest(string Code);

    /// <summary>Xác minh mã TOTP đầu tiên để chính thức bật 2FA — trả về recovery codes, CHỈ hiện đúng 1 lần.</summary>
    [HttpPost("me/2fa/enable")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyAndEnable2FA([FromBody] Verify2FARequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, recoveryCodes, errors) = await Mediator.Send(new VerifyAndEnable2FACommand(CurrentUserId, request.Code), cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { recoveryCodes });
    }

    public record Disable2FARequest(string Password);

    /// <summary>Tắt 2FA — yêu cầu xác thực lại mật khẩu.</summary>
    [HttpPost("me/2fa/disable")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disable2FA([FromBody] Disable2FARequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, errors) = await Mediator.Send(new Disable2FACommand(CurrentUserId, request.Password), cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "2FA disabled." });
    }

    /// <summary>Bước 2 của login khi tài khoản đã bật 2FA — xác minh mã TOTP (hoặc recovery code) kèm vé từ bước 1, rồi phát token như login thường.</summary>
    [HttpPost("login/verify-2fa")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyTwoFactorLogin([FromBody] VerifyTwoFactorLoginCommand command, CancellationToken cancellationToken)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var commandWithIp = command with { ClientIp = clientIp, UserAgent = HttpContext.Request.Headers.UserAgent.ToString() };

        var (success, authResponse, requiresPasswordSetup, passwordSetupTicket, suggestedFullName, errors) = await Mediator.Send(commandWithIp, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        if (requiresPasswordSetup)
        {
            return OkEnvelope(new
            {
                requiresPasswordSetup = true,
                passwordSetupTicket,
                email = command.Email,
                fullName = suggestedFullName
            });
        }

        if (authResponse == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(authResponse);
    }
}
