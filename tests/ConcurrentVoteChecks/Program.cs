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

    var creatorProfile = new CreatorProfile
    {
        Id = Guid.NewGuid(),
        UserId = submitterId,
        DisplayName = "Submitter Creator"
    };
    seedDb.CreatorProfiles.Add(creatorProfile);

    var artworkId = Guid.NewGuid();
    var artwork = new Artwork
    {
        Id = artworkId,
        CreatorProfileId = creatorProfile.Id,
        Title = "Sample Art",
        Description = "Sample Desc",
        ImageUrl = "https://example.com/art.jpg",
        CreatedAt = now
    };
    seedDb.Artworks.Add(artwork);

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

    // Open Event A (In active submission period)
    var openEventA = new PlatformEvent
    {
        Id = Guid.NewGuid(),
        Title = "Open Event A",
        Description = "Open Event A Desc",
        Status = EventStatus.Open,
        MaxVote = 1,
        CreatedByAdminId = adminId,
        SubmissionStartAt = now.AddDays(-2),
        SubmissionEndAt = now.AddDays(5),
        JudgingStartAt = now.AddDays(6),
        JudgingEndAt = now.AddDays(10),
        VotingStartAt = now.AddDays(11),
        VotingEndAt = now.AddDays(15),
        ResultAnnouncementAt = now.AddDays(16),
        CreatedAt = now
    };

    // Open Event B (In active submission period)
    var openEventB = new PlatformEvent
    {
        Id = Guid.NewGuid(),
        Title = "Open Event B",
        Description = "Open Event B Desc",
        Status = EventStatus.Open,
        MaxVote = 1,
        CreatedByAdminId = adminId,
        SubmissionStartAt = now.AddDays(-2),
        SubmissionEndAt = now.AddDays(5),
        JudgingStartAt = now.AddDays(6),
        JudgingEndAt = now.AddDays(10),
        VotingStartAt = now.AddDays(11),
        VotingEndAt = now.AddDays(15),
        ResultAnnouncementAt = now.AddDays(16),
        CreatedAt = now
    };

    seedDb.PlatformEvents.AddRange(openEventA, openEventB);

    var subA = new EventSubmission
    {
        Id = Guid.NewGuid(),
        EventId = openEventA.Id,
        SubmitterId = submitterId,
        ArtworkId = artworkId,
        Title = "Original Title Sub A",
        AiScanPassed = true,
        VoteCount = 0,
        SubmittedAt = now.AddDays(-1)
    };

    var subB = new EventSubmission
    {
        Id = Guid.NewGuid(),
        EventId = openEventB.Id,
        SubmitterId = submitterId,
        ArtworkId = artworkId,
        Title = "Original Title Sub B",
        AiScanPassed = true,
        VoteCount = 0,
        SubmittedAt = now.AddDays(-1)
    };

    seedDb.EventSubmissions.AddRange(subA, subB);

    await seedDb.SaveChangesAsync();
}

List<Guid> e1Submissions;
List<Guid> e2Submissions;
Guid e1Id;
Guid e2Id;
Guid eventAId;
Guid eventBId;
Guid submissionAId;
Guid submissionBId;

