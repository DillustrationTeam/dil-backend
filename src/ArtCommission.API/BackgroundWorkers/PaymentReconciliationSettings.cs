namespace ArtCommission.API.BackgroundWorkers;

/// <summary>
/// Cấu hình job đối soát thanh toán. Bind từ section "PaymentReconciliation".
/// </summary>
public class PaymentReconciliationSettings
{
    public const string SectionName = "PaymentReconciliation";

    /// <summary>Bật/tắt job. Mặc định bật.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Chu kỳ chạy job (phút).</summary>
    public int IntervalMinutes { get; set; } = 5;

    /// <summary>Chỉ đối soát các đơn Pending cũ hơn bao nhiêu phút (tránh đụng đơn vừa tạo).</summary>
    public int OlderThanMinutes { get; set; } = 3;

    /// <summary>Số đơn tối đa xử lý mỗi lượt.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Chờ bao lâu sau khi khởi động trước khi chạy lượt đầu.</summary>
    public int InitialDelaySeconds { get; set; } = 30;
}
