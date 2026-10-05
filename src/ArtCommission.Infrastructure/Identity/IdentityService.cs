using System.Security.Cryptography;
using System.Text;
using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IApplicationDbContext _db;

    public IdentityService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole<Guid>> roleManager, IApplicationDbContext db)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
    }

    private static string HashRecoveryCode(string code)
    {
        var bytes = Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant());
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public async Task<(bool Success, Guid UserId, string[] Errors)> RegisterUserAsync(string email, string password, string fullName, string? role = null, bool isVerified = false, CancellationToken cancellationToken = default)
    {
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            return (false, Guid.Empty, new[] { "Email address is already registered." });
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            FullName = fullName,
            IsVerified = isVerified,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return (false, Guid.Empty, result.Errors.Select(e => e.Description).ToArray());
        }

        // 1. Every registered user receives the default 'Client' role
        if (!await _roleManager.RoleExistsAsync(UserRoleNames.Client))
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(UserRoleNames.Client));
        }
        await _userManager.AddToRoleAsync(user, UserRoleNames.Client);

        // 2. If registering directly as Artist / Creator, ALSO grant the 'Creator' role (Dual-role)
        if (string.Equals(role, "artist", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(role, "creator", StringComparison.OrdinalIgnoreCase))
        {
            if (!await _roleManager.RoleExistsAsync(UserRoleNames.Creator))
            {
                await _roleManager.CreateAsync(new IdentityRole<Guid>(UserRoleNames.Creator));
            }
            await _userManager.AddToRoleAsync(user, UserRoleNames.Creator);
        }

        return (true, user.Id, Array.Empty<string>());
    }

    public async Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> AuthenticateUserAsync(string emailOrUsername, string password, CancellationToken cancellationToken = default)
    {
        var user = await ResolveUserByEmailOrUsernameAsync(emailOrUsername, cancellationToken);
        if (user == null || user.IsDeleted)
        {
            return (false, null, Array.Empty<string>(), new[] { "Invalid email/username or password." });
        }

        var isValidPassword = await _userManager.CheckPasswordAsync(user, password);
        if (!isValidPassword)
        {
            return (false, null, Array.Empty<string>(), new[] { "Invalid email/username or password." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userDto = new UserDto(user.Id, user.Email!, user.FullName, user.IsVerified, user.CreatedAt, user.AvatarUrl, user.CoverUrl, user.Bio, user.SocialLinks);

        return (true, userDto, roles.ToArray(), Array.Empty<string>());
    }

    /// <summary>
    /// Thử tìm theo email trước (đa số trường hợp). Nếu không khớp, thử theo ApplicationUser.LoginUsername
    /// (vd tài khoản hoàn tất đăng ký qua Google chọn username lúc đó). Nếu vẫn không khớp, coi input
    /// là username công khai (ClientProfile.Username — hiện chỉ Client mới có field này).
    /// </summary>
    private async Task<ApplicationUser?> ResolveUserByEmailOrUsernameAsync(string identifier, CancellationToken cancellationToken)
    {
        var trimmed = identifier.Trim();
        var byEmail = await _userManager.FindByEmailAsync(trimmed);
        if (byEmail is not null)
        {
            return byEmail;
        }

        var normalizedUsername = NormalizeUsername(trimmed);
        var byUsername = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedLoginUsername == normalizedUsername, cancellationToken);
        if (byUsername is not null)
        {
            return await _userManager.FindByIdAsync(byUsername.Id.ToString());
        }

        var normalizedClientUsername = trimmed.ToLowerInvariant();
        var profile = await _db.ClientProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Username == normalizedClientUsername && !p.IsDeleted, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        return await _userManager.FindByIdAsync(profile.UserId.ToString());
    }

    public async Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return (false, null, Array.Empty<string>(), new[] { "User not found." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userDto = new UserDto(user.Id, user.Email!, user.FullName, user.IsVerified, user.CreatedAt, user.AvatarUrl, user.CoverUrl, user.Bio, user.SocialLinks);

        return (true, userDto, roles.ToArray(), Array.Empty<string>());
    }

    public async Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || user.IsDeleted)
        {
            return (false, null, Array.Empty<string>(), new[] { "User not found." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userDto = new UserDto(user.Id, user.Email!, user.FullName, user.IsVerified, user.CreatedAt, user.AvatarUrl, user.CoverUrl, user.Bio, user.SocialLinks);

        return (true, userDto, roles.ToArray(), Array.Empty<string>());
    }

    public async Task<bool> IsEmailUniqueAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user == null;
    }

    public async Task<bool> VerifyUserEmailAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return false;
        }

        user.IsVerified = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        return result.Succeeded;
    }

    public async Task<(bool Success, string[] Errors)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return (false, new[] { "User not found." });
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            return (false, result.Errors.Select(e => e.Description).ToArray());
        }

        return (true, Array.Empty<string>());
    }

    public async Task<string> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || user.IsDeleted)
        {
            return string.Empty;
        }

        return await _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<(bool Success, string[] Errors)> ResetPasswordAsync(string email, string resetToken, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || user.IsDeleted)
        {
            return (false, new[] { "Invalid reset password request." });
        }

        var result = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);
        if (!result.Succeeded)
        {
            return (false, result.Errors.Select(e => e.Description).ToArray());
        }

        return (true, Array.Empty<string>());
    }

    public async Task<bool> HasPasswordAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        return await _userManager.HasPasswordAsync(user);
    }

    public async Task<(bool Success, string[] Errors)> AddPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return (false, new[] { "User not found." });
        }

        var result = await _userManager.AddPasswordAsync(user, newPassword);
        if (!result.Succeeded)
        {
            return (false, result.Errors.Select(e => e.Description).ToArray());
        }

        return (true, Array.Empty<string>());
    }

    public async Task<(bool HasPassword, IReadOnlyList<string> LinkedProviders)> GetLinkedAccountsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return (false, Array.Empty<string>());
        }

        var hasPassword = await _userManager.HasPasswordAsync(user);
        var logins = await _userManager.GetLoginsAsync(user);
        return (hasPassword, logins.Select(l => l.LoginProvider).ToList());
    }

    public async Task<(bool Success, string[] Errors)> LinkExternalLoginAsync(Guid userId, string provider, string providerKey, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return (false, new[] { "User not found." });
        }

        var existingOwner = await _userManager.FindByLoginAsync(provider, providerKey);
        if (existingOwner != null)
        {
            return (false, existingOwner.Id == userId
                ? new[] { "This account is already linked." }
                : new[] { "This external account is already linked to another user." });
        }

        var result = await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerKey, provider));
        if (!result.Succeeded)
        {
            return (false, result.Errors.Select(e => e.Description).ToArray());
        }

        return (true, Array.Empty<string>());
    }

    public async Task<(bool Success, string[] Errors)> UnlinkExternalLoginAsync(Guid userId, string provider, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return (false, new[] { "User not found." });
        }

        var hasPassword = await _userManager.HasPasswordAsync(user);
        var logins = await _userManager.GetLoginsAsync(user);
        var targetLogin = logins.FirstOrDefault(l => string.Equals(l.LoginProvider, provider, StringComparison.OrdinalIgnoreCase));

        if (targetLogin == null)
        {
            return (false, new[] { "This provider is not linked to your account." });
        }

        if (!hasPassword && logins.Count <= 1)
        {
            return (false, new[] { "You must set a password before unlinking your only sign-in method." });
        }

        var result = await _userManager.RemoveLoginAsync(user, targetLogin.LoginProvider, targetLogin.ProviderKey);
        if (!result.Succeeded)
        {
            return (false, result.Errors.Select(e => e.Description).ToArray());
        }

        return (true, Array.Empty<string>());
    }

    public async Task<bool> VerifyPasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return false;
        }

        return await _userManager.CheckPasswordAsync(user, password);
    }

    public async Task<(bool Success, string[] Errors)> DeactivateAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return (false, new[] { "User not found." });
        }

        user.IsDeleted = true;
        user.DeletedAt = DateTimeOffset.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return (false, result.Errors.Select(e => e.Description).ToArray());
        }

        return (true, Array.Empty<string>());
    }

    public async Task<(bool Success, string[] Errors)> ChangeEmailAsync(Guid userId, string newEmail, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return (false, new[] { "User not found." });
        }

        var setEmailResult = await _userManager.SetEmailAsync(user, newEmail);
        if (!setEmailResult.Succeeded)
        {
            return (false, setEmailResult.Errors.Select(e => e.Description).ToArray());
        }

        var setUserNameResult = await _userManager.SetUserNameAsync(user, newEmail);
        if (!setUserNameResult.Succeeded)
        {
            return (false, setUserNameResult.Errors.Select(e => e.Description).ToArray());
        }

        // SetEmailAsync tự đặt EmailConfirmed = false khi email thực sự đổi — nhưng email mới ở đây
        // đã được xác minh bằng OTP trước khi gọi vào đây, nên cần đặt lại true.
        user.EmailConfirmed = true;
        var confirmResult = await _userManager.UpdateAsync(user);
        if (!confirmResult.Succeeded)
        {
            return (false, confirmResult.Errors.Select(e => e.Description).ToArray());
        }

        return (true, Array.Empty<string>());
    }

    public async Task<(bool Success, string[] Errors)> AddCreatorRoleAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return (false, new[] { "User not found." });
        }

        if (!await _roleManager.RoleExistsAsync(UserRoleNames.Creator))
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(UserRoleNames.Creator));
        }

        if (!await _userManager.IsInRoleAsync(user, UserRoleNames.Creator))
        {
            var result = await _userManager.AddToRoleAsync(user, UserRoleNames.Creator);
            if (!result.Succeeded)
            {
                return (false, result.Errors.Select(e => e.Description).ToArray());
            }
        }

        return (true, Array.Empty<string>());
    }

    public async Task<(bool Success, UserDto? User, string[] Roles, ExternalRegistrationPendingDto? PendingRegistration, string[] Errors)> AuthenticateOrRegisterExternalAsync(
        string provider, string providerKey, string email, bool emailVerified, string? fullName,
        CancellationToken cancellationToken = default)
    {
        // 1. Đã từng link provider này trước đó -> user quay lại, đăng nhập luôn.
        var linkedUser = await _userManager.FindByLoginAsync(provider, providerKey);
        if (linkedUser != null && !linkedUser.IsDeleted)
        {
            var linkedRoles = await _userManager.GetRolesAsync(linkedUser);
            var linkedUserDto = new UserDto(linkedUser.Id, linkedUser.Email!, linkedUser.FullName, linkedUser.IsVerified, linkedUser.CreatedAt, linkedUser.AvatarUrl, linkedUser.CoverUrl, linkedUser.Bio, linkedUser.SocialLinks);
            return (true, linkedUserDto, linkedRoles.ToArray(), null, Array.Empty<string>());
        }

        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            if (existingUser.IsDeleted)
            {
                return (false, null, Array.Empty<string>(), null, new[] { "This account is no longer active." });
            }

            if (!emailVerified)
            {
                return (false, null, Array.Empty<string>(), null, new[] { "This email is already registered. Please sign in with your password." });
            }

            // 2. Email đã xác thực từ provider khớp một tài khoản có sẵn -> merge (link thêm login).
            await _userManager.AddLoginAsync(existingUser, new UserLoginInfo(provider, providerKey, provider));

            var existingRoles = await _userManager.GetRolesAsync(existingUser);
            var existingUserDto = new UserDto(existingUser.Id, existingUser.Email!, existingUser.FullName, existingUser.IsVerified, existingUser.CreatedAt, existingUser.AvatarUrl, existingUser.CoverUrl, existingUser.Bio, existingUser.SocialLinks);
            return (true, existingUserDto, existingRoles.ToArray(), null, Array.Empty<string>());
        }

        // 3. Chưa từng có tài khoản nào -> KHÔNG tạo ngay (tránh tài khoản không mật khẩu).
        // Trả về pending để FE hiển thị form username + password, hoàn tất ở RegisterExternalUserAsync.
        var pending = new ExternalRegistrationPendingDto(provider, providerKey, email, emailVerified, fullName);
        return (true, null, Array.Empty<string>(), pending, Array.Empty<string>());
    }

    public async Task<bool> IsUsernameUniqueAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeUsername(username);
        return !await _db.Users.AsNoTracking().AnyAsync(u => u.NormalizedLoginUsername == normalized, cancellationToken);
    }

    public async Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> RegisterExternalUserAsync(
        string provider, string providerKey, string email, bool emailVerified, string? fullName,
        string username, string password, CancellationToken cancellationToken = default)
    {
        // Re-check tại thời điểm hoàn tất (không chỉ lúc GenerateTicket) để tránh race condition:
        // provider login hoặc email đã được tài khoản khác chiếm trong lúc user điền form.
        var linkedUser = await _userManager.FindByLoginAsync(provider, providerKey);
        if (linkedUser != null && !linkedUser.IsDeleted)
        {
            return (false, null, Array.Empty<string>(), new[] { "This Google account is already linked. Please sign in instead." });
        }

        var existingByEmail = await _userManager.FindByEmailAsync(email);
        if (existingByEmail != null)
        {
            return (false, null, Array.Empty<string>(), new[] { "This email is already registered. Please sign in instead." });
        }

        var normalizedUsername = NormalizeUsername(username);
        if (await _db.Users.AsNoTracking().AnyAsync(u => u.NormalizedLoginUsername == normalizedUsername, cancellationToken))
        {
            return (false, null, Array.Empty<string>(), new[] { "This username is already taken." });
        }

        var newUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            LoginUsername = username.Trim(),
            NormalizedLoginUsername = normalizedUsername,
            FullName = fullName ?? username.Trim(),
            IsVerified = emailVerified,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await _userManager.CreateAsync(newUser, password);
        if (!createResult.Succeeded)
        {
            return (false, null, Array.Empty<string>(), createResult.Errors.Select(e => e.Description).ToArray());
        }

        if (!await _roleManager.RoleExistsAsync(UserRoleNames.Client))
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(UserRoleNames.Client));
        }
        await _userManager.AddToRoleAsync(newUser, UserRoleNames.Client);

        await _userManager.AddLoginAsync(newUser, new UserLoginInfo(provider, providerKey, provider));

        var newUserDto = new UserDto(newUser.Id, newUser.Email!, newUser.FullName, newUser.IsVerified, newUser.CreatedAt, newUser.AvatarUrl, newUser.CoverUrl, newUser.Bio, newUser.SocialLinks);
        return (true, newUserDto, new[] { UserRoleNames.Client }, Array.Empty<string>());
    }

    public async Task<(bool Success, string[] Errors)> CompleteAccountSetupAsync(Guid userId, string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return (false, new[] { "User not found." });
        }

        if (await _userManager.HasPasswordAsync(user))
        {
            return (false, new[] { "This account already has a password." });
        }

        var normalizedUsername = NormalizeUsername(username);
        var usernameTaken = await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Id != userId && u.NormalizedLoginUsername == normalizedUsername, cancellationToken);
        if (usernameTaken)
        {
            return (false, new[] { "This username is already taken." });
        }

        var addPasswordResult = await _userManager.AddPasswordAsync(user, password);
        if (!addPasswordResult.Succeeded)
        {
            return (false, addPasswordResult.Errors.Select(e => e.Description).ToArray());
        }

        user.LoginUsername = username.Trim();
        user.NormalizedLoginUsername = normalizedUsername;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return (false, updateResult.Errors.Select(e => e.Description).ToArray());
        }

        return (true, Array.Empty<string>());
    }

    private static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();

    public async Task<(bool Success, string SharedKey, string AuthenticatorUri, string[] Errors)> Setup2FAAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return (false, string.Empty, string.Empty, new[] { "User not found." });
        }

        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var authenticatorUri = $"otpauth://totp/Dillustration:{Uri.EscapeDataString(user.Email!)}?secret={unformattedKey}&issuer=Dillustration&digits=6";
        return (true, unformattedKey!, authenticatorUri, Array.Empty<string>());
    }

    public async Task<(bool Success, string[] RecoveryCodes, string[] Errors)> VerifyAndEnable2FAAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return (false, Array.Empty<string>(), new[] { "User not found." });
        }

        var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code);
        if (!isValid)
        {
            return (false, Array.Empty<string>(), new[] { "Invalid verification code." });
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        var codesArray = recoveryCodes?.ToArray() ?? Array.Empty<string>();

        // Xoá dấu vết các mã cũ (nếu từng bật 2FA trước đó) rồi ghi lại hash của 10 mã mới —
        // dùng để phân biệt "mã sai" với "mã đúng nhưng đã dùng rồi" lúc verify.
        var oldCodes = await _db.TwoFactorRecoveryCodes.Where(c => c.UserId == userId).ToListAsync(cancellationToken);
        _db.TwoFactorRecoveryCodes.RemoveRange(oldCodes);
        foreach (var plainCode in codesArray)
        {
            _db.TwoFactorRecoveryCodes.Add(new TwoFactorRecoveryCode
            {
                UserId = userId,
                CodeHash = HashRecoveryCode(plainCode),
                IsUsed = false
            });
        }
        await _db.SaveChangesAsync(cancellationToken);

        return (true, codesArray, Array.Empty<string>());
    }

    public async Task<(bool Success, string[] Errors)> Disable2FAAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return (false, new[] { "User not found." });
        }

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        // Reset key để lần bật lại sau phải quét QR mới — tránh tái dùng secret/recovery code cũ.
        await _userManager.ResetAuthenticatorKeyAsync(user);

        return (true, Array.Empty<string>());
    }

    public async Task<bool> IsTwoFactorEnabledAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return false;
        }

        return await _userManager.GetTwoFactorEnabledAsync(user);
    }

    public async Task<(bool Success, bool IsRecoveryCode, bool CodeAlreadyUsed)> VerifyTwoFactorCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return (false, false, false);
        }

        var isValidTotp = await _userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code);
        if (isValidTotp)
        {
            return (true, false, false);
        }

        var codeHash = HashRecoveryCode(code);
        var tracked = await _db.TwoFactorRecoveryCodes
            .FirstOrDefaultAsync(c => c.UserId == userId && c.CodeHash == codeHash, cancellationToken);

        var redeemResult = await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, code);
        if (redeemResult.Succeeded)
        {
            if (tracked is not null)
            {
                tracked.IsUsed = true;
                tracked.UsedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return (true, true, false);
        }

        // ASP.NET Identity tự xoá mã khỏi danh sách còn hiệu lực ngay khi redeem thành công, nên bản thân
        // nó không phân biệt được "mã sai" với "mã đã dùng" — dựa vào bảng theo dõi riêng để báo đúng lý do.
        var alreadyUsed = tracked is { IsUsed: true };
        return (false, false, alreadyUsed);
    }
}
