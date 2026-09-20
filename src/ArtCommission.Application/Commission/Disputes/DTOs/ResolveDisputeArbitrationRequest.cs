namespace ArtCommission.Application.Commission.Disputes.DTOs;

/// <summary>
/// Payload gửi lên khi Admin thực thi phán quyết trọng tài và phân bổ tiền ký quỹ Escrow (SCR-22 / UC30).
/// </summary>
public record ResolveDisputeArbitrationRequest
{
    /// <summary>
    /// Tỷ lệ hoàn tiền cho Client (từ 0% đến 100%).
    /// 100% = Client thắng toàn bộ (hoàn 100% cho Client).
    /// 0% = Creator thắng toàn bộ (giải ngân 100% cho Creator, trừ platform fee nếu có).
    /// 1% - 99% = Phân chia tiền ký quỹ giữa hai bên.
    /// </summary>
    public decimal ClientRefundPercent { get; init; }

    /// <summary>
    /// Ghi chú phán quyết / Căn cứ giải quyết tranh chấp của Moderator / Admin (Bắt buộc).
    /// </summary>
    public string AdminNote { get; init; } = string.Empty;

    /// <summary>
    /// Tùy chọn phán quyết mở rộng (ClientWin100, ArtistWin100, SplitCustom).
    /// </summary>
    public string? Resolution { get; init; }
}
