namespace ArtCommission.Application.Event.DTOs;

/// <summary>
/// DTO gửi lên để Creator chỉnh sửa bài dự thi của chính mình.
/// Hỗ trợ cả 3 cách:
/// 1. Chỉ sửa Title / Description.
/// 2. Đổi sang một Artwork có sẵn khác trong kho của mình (truyền ArtworkId).
/// 3. Upload tranh mới từ máy (truyền ImageUrl, ThumbnailUrl, IsAiGenerated, Tags).
/// </summary>
public sealed record UpdateSubmissionDto
{
    /// <summary>
    /// Tiêu đề bài dự thi (tùy chọn cập nhật).
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Mô tả bài dự thi (tùy chọn cập nhật).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Id tác phẩm có sẵn trong kho nếu muốn trỏ sang tác phẩm khác của mình.
    /// </summary>
    public Guid? ArtworkId { get; set; }

    /// <summary>
    /// URL ảnh mới upload từ máy (nếu thí sinh tải bản vẽ mới lên thay thế).
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// URL thumbnail ảnh mới (tùy chọn).
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Cờ đánh dấu ảnh mới có phải do AI tạo hay không. Mặc định là false.
    /// </summary>
    public bool IsAiGenerated { get; set; } = false;

    /// <summary>
    /// Điểm tự động nhận diện AI (nếu có).
    /// </summary>
    public decimal? AiDetectionScore { get; set; }

    /// <summary>
    /// Danh sách các tag thể loại / phong cách cho tranh mới.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; set; }
}
