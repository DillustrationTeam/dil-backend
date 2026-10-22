namespace ArtCommission.Application.Commission.DTOs;

public class SaveCreatorAddonRequest
{
    public Guid? Id { get; set; }
    public Guid? PackageId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string AllowedMilestone { get; set; } = "Sketch";
    public int ExtendDays { get; set; }
    public string? PresetKey { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ProposeAddonRequest
{
    public Guid? CreatorAddonId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string AllowedMilestone { get; set; } = "Sketch";
    public int ExtendDays { get; set; }
}

public record RespondAddonRequest(string Action);
public record AddonPreset(string Key, string Title, string AllowedMilestone);

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
        new("rush-order", "Giao gấp 24–48h", "Final")
    ];

    public static void Validate(string title, decimal price, string milestone, int days, string? description)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200 || description?.Length > 2000
            || price <= 0 || price > 999999999999m || decimal.Truncate(price) != price
            || days < 0 || days > 365 || milestone is not ("Sketch" or "Lineart" or "Final" or "Any"))
            throw new ArgumentException("Phụ phí cần tên (tối đa 200 ký tự), giá nguyên VND dương, mốc hợp lệ và gia hạn 0–365 ngày.");
    }

    // Packages may have an extra coloring stage. The first stage is Sketch,
    // the last is Final, intermediate stages permit only Lineart variations.
    public static string Stage(int sequence, int count) => sequence == 1 ? "Sketch" : sequence == count ? "Final" : "Lineart";
}
