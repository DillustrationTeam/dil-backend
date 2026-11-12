using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Domain.Entities.Support;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.API.Controllers;

[Route("api/v1/support")]
public class SupportController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IEmailService _emailService;
    private readonly ILogger<SupportController> _logger;

    public SupportController(
        AppDbContext db,
        INotificationPublisher notificationPublisher,
        IEmailService emailService,
        ILogger<SupportController> logger)
    {
        _db = db;
        _notificationPublisher = notificationPublisher;
        _emailService = emailService;
        _logger = logger;
    }

    public record CreateBugReportRequest(string Title, string Description, string? PageUrl, string? Email);
    public record UpdateBugStatusRequest(BugReportStatus Status, string? AdminNote);
    public record ReplyBugReportRequest(string ReplyMessage, BugReportStatus? Status);

    [HttpPost("bugs")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateBugReport([FromBody] CreateBugReportRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { message = "Tiêu đề và nội dung mô tả không được để trống." });

        var reporterId = CurrentUserId == Guid.Empty ? (Guid?)null : CurrentUserId;
        var resolvedEmail = request.Email?.Trim();

        if (string.IsNullOrWhiteSpace(resolvedEmail) && reporterId.HasValue)
        {
            resolvedEmail = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == reporterId.Value)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(ct);
        }

        var report = new BugReport
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            PageUrl = request.PageUrl?.Trim(),
            ReporterId = reporterId,
            ReporterEmail = string.IsNullOrWhiteSpace(resolvedEmail) ? (User?.Identity?.Name ?? "Anonymous") : resolvedEmail,
            Status = BugReportStatus.Open
        };

        _db.BugReports.Add(report);
        await _db.SaveChangesAsync(ct);

        return OkEnvelope(report);
    }

    [HttpGet("bugs")]
    [Authorize(Roles = "Administrator,Admin")]
    public async Task<IActionResult> GetBugReports([FromQuery] BugReportStatus? status, CancellationToken ct)
    {
        var query = _db.BugReports.AsNoTracking().Where(b => !b.IsDeleted);

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        var list = await query.OrderByDescending(b => b.CreatedAt).ToListAsync(ct);
        return OkEnvelope(list);
    }

    [HttpPatch("bugs/{id:guid}/status")]
    [Authorize(Roles = "Administrator,Admin")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateBugStatusRequest request, CancellationToken ct)
    {
        var report = await _db.BugReports.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, ct);
        if (report is null)
            return NotFound(new { message = "Không tìm thấy báo cáo lỗi." });

        report.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.AdminNote))
            report.AdminNote = request.AdminNote;
        report.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return OkEnvelope(report);
    }

    [HttpPost("bugs/{id:guid}/reply")]
    [Authorize(Roles = "Administrator,Admin")]
    public async Task<IActionResult> ReplyBugReport(Guid id, [FromBody] ReplyBugReportRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ReplyMessage))
            return BadRequest(new { message = "Nội dung phản hồi không được để trống." });

        var report = await _db.BugReports.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, ct);
        if (report is null)
            return NotFound(new { message = "Không tìm thấy báo cáo lỗi." });

        var reply = request.ReplyMessage.Trim();
        report.AdminNote = reply;
        report.Status = request.Status ?? BugReportStatus.Resolved;
        report.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        var notifiedInApp = false;
        if (report.ReporterId.HasValue && report.ReporterId.Value != Guid.Empty)
        {
            try
            {
                await _notificationPublisher.PublishAsync(
                    userId: report.ReporterId.Value,
                    notificationType: NotificationType.BugReportReplied,
                    title: $"Phản hồi báo cáo: {report.Title}",
                    body: reply,
                    refType: "BugReport",
                    refId: report.Id,
                    channel: NotificationChannel.InApp,
                    dedupKey: null,
                    cancellationToken: ct);
                notifiedInApp = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi thông báo in-app cho báo cáo {BugReportId}", report.Id);
            }
        }

        string? emailedTo = null;
        var targetEmail = !string.IsNullOrWhiteSpace(report.ReporterEmail) && report.ReporterEmail.Contains('@')
            ? report.ReporterEmail.Trim()
            : null;

        if (targetEmail is null && report.ReporterId.HasValue && report.ReporterId.Value != Guid.Empty)
        {
            targetEmail = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == report.ReporterId.Value)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(ct);
        }

        if (!string.IsNullOrWhiteSpace(targetEmail) && targetEmail.Contains('@'))
        {
            var statusLabel = report.Status switch
            {
                BugReportStatus.Open => "Chờ khắc phục",
                BugReportStatus.InProgress => "Đang xử lý",
                BugReportStatus.Resolved => "Đã xử lý",
                _ => report.Status.ToString()
            };

            try
            {
                await _emailService.SendBugReportReplyEmailAsync(targetEmail, report.Title, reply, statusLabel, ct);
                emailedTo = targetEmail;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email phản hồi báo cáo {BugReportId} tới {Email}", report.Id, targetEmail);
            }
        }

        return OkEnvelope(new
        {
            report,
            notifiedInApp,
            emailedTo
        });
    }
}
