using ArtCommission.Application.Common.DTOs;

namespace ArtCommission.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<(bool Success, Guid UserId, string[] Errors)> RegisterUserAsync(string email, string password, string fullName, string? role = null, CancellationToken cancellationToken = default);
    Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> AuthenticateUserAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsEmailUniqueAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> VerifyUserEmailAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(bool Success, string[] Errors)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task<string> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default);
    Task<(bool Success, string[] Errors)> ResetPasswordAsync(string email, string resetToken, string newPassword, CancellationToken cancellationToken = default);
    Task<(bool Success, string[] Errors)> AddCreatorRoleAsync(Guid userId, CancellationToken cancellationToken = default);
}
