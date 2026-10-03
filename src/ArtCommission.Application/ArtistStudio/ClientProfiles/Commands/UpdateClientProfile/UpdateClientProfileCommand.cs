using ArtCommission.Application.ArtistStudio.ClientProfiles.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.ArtistStudio;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.ClientProfiles.Commands.UpdateClientProfile;

public record UpdateClientProfileCommand(
    Guid UserId,
    string? Username,
    List<string>? InterestTags,
    string? Country,
    string? Timezone,
    List<string>? PreferredLanguages,
    string? CurrentPassword = null
) : IRequest<(bool Success, ClientProfileDto? Data, string[] Errors)>;

public class UpdateClientProfileCommandValidator : AbstractValidator<UpdateClientProfileCommand>
{
    public UpdateClientProfileCommandValidator()
    {
        RuleFor(x => x.Username)
            .Matches("^[a-zA-Z0-9_.]+$").WithMessage("Username can only contain letters, numbers, '.' and '_'.")
            .Length(3, 30).WithMessage("Username must be 3-30 characters.")
            .Must(username => !ReservedUsernames.Set.Contains(username!.Trim()))
            .WithMessage("This username is reserved and cannot be used.")
            .When(x => !string.IsNullOrWhiteSpace(x.Username));

        RuleFor(x => x.InterestTags)
            .Must(tags => tags == null || tags.Count <= 10)
            .WithMessage("You can select up to 10 interest tags.");

        RuleFor(x => x.PreferredLanguages)
            .Must(langs => langs == null || langs.Count <= 10)
            .WithMessage("You can select up to 10 languages.");

        RuleFor(x => x.Country)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Country));

        RuleFor(x => x.Timezone)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Timezone));
    }
}

public class UpdateClientProfileCommandHandler : IRequestHandler<UpdateClientProfileCommand, (bool Success, ClientProfileDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identityService;

    public UpdateClientProfileCommandHandler(IApplicationDbContext db, IIdentityService identityService)
    {
        _db = db;
        _identityService = identityService;
    }

    public async Task<(bool Success, ClientProfileDto? Data, string[] Errors)> Handle(UpdateClientProfileCommand request, CancellationToken cancellationToken)
    {
        var validation = new UpdateClientProfileCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == request.UserId && !x.IsDeleted, cancellationToken);
        if (user is null)
        {
            return (false, null, new[] { "User not found." });
        }

        var profile = await _db.ClientProfiles.FirstOrDefaultAsync(x => x.UserId == request.UserId && !x.IsDeleted, cancellationToken);

        // Chuẩn hóa về chữ thường để "Nhat" và "nhat" không bị coi là 2 username khác nhau —
        // áp dụng nhất quán cho cả so sánh trùng lặp, so sánh đổi-hay-không, và giá trị lưu DB.
        var normalizedUsername = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim().ToLowerInvariant();

        if (normalizedUsername is not null)
        {
            var usernameTaken = await _db.ClientProfiles.AnyAsync(
                x => x.Username == normalizedUsername && x.UserId != request.UserId && !x.IsDeleted,
                cancellationToken);
            if (usernameTaken)
            {
                return (false, null, new[] { "This username is already taken." });
            }
        }

        if (profile is null)
        {
            profile = new ClientProfile
            {
                UserId = request.UserId,
                Username = normalizedUsername,
                UsernameChangedAt = normalizedUsername is not null ? DateTimeOffset.UtcNow : null,
                InterestTags = NormalizeList(request.InterestTags),
                Country = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.Trim(),
                Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? null : request.Timezone.Trim(),
                PreferredLanguages = NormalizeList(request.PreferredLanguages)
            };
            _db.ClientProfiles.Add(profile);
        }
        else
        {
            if (normalizedUsername is not null)
            {
                var isActualChange = !string.Equals(profile.Username, normalizedUsername, StringComparison.Ordinal);

                if (isActualChange)
                {
                    // Không còn giới hạn 30 ngày — đổi được bất cứ lúc nào, nhưng phải xác thực lại mật khẩu
                    // (nếu tài khoản có mật khẩu; tài khoản chỉ đăng nhập Google thì bỏ qua bước này).
                    var hasPassword = await _identityService.HasPasswordAsync(request.UserId, cancellationToken);
                    if (hasPassword)
                    {
                        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                        {
                            return (false, null, new[] { "Please enter your current password to change your username." });
                        }

                        var passwordValid = await _identityService.VerifyPasswordAsync(request.UserId, request.CurrentPassword, cancellationToken);
                        if (!passwordValid)
                        {
                            return (false, null, new[] { "Current password is incorrect." });
                        }
                    }

                    profile.UsernameChangedAt = DateTimeOffset.UtcNow;
                    profile.Username = normalizedUsername;
                }
            }

            if (request.InterestTags is not null)
            {
                profile.InterestTags = NormalizeList(request.InterestTags);
            }

            if (request.Country is not null)
            {
                profile.Country = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.Trim();
            }

            if (request.Timezone is not null)
            {
                profile.Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? null : request.Timezone.Trim();
            }

            if (request.PreferredLanguages is not null)
            {
                profile.PreferredLanguages = NormalizeList(request.PreferredLanguages);
            }

            profile.UpdatedAt = DateTimeOffset.UtcNow;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (LooksLikeUniqueViolation(ex))
        {
            // Đua nhau: người khác vừa đặt trùng username này trong lúc ta đang lưu.
            return (false, null, new[] { "This username is already taken." });
        }

        return (true, new ClientProfileDto(
            user.Id,
            user.FullName,
            user.AvatarUrl,
            user.CoverUrl,
            user.Bio,
            user.IsVerified,
            user.CreatedAt,
            profile.Username,
            profile.InterestTags,
            profile.Country,
            profile.Timezone,
            profile.PreferredLanguages,
            profile.UsernameChangedAt
        ), Array.Empty<string>());
    }

    private static List<string> NormalizeList(List<string>? values) =>
        values?.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList() ?? new List<string>();

    /// <summary>
    /// EF Core không có cách chung chung để phân biệt loại DbUpdateException theo provider (chỉ SQL Server
    /// ở đây), nên nhận diện qua thông điệp lỗi của provider.
    /// </summary>
    private static bool LooksLikeUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;

        return message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
               || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase);
    }
}
