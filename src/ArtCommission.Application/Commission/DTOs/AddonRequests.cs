namespace ArtCommission.Application.Commission.DTOs;

public record AddonPreset(string Key, string Title, string AllowedMilestone);

public record SaveCreatorAddonRequest
{
    public Guid? Id { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public string AllowedMilestone { get; init; } = "Sketch";
    public int ExtendDays { get; init; }
    public string? PresetKey { get; init; }
    public Guid? PackageId { get; init; }
    public bool IsActive { get; init; } = true;
}

public record ProposeAddonRequest
{
    public Guid? CreatorAddonId { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public string AllowedMilestone { get; init; } = "Sketch";
    public int ExtendDays { get; init; }
}

public record RespondAddonRequest(string Action);

public static class AddonRules
{
    public static readonly AddonPreset[] Presets =
    [
        new("extra-character", "Thêm nhân vật", "Sketch"),
        new("detailed-background", "Background chi tiết", "Sketch"),
        new("pet-weapon", "Thêm thú cưng / Vũ khí lớn", "Sketch"),
        new("complex-pose", "Đổi dáng phức tạp", "Sketch"),
        new("alt-expression", "Thêm biểu cảm mặt", "Lineart"),
        new("alt-palette", "Thêm phối màu khác", "Lineart"),
        new("costume-pattern", "Hoa văn trang phục nhỏ", "Lineart"),
        new("source-file", "File nguồn PSD tách layer", "Final"),
        new("vfx-lighting", "Hiệu ứng ánh sáng / Phép thuật", "Final"),
        new("commercial-license", "Nâng cấp bản quyền thương mại", "Final"),
        new("rush-order", "Giao gấp 24-48h", "Any")
    ];

    public static bool IsValidMilestone(string milestone)
    {
        if (string.IsNullOrWhiteSpace(milestone)) return false;
        if (milestone == "Any") return true;
        var parts = milestone.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && parts.All(p => p is "Sketch" or "Lineart" or "Final");
    }

    public static bool IsStageAllowed(string allowed, string currentStage)
    {
        if (string.Equals(allowed, "Any", StringComparison.OrdinalIgnoreCase)) return true;
        var parts = allowed.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Contains(currentStage, StringComparer.OrdinalIgnoreCase);
    }

    public static void Validate(string title, decimal price, string milestone, int days, string? description)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200 || description?.Length > 2000
            || price <= 0 || price > 999999999999m || decimal.Truncate(price) != price
            || days < 0 || days > 365 || !IsValidMilestone(milestone))
            throw new ArgumentException("Phụ phí cần tên (tối đa 200 ký tự), giá nguyên VND dương, mốc hợp lệ và gia hạn 0–365 ngày.");
    }

    // Packages may have an extra coloring stage. The first stage is Sketch,
    // the last is Final, intermediate stages permit only Lineart variations.
    public static string Stage(int sequence, int count) => sequence == 1 ? "Sketch" : sequence == count ? "Final" : "Lineart";
}
