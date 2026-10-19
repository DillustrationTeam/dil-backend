using ArtCommission.Application.Event.Commands;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Event;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

Console.WriteLine("=== STARTING CONCURRENT VOTE CHECKS ===");

var dbName = "ConcurrentVoteTestDb_" + Guid.NewGuid();
var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase(dbName)
    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
    .Options;

AppDbContext CreateDb() => new(dbOptions);

var now = DateTimeOffset.UtcNow;
var adminId = Guid.NewGuid();
var voterA = Guid.NewGuid();
var voterB = Guid.NewGuid();
var submitterId = Guid.NewGuid();

// Setup Seed Data
await using (var seedDb = CreateDb())
{
    var adminUser = new ApplicationUser { Id = adminId, UserName = "admin", Email = "admin@test.com" };
    var voterAUser = new ApplicationUser { Id = voterA, UserName = "voterA", Email = "voterA@test.com" };
    var voterBUser = new ApplicationUser { Id = voterB, UserName = "voterB", Email = "voterB@test.com" };
    var submitterUser = new ApplicationUser { Id = submitterId, UserName = "submitter", Email = "submitter@test.com" };

    seedDb.Users.AddRange(adminUser, voterAUser, voterBUser, submitterUser);

    var artworkId = Guid.NewGuid();

    // Event 1: MaxVote = 1
    var event1 = new PlatformEvent
    {
        Id = Guid.NewGuid(),
        Title = "Event MaxVote 1",
        Description = "Event MaxVote 1 Desc",
        Status = EventStatus.Open,
        MaxVote = 1,
        CreatedByAdminId = adminId,
        SubmissionStartAt = now.AddDays(-10),
        SubmissionEndAt = now.AddDays(-5),
        JudgingStartAt = now.AddDays(-4),
        JudgingEndAt = now.AddDays(-1),
        VotingStartAt = now.AddDays(-1),
        VotingEndAt = now.AddDays(5),
        ResultAnnouncementAt = now.AddDays(6),
        CreatedAt = now
    };

    // Event 2: MaxVote = 3
    var event2 = new PlatformEvent
    {
        Id = Guid.NewGuid(),
        Title = "Event MaxVote 3",
        Description = "Event MaxVote 3 Desc",
        Status = EventStatus.Open,
        MaxVote = 3,
        CreatedByAdminId = adminId,
        SubmissionStartAt = now.AddDays(-10),
        SubmissionEndAt = now.AddDays(-5),
        JudgingStartAt = now.AddDays(-4),
        JudgingEndAt = now.AddDays(-1),
        VotingStartAt = now.AddDays(-1),
        VotingEndAt = now.AddDays(5),
        ResultAnnouncementAt = now.AddDays(6),
        CreatedAt = now
    };

    seedDb.PlatformEvents.AddRange(event1, event2);

    // 10 submissions for Event 1
    for (int i = 0; i < 10; i++)
    {
        seedDb.EventSubmissions.Add(new EventSubmission
        {
            Id = Guid.NewGuid(),
            EventId = event1.Id,
            SubmitterId = submitterId,
            ArtworkId = artworkId,
            Title = $"Sub E1 #{i + 1}",
            AiScanPassed = true,
            VoteCount = 0,
            SubmittedAt = now.AddDays(-6)
        });
    }

    // 10 submissions for Event 2
    for (int i = 0; i < 10; i++)
    {
        seedDb.EventSubmissions.Add(new EventSubmission
        {
            Id = Guid.NewGuid(),
            EventId = event2.Id,
            SubmitterId = submitterId,
            ArtworkId = artworkId,
            Title = $"Sub E2 #{i + 1}",
            AiScanPassed = true,
            VoteCount = 0,
            SubmittedAt = now.AddDays(-6)
        });
    }

    await seedDb.SaveChangesAsync();
}

List<Guid> e1Submissions;
List<Guid> e2Submissions;
Guid e1Id;
Guid e2Id;

await using (var queryDb = CreateDb())
{
    var e1 = await queryDb.PlatformEvents.FirstAsync(e => e.MaxVote == 1);
    var e2 = await queryDb.PlatformEvents.FirstAsync(e => e.MaxVote == 3);
    e1Id = e1.Id;
    e2Id = e2.Id;
    e1Submissions = await queryDb.EventSubmissions.Where(s => s.EventId == e1.Id).Select(s => s.Id).ToListAsync();
    e2Submissions = await queryDb.EventSubmissions.Where(s => s.EventId == e2.Id).Select(s => s.Id).ToListAsync();
}

// -------------------------------------------------------------
// TEST 1: Concurrency race for voterA in Event 1 (MaxVote = 1)
// voterA sends 10 concurrent requests to vote for 10 different submissions
// -------------------------------------------------------------
Console.WriteLine("\n[TEST 1] 10 concurrent votes by voterA on 10 different submissions in Event 1 (MaxVote = 1)...");

var tasksTest1 = e1Submissions.Select(async subId =>
{
    await using var db = CreateDb();
    var handler = new CreateVoteCommandHandler(db);
    return await handler.Handle(new CreateVoteCommand(subId, voterA), CancellationToken.None);
}).ToArray();

var resultsTest1 = await Task.WhenAll(tasksTest1);

var successCountTest1 = resultsTest1.Count(r => r.Success);
var failCountTest1 = resultsTest1.Count(r => !r.Success);

Console.WriteLine($"Test 1 Results: Success={successCountTest1}, Fail={failCountTest1}");

