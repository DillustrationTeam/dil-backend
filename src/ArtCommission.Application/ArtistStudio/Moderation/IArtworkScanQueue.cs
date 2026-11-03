namespace ArtCommission.Application.ArtistStudio.Moderation;

/// <summary>
/// Hàng đợi yêu cầu quét AI cho tác phẩm (SCR-20 / UC28).
/// Cho phép các command / controller đưa tác phẩm vào hàng đợi quét mà không phụ thuộc trực tiếp vào Hangfire hay cơ chế hàng đợi cụ thể.
/// </summary>
public interface IArtworkScanQueue
{
    /// <summary>
    /// Đưa tác phẩm vào hàng đợi quét AI trong nền.
    /// </summary>
    /// <param name="artworkId">Mã định danh tác phẩm.</param>
    void Enqueue(Guid artworkId);
}

