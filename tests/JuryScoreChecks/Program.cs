using ArtCommission.Application.Event.Commands;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Domain.Entities.Event;
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

// Create 2 events
var event1 = new PlatformEvent
{
    Id = Guid.NewGuid(),
    Title = "Event 1",
    Description = "Event 1 Desc",
    Status = EventStatus.Open,
    CreatedByAdminId = adminId,
    SubmissionStartAt = now.AddDays(-10),
    SubmissionEndAt = now.AddDays(-5),
    JudgingStartAt = now.AddDays(-4),
    JudgingEndAt = now.AddDays(5),
    VotingStartAt = now.AddDays(6),
    VotingEndAt = now.AddDays(10),
    ResultAnnouncementAt = now.AddDays(11),
    CreatedAt = now,
    IsDeleted = false
};

var event2 = new PlatformEvent
{
    Id = Guid.NewGuid(),
    Title = "Event 2",
    Description = "Event 2 Desc",
    Status = EventStatus.Open,
    CreatedByAdminId = adminId,
    SubmissionStartAt = now.AddDays(-10),
    SubmissionEndAt = now.AddDays(-5),
    JudgingStartAt = now.AddDays(-4),
    JudgingEndAt = now.AddDays(5),
    VotingStartAt = now.AddDays(6),
    VotingEndAt = now.AddDays(10),
    ResultAnnouncementAt = now.AddDays(11),
    CreatedAt = now,
    IsDeleted = false
};

db.PlatformEvents.AddRange(event1, event2);

// Add criteria for Event 1
var crit1A = new EventCriteria
{
    Id = Guid.NewGuid(),
    EventId = event1.Id,
    Name = "Crit 1A",
    MaxScore = 40,
    Weight = 0.4m,
    CreatedAt = now
};
var crit1B = new EventCriteria
{
    Id = Guid.NewGuid(),
    EventId = event1.Id,
    Name = "Crit 1B",
    MaxScore = 60,
    Weight = 0.6m,
    CreatedAt = now
};

// Add criteria for Event 2
var crit2A = new EventCriteria
{
    Id = Guid.NewGuid(),
    EventId = event2.Id,
    Name = "Crit 2A",
    MaxScore = 50,
    Weight = 0.5m,
    CreatedAt = now
};

db.EventCriteria.AddRange(crit1A, crit1B, crit2A);

var juryCreator = new ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile
{
    Id = Guid.NewGuid(),
    UserId = juryUserId,
    DisplayName = "Jury"
};
db.CreatorProfiles.Add(juryCreator);

// Add Jury for Event 1
var jury1 = new Jury
{
    Id = Guid.NewGuid(),
    EventId = event1.Id,
    CreatorId = juryCreator.Id,
    Creator = juryCreator,
    CreatedAt = now,
    IsDeleted = false
};
db.Juries.Add(jury1);

// Add Submission for Event 1
var submission1 = new EventSubmission
{
    Id = Guid.NewGuid(),
    EventId = event1.Id,
    SubmitterId = submitterId,
    ArtworkId = Guid.NewGuid(),
    Title = "Submission 1",
    SubmittedAt = now.AddDays(-6)
};
db.EventSubmissions.Add(submission1);

await db.SaveChangesAsync();

var handler = new SubmitJuryScoreCommandHandler(db);

void Assert(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL: " + message);
    Console.WriteLine("PASS: " + message);
}

// 1. Rejects negative total score
{
    var dto = new SubmitScoreDto
    {
        SubmissionId = submission1.Id,
        TotalScore = -5,
        CriteriaScores = new List<CriteriaScoreItemDto>
        {
            new() { CriteriaId = crit1A.Id, Score = 20 }
        }
    };
    var result = await handler.Handle(new SubmitJuryScoreCommand(submission1.Id, juryUserId, dto), default);
    Assert(!result.Success, "Must reject negative total score");
    Assert(result.Errors.Any(e => e.Contains("âm")), "Error must mention negative score");
}

// 2. Rejects negative criterion score
{
    var dto = new SubmitScoreDto
    {
        SubmissionId = submission1.Id,
        TotalScore = 20,
        CriteriaScores = new List<CriteriaScoreItemDto>
        {
            new() { CriteriaId = crit1A.Id, Score = -10 }
        }
    };
    var result = await handler.Handle(new SubmitJuryScoreCommand(submission1.Id, juryUserId, dto), default);
    Assert(!result.Success, "Must reject negative criterion score");
    Assert(result.Errors.Any(e => e.Contains("âm")), "Error must mention negative score");
}

// 3. Rejects criteria belonging to another event (crit2A on submission1 which is event1)
{
    var dto = new SubmitScoreDto
    {
        SubmissionId = submission1.Id,
        TotalScore = 30,
        CriteriaScores = new List<CriteriaScoreItemDto>
        {
            new() { CriteriaId = crit2A.Id, Score = 30 }
        }
    };
    var result = await handler.Handle(new SubmitJuryScoreCommand(submission1.Id, juryUserId, dto), default);
    Assert(!result.Success, "Must reject criteria belonging to another event");
    Assert(result.Errors.Any(e => e.Contains("không thuộc sự kiện")), "Error must state criteria does not belong to event");
}

