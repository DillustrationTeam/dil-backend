using ArtCommission.Application.Ai.Commands.CreateAiConversation;
using ArtCommission.Application.Ai.Commands.CreateDeadlineReminder;
using ArtCommission.Application.Ai.Commands.PredictDeadlineRisk;
using ArtCommission.Application.Ai.Commands.SendAiMessage;
using ArtCommission.Application.Ai.Commands.SubmitAiFeedback;
using ArtCommission.Application.Ai.Commands.UpdateReminderSettings;
using ArtCommission.Application.Ai.Queries.GetAiConversations;
using ArtCommission.Application.Ai.Queries.GetAiMessages;
using ArtCommission.Application.Ai.Queries.GetDeadlineRisk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>Thân request mở phiên hội thoại AI.</summary>
public sealed record CreateAiConversationRequest(
    string? Topic,
    string? ContextType,
    Guid? ContextId);

/// <summary>Thân request gửi câu hỏi cho trợ lý ảo.</summary>
public sealed record SendAiMessageRequest(string Content);

/// <summary>Thân request đánh giá câu trả lời của trợ lý ảo.</summary>
public sealed record AiFeedbackRequest(bool Helpful, string? Note = null);

/// <summary>Thân request dự đoán rủi ro trễ hạn.</summary>
public sealed record PredictDeadlineRiskRequest(string? ModelVersion = null);

/// <summary>Thân request tạo nhắc nhở thủ công.</summary>
public sealed record CreateDeadlineReminderRequest(
    Guid? MilestoneId,
    DateTimeOffset RemindAt,
    string? Channel);

/// <summary>Thân request đổi cấu hình nhận nhắc nhở.</summary>
public sealed record UpdateReminderSettingsRequest(
    bool EmailEnabled,
    bool PushEnabled,
    int LeadHours);

/// <summary>
/// UC44 + UC46 — AI Assistant (9 endpoint).
///
/// NĂM endpoint đầu thuộc tiền tố <c>/ai</c>. BỐN endpoint sau thuộc
/// <c>/commissions/{id}/deadline-*</c> và <c>/users/me/reminder-settings</c> —
/// dùng route tuyệt đối (<c>~/</c>) vì chúng nằm ngoài tiền tố của controller này.
/// Gộp một chỗ vì tất cả đều là nghiệp vụ trợ lý ảo, giúp đọc một file là thấy hết module.
/// </summary>
[Authorize]
[Route("api/v1/ai")]
public class AiController : ApiControllerBase
{
    // =================================================================
    // UC44 — Hội thoại với chatbot
    // =================================================================

    /// <summary>Mở phiên hỏi đáp mới với trợ lý ảo, có thể gắn ngữ cảnh đơn/tranh (UC44).</summary>
    [HttpPost("conversations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateConversation(
        [FromBody] CreateAiConversationRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new CreateAiConversationCommand(
                CurrentUserId,
                request.Topic,
                request.ContextType,
                request.ContextId),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Danh sách phiên hội thoại AI của mình (UC44).</summary>
    [HttpGet("conversations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetConversations(
        [FromQuery] string? cursor,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, meta, errors) = await Mediator.Send(
            new GetAiConversationsQuery(CurrentUserId, cursor, limit),
            cancellationToken);

        return success ? OkEnvelope(data, meta) : BadRequestEnvelope(errors);
    }

    /// <summary>Tải lại nội dung một phiên hội thoại AI (UC44).</summary>
    [HttpGet("conversations/{conversationId:guid}/messages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMessages(
        Guid conversationId,
        [FromQuery] string? cursor,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, meta, errors) = await Mediator.Send(
            new GetAiMessagesQuery(CurrentUserId, conversationId, cursor, limit),
            cancellationToken);

        return success ? OkEnvelope(data, meta) : BadRequestEnvelope(errors);
    }

    /// <summary>Gửi câu hỏi cho trợ lý ảo và nhận câu trả lời (UC44).</summary>
    [HttpPost("conversations/{conversationId:guid}/messages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SendMessage(
        Guid conversationId,
        [FromBody] SendAiMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new SendAiMessageCommand(CurrentUserId, conversationId, request.Content),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Đánh giá câu trả lời của trợ lý ảo (UC44).</summary>
    [HttpPost("messages/{aiMessageId:guid}/feedback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SubmitFeedback(
        Guid aiMessageId,
        [FromBody] AiFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new SubmitAiFeedbackCommand(CurrentUserId, aiMessageId, request.Helpful, request.Note),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    // =================================================================
    // UC46 — Rủi ro trễ hạn và nhắc nhở
    // =================================================================

    /// <summary>Xem điểm rủi ro trễ hạn của đơn đặt vẽ (UC46).</summary>
    [HttpGet("~/api/v1/commissions/{commissionId:guid}/deadline-risks")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDeadlineRisks(
        Guid commissionId,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new GetDeadlineRiskQuery(CurrentUserId, commissionId),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Tính lại điểm rủi ro và ghi bản ghi nhắc nhở mới (UC46).</summary>
    [HttpPost("~/api/v1/commissions/{commissionId:guid}/deadline-risks/predict")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PredictDeadlineRisk(
        Guid commissionId,
        [FromBody] PredictDeadlineRiskRequest? request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new PredictDeadlineRiskCommand(CurrentUserId, commissionId, request?.ModelVersion),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Tạo nhắc nhở thủ công cho một mốc sắp tới hạn (UC46).</summary>
    [HttpPost("~/api/v1/commissions/{commissionId:guid}/deadline-reminders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateDeadlineReminder(
        Guid commissionId,
        [FromBody] CreateDeadlineReminderRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new CreateDeadlineReminderCommand(
                CurrentUserId,
                commissionId,
                request.MilestoneId,
                request.RemindAt,
                request.Channel),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Bật/tắt kênh nhận nhắc nhở deadline của mình (UC46).</summary>
    [HttpPut("~/api/v1/users/me/reminder-settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateReminderSettings(
        [FromBody] UpdateReminderSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new UpdateReminderSettingsCommand(
                CurrentUserId,
                request.EmailEnabled,
                request.PushEnabled,
                request.LeadHours),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }
}
