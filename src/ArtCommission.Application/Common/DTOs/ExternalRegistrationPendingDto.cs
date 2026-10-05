namespace ArtCommission.Application.Common.DTOs;

/// <summary>
/// Thông tin từ external provider (vd Google) khi phát hiện đây là tài khoản HOÀN TOÀN MỚI —
/// chưa tạo user, chờ FE thu thập username + password rồi gọi complete-registration.
/// </summary>
public record ExternalRegistrationPendingDto(
    string Provider,
    string ProviderKey,
    string Email,
    bool EmailVerified,
    string? FullName
);
