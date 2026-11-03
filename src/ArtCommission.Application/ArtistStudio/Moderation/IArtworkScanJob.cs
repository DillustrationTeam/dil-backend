namespace ArtCommission.Application.ArtistStudio.Moderation;

/// <summary>
/// Hợp đồng background job xử lý quét AI cho tác phẩm (SCR-20 / UC28).
/// Cho phép các command / controller enqueue qua Hangfire mà không phụ thuộc trực tiếp vào tầng Infrastructure.
/// </summary>
public interface IArtworkScanJob
{
    /// <summary>
    /// Thực thi job quét AI bất đồng bộ cho một tác phẩm trong nền.
    /// Tải thông tin tác phẩm, gọi <see cref="IArtworkScanService"/>, cập nhật kết quả vào DB
    /// và cập nhật trạng thái kiểm duyệt nếu phát hiện vi phạm.
    /// </summary>
    /// <param name="artworkId">Mã định danh tác phẩm cần quét.</param>
    /// <param name="cancellationToken">CancellationToken.</param>
    Task ProcessArtworkScanAsync(Guid artworkId, CancellationToken cancellationToken = default);
}

