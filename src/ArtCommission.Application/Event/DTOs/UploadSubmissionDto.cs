namespace ArtCommission.Application.Event.DTOs;

/// <summary>
/// DTO gửi lên để nộp bài dự thi sự kiện (Hỗ trợ cả 2 cách: chọn tranh có sẵn hoặc upload tranh mới).
/// </summary>
public sealed record UploadSubmissionDto
{
    /// <summary>
    /// Id của sự kiện nộp bài (tùy chọn trong body nếu đã truyền qua query string hoặc route).
    /// </summary>
    public Guid? EventId { get; set; }

    /// <summary>
    /// Id tranh có sẵn trong kho của người dùng (Nếu chọn tranh đã có).
    /// </summary>
    public Guid? ArtworkId { get; set; }

    /// <summary>
    /// Tiêu đề bài dự thi. Nếu để trống khi chọn ArtworkId, sẽ lấy tiêu đề của Artwork đó.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Mô tả bài dự thi hoặc lời nhắn của tác giả.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// URL ảnh tác phẩm (Bắt buộc nếu không truyền ArtworkId).
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// URL ảnh thu nhỏ (Thumbnail).
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Đánh dấu tranh có do AI tạo hay không. Mặc định là false.
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
