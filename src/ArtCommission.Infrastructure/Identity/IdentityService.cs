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

    public IdentityService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole<Guid>> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<(bool Success, Guid UserId, string[] Errors)> RegisterUserAsync(string email, string password, string fullName, string? role = null, CancellationToken cancellationToken = default)
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
            IsVerified = false,
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

    public async Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> AuthenticateUserAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || user.IsDeleted)
        {
            return (false, null, Array.Empty<string>(), new[] { "Invalid email or password." });
        }

        var isValidPassword = await _userManager.CheckPasswordAsync(user, password);
        if (!isValidPassword)
        {
            return (false, null, Array.Empty<string>(), new[] { "Invalid email or password." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userDto = new UserDto(user.Id, user.Email!, user.FullName, user.IsVerified, user.CreatedAt);

        return (true, userDto, roles.ToArray(), Array.Empty<string>());
    }

    public async Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            return (false, null, Array.Empty<string>(), new[] { "User not found." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userDto = new UserDto(user.Id, user.Email!, user.FullName, user.IsVerified, user.CreatedAt);

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
}