// 4. Rejects empty/missing CriteriaId
{
    var dto = new SubmitScoreDto
    {
        SubmissionId = submission1.Id,
        TotalScore = 20,
        CriteriaScores = new List<CriteriaScoreItemDto>
        {
            new() { CriteriaId = null, Score = 20 }
        }
    };
    var result = await handler.Handle(new SubmitJuryScoreCommand(submission1.Id, juryUserId, dto), default);
    Assert(!result.Success, "Must reject null CriteriaId");
}

// 5. Rejects score exceeding MaxScore
{
    var dto = new SubmitScoreDto
    {
        SubmissionId = submission1.Id,
        TotalScore = 50,
        CriteriaScores = new List<CriteriaScoreItemDto>
        {
            new() { CriteriaId = crit1A.Id, Score = 45 } // MaxScore is 40
        }
    };
    var result = await handler.Handle(new SubmitJuryScoreCommand(submission1.Id, juryUserId, dto), default);
    Assert(!result.Success, "Must reject score exceeding MaxScore");
    Assert(result.Errors.Any(e => e.Contains("vượt quá")), "Error must state score exceeds MaxScore");
}

// 6. Accepts valid criteria with valid scores
{
    var dto = new SubmitScoreDto
    {
        SubmissionId = submission1.Id,
        TotalScore = 80,
        Notes = "Good job",
        CriteriaScores = new List<CriteriaScoreItemDto>
        {
            new() { CriteriaId = crit1A.Id, Score = 35 },
            new() { CriteriaId = crit1B.Id, Score = 45 }
        }
    };
    var result = await handler.Handle(new SubmitJuryScoreCommand(submission1.Id, juryUserId, dto), default);
    Assert(result.Success, "Valid score submission must succeed");
    Assert(result.Data != null && result.Data.Score == 80, "Returned score must match");
    var updated = await db.EventSubmissions.FindAsync(submission1.Id);
    Assert(updated!.Score == 80, "Submission Score must be updated in DB");
}

// 7. Rejects editing score when already submitted and locked
{
    var dto = new SubmitScoreDto
    {
        SubmissionId = submission1.Id,
        TotalScore = 90,
        CriteriaScores = new List<CriteriaScoreItemDto>
        {
            new() { CriteriaId = crit1A.Id, Score = 40 },
            new() { CriteriaId = crit1B.Id, Score = 50 }
        }
    };
    var result = await handler.Handle(new SubmitJuryScoreCommand(submission1.Id, juryUserId, dto), default);
    Assert(!result.Success, "Must reject modifying an already locked score");
    Assert(result.Errors.Any(e => e.Contains("khóa")), "Error must mention score is locked");
}

// 8. Rejects when judging period has ended
{
    var pastEvent = new PlatformEvent
    {
        Id = Guid.NewGuid(),
        Title = "Past Event",
        Description = "Past Event Desc",
        Status = EventStatus.Open,
        CreatedByAdminId = adminId,
        SubmissionStartAt = now.AddDays(-20),
        SubmissionEndAt = now.AddDays(-15),
        JudgingStartAt = now.AddDays(-14),
        JudgingEndAt = now.AddDays(-1),
        VotingStartAt = now.AddDays(1),
        VotingEndAt = now.AddDays(5),
        ResultAnnouncementAt = now.AddDays(6),
        CreatedAt = now.AddDays(-25),
        IsDeleted = false
    };
    db.PlatformEvents.Add(pastEvent);

    var pastCrit = new EventCriteria
    {
        Id = Guid.NewGuid(),
        EventId = pastEvent.Id,
        Name = "Crit Past",
        MaxScore = 100,
        Weight = 1.0m,
        CreatedAt = now
    };
    db.EventCriteria.Add(pastCrit);

    var pastJury = new Jury
    {
        Id = Guid.NewGuid(),
        EventId = pastEvent.Id,
        CreatorId = juryCreator.Id,
        Creator = juryCreator,
        CreatedAt = now,
        IsDeleted = false
    };
    db.Juries.Add(pastJury);

    var pastSub = new EventSubmission
    {
        Id = Guid.NewGuid(),
        EventId = pastEvent.Id,
        SubmitterId = submitterId,
        ArtworkId = Guid.NewGuid(),
        Title = "Past Sub",
        SubmittedAt = now.AddDays(-16)
    };
    db.EventSubmissions.Add(pastSub);
    await db.SaveChangesAsync();

    var dto = new SubmitScoreDto
    {
        SubmissionId = pastSub.Id,
        TotalScore = 85,
        CriteriaScores = new List<CriteriaScoreItemDto>
        {
            new() { CriteriaId = pastCrit.Id, Score = 85 }
        }
    };
    var result = await handler.Handle(new SubmitJuryScoreCommand(pastSub.Id, juryUserId, dto), default);
    Assert(!result.Success, "Must reject scoring after judging period ended");
    Assert(result.Errors.Any(e => e.Contains("kết thúc") || e.Contains("khóa")), "Error must mention judging ended / locked");
}

Console.WriteLine("ALL JURY SCORE CHECKS PASSED!");
