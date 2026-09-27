using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Revenue.Queries.GetRevenueSnapshots;

/// <summary>
/// UC51 — GET /api/v1/creator/revenue/snapshots
/// Xem các bản ghi chốt doanh thu định kỳ phục vụ đối soát.
/// </summary>
public record GetRevenueSnapshotsQuery(
    Guid UserId,
    string? Scope = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Cursor = null,
    int Limit = 20
) : IRequest<(bool Success, IReadOnlyList<RevenueSnapshotDto>? Data, object? Meta, string[] Errors)>;

public class GetRevenueSnapshotsQueryValidator : AbstractValidator<GetRevenueSnapshotsQuery>
{
    public GetRevenueSnapshotsQueryValidator()
    {
        RuleFor(x => x.Scope)
            .Must(scope => string.IsNullOrWhiteSpace(scope)
                           || Enum.TryParse<RevenueSnapshotScope>(scope, ignoreCase: true, out _))
            .WithMessage("Chu kỳ không hợp lệ (Daily / Weekly / Monthly).");
    }
}

public class GetRevenueSnapshotsQueryHandler
    : IRequestHandler<GetRevenueSnapshotsQuery, (bool, IReadOnlyList<RevenueSnapshotDto>?, object?, string[])>
{
    private const int MaxLimit = 100;
    private const int DefaultLimit = 20;

    private readonly IApplicationDbContext _db;

    public GetRevenueSnapshotsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, IReadOnlyList<RevenueSnapshotDto>?, object?, string[])> Handle(
        GetRevenueSnapshotsQuery request,
        CancellationToken cancellationToken)
    {
        var validation = new GetRevenueSnapshotsQueryValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var limit = request.Limit <= 0 ? DefaultLimit : Math.Min(request.Limit, MaxLimit);

        var query = _db.RevenueSnapshots
            .AsNoTracking()
            .Where(s => s.CreatorId == request.UserId && !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Scope)
            && Enum.TryParse<RevenueSnapshotScope>(request.Scope, ignoreCase: true, out var scope))
        {
            query = query.Where(s => s.Scope == scope);
        }

        if (request.From.HasValue)
        {
            query = query.Where(s => s.SnapshotDate >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(s => s.SnapshotDate <= request.To.Value);
        }

        // Snapshot mới nhất trước: đối soát thì kỳ gần nhất là kỳ đang quan tâm.
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!RevenueSnapshotCursor.TryDecode(request.Cursor, out var cursorDate, out var cursorId))
            {
                return (false, null, null, ["Cursor không hợp lệ."]);
            }

            query = query.Where(s => s.SnapshotDate < cursorDate
                                     || (s.SnapshotDate == cursorDate && s.Id.CompareTo(cursorId) > 0));
        }

        var rows = await query
            .OrderByDescending(s => s.SnapshotDate)
            .ThenBy(s => s.Id)
            .Take(limit + 1)
            .Select(s => new RevenueSnapshotDto(
                s.Id,
                s.Scope.ToString(),
                s.SnapshotDate,
                s.GrossAmount,
                s.FeeAmount,
                s.NetAmount,
                s.CompletedOrderCount,
                s.RebuiltAt,
                s.RebuildCount))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;

        var last = page.LastOrDefault();
        var nextCursor = hasMore && last is not null
            ? RevenueSnapshotCursor.Encode(last.SnapshotDate, last.RevenueSnapshotId)
            : null;

        return (true, page, new { nextCursor, count = page.Count }, []);
    }
}

/// <summary>Mã hoá cursor phân trang snapshot theo (SnapshotDate, Id).</summary>
public static class RevenueSnapshotCursor
{
    public static string Encode(DateOnly snapshotDate, Guid id)
    {
        var payload = string.Join('|', snapshotDate.ToString("yyyy-MM-dd"), id.ToString("N"));
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string cursor, out DateOnly snapshotDate, out Guid id)
    {
        snapshotDate = default;
        id = Guid.Empty;

        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');

            if (parts.Length != 2)
            {
                return false;
            }

            if (!DateOnly.TryParseExact(parts[0], "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                return false;
            }

            if (!Guid.TryParseExact(parts[1], "N", out var parsedId))
            {
                return false;
            }

            snapshotDate = parsedDate;
            id = parsedId;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
