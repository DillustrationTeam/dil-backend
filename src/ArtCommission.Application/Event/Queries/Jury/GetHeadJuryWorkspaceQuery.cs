using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetHeadJuryWorkspaceQuery(Guid EventId) : IRequest<HeadJuryWorkspaceDto?>;

public class GetHeadJuryWorkspaceQueryHandler : IRequestHandler<GetHeadJuryWorkspaceQuery, HeadJuryWorkspaceDto?>
{
    private readonly IApplicationDbContext _db;

    public GetHeadJuryWorkspaceQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<HeadJuryWorkspaceDto?> Handle(
        GetHeadJuryWorkspaceQuery request,
        CancellationToken cancellationToken)
    {
        var platformEvent = await _db.PlatformEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.EventId && !e.IsDeleted, cancellationToken);

        if (platformEvent == null) return null;

        var juries = await _db.Juries.AsNoTracking()
            .Include(j => j.Creator)
                .ThenInclude(c => c!.User)
            .Where(j => j.EventId == request.EventId && !j.IsDeleted)
            .OrderByDescending(j => j.IsHeadJury)
            .ThenBy(j => j.CreatedAt)
            .ToListAsync(cancellationToken);

        var submissions = await _db.EventSubmissions.AsNoTracking()
            .Include(s => s.Artwork)
            .Where(s => s.EventId == request.EventId && !s.IsDisqualified)
            .OrderByDescending(s => s.Score ?? 0)
            .ThenBy(s => s.SubmittedAt)
            .ToListAsync(cancellationToken);

        var submissionIds = submissions.Select(s => s.Id).ToList();

        var criteriaScores = await _db.CriteriaScores.AsNoTracking()
            .Where(cs => submissionIds.Contains(cs.SubmissionId))
            .ToListAsync(cancellationToken);

        var disputes = await _db.SubmissionDisputes.AsNoTracking()
            .Where(d => d.EventId == request.EventId && !d.IsDeleted)
            .ToListAsync(cancellationToken);

        int totalSubmissions = submissions.Count;
        int totalJuryCount = juries.Count;

        // Group criteriaScores by GradedByJuryId to determine each jury's progress
        var scoresByJury = criteriaScores
            .GroupBy(cs => cs.GradedByJuryId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(cs => cs.SubmissionId).Distinct().ToList()
            );

        var headJury = juries.FirstOrDefault(j => j.IsHeadJury);
        var headJuryId = headJury?.Id;

        var juryProgressList = new List<JuryProgressItemDto>();
        int completedJuryCount = 0;

        foreach (var j in juries)
        {
            var gradedIds = scoresByJury.TryGetValue(j.Id, out var sIds) ? sIds : new List<Guid>();
            int completedCount = gradedIds.Count;
            int totalAssigned = totalSubmissions;
            bool isCompleted = totalAssigned > 0 && completedCount >= totalAssigned;
            if (isCompleted) completedJuryCount++;

            int gradedPercent = totalAssigned > 0 ? (int)Math.Round((double)completedCount / totalAssigned * 100) : 0;
            string statusLabel = isCompleted ? "Completed" : (completedCount > 0 ? "In Progress" : "Pending");

            string displayName = j.Creator?.DisplayName 
                ?? j.Creator?.User?.FullName 
                ?? j.Creator?.User?.UserName 
                ?? "Jury Member";

            if (j.IsHeadJury) displayName += " (Head Jury)";

            juryProgressList.Add(new JuryProgressItemDto
            {
                Id = j.Id,
                Name = displayName,
                GradedPercent = gradedPercent,
                CompletedCount = completedCount,
                TotalAssigned = totalAssigned,
                StatusLabel = statusLabel,
                IsCompleted = isCompleted,
                IsHeadJury = j.IsHeadJury,
                GradedSubmissionIds = gradedIds
            });
        }

        // Submissions that have been graded by at least one jury or have a score
        var gradedSubmissionIdsSet = criteriaScores
            .Select(cs => cs.SubmissionId)
            .Union(submissions.Where(s => s.Score.HasValue && s.Score > 0).Select(s => s.Id))
            .ToHashSet();

        int gradedSubmissions = gradedSubmissionIdsSet.Count;

        int totalExpectedGrades = totalJuryCount * totalSubmissions;
        int totalDoneGrades = scoresByJury.Values.Sum(list => list.Count);
        int overallProgressPercent = totalExpectedGrades > 0 
            ? (int)Math.Round((double)totalDoneGrades / totalExpectedGrades * 100) 
            : 0;

