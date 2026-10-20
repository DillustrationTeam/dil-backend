using ArtCommission.Application.Dispute.SubmissionDispute.Commands;
using ArtCommission.Application.Dispute.SubmissionDispute.Queries;
using ArtCommission.Application.Event.Commands;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Queries;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Dispute;
using ArtCommission.Domain.Entities.Event;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .Options);

var now = DateTimeOffset.UtcNow;
var adminId = Guid.NewGuid();
var juryUserId = Guid.NewGuid();
var submitterId = Guid.NewGuid();
var voterId = Guid.NewGuid();

// 1. Seed Event (Voting phase)
var platformEvent = new PlatformEvent
{
    Id = Guid.NewGuid(),
    Title = "Contest 2026",
    Description = "Test Event",
    Status = EventStatus.Open,
    CreatedByAdminId = adminId,
    SubmissionStartAt = now.AddDays(-10),
    SubmissionEndAt = now.AddDays(-5),
    JudgingStartAt = now.AddDays(-4),
    JudgingEndAt = now.AddDays(5),
    VotingStartAt = now.AddDays(-3),
    VotingEndAt = now.AddDays(5),
    ResultAnnouncementAt = now.AddDays(10),
    CreatedAt = now,
    IsDeleted = false
};
db.PlatformEvents.Add(platformEvent);

// Seed Submitter & Voter
var submitterUser = new ApplicationUser { Id = submitterId, UserName = "artist1", FullName = "Artist One" };
var voterUser = new ApplicationUser { Id = voterId, UserName = "voter1", FullName = "Voter One" };
db.Users.AddRange(submitterUser, voterUser);

var creatorProfile = new CreatorProfile { Id = Guid.NewGuid(), UserId = submitterId, IsDeleted = false };
db.CreatorProfiles.Add(creatorProfile);

// Seed Jury
var juryProfile = new CreatorProfile { Id = Guid.NewGuid(), UserId = juryUserId, IsDeleted = false };
db.CreatorProfiles.Add(juryProfile);
var jury = new Jury { Id = Guid.NewGuid(), EventId = platformEvent.Id, CreatorId = juryProfile.Id, IsHeadJury = true, IsDeleted = false };
db.Juries.Add(jury);

// Seed Criteria
var criteria = new EventCriteria { Id = Guid.NewGuid(), EventId = platformEvent.Id, Name = "Creativity", MaxScore = 10, Weight = 1m, IsDeleted = false };
db.EventCriteria.Add(criteria);

// Case 1: Artwork uploaded from local storage (no interactions, not used elsewhere)
var localArtwork = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = creatorProfile.Id,
    Title = "Local Upload Artwork",
    ImageUrl = "https://example.com/local.jpg",
    ThumbnailUrl = "https://example.com/local_thumb.jpg",
    ModerationStatus = "Approved",
    IsDeleted = false,
    CreatedAt = now
};
db.Artworks.Add(localArtwork);

var submission1 = new EventSubmission
{
    Id = Guid.NewGuid(),
    EventId = platformEvent.Id,
    SubmitterId = submitterId,
    ArtworkId = localArtwork.Id,
    Title = "Submission 1 (Local Upload)",
    Description = "Description 1",
    VoteCount = 5,
    Score = 8.5m,
    IsDisqualified = false,
    SubmittedAt = now.AddDays(-8)
};
db.EventSubmissions.Add(submission1);

// Add initial votes and scores
var vote1 = new EventVote { SubmissionId = submission1.Id, VoterId = voterId, VotedAt = now };
db.EventVotes.Add(vote1);

var score1 = new CriteriaScore { EventCriteriaId = criteria.Id, SubmissionId = submission1.Id, GradedByJuryId = jury.Id, Score = 8.5m, UpdatedAt = now };
db.CriteriaScores.Add(score1);

// Add SubmissionDispute on submission1
var dispute1 = new SubmissionDispute
{
    Id = Guid.NewGuid(),
    EventId = platformEvent.Id,
    SubmissionId = submission1.Id,
    RaisedById = juryUserId,
    Reason = "Plagiarized artwork from online source",
    Status = "Pending",
    Priority = "High",
    CreatedAt = now
};
db.SubmissionDisputes.Add(dispute1);

// Also add a second submission that is legitimate
var legitArtwork = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = creatorProfile.Id,
    Title = "Legit Artwork",
    ImageUrl = "https://example.com/legit.jpg",
    ModerationStatus = "Approved",
    IsDeleted = false,
    CreatedAt = now
};
db.Artworks.Add(legitArtwork);

var submission2 = new EventSubmission
{
    Id = Guid.NewGuid(),
    EventId = platformEvent.Id,
    SubmitterId = Guid.NewGuid(),
    ArtworkId = legitArtwork.Id,
    Title = "Submission 2 (Legit)",
    VoteCount = 3,
    Score = 9.0m,
    IsDisqualified = false,
    SubmittedAt = now.AddDays(-7)
};
db.EventSubmissions.Add(submission2);

