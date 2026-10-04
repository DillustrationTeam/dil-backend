namespace ArtCommission.Application.ArtistStudio.ClientProfiles;

/// <summary>
/// Danh sách username bị cấm — dùng chung cho <c>UpdateClientProfileCommandValidator</c> và
/// <c>GetUsernameAvailabilityQuery</c> để không bị lệch nhau. Gồm các từ khóa hệ thống cộng với
/// các route thật dưới `dil-frontend/src/app` (chỉ liệt kê route không chứa dấu gạch ngang —
/// route có gạch ngang như "forgot-password" không thể trùng vì username chỉ cho phép chữ/số/"."/"_" ).
/// </summary>
public static class ReservedUsernames
{
    public static readonly HashSet<string> Set = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "api", "login", "register", "settings", "support", "help",
        "root", "moderator", "dillustration", "client", "creator",
        "me", "profile", "artworks", "auctions", "clients", "creators",
        "explore", "search", "ai", "checkout", "deposit", "notifications",
        "payout", "vouchers", "wallet", "workroom", "payment", "collections", "commission"
    };
}