        // Combined scores and rankings
        var combinedScores = new List<CombinedScoreItemDto>();
        int rank = 1;

        foreach (var sub in submissions)
        {
            var subScores = criteriaScores.Where(cs => cs.SubmissionId == sub.Id).ToList();

            // Head jury total score for this submission
            decimal headScore = 0;
            if (headJuryId.HasValue)
            {
                headScore = subScores
                    .Where(cs => cs.GradedByJuryId == headJuryId.Value)
                    .Sum(cs => cs.Score);
            }

            // Other juries' average total score
            var otherJuryTotals = subScores
                .Where(cs => !headJuryId.HasValue || cs.GradedByJuryId != headJuryId.Value)
                .GroupBy(cs => cs.GradedByJuryId)
                .Select(g => g.Sum(cs => cs.Score))
                .ToList();

            decimal juryAvg = otherJuryTotals.Count > 0 
                ? Math.Round(otherJuryTotals.Average(), 2) 
                : (sub.Score ?? 0);

            decimal deviation = 0;
            if (juryAvg > 0 && headScore > 0)
            {
                deviation = Math.Round(Math.Abs(headScore - juryAvg) / juryAvg * 100, 1);
            }

            string code = $"#SUB-{sub.Id.ToString()[..4].ToUpper()}";
            string artworkUrl = sub.Artwork?.ImageUrl ?? sub.Artwork?.ThumbnailUrl ?? sub.Artwork?.WatermarkedUrl ?? "/seedimg/miyu.png";

            combinedScores.Add(new CombinedScoreItemDto
            {
                Rank = rank++,
                SubmissionId = sub.Id,
                Title = sub.Title,
                Code = code,
                Category = "Illustration",
                ArtworkImageUrl = artworkUrl,
                JuryScoreAvg = juryAvg,
                HeadJuryScore = headScore > 0 ? headScore : (sub.Score ?? 0),
                DeviationPercent = deviation
            });
        }

        // Arbitration cases: either from disputes or high deviation submissions (> 25%)
        var arbitrationCases = new List<ArbitrationCaseDto>();
        foreach (var d in disputes.Where(d => d.Status != "Resolved" && d.Status != "Rejected"))
        {
            var relatedSub = submissions.FirstOrDefault(s => s.Id == d.SubmissionId);
            arbitrationCases.Add(new ArbitrationCaseDto
            {
                Id = d.Id,
                SubmissionId = d.SubmissionId,
                Title = relatedSub?.Title ?? "Tác phẩm khiếu nại",
                Code = relatedSub != null ? $"#SUB-{relatedSub.Id.ToString()[..4].ToUpper()}" : "#DISP",
                Category = "Illustration",
                ArtworkImageUrl = relatedSub?.Artwork?.ImageUrl ?? "/seedimg/miyu.png",
                DeviationPercent = 0,
                Notes = d.Reason,
                Status = d.Status,
                Decision = d.Resolution
            });
        }

        // Final winners
        var finalWinners = new List<WinnerApprovalItemDto>();
        if (combinedScores.Count > 0)
        {
            finalWinners.Add(new WinnerApprovalItemDto
            {
                AwardName = "Giải Nhất (Grand Prize)",
                SubmissionId = combinedScores[0].SubmissionId,
                Title = combinedScores[0].Title,
                Code = combinedScores[0].Code,
                ScoreOrVotes = $"{combinedScores[0].HeadJuryScore:0.0} pts",
                Status = platformEvent.IsHeadJurySignedOff ? "Approved" : "Pending Approval"
            });
        }

        bool allJuryCompleted = totalJuryCount > 0 && completedJuryCount == totalJuryCount;
        bool disputesResolved = disputes.All(d => d.Status == "Resolved" || d.Status == "Rejected");

        return new HeadJuryWorkspaceDto
        {
            TotalSubmissions = totalSubmissions,
            GradedSubmissions = gradedSubmissions,
            CompletedJuryCount = completedJuryCount,
            TotalJuryCount = totalJuryCount,
            AnomalyFlagsCount = arbitrationCases.Count,
            OverallProgressPercent = overallProgressPercent,
            JuryProgressList = juryProgressList,
            CombinedScores = combinedScores,
            ArbitrationCases = arbitrationCases,
            FinalWinners = finalWinners,
            AllJuryCompleted = allJuryCompleted,
            DisputesResolved = disputesResolved,
            WinnersConfirmed = platformEvent.IsHeadJurySignedOff,
            IsResultsApproved = platformEvent.IsHeadJurySignedOff
        };
    }
}
