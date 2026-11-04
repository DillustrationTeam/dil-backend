using ArtCommission.Domain.Entities.Support;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.API.Controllers;

[Route("api/v1/support")]
public class SupportController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public SupportController(AppDbContext db)
    {
        _db = db;
    }

    public record CreateBugReportRequest(string Title, string Description, string? PageUrl, string? Email);
    public record UpdateBugStatusRequest(BugReportStatus Status, string? AdminNote);

    [HttpPost("bugs")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateBugReport([FromBody] CreateBugReportRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { message = "Tiêu đề và nội dung mô tả không được để trống." });

        var report = new BugReport
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            PageUrl = request.PageUrl?.Trim(),
            ReporterId = CurrentUserId == Guid.Empty ? null : CurrentUserId,
            ReporterEmail = request.Email?.Trim() ?? (User?.Identity?.Name ?? "Anonymous"),
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
}
