namespace ArtCommission.Application.Common.DTOs;

public record UserDto(
    Guid Id,
    string Email,
    string FullName,
    bool IsVerified,
    DateTimeOffset CreatedAt
);
