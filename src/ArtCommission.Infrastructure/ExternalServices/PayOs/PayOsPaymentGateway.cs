using System.Text.Json;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Exceptions;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using PayOsCreateRequest = PayOS.Models.V2.PaymentRequests.CreatePaymentLinkRequest;
using PayOsLinkItem = PayOS.Models.V2.PaymentRequests.PaymentLinkItem;

namespace ArtCommission.Infrastructure.ExternalServices.PayOs;

/// <summary>
/// Bọc SDK payOS lại sau <see cref="IPaymentGateway"/>.
/// Không để type của SDK rò rỉ ra tầng Application.
/// </summary>
public class PayOsPaymentGateway : IPaymentGateway
{
    /// <summary>
    /// payOS giới hạn orderCode theo JS safe integer (Number.MAX_SAFE_INTEGER).
    /// Vượt ngưỡng này SDK sẽ ném ArgumentOutOfRangeException.
    /// </summary>
    public const long MaxSafeOrderCode = 9_007_199_254_740_991L;

    private readonly PayOSClient _client;
    private readonly PayOsOptions _options;
    private readonly ILogger<PayOsPaymentGateway> _logger;

    private static readonly JsonSerializerOptions WebhookJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PayOsPaymentGateway(
        PayOSClient client,
        IOptions<PayOsOptions> options,
        ILogger<PayOsPaymentGateway> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public string GatewayName => Domain.Enums.PaymentGatewayNames.PayOS;

    public async Task<PaymentLinkResult> CreatePaymentLinkAsync(
        PaymentLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OrderCode <= 0 || request.OrderCode > MaxSafeOrderCode)
        {
            throw new PaymentGatewayException(
                $"orderCode phải nằm trong khoảng 1..{MaxSafeOrderCode} (giới hạn JS safe integer của payOS).");
        }

        if (request.Amount <= 0)
        {
            throw new PaymentGatewayException("Số tiền nạp phải lớn hơn 0.");
        }

        var payload = new PayOsCreateRequest
        {
            OrderCode = request.OrderCode,
            Amount = request.Amount,
            Description = request.Description,
            ReturnUrl = request.ReturnUrl,
            CancelUrl = request.CancelUrl,
            BuyerName = request.BuyerName,
            BuyerEmail = request.BuyerEmail,
            ExpiredAt = request.ExpiredAt?.ToUnixTimeSeconds(),
            Items = request.Items?
                .Select(i => new PayOsLinkItem
                {
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Price = i.Price
                })
                .ToList()
        };

        try
        {
            var response = await _client.PaymentRequests.CreateAsync(
                payload,
                new RequestOptions<PayOsCreateRequest> { CancellationToken = cancellationToken });

            return new PaymentLinkResult(
                PaymentLinkId: response.PaymentLinkId,
                CheckoutUrl: response.CheckoutUrl,
                QrCode: response.QrCode,
                AccountNumber: response.AccountNumber,
                AccountName: response.AccountName,
                Bin: response.Bin,
                Status: response.Status.ToString().ToUpperInvariant(),
                ExpiredAt: ToDateTimeOffset(response.ExpiredAt)
            );
        }
        catch (PayOSException ex)
        {
            _logger.LogError(ex, "payOS từ chối tạo link thanh toán cho orderCode {OrderCode}", request.OrderCode);
            throw new PaymentGatewayException("Cổng thanh toán từ chối tạo link thanh toán.", ex);
        }
    }

    public async Task<PaymentLinkInfo> GetPaymentLinkAsync(
        long orderCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var link = await _client.PaymentRequests.GetAsync(
                orderCode,
                new RequestOptions { CancellationToken = cancellationToken });

            return new PaymentLinkInfo(
                OrderCode: link.OrderCode,
                Amount: link.Amount,
                AmountPaid: link.AmountPaid,
                AmountRemaining: link.AmountRemaining,
                Status: link.Status.ToString().ToUpperInvariant(),
                PaymentLinkId: link.Id,
                Transactions: link.Transactions?
                    .Select(t => new PaymentLinkTransactionInfo(
                        Reference: t.Reference,
                        Amount: t.Amount,
                        AccountNumber: t.AccountNumber,
                        Description: t.Description,
                        TransactionDateTime: t.TransactionDateTime))
                    .ToList() ?? []
            );
        }
        catch (PayOSException ex)
        {
            _logger.LogError(ex, "Không đối chiếu được trạng thái link thanh toán orderCode {OrderCode}", orderCode);
            throw new PaymentGatewayException("Không đối chiếu được trạng thái thanh toán với cổng.", ex);
        }
    }

    public async Task<PaymentWebhookData> VerifyWebhookAsync(
        JsonElement webhookBody,
        CancellationToken cancellationToken = default)
    {
        Webhook? webhook;
        try
        {
            webhook = webhookBody.Deserialize<Webhook>(WebhookJsonOptions);
        }
        catch (JsonException ex)
        {
            // Body không đúng shape webhook của payOS
            throw new PaymentSignatureException("Dữ liệu webhook không hợp lệ.", ex);
        }

        if (webhook?.Data is null || string.IsNullOrEmpty(webhook.Signature))
        {
            throw new PaymentSignatureException("Dữ liệu webhook không hợp lệ.");
        }

        WebhookData verified;
        try
        {
            // SDK kiểm HMAC-SHA256 trên chuỗi key=value đã sắp xếp alphabet của object `data`,
            // dùng ChecksumKey. Sai chữ ký => ném WebhookException.
            verified = await _client.Webhooks.VerifyAsync(webhook);
        }
        catch (Exception ex) when (ex is WebhookException or InvalidSignatureException or PayOSException)
        {
            _logger.LogWarning("Webhook payOS sai chữ ký cho orderCode {OrderCode}", webhook.Data.OrderCode);
            throw new PaymentSignatureException("Chữ ký webhook không hợp lệ.", ex);
        }

        return new PaymentWebhookData(
            Success: webhook.Success && string.Equals(verified.Code, "00", StringComparison.Ordinal),
            Code: verified.Code,
            Description: webhook.Description,
            OrderCode: verified.OrderCode,
            Amount: verified.Amount,
            Reference: verified.Reference,
            PaymentLinkId: verified.PaymentLinkId,
            AccountNumber: verified.AccountNumber,
            TransactionDateTime: verified.TransactionDateTime,
            Currency: verified.Currency,
            CounterAccountName: verified.CounterAccountName,
            CounterAccountNumber: verified.CounterAccountNumber
        );
    }

    public async Task<bool> ConfirmWebhookAsync(
        string webhookUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            throw new PaymentGatewayException("Webhook URL không được để trống.");
        }

        try
        {
            var result = await _client.Webhooks.ConfirmAsync(
                webhookUrl,
                new RequestOptions<ConfirmWebhookRequest> { CancellationToken = cancellationToken });
            _logger.LogInformation("Đăng ký webhook payOS thành công: {WebhookUrl}", result.WebhookUrl);
            return true;
        }
        catch (PayOSException ex)
        {
            _logger.LogError(ex, "Đăng ký webhook payOS thất bại cho URL {WebhookUrl}", webhookUrl);
            throw new PaymentGatewayException("Không đăng ký được webhook với cổng thanh toán.", ex);
        }
    }

    private static DateTimeOffset? ToDateTimeOffset(long? unixSeconds) =>
        unixSeconds.HasValue
            ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value)
            : null;
}
