namespace ArtCommission.Application.Payment.Common;

/// <summary>Ví của người dùng.</summary>
public record WalletDto(
    Guid WalletId,
    decimal Balance,
    decimal LockedBalance,
    string Currency,
    string Status
);

/// <summary>Đơn nạp tiền trả về khi vừa tạo (UC48).</summary>
public record PaymentOrderCreatedDto(
    Guid PaymentOrderId,
    string OrderRef,
    decimal Amount,
    string Gateway,
    string PaymentOrderStatus,
    string? PaymentUrl,
    string? QrCode,
    string? AccountNumber,
    string? AccountName,
    string? BankBin
);

/// <summary>Chi tiết một đơn nạp tiền — dùng cho màn hình chờ thanh toán (poll).</summary>
public record PaymentOrderDetailDto(
    Guid PaymentOrderId,
    string OrderRef,
    decimal Amount,
    string Gateway,
    string PaymentOrderStatus,
    DateTimeOffset? PaidAt,
    decimal WalletBalance,
    string? PaymentUrl,
    /// <summary>Mã đơn phía cổng — dùng để đối soát và kiểm thử webhook.</summary>
    long GatewayOrderCode
);

/// <summary>Một dòng trong lịch sử nạp tiền.</summary>
public record PaymentOrderSummaryDto(
    Guid PaymentOrderId,
    string OrderRef,
    string Gateway,
    decimal Amount,
    string PaymentOrderStatus,
    DateTimeOffset CreatedAt
);

/// <summary>Một dòng trong lịch sử rút tiền.</summary>
public record PayoutRequestSummaryDto(
    Guid PayoutRequestId,
    decimal Amount,
    string PayoutStatus,
    string? PayoutNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt
);

/// <summary>Kết quả xử lý webhook.</summary>
public record WebhookHandledDto(bool Received, bool Applied, string? Reason);
