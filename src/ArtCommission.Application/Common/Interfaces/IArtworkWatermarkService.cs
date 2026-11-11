using System.Collections.Generic;
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

public sealed record ProtectedFileRecord(
    string FileName,
    string Sha256,
    bool HasDwtWatermark
);

public sealed record DeliverableWatermarkOutput(
    Stream OutputStream,
    string OutputFileName,
    string OutputContentType,
    string OuterSha256,
    long LengthBytes,
    IReadOnlyList<ProtectedFileRecord> Records
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
    /// Tạo bản hiển thị công khai giảm độ phân giải cho Marketplace/Đấu giá và nhúng DWT 64-bit.
    /// </summary>
    Task<WatermarkResult> CreateReducedMarketplacePreviewAsync(
        Stream sourceImageStream,
        ulong payload,
        string receiptToken,
        int maxDimension = 1200,
        CancellationToken ct = default);

    /// <summary>
    /// Gắn thuỷ vân cho sản phẩm bàn giao cuối cùng:
    /// nhúng DWT vào các file ảnh (đơn lẻ hoặc bên trong .zip) và tính dấu vân tay SHA-256 cho cả file kỹ thuật số (.psd/.clip/.ai/.pdf).
    /// </summary>
    Task<DeliverableWatermarkOutput> ProcessDeliverableWatermarkAsync(
        Stream sourceStream,
        string fileName,
        string contentType,
        ulong payload,
        string receiptToken,
        string receiptSummaryText,
        CancellationToken ct = default);

    /// <summary>
    /// Trích xuất mã số 64-bit ẩn từ ảnh nghi ngờ rò rỉ hoặc ảnh chụp màn hình.
    /// </summary>
    Task<ulong?> ExtractInvisibleWatermarkAsync(
        Stream imageStream,
        CancellationToken ct = default);

    /// <summary>
    /// Tính mã băm thị giác 64-bit (dHash) của ảnh để đối chiếu khi ảnh bị chụp màn hình / crop / nén.
    /// </summary>
    Task<long?> ComputeVisualHashAsync(
        Stream imageStream,
        bool trimBorders = false,
        CancellationToken ct = default);

    /// <summary>
    /// Phân tích ảnh chụp màn hình: tự cắt viền UI tối xung quanh, trích DWT trên phần ảnh đã cắt và sinh các biến thể dHash.
    /// </summary>
    Task<(ulong? TrimmedDwtPayload, IReadOnlyList<long> VisualHashes)> AnalyzeScreenshotVariantsAsync(
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