if (successCountTest1 != 1 || failCountTest1 != 9)
{
    throw new Exception($"TEST 1 FAILED! Expected 1 success and 9 fails, but got {successCountTest1} success and {failCountTest1} fails.");
}

await using (var verifyDb = CreateDb())
{
    var totalVotesInDb = await verifyDb.EventVotes
        .CountAsync(v => v.Submission!.EventId == e1Id && v.VoterId == voterA);
    if (totalVotesInDb != 1)
    {
        throw new Exception($"TEST 1 DB FAILED! Expected exactly 1 vote in DB for voterA, but found {totalVotesInDb}.");
    }
}
Console.WriteLine("--> TEST 1 PASSED: Exactly 1 vote succeeded, voterA did NOT exceed MaxVote=1.");

// -------------------------------------------------------------
// TEST 2: Concurrency race for voterA in Event 2 (MaxVote = 3)
// voterA sends 10 concurrent requests to vote for 10 different submissions
// -------------------------------------------------------------
Console.WriteLine("\n[TEST 2] 10 concurrent votes by voterA on 10 different submissions in Event 2 (MaxVote = 3)...");

var tasksTest2 = e2Submissions.Select(async subId =>
{
    await using var db = CreateDb();
    var handler = new CreateVoteCommandHandler(db);
    return await handler.Handle(new CreateVoteCommand(subId, voterA), CancellationToken.None);
}).ToArray();

var resultsTest2 = await Task.WhenAll(tasksTest2);

var successCountTest2 = resultsTest2.Count(r => r.Success);
var failCountTest2 = resultsTest2.Count(r => !r.Success);

Console.WriteLine($"Test 2 Results: Success={successCountTest2}, Fail={failCountTest2}");

if (successCountTest2 != 3 || failCountTest2 != 7)
{
    throw new Exception($"TEST 2 FAILED! Expected 3 success and 7 fails, but got {successCountTest2} success and {failCountTest2} fails.");
}

await using (var verifyDb = CreateDb())
{
    var totalVotesInDb = await verifyDb.EventVotes
        .CountAsync(v => v.Submission!.EventId == e2Id && v.VoterId == voterA);
    if (totalVotesInDb != 3)
    {
        throw new Exception($"TEST 2 DB FAILED! Expected exactly 3 votes in DB for voterA, but found {totalVotesInDb}.");
    }
}
Console.WriteLine("--> TEST 2 PASSED: Exactly 3 votes succeeded, voterA did NOT exceed MaxVote=3.");

// -------------------------------------------------------------
// TEST 3: Rapid repeated votes by voterB for the SAME submission
// voterB sends 5 concurrent requests to vote for the SAME submission
// -------------------------------------------------------------
Console.WriteLine("\n[TEST 3] 5 concurrent votes by voterB for the SAME submission in Event 2...");

var targetSub = e2Submissions.First();
var tasksTest3 = Enumerable.Range(0, 5).Select(async _ =>
{
    await using var db = CreateDb();
    var handler = new CreateVoteCommandHandler(db);
    return await handler.Handle(new CreateVoteCommand(targetSub, voterB), CancellationToken.None);
}).ToArray();

var resultsTest3 = await Task.WhenAll(tasksTest3);

var successCountTest3 = resultsTest3.Count(r => r.Success);
var failCountTest3 = resultsTest3.Count(r => !r.Success);

Console.WriteLine($"Test 3 Results: Success={successCountTest3}, Fail={failCountTest3}");

if (successCountTest3 != 1 || failCountTest3 != 4)
{
    throw new Exception($"TEST 3 FAILED! Expected 1 success and 4 fails, but got {successCountTest3} success and {failCountTest3} fails.");
}

await using (var verifyDb = CreateDb())
{
    var votesForSub = await verifyDb.EventVotes
        .CountAsync(v => v.SubmissionId == targetSub && v.VoterId == voterB);
    if (votesForSub != 1)
    {
        throw new Exception($"TEST 3 DB FAILED! Expected 1 vote for voterB on this submission, found {votesForSub}.");
    }
}
Console.WriteLine("--> TEST 3 PASSED: Exactly 1 vote succeeded, duplicate votes cleanly rejected.");

// -------------------------------------------------------------
// TEST 4: DeleteVote and Vote concurrency consistency
// voterB unvotes targetSub, remaining votes count is recalculated accurately
// -------------------------------------------------------------
Console.WriteLine("\n[TEST 4] Unvoting and remaining vote count calculation...");
await using (var unvoteDb = CreateDb())
{
    var unvoteHandler = new DeleteVoteCommandHandler(unvoteDb);
    var unvoteResult = await unvoteHandler.Handle(new DeleteVoteCommand(targetSub, voterB), CancellationToken.None);
    if (!unvoteResult.Success)
    {
        throw new Exception($"TEST 4 UNVOTE FAILED: {string.Join(", ", unvoteResult.Errors)}");
    }
    if (unvoteResult.Data?.RemainingVotesInEvent != 3)
    {
        throw new Exception($"TEST 4 FAILED: Expected 3 remaining votes after unvote, got {unvoteResult.Data?.RemainingVotesInEvent}");
    }
}
Console.WriteLine("--> TEST 4 PASSED: Unvote succeeded and remaining votes accurately updated.");

Console.WriteLine("\n=== ALL CONCURRENT VOTE CHECKS PASSED SUCCESSFULLY! ===");
