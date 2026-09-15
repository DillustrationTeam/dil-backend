using System.Text.Json;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Payment.Commands.HandlePaymentWebhook;

/// <summary>
/// UC48 — POST /api/v1/payments/webhooks/{gateway}
/// Cổng thanh toán gọi IPN/callback về hệ thống.
///
/// Quy tắc bắt buộc, theo đúng thứ tự:
///   1. Xác thực chữ ký HMAC-SHA256 — sai thì 400, KHÔNG lộ chi tiết
///   2. Cổng báo thất bại → ghi log, trả 200, KHÔNG cộng tiền
///   3. Tìm đơn theo (cổng, orderCode)
///   4. Idempotency: đơn đã Paid hoặc mã giao dịch đã dùng → trả 200 luôn
///   5. Đối chiếu số tiền — lệch thì đánh Failed, KHÔNG cộng tiền
///   6. Cộng ví + ghi sổ cái + đổi trạng thái trong MỘT transaction ACID
///   7. Trả 200 { received: true }
/// </summary>
public record HandlePaymentWebhookCommand(string? Gateway, JsonElement Body)
    : IRequest<WebhookResult>;

/// <summary>Kết quả xử lý webhook. HttpStatus do controller dùng để trả về.</summary>
public record WebhookResult(int HttpStatus, WebhookHandledDto? Data);

public class HandlePaymentWebhookCommandHandler : IRequestHandler<HandlePaymentWebhookCommand, WebhookResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IEnumerable<IPaymentGateway> _gateways;
    private readonly IPaymentSettlementService _settlement;
    private readonly ILogger<HandlePaymentWebhookCommandHandler> _logger;

    public HandlePaymentWebhookCommandHandler(
        IApplicationDbContext db,
        IEnumerable<IPaymentGateway> gateways,
        IPaymentSettlementService settlement,
        ILogger<HandlePaymentWebhookCommandHandler> logger)
    {
        _db = db;
        _gateways = gateways;
        _settlement = settlement;
        _logger = logger;
    }

    public async Task<WebhookResult> Handle(
        HandlePaymentWebhookCommand request,
        CancellationToken cancellationToken)
    {
        // --- Bước 1: cổng có được hỗ trợ không ---
        if (string.IsNullOrWhiteSpace(request.Gateway))
        {
            return Rejected("Thiếu tham số gateway.");
        }

        var gateway = _gateways.FirstOrDefault(
            g => string.Equals(g.GatewayName, request.Gateway, StringComparison.OrdinalIgnoreCase));

        if (gateway is null)
        {
            _logger.LogWarning("Webhook gọi vào cổng không hỗ trợ: {Gateway}", request.Gateway);
            return Rejected("Cổng thanh toán không được hỗ trợ.");
        }

        // --- Bước 2: xác thực chữ ký TRƯỚC MỌI THỨ ---
        PaymentWebhookData webhook;
        try
        {
            webhook = await gateway.VerifyWebhookAsync(request.Body, cancellationToken);
        }
        catch (PaymentSignatureException)
        {
            // Không trả lý do cụ thể ra ngoài
            return Rejected("Chữ ký webhook không hợp lệ.");
        }

        // --- Bước 3: tìm đơn ---
        var order = await _db.PaymentOrders
            .FirstOrDefaultAsync(
                o => o.PayOsOrderCode == webhook.OrderCode
                     && o.Gateway == PaymentGateway.PayOS
                     && !o.IsDeleted,
                cancellationToken);

        if (order is null)
        {
            _logger.LogWarning(
                "Webhook payOS cho orderCode {OrderCode} nhưng không tìm thấy đơn nào", webhook.OrderCode);
            // Trả 200 để cổng không retry vô hạn một đơn không tồn tại
            return Accepted(new WebhookHandledDto(true, false, "Không tìm thấy đơn tương ứng."));
        }

        // --- Bước 4: cổng báo giao dịch thất bại ---
        if (!webhook.Success)
        {
            _logger.LogInformation(
                "Webhook payOS báo thất bại cho đơn {OrderRef}, code={Code}", order.OrderRef, webhook.Code);

            if (order.Status == PaymentOrderStatus.Pending)
            {
                order.Status = PaymentOrderStatus.Failed;
                order.FailureReason = $"Cổng báo thất bại (code {webhook.Code}).";
                order.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return Accepted(new WebhookHandledDto(true, false, "Cổng báo giao dịch thất bại."));
        }

        // --- Bước 5+6: idempotency, đối chiếu tiền, cộng ví trong 1 transaction ---
        var outcome = await _settlement.ApplyPaidOrderAsync(
            order.Id,
            webhook.Reference,
            webhook.Amount,
            ParseTransactionTime(webhook.TransactionDateTime),
            source: "webhook",
            cancellationToken);

        if (outcome.Applied)
        {
            _logger.LogInformation(
                "Đã cộng {Amount} VND vào ví cho đơn {OrderRef} (giao dịch {Reference})",
                order.Amount, order.OrderRef, webhook.Reference);
        }
        else
        {
            _logger.LogInformation(
                "Webhook đơn {OrderRef} không áp dụng: {Reason}", order.OrderRef, outcome.Reason);
        }

        // Dù bỏ qua hay áp dụng đều trả 200 để cổng ngừng retry
        return Accepted(new WebhookHandledDto(true, outcome.Applied, outcome.Reason));
    }

    /// <summary>400 — chữ ký sai hoặc cổng không hợp lệ.</summary>
    private static WebhookResult Rejected(string reason) => new(400, null);

    /// <summary>200 — đã nhận và xử lý xong (kể cả trường hợp bỏ qua).</summary>
    private static WebhookResult Accepted(WebhookHandledDto data) => new(200, data);

    /// <summary>payOS trả "yyyy-MM-dd HH:mm:ss" theo giờ Việt Nam (UTC+7).</summary>
    private static DateTimeOffset? ParseTransactionTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTime.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsed))
        {
            return null;
        }

        var unspecified = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        var vnOffset = TimeSpan.FromHours(7);

        return new DateTimeOffset(unspecified, vnOffset);
    }
}
