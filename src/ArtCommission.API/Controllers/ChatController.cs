using ArtCommission.Application.Chat.Commands.MarkRoomRead;
using ArtCommission.Application.Chat.Commands.TranslateMessage;
using ArtCommission.Application.Chat.Commands.UploadAttachment;
using ArtCommission.Application.Chat.Queries.GetChatRoom;
using ArtCommission.Application.Chat.Queries.GetRoomMessages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>Thân request dịch tin nhắn.</summary>
public sealed record TranslateMessageRequest(string TargetLang);

/// <summary>
/// UC43 — Workroom Chat (5 REST + SignalR <c>/hubs/chat</c>).
///
/// Gửi và nhận tin real-time đi qua hub; REST phục vụ tải lịch sử, thông tin phòng,
/// đánh dấu đã đọc, upload file và dịch tin nhắn.
/// </summary>
[Authorize]
[Route("api/v1/chat")]
public class ChatController : ApiControllerBase
{
    /// <summary>Lịch sử tin nhắn của phòng, cursor theo sent_at (UC43).</summary>
    /// <remarks>
    /// <c>roomId</c> nhận cả Id phòng chat và Id commission — FE gọi /workroom/{commissionId}.
    /// Hệ thống tự tạo phòng cho commission nếu chưa có.
    /// </remarks>
    [HttpGet("rooms/{roomId:guid}/messages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMessages(
        Guid roomId,
        [FromQuery] string? cursor,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, meta, errors) = await Mediator.Send(
            new GetRoomMessagesQuery(CurrentUserId, roomId, cursor, limit),
            cancellationToken);

        return success ? OkEnvelope(data, meta) : BadRequestEnvelope(errors);
    }

    /// <summary>Thông tin phòng chat: loại phòng, tiêu đề, thành viên (UC43).</summary>
    [HttpGet("rooms/{roomId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetRoom(Guid roomId, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new GetChatRoomQuery(CurrentUserId, roomId), cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Đánh dấu đã đọc tới thời điểm hiện tại (UC43).</summary>
    [HttpPost("rooms/{roomId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkRead(Guid roomId, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new MarkRoomReadCommand(CurrentUserId, roomId), cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Upload ảnh/file tham khảo vào phòng chat (UC43).</summary>
    /// <remarks>
    /// multipart/form-data với trường <c>file</c>; <c>messageId</c> tuỳ chọn để gắn
    /// file vào tin nhắn đã có thay vì tạo tin nhắn mới.
    /// </remarks>
    [HttpPost("rooms/{roomId:guid}/attachments")]
    [RequestSizeLimit(UploadAttachmentCommandValidator.MaxFileSizeBytes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadAttachment(
        Guid roomId,
        IFormFile? file,
        [FromForm] Guid? messageId,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequestEnvelope("Vui lòng chọn file cần gửi.");
        }

        await using var stream = file.OpenReadStream();

        var (success, data, resolvedMessageId, errors) = await Mediator.Send(
            new UploadAttachmentCommand(
                CurrentUserId,
                roomId,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                messageId),
            cancellationToken);

        return success
            ? OkEnvelope(data, new { messageId = resolvedMessageId })
            : BadRequestEnvelope(errors);
    }

    /// <summary>Dịch một tin nhắn sang ngôn ngữ người nhận (UC43).</summary>
    [HttpPost("messages/{messageId:guid}/translate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Translate(
        Guid messageId,
        [FromBody] TranslateMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new TranslateMessageCommand(CurrentUserId, messageId, request.TargetLang),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }
}
