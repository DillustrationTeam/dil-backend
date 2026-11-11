using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.ArtistStudio;

/// <summary>
/// Danh mục lớn (broad category) dùng để phân loại tác phẩm.
/// Creator bắt buộc chọn 1 khi đăng tải; Admin có thể tạo/sửa/xóa.
/// </summary>
public class ArtworkCategory : BaseEntity
{
    /// <summary>Tên hiển thị, ví dụ: "Anime / Manga"</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Slug URL-friendly, ví dụ: "anime-manga"</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Mô tả ngắn cho trang Admin</summary>
    public string? Description { get; set; }

    /// <summary>Thứ tự hiển thị trên UI (tăng dần)</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Trạng thái hoạt động; danh mục inactive không xuất hiện cho Creator chọn</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<Artwork> Artworks { get; set; } = new List<Artwork>();
}
