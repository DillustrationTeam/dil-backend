using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Payment.Commands.CreatePaymentOrder;

/// <summary>
/// UC48 — POST /api/v1/payments/orders
/// Người dùng nhập số tiền nạp và chọn cổng thanh toán.
/// </summary>
public record CreatePaymentOrderCommand(
    Guid UserId,
    decimal Amount,
    string Gateway,
    string? ReturnUrl = null,
    string? CancelUrl = null,
    /// <summary>
    /// Mã đơn do phía client tự đặt (tuỳ chọn) — để đối soát với hệ thống ngoài.
    /// Bỏ trống thì hệ thống tự sinh.
    /// </summary>
    string? ClientOrderRef = null
) : IRequest<(bool Success, PaymentOrderCreatedDto? Data, string[] Errors)>;

public class CreatePaymentOrderCommandValidator : AbstractValidator<CreatePaymentOrderCommand>
{
    public CreatePaymentOrderCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Không xác định được người dùng.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền nạp phải lớn hơn 0.")
            .LessThanOrEqualTo(500_000_000m).WithMessage("Số tiền nạp tối đa 500.000.000 VND mỗi giao dịch.");

        RuleFor(x => x.Gateway)
            .NotEmpty().WithMessage("Vui lòng chọn cổng thanh toán.")
            .Must(g => PaymentGatewayNames.All.Contains(g))
            .WithMessage($"Cổng thanh toán không hợp lệ. Hỗ trợ: {string.Join(", ", PaymentGatewayNames.All)}.");

        RuleFor(x => x.ClientOrderRef)
            .MaximumLength(50).WithMessage("Mã đơn của bạn tối đa 50 ký tự.")
            .Matches(@"^[0-9A-Za-z\-_]+$")
            .WithMessage("Mã đơn của bạn chỉ được chứa chữ, số, gạch ngang và gạch dưới.")
            .When(x => !string.IsNullOrWhiteSpace(x.ClientOrderRef));
    }
}

public class CreatePaymentOrderCommandHandler
    : IRequestHandler<CreatePaymentOrderCommand, (bool, PaymentOrderCreatedDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly IWalletService _walletService;
    private readonly ILogger<CreatePaymentOrderCommandHandler> _logger;

    public CreatePaymentOrderCommandHandler(
        IApplicationDbContext db,
        IPaymentGateway gateway,
        IWalletService walletService,
        ILogger<CreatePaymentOrderCommandHandler> logger)
    {
        _db = db;
        _gateway = gateway;
        _walletService = walletService;
        _logger = logger;
    }

    public async Task<(bool, PaymentOrderCreatedDto?, string[])> Handle(
        CreatePaymentOrderCommand request,
        CancellationToken cancellationToken)
    {
        var validators = new CreatePaymentOrderCommandValidator().Validate(request);
        if (!validators.IsValid)
        {
            return (false, null, validators.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // Cổng được cấu hình cụ thể chưa? Hiện chỉ payOS được triển khai.
        if (!string.Equals(request.Gateway, PaymentGatewayNames.PayOS, StringComparison.OrdinalIgnoreCase))
        {
            return (false, null, [$"Cổng {request.Gateway} chưa được triển khai. Hiện chỉ hỗ trợ PayOS."]);
        }

        var wallet = await _walletService.GetOrCreateWalletAsync(request.UserId, cancellationToken);

        // Mã đơn hiển thị: client tự đặt thì dùng, không thì hệ thống sinh
        var clientRef = request.ClientOrderRef?.Trim();
        if (!string.IsNullOrEmpty(clientRef))
        {
            var refTaken = await _db.PaymentOrders
                .AnyAsync(o => o.OrderRef == clientRef && !o.IsDeleted, cancellationToken);

            if (refTaken)
            {
                return (false, null, ["Mã đơn bạn nhập đã tồn tại, vui lòng chọn mã khác."]);
            }
        }

        var orderRef = string.IsNullOrEmpty(clientRef) ? GenerateOrderRef() : clientRef;
        var payOsOrderCode = await GenerateOrderCodeAsync(cancellationToken);

        var order = new PaymentOrder
        {
            OrderRef = orderRef,
            UserId = request.UserId,
            WalletId = wallet.Id,
            Amount = request.Amount,
            Gateway = PaymentGateway.PayOS,
            Status = PaymentOrderStatus.Pending,
            PayOsOrderCode = payOsOrderCode
        };

        _db.PaymentOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        // Nội dung chuyển khoản: chỉ ASCII, tối đa 25 ký tự (giới hạn hiển thị của ngân hàng/payOS)
        var description = BuildDescription(orderRef);

        try
        {
            var link = await _gateway.CreatePaymentLinkAsync(
                new PaymentLinkRequest(
                    OrderCode: payOsOrderCode,
                    Amount: (long)request.Amount,
                    Description: description,
                    ReturnUrl: request.ReturnUrl ?? string.Empty,
                    CancelUrl: request.CancelUrl ?? string.Empty,
                    BuyerName: null,
                    BuyerEmail: null),
                cancellationToken);

            order.PaymentLinkId = link.PaymentLinkId;
            order.CheckoutUrl = link.CheckoutUrl;
            order.QrCode = link.QrCode;
            order.ExpiredAt = link.ExpiredAt;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Đã tạo đơn nạp {OrderRef} (orderCode {OrderCode}) cho user {UserId}, số tiền {Amount} VND",
                order.OrderRef, order.PayOsOrderCode, order.UserId, order.Amount);

            return (true, new PaymentOrderCreatedDto(
                PaymentOrderId: order.Id,
                OrderRef: order.OrderRef,
                Amount: order.Amount,
                Gateway: order.Gateway.ToString(),
                PaymentOrderStatus: order.Status.ToString(),
                PaymentUrl: link.CheckoutUrl,
                QrCode: link.QrCode,
                AccountNumber: link.AccountNumber,
                AccountName: link.AccountName,
                BankBin: link.Bin
            ), []);
        }
        catch (PaymentGatewayException ex)
        {
            // Giữ lại đơn ở trạng thái Failed để tra soát, không xoá
            order.Status = PaymentOrderStatus.Failed;
            order.FailureReason = ex.Message;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Tạo link thanh toán thất bại cho đơn {OrderRef}", order.OrderRef);
            return (false, null, ["Không tạo được link thanh toán. Vui lòng thử lại sau."]);
        }
    }

    /// <summary>Mã đơn hiển thị cho người dùng. Tiền tố DIL, 12 chữ số.</summary>
    private static string GenerateOrderRef() =>
        $"DIL{DateTimeOffset.UtcNow:yyMMddHHmmss}{Random.Shared.Next(10, 99)}";

    /// <summary>
    /// Sinh orderCode dạng số cho payOS (bắt buộc long, không dùng Guid).
    /// Ghép timestamp giây với 3 chữ số ngẫu nhiên => duy nhất và nằm trong ngưỡng an toàn.
    /// </summary>
    private async Task<long> GenerateOrderCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = long.Parse(
                $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{Random.Shared.Next(100, 999)}");

            var exists = await _db.PaymentOrders
                .AnyAsync(o => o.PayOsOrderCode == candidate, cancellationToken);

            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Không sinh được orderCode duy nhất cho payOS.");
    }

    /// <summary>Nội dung chuyển khoản — chỉ ASCII, tối đa 25 ký tự.</summary>
    private static string BuildDescription(string orderRef)
    {
        var raw = $"DIL {orderRef}";
        return raw.Length <= 25 ? raw : raw[..25];
    }
}