await db.SaveChangesAsync();

// Verify BEFORE Disqualification:
// Leaderboard should have both submissions
var leaderboardHandler = new GetEventLeaderboardQueryHandler(db);
var (lbItemsBefore, lbCountBefore, _) = await leaderboardHandler.Handle(
    new GetEventLeaderboardQuery(platformEvent.Id, SortBy: "votes", IsAdmin: true), CancellationToken.None);

if (lbCountBefore != 2)
{
    Console.WriteLine($"FAIL: Expected 2 items on leaderboard before disqualify, got {lbCountBefore}");
    return 1;
}
Console.WriteLine("PASS: Leaderboard has 2 submissions before disqualification.");

// 2. DISQUALIFY submission1
var resolveHandler = new ResolveSubmissionDisputeCommandHandler(db);
var resolveCommand = new ResolveSubmissionDisputeCommand(
    DisputeId: dispute1.Id,
    ModeratorId: adminId,
    Resolution: "Disqualify",
    Note: "Xác thực đạo tranh, truất quyền thi đấu"
);
var (resolveSuccess, resolveMsg, resolveErrors) = await resolveHandler.Handle(resolveCommand, CancellationToken.None);

if (!resolveSuccess)
{
    Console.WriteLine($"FAIL: ResolveSubmissionDisputeCommand failed: {string.Join(", ", resolveErrors)}");
    return 1;
}
Console.WriteLine($"PASS: Disqualify resolved successfully: {resolveMsg}");

// 3. Verify Submission status
var updatedSubmission1 = await db.EventSubmissions.FirstAsync(s => s.Id == submission1.Id);
if (!updatedSubmission1.IsDisqualified)
{
    Console.WriteLine("FAIL: submission1.IsDisqualified is not true!");
    return 1;
}
Console.WriteLine("PASS: submission1.IsDisqualified is true.");

if (updatedSubmission1.VoteCount != 0 || updatedSubmission1.Score != null)
{
    Console.WriteLine($"FAIL: submission1 VoteCount ({updatedSubmission1.VoteCount}) or Score ({updatedSubmission1.Score}) was not reset!");
    return 1;
}
Console.WriteLine("PASS: submission1 VoteCount reset to 0 and Score reset to null.");

// 4. Verify CriteriaScores and EventVotes deleted for submission1
var remainingScores = await db.CriteriaScores.Where(cs => cs.SubmissionId == submission1.Id).CountAsync();
if (remainingScores != 0)
{
    Console.WriteLine($"FAIL: CriteriaScores for submission1 not removed: count = {remainingScores}");
    return 1;
}
Console.WriteLine("PASS: CriteriaScores for disqualified submission were removed.");

var remainingVotes = await db.EventVotes.Where(v => v.SubmissionId == submission1.Id).CountAsync();
if (remainingVotes != 0)
{
    Console.WriteLine($"FAIL: EventVotes for submission1 not removed: count = {remainingVotes}");
    return 1;
}
Console.WriteLine("PASS: EventVotes for disqualified submission were removed.");

// 5. Verify local storage artwork soft-deleted
var updatedLocalArtwork = await db.Artworks.FirstAsync(a => a.Id == localArtwork.Id);
if (!updatedLocalArtwork.IsDeleted)
{
    Console.WriteLine("FAIL: Local storage artwork was NOT soft-deleted (IsDeleted is false)!");
    return 1;
}
Console.WriteLine("PASS: Local storage artwork soft-deleted (IsDeleted is true).");

// 6. Verify LEADERBOARD excludes disqualified submission
var (lbItemsAfter, lbCountAfter, _) = await leaderboardHandler.Handle(
    new GetEventLeaderboardQuery(platformEvent.Id, SortBy: "votes", IsAdmin: true), CancellationToken.None);

if (lbCountAfter != 1 || lbItemsAfter.Any(i => i.SubmissionId == submission1.Id))
{
    Console.WriteLine($"FAIL: Leaderboard still contains disqualified submission! Count = {lbCountAfter}");
    return 1;
}
Console.WriteLine("PASS: Leaderboard successfully excludes disqualified submission.");

// 7. Verify VOTING rejects voting on disqualified submission
var voteHandler = new CreateVoteCommandHandler(db);
var (voteSuccess, _, voteErrors) = await voteHandler.Handle(
    new CreateVoteCommand(submission1.Id, voterId), CancellationToken.None);

if (voteSuccess)
{
    Console.WriteLine("FAIL: Voting on disqualified submission should have been rejected!");
    return 1;
}
Console.WriteLine($"PASS: Voting on disqualified submission rejected as expected ({string.Join(", ", voteErrors)}).");

