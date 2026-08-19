namespace ArtCommission.Application.Common.DTOs;

public record AuthResponseDto(
    UserDto User,
    IReadOnlyList<string> Roles,
    TokenDto Tokens
);
