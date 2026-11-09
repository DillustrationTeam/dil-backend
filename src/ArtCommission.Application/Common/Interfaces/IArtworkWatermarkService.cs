using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArtCommission.Application.Common.Interfaces;

public record WatermarkResult(
    Stream ProcessedStream,
    string ContentType,
    long NumericPayload,
    string ReceiptToken
);

public interface IArtworkWatermarkService
{
    /// <summary>
    /// Nhúng thuỷ vân số ẩn (DWT / Frequency Domain) vào dải tần số của ảnh.
    /// Mắt thường không nhận ra thay đổi, nhưng kháng nén JPEG, resize và chụp lại màn hình.
    /// </summary>
    Task<WatermarkResult> EmbedInvisibleWatermarkAsync(
        Stream sourceImageStream,
        ulong payload,
        string receiptToken,
        CancellationToken ct = default);

    /// <summary>
    /// Trích xuất mã số 64-bit ẩn từ ảnh nghi ngờ rò rỉ hoặc ảnh chụp màn hình.
    /// </summary>
    Task<ulong?> ExtractInvisibleWatermarkAsync(
        Stream imageStream,
        CancellationToken ct = default);

    /// <summary>
    /// Tạo lớp phủ watermark thương hiệu / Moiré pattern bán trong suốt (được render ở preview).
    /// </summary>
    Task<Stream> ApplyVisibleSecurityOverlayAsync(
        Stream sourceImageStream,
        string creatorName,
        string receiptToken,
        bool applyMoirePattern = true,
        CancellationToken ct = default);
}