// 8. Verify SCORING rejects scoring on disqualified submission
var scoreHandler = new SubmitJuryScoreCommandHandler(db);
var (scoreSuccess, _, scoreErrors) = await scoreHandler.Handle(
    new SubmitJuryScoreCommand(
        submission1.Id,
        juryUserId,
        new SubmitScoreDto
        {
            SubmissionId = submission1.Id,
            TotalScore = 5m,
            CriteriaScores = new List<CriteriaScoreItemDto>
            {
                new() { CriteriaId = criteria.Id, Score = 5m }
            }
        }
    ), CancellationToken.None);

if (scoreSuccess)
{
    Console.WriteLine("FAIL: Scoring disqualified submission should have been rejected!");
    return 1;
}
Console.WriteLine($"PASS: Scoring disqualified submission rejected as expected ({string.Join(", ", scoreErrors)}).");

// 9. Verify GetSubmissionsByEventIdQuery excludes disqualified submission
var eventSubmissionsHandler = new GetSubmissionsByEventIdQueryHandler(db);
var (submissionsItems, submissionsTotal) = await eventSubmissionsHandler.Handle(
    new GetSubmissionsByEventIdQuery(platformEvent.Id), CancellationToken.None);

if (submissionsTotal != 1 || submissionsItems.Any(s => s.Id == submission1.Id))
{
    Console.WriteLine($"FAIL: GetSubmissionsByEventIdQuery still includes disqualified submission! Count = {submissionsTotal}");
    return 1;
}
Console.WriteLine("PASS: GetSubmissionsByEventIdQuery excludes disqualified submission.");

// 10. Verify Dispute Audit Trail: SubmissionDispute STILL EXISTS and is Resolved / Disqualify
var disputeQueryHandler = new GetSubmissionDisputesQueryHandler(db);
var (disputes, disputeCount) = await disputeQueryHandler.Handle(
    new GetSubmissionDisputesQuery(EventId: platformEvent.Id), CancellationToken.None);

if (disputeCount != 1 || disputes[0].Resolution != "Disqualify" || disputes[0].Status != "Resolved")
{
    Console.WriteLine($"FAIL: Dispute record corrupted or missing! Count = {disputeCount}");
    return 1;
}
Console.WriteLine($"PASS: SubmissionDispute successfully retained in DB with Status = {disputes[0].Status}, Resolution = {disputes[0].Resolution}.");

// 11. Test Case 2: Portfolio artwork with favorites or collections must NOT be deleted
var portfolioArtwork = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = creatorProfile.Id,
    Title = "Portfolio Artwork In Collection",
    ImageUrl = "https://example.com/portfolio.jpg",
    ModerationStatus = "Approved",
    IsDeleted = false,
    CreatedAt = now
};
db.Artworks.Add(portfolioArtwork);
db.CollectionArtworks.Add(new CollectionArtwork
{
    CollectionId = Guid.NewGuid(),
    ArtworkId = portfolioArtwork.Id,
    CreatedAt = now
});

var submission3 = new EventSubmission
{
    Id = Guid.NewGuid(),
    EventId = platformEvent.Id,
    SubmitterId = submitterId,
    ArtworkId = portfolioArtwork.Id,
    Title = "Submission 3 (Portfolio)",
    VoteCount = 2,
    Score = 7.0m,
    IsDisqualified = false,
    SubmittedAt = now.AddDays(-6)
};
db.EventSubmissions.Add(submission3);

var dispute2 = new SubmissionDispute
{
    Id = Guid.NewGuid(),
    EventId = platformEvent.Id,
    SubmissionId = submission3.Id,
    RaisedById = adminId,
    Reason = "Theme mismatch",
    Status = "Pending",
    Priority = "Medium",
    CreatedAt = now
};
db.SubmissionDisputes.Add(dispute2);
await db.SaveChangesAsync();

var resolveCommand2 = new ResolveSubmissionDisputeCommand(
    DisputeId: dispute2.Id,
    ModeratorId: adminId,
    Resolution: "Disqualify",
    Note: "Sai chủ đề"
);
var (resolve2Success, _, _) = await resolveHandler.Handle(resolveCommand2, CancellationToken.None);
if (!resolve2Success)
{
    Console.WriteLine("FAIL: Failed to resolve dispute2");
    return 1;
}

var updatedPortfolioArtwork = await db.Artworks.FirstAsync(a => a.Id == portfolioArtwork.Id);
if (updatedPortfolioArtwork.IsDeleted)
{
    Console.WriteLine("FAIL: Portfolio artwork with collection was wrongly deleted!");
    return 1;
}
Console.WriteLine("PASS: Portfolio artwork with external collection was NOT deleted.");

Console.WriteLine("ALL DISQUALIFY CHECKS PASSED SUCCESSFULLY!");
return 0;