await using (var queryDb = CreateDb())
{
    var e1 = await queryDb.PlatformEvents.FirstAsync(e => e.MaxVote == 1 && e.Title == "Event MaxVote 1");
    var e2 = await queryDb.PlatformEvents.FirstAsync(e => e.MaxVote == 3 && e.Title == "Event MaxVote 3");
    var evA = await queryDb.PlatformEvents.FirstAsync(e => e.Title == "Open Event A");
    var evB = await queryDb.PlatformEvents.FirstAsync(e => e.Title == "Open Event B");
    e1Id = e1.Id;
    e2Id = e2.Id;
    eventAId = evA.Id;
    eventBId = evB.Id;
    e1Submissions = await queryDb.EventSubmissions.Where(s => s.EventId == e1.Id).Select(s => s.Id).ToListAsync();
    e2Submissions = await queryDb.EventSubmissions.Where(s => s.EventId == e2.Id).Select(s => s.Id).ToListAsync();
    submissionAId = (await queryDb.EventSubmissions.FirstAsync(s => s.EventId == evA.Id)).Id;
    submissionBId = (await queryDb.EventSubmissions.FirstAsync(s => s.EventId == evB.Id)).Id;
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

// -------------------------------------------------------------
// TEST 5: Cross-event submission modification prevention
// Route Event B cannot edit or withdraw submission belonging to Event A
// -------------------------------------------------------------
Console.WriteLine("\n[TEST 5] Verifying Route of Event B cannot edit or withdraw submission belonging to Event A...");

// 5A: Edit submission A (belongs to Event A) with EventId = Event B (Mismatch)
await using (var editDb = CreateDb())
{
    var updateHandler = new UpdateSubmissionCommandHandler(editDb, null!);
    var mismatchCommand = new UpdateSubmissionCommand(
        SubmissionId: submissionAId,
        UserId: submitterId,
        EventId: eventBId, // Intentional mismatch: submissionAId belongs to eventAId
        Title: "Hacked Title"
    );

    var (success, data, errors) = await updateHandler.Handle(mismatchCommand, CancellationToken.None);
    if (success)
    {
        throw new Exception("TEST 5A FAILED: Update succeeded when EventId mismatched!");
    }

    if (!errors.Any(e => e.Contains("Bài dự thi không thuộc sự kiện này.")))
    {
        throw new Exception($"TEST 5A FAILED: Error message did not mention event mismatch. Got: {string.Join(", ", errors)}");
    }

    Console.WriteLine("--> TEST 5A PASSED: Update correctly rejected with 'Bài dự thi không thuộc sự kiện này.'");
}

// 5B: Edit submission A (belongs to Event A) with EventId = Event A (Match)
await using (var editDb = CreateDb())
{
    var updateHandler = new UpdateSubmissionCommandHandler(editDb, null!);
    var validCommand = new UpdateSubmissionCommand(
        SubmissionId: submissionAId,
        UserId: submitterId,
        EventId: eventAId, // Matching EventId
        Title: "Updated Valid Title"
    );

    var (success, data, errors) = await updateHandler.Handle(validCommand, CancellationToken.None);
    if (!success)
    {
        throw new Exception($"TEST 5B FAILED: Update failed for valid matching event. Errors: {string.Join(", ", errors)}");
    }

    if (data?.Title != "Updated Valid Title")
    {
        throw new Exception($"TEST 5B FAILED: Title was not updated. Got: {data?.Title}");
    }

    Console.WriteLine("--> TEST 5B PASSED: Update succeeded when EventId matched.");
}

// 5C: Withdraw submission A (belongs to Event A) with EventId = Event B (Mismatch)
await using (var withdrawDb = CreateDb())
{
    var withdrawHandler = new WithdrawSubmissionCommandHandler(withdrawDb);
    var mismatchWithdrawCommand = new WithdrawSubmissionCommand(
        SubmissionId: submissionAId,
        UserId: submitterId,
        EventId: eventBId // Intentional mismatch
    );

    var (success, errors) = await withdrawHandler.Handle(mismatchWithdrawCommand, CancellationToken.None);
    if (success)
    {
        throw new Exception("TEST 5C FAILED: Withdraw succeeded when EventId mismatched!");
    }

    if (!errors.Any(e => e.Contains("Bài dự thi không thuộc sự kiện này.")))
    {
        throw new Exception($"TEST 5C FAILED: Error message did not mention event mismatch. Got: {string.Join(", ", errors)}");
    }

    Console.WriteLine("--> TEST 5C PASSED: Withdraw correctly rejected with 'Bài dự thi không thuộc sự kiện này.'");
}

// 5D: Withdraw submission A (belongs to Event A) with EventId = Event A (Match)
await using (var withdrawDb = CreateDb())
{
    var withdrawHandler = new WithdrawSubmissionCommandHandler(withdrawDb);
    var validWithdrawCommand = new WithdrawSubmissionCommand(
        SubmissionId: submissionAId,
        UserId: submitterId,
        EventId: eventAId // Matching EventId
    );

    var (success, errors) = await withdrawHandler.Handle(validWithdrawCommand, CancellationToken.None);
    if (!success)
    {
        throw new Exception($"TEST 5D FAILED: Withdraw failed for valid matching event. Errors: {string.Join(", ", errors)}");
    }

    Console.WriteLine("--> TEST 5D PASSED: Withdraw succeeded when EventId matched.");
}

Console.WriteLine("\n=== ALL CHECKS (INCLUDING CONCURRENT VOTING & CROSS-EVENT PREVENTION) PASSED SUCCESSFULLY! ===");

