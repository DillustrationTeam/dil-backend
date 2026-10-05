using System.Net;
using System.Net.Sockets;
using ArtCommission.Application.Payment.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Exceptions;
using PayOS.Models.V1.Payouts;

namespace ArtCommission.Infrastructure.ExternalServices.PayOs;

/// <summary>
/// Thực hiện gọi API chi tiền tự động (Disbursement) qua payOS SDK.
/// </summary>
public class PayOsPayoutService : IPayOsPayoutService
{
    private readonly PayOsOptions _options;
    private readonly ILogger<PayOsPayoutService> _logger;

    public PayOsPayoutService(
        IOptions<PayOsOptions> options,
        ILogger<PayOsPayoutService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PayOsPayoutResult> CreatePayoutAsync(
        Application.Payment.Common.PayOsPayoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsPayoutConfigured)
        {
            _logger.LogError("payOS chưa được cấu hình ClientId, ApiKey hoặc ChecksumKey cho Kênh chi.");
            return new PayOsPayoutResult(false, null, "NOT_CONFIGURED", "Cấu hình Kênh chi payOS chưa đầy đủ trên server.");
        }

        // Tạo custom SocketsHttpHandler ép kết nối qua IPv4 để khớp với IP Whitelist trên payOS
        var ipv4Handler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, ct) =>
            {
                var entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, ct);
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(entry.AddressList[0], context.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
        };

        var httpClient = new HttpClient(ipv4Handler);

        var client = new PayOSClient(new global::PayOS.PayOSOptions
        {
            ClientId = _options.EffectivePayoutClientId,
            ApiKey = _options.EffectivePayoutApiKey,
            ChecksumKey = _options.EffectivePayoutChecksumKey,
            HttpClient = httpClient
        });

        _logger.LogInformation(
            "Gửi lệnh chi tiền payOS qua SDK: Ref {ReferenceId}, Amount {Amount}, To {ToBin}-{ToAccountNumber}",
            request.ReferenceId, request.Amount, request.ToBin, request.ToAccountNumber);

        try
        {
            var payoutRequest = new global::PayOS.Models.V1.Payouts.PayoutRequest
            {
                ReferenceId = request.ReferenceId,
                Amount = request.Amount,
                Description = request.Description,
                ToBin = request.ToBin,
                ToAccountNumber = request.ToAccountNumber
            };

            var idempotencyKey = Guid.NewGuid().ToString();
            var payout = await client.Payouts.CreateAsync(payoutRequest, idempotencyKey);

            var txRef = payout?.Id ?? request.ReferenceId;
            var approvalState = payout != null ? payout.ApprovalState.ToString() : "COMPLETED";

            _logger.LogInformation(
                "Tạo lệnh chi payOS thành công: PayoutId {PayoutId}, State {State}",
                txRef, approvalState);

            return new PayOsPayoutResult(true, txRef, "00", $"Chi tiền thành công ({approvalState})");
        }
        catch (PayOSException ex)
        {
            _logger.LogError(ex, "payOS từ chối lệnh chi cho Ref {ReferenceId}", request.ReferenceId);
            return new PayOsPayoutResult(false, null, ex.GetType().Name, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi gọi payOS Payout SDK cho Ref {ReferenceId}", request.ReferenceId);
            return new PayOsPayoutResult(false, null, "EXCEPTION", ex.Message);
        }
    }
}
