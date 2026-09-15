using System.Text.Json;
using ArtCommission.Application.Payment.Commands.CreatePaymentOrder;
using ArtCommission.Application.Payment.Commands.HandlePaymentWebhook;
using ArtCommission.Application.Payment.Queries.GetPaymentOrder;
using ArtCommission.Application.Payment.Queries.GetPaymentOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// UC48 — Nạp tiền vào ví sàn qua cổng thanh toán.
/// </summary>
[Authorize]
[Route("api/v1/payments")]
public class PaymentsController : ApiControllerBase
{
    /// <summary>
    /// Tạo đơn nạp tiền và lấy link thanh toán (UC48).
    /// </summary>
    /// <remarks>
    /// Trả về <c>paymentUrl</c> để mở trang thanh toán, hoặc <c>qrCode</c> để hiển thị mã VietQR.
    /// </remarks>
    [HttpPost("orders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreatePaymentOrderCommand command,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            command with { UserId = CurrentUserId }, cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Lấy trạng thái một đơn nạp tiền (UC48) — màn hình chờ thanh toán poll endpoint này.
    /// </summary>
    /// <remarks>
    /// Nếu cổng chưa gọi webhook về, endpoint này chủ động đối chiếu với cổng rồi cập nhật.
    /// </remarks>
    [HttpGet("orders/{paymentOrderId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOrder(
        Guid paymentOrderId,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new GetPaymentOrderQuery(CurrentUserId, paymentOrderId), cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Lịch sử nạp tiền của người dùng (UC48), phân trang bằng cursor.
    /// </summary>
    /// <remarks>Truyền <c>orderRef</c> để lấy đúng một đơn theo mã đơn hiển thị.</remarks>
    [HttpGet("orders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string? status,
        [FromQuery] string? gateway,
        [FromQuery] string? cursor,
        [FromQuery] int limit,
        [FromQuery] string? orderRef,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, page, errors) = await Mediator.Send(
            new GetPaymentOrdersQuery(
                CurrentUserId, status, gateway, cursor, limit <= 0 ? 20 : limit, orderRef),
            cancellationToken);

        if (!success || page is null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(page.Items, new { nextCursor = page.NextCursor });
    }

    /// <summary>
    /// Kiểm tra endpoint webhook có sống không (UC48) — chỉ để chẩn đoán.
    /// </summary>
    /// <remarks>
    /// payOS chỉ gọi POST vào endpoint này. Endpoint GET tồn tại để phân biệt
    /// "API của mình đang chạy" với "trang cảnh báo của dịch vụ tunnel" —
    /// khi mở URL webhook bằng trình duyệt, nếu thấy JSON này thì đã tới đúng API.
    /// </remarks>
    [HttpGet("webhooks/{gateway}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult WebhookHealth(string gateway) =>
        Ok(new
        {
            endpoint = "payments.webhook",
            gateway,
            alive = true,
            expects = "POST",
            hint = "payOS gọi POST vào chính URL này. Chữ ký HMAC-SHA256 bắt buộc."
        });

    /// <summary>
    /// Webhook nhận thông báo thanh toán từ cổng (UC48) — cổng gọi vào, KHÔNG dùng JWT.
    /// </summary>
    /// <remarks>
    /// Xác thực chữ ký HMAC-SHA256 trên body. Sai chữ ký trả 400 và không tiết lộ chi tiết.
    /// Mọi trường hợp đã xử lý xong đều trả 200 để cổng ngừng retry.
    /// </remarks>
    [HttpPost("webhooks/{gateway}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> HandleWebhook(
        string gateway,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new HandlePaymentWebhookCommand(gateway, body), cancellationToken);

        if (result.HttpStatus == StatusCodes.Status400BadRequest)
        {
            // Body trung tính — cố ý không nói rõ sai ở đâu
            return BadRequest(new { received = false });
        }

        return Ok(result.Data);
    }
}
