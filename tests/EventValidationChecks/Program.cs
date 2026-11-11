using ArtCommission.Application.ArtistStudio.Marketplace;
using ArtCommission.Application.Dispute.SubmissionDispute.Commands;
using ArtCommission.Application.Dispute.SubmissionDispute.Queries;
using ArtCommission.Application.Event.Commands;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Queries;
using ArtCommission.Application.Event.Validators;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Event;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

Console.WriteLine("=================================================");
Console.WriteLine("STARTING EVENT DTO & ENTITY VALIDATION TEST SUITE");
Console.WriteLine("=================================================\n");

int passed = 0;
int failed = 0;

void Assert(bool condition, string message)
{
    if (condition)
    {
        Console.WriteLine($"[PASS] {message}");
        passed++;
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {message}");
        Console.ResetColor();
        failed++;
        throw new Exception($"Test assertion failed: {message}");
    }
}

var now = DateTimeOffset.UtcNow;
var adminId = Guid.NewGuid();

// =========================================================================
// SECTION 1: CreateEventCommandValidator (DTO & Timeline Constraints)
// =========================================================================
Console.WriteLine("--- Section 1: CreateEventCommandValidator Tests ---");
var createValidator = new CreateEventCommandValidator();

var baseValidCreate = new CreateEventCommand(
    Title: "Summer Art Contest 2026",
    Description: "Annual illustration contest for indie artists.",
    BannerUrl: "https://example.com/banner.png",
    Rules: "Original digital art only.",
    Prize: "$1000 First Place",
    MaxVote: 1,
    Status: EventStatus.Draft,
    SubmissionStartAt: now.AddDays(1),
    SubmissionEndAt: now.AddDays(10),
    JudgingStartAt: now.AddDays(11),
    JudgingEndAt: now.AddDays(20),
    VotingStartAt: now.AddDays(21),
    VotingEndAt: now.AddDays(30),
    ResultAnnouncementAt: now.AddDays(31),
    AdminId: adminId
);

// 1.1 Valid command passes
var resValid = await createValidator.ValidateAsync(baseValidCreate);
Assert(resValid.IsValid, "Valid CreateEventCommand must pass validation.");

// 1.2 Empty AdminId fails
var resEmptyAdmin = await createValidator.ValidateAsync(baseValidCreate with { AdminId = Guid.Empty });
Assert(!resEmptyAdmin.IsValid && resEmptyAdmin.Errors.Any(e => e.PropertyName == "AdminId"),
    "Empty AdminId must be rejected.");

// 1.3 Empty / Whitespace Title fails
var resEmptyTitle = await createValidator.ValidateAsync(baseValidCreate with { Title = "   " });
Assert(!resEmptyTitle.IsValid && resEmptyTitle.Errors.Any(e => e.PropertyName == "Title"),
    "Empty or whitespace Title must be rejected.");

// 1.4 Title > 200 chars fails
var resLongTitle = await createValidator.ValidateAsync(baseValidCreate with { Title = new string('A', 201) });
Assert(!resLongTitle.IsValid && resLongTitle.Errors.Any(e => e.ErrorMessage.Contains("200 characters")),
    "Title longer than 200 characters must be rejected.");

// 1.5 Empty Description fails
var resEmptyDesc = await createValidator.ValidateAsync(baseValidCreate with { Description = "" });
Assert(!resEmptyDesc.IsValid && resEmptyDesc.Errors.Any(e => e.PropertyName == "Description"),
    "Empty Description must be rejected.");

// 1.6 BannerUrl validation: Invalid URL fails, valid absolute URL passes, null/empty passes
var resInvalidUrl = await createValidator.ValidateAsync(baseValidCreate with { BannerUrl = "not-a-valid-url" });
Assert(!resInvalidUrl.IsValid && resInvalidUrl.Errors.Any(e => e.PropertyName == "BannerUrl"),
    "Malformed BannerUrl must be rejected.");

var resEmptyBanner = await createValidator.ValidateAsync(baseValidCreate with { BannerUrl = null });
Assert(resEmptyBanner.IsValid, "Null BannerUrl must be allowed.");

// 1.7 Prize > 1000 chars fails
var resLongPrize = await createValidator.ValidateAsync(baseValidCreate with { Prize = new string('P', 1001) });
Assert(!resLongPrize.IsValid && resLongPrize.Errors.Any(e => e.PropertyName == "Prize"),
    "Prize longer than 1000 characters must be rejected.");

// 1.8 MaxVote <= 0 fails
var resZeroVote = await createValidator.ValidateAsync(baseValidCreate with { MaxVote = 0 });
Assert(!resZeroVote.IsValid && resZeroVote.Errors.Any(e => e.PropertyName == "MaxVote"),
    "MaxVote of 0 must be rejected.");

var resNegVote = await createValidator.ValidateAsync(baseValidCreate with { MaxVote = -5 });
Assert(!resNegVote.IsValid && resNegVote.Errors.Any(e => e.PropertyName == "MaxVote"),
    "Negative MaxVote must be rejected.");

// 1.9 Timeline: SubmissionEndAt <= SubmissionStartAt fails
var resTimeline1 = await createValidator.ValidateAsync(baseValidCreate with { SubmissionEndAt = baseValidCreate.SubmissionStartAt });
Assert(!resTimeline1.IsValid && resTimeline1.Errors.Any(e => e.ErrorMessage.Contains("Submission end date must be after submission start date")),
    "SubmissionEndAt <= SubmissionStartAt must be rejected.");

// 1.10 Timeline: JudgingStartAt <= SubmissionEndAt fails
var resTimeline2 = await createValidator.ValidateAsync(baseValidCreate with { JudgingStartAt = baseValidCreate.SubmissionEndAt.AddDays(-1) });
Assert(!resTimeline2.IsValid && resTimeline2.Errors.Any(e => e.ErrorMessage.Contains("Judging start date must be after submission end date")),
    "JudgingStartAt <= SubmissionEndAt must be rejected.");

// 1.11 Timeline: JudgingEndAt <= JudgingStartAt fails
var resTimeline3 = await createValidator.ValidateAsync(baseValidCreate with { JudgingEndAt = baseValidCreate.JudgingStartAt });
Assert(!resTimeline3.IsValid && resTimeline3.Errors.Any(e => e.ErrorMessage.Contains("Judging end date must be after judging start date")),
    "JudgingEndAt <= JudgingStartAt must be rejected.");

// 1.12 Timeline: VotingStartAt <= JudgingEndAt fails
var resTimeline4 = await createValidator.ValidateAsync(baseValidCreate with { VotingStartAt = baseValidCreate.JudgingEndAt.AddHours(-1) });
Assert(!resTimeline4.IsValid && resTimeline4.Errors.Any(e => e.ErrorMessage.Contains("Voting start date must be after judging end date")),
    "VotingStartAt <= JudgingEndAt must be rejected.");

// 1.13 Timeline: VotingEndAt <= VotingStartAt fails
var resTimeline5 = await createValidator.ValidateAsync(baseValidCreate with { VotingEndAt = baseValidCreate.VotingStartAt });
Assert(!resTimeline5.IsValid && resTimeline5.Errors.Any(e => e.ErrorMessage.Contains("Voting end date must be after voting start date")),
    "VotingEndAt <= VotingStartAt must be rejected.");

// 1.14 Timeline: ResultAnnouncementAt <= VotingEndAt fails
var resTimeline6 = await createValidator.ValidateAsync(baseValidCreate with { ResultAnnouncementAt = baseValidCreate.VotingEndAt });
Assert(!resTimeline6.IsValid && resTimeline6.Errors.Any(e => e.ErrorMessage.Contains("Result announcement date must be after voting end date")),
    "ResultAnnouncementAt <= VotingEndAt must be rejected.");


// =========================================================================
// SECTION 2: UpdateEventCommandValidator & ValidateTimelineTransition
// =========================================================================
Console.WriteLine("\n--- Section 2: UpdateEventCommandValidator & Timeline Transition Tests ---");
var updateValidator = new UpdateEventCommandValidator();
var eventId = Guid.NewGuid();

var baseValidUpdate = new UpdateEventCommand(
    EventId: eventId,
    Title: "Updated Art Contest",
    Description: "Updated description.",
    BannerUrl: "https://example.com/banner-v2.png",
    Rules: null,
    Prize: "$2000",
    MaxVote: 2,
    Status: EventStatus.Open,
    SubmissionStartAt: now.AddDays(1),
    SubmissionEndAt: now.AddDays(10),
    JudgingStartAt: now.AddDays(11),
    JudgingEndAt: now.AddDays(20),
    VotingStartAt: now.AddDays(21),
    VotingEndAt: now.AddDays(30),
    ResultAnnouncementAt: now.AddDays(31),
    AdminId: adminId
);

// 2.1 Empty EventId fails
var resEmptyEventId = await updateValidator.ValidateAsync(baseValidUpdate with { EventId = Guid.Empty });
Assert(!resEmptyEventId.IsValid && resEmptyEventId.Errors.Any(e => e.PropertyName == "EventId"),
    "Empty EventId on UpdateEventCommand must be rejected.");

// 2.2 ValidateTimelineTransition: Past Phase Modification Rule
var pastEvent = new PlatformEvent
{
    Id = eventId,
    Title = "Past Event",
    Description = "Desc",
    Status = EventStatus.Open,
    CreatedByAdminId = adminId,
    SubmissionStartAt = now.AddDays(-20),
    SubmissionEndAt = now.AddDays(-10),
    JudgingStartAt = now.AddDays(-9),
    JudgingEndAt = now.AddDays(-5),
    VotingStartAt = now.AddDays(-4),
    VotingEndAt = now.AddDays(-1),
    ResultAnnouncementAt = now.AddDays(2),
    CreatedAt = now.AddDays(-25)
};

// Modifying past SubmissionStartAt must fail
var updatePastStart = baseValidUpdate with
{
    SubmissionStartAt = now.AddDays(-18),
    SubmissionEndAt = pastEvent.SubmissionEndAt,
    JudgingStartAt = pastEvent.JudgingStartAt,
    JudgingEndAt = pastEvent.JudgingEndAt,
    VotingStartAt = pastEvent.VotingStartAt,
    VotingEndAt = pastEvent.VotingEndAt,
    ResultAnnouncementAt = pastEvent.ResultAnnouncementAt
};
var pastErrors = UpdateEventCommandValidator.ValidateTimelineTransition(pastEvent, updatePastStart, now);
Assert(pastErrors.Any(e => e.Contains("Submission start time has already started or passed")),
    "Modifying already started/passed SubmissionStartAt must be rejected.");

// Modifying past SubmissionEndAt must fail
var updatePastEnd = baseValidUpdate with
{
    SubmissionStartAt = pastEvent.SubmissionStartAt,
    SubmissionEndAt = now.AddDays(-8),
    JudgingStartAt = pastEvent.JudgingStartAt,
    JudgingEndAt = pastEvent.JudgingEndAt,
    VotingStartAt = pastEvent.VotingStartAt,
    VotingEndAt = pastEvent.VotingEndAt,
    ResultAnnouncementAt = pastEvent.ResultAnnouncementAt
};
var pastEndErrors = UpdateEventCommandValidator.ValidateTimelineTransition(pastEvent, updatePastEnd, now);
Assert(pastEndErrors.Any(e => e.Contains("Submission end time has already passed")),
    "Modifying already passed SubmissionEndAt must be rejected.");

// 2.3 ValidateTimelineTransition: Active Phase Rule (Can only extend, not shorten)
var activeSubmissionEvent = new PlatformEvent
{
    Id = eventId,
    Title = "Active Event",
    Description = "Desc",
    Status = EventStatus.Open,
    CreatedByAdminId = adminId,
    SubmissionStartAt = now.AddDays(-5),
    SubmissionEndAt = now.AddDays(5), // active now
    JudgingStartAt = now.AddDays(6),
    JudgingEndAt = now.AddDays(10),
    VotingStartAt = now.AddDays(11),
    VotingEndAt = now.AddDays(15),
    ResultAnnouncementAt = now.AddDays(16),
    CreatedAt = now.AddDays(-10)
};

// Shortening SubmissionEndAt (e.g. from +5 days to +2 days) must fail
var shortenActiveEnd = baseValidUpdate with
{
    SubmissionStartAt = activeSubmissionEvent.SubmissionStartAt,
    SubmissionEndAt = now.AddDays(2), // shortened!
    JudgingStartAt = now.AddDays(6),
    JudgingEndAt = now.AddDays(10),
    VotingStartAt = now.AddDays(11),
    VotingEndAt = now.AddDays(15),
    ResultAnnouncementAt = now.AddDays(16)
};
var shortenErrors = UpdateEventCommandValidator.ValidateTimelineTransition(activeSubmissionEvent, shortenActiveEnd, now);
Assert(shortenErrors.Any(e => e.Contains("submission phase is currently active; its end time can only be extended, not shortened")),
    "Shortening active phase end time must be rejected.");

// Extending SubmissionEndAt (e.g. from +5 days to +8 days) must succeed
var extendActiveEnd = baseValidUpdate with
{
    SubmissionStartAt = activeSubmissionEvent.SubmissionStartAt,
    SubmissionEndAt = now.AddDays(8), // extended!
    JudgingStartAt = now.AddDays(9),
    JudgingEndAt = now.AddDays(12),
    VotingStartAt = now.AddDays(13),
    VotingEndAt = now.AddDays(16),
    ResultAnnouncementAt = now.AddDays(17)
};
var extendErrors = UpdateEventCommandValidator.ValidateTimelineTransition(activeSubmissionEvent, extendActiveEnd, now);
Assert(extendErrors.Count == 0,
    "Extending active phase end time must be allowed.");

// 2.4 ValidateTimelineTransition: Future Milestones cannot be backdated to the past
var futureEvent = new PlatformEvent
{
    Id = eventId,
    Title = "Future Event",
    Description = "Desc",
    Status = EventStatus.Draft,
    CreatedByAdminId = adminId,
    SubmissionStartAt = now.AddDays(5),
    SubmissionEndAt = now.AddDays(10),
    JudgingStartAt = now.AddDays(11),
    JudgingEndAt = now.AddDays(15),
    VotingStartAt = now.AddDays(16),
    VotingEndAt = now.AddDays(20),
    ResultAnnouncementAt = now.AddDays(21),
    CreatedAt = now
};

var backdatedFutureStart = baseValidUpdate with
{
    SubmissionStartAt = now.AddDays(-1), // backdated to past!
    SubmissionEndAt = now.AddDays(10),
    JudgingStartAt = now.AddDays(11),
    JudgingEndAt = now.AddDays(15),
    VotingStartAt = now.AddDays(16),
    VotingEndAt = now.AddDays(20),
    ResultAnnouncementAt = now.AddDays(21)
};
var backdateErrors = UpdateEventCommandValidator.ValidateTimelineTransition(futureEvent, backdatedFutureStart, now);
Assert(backdateErrors.Any(e => e.Contains("new submission start time must be in the future")),
    "Backdating future submission start time to past must be rejected.");


// =========================================================================
// SECTION 3: UploadSubmissionCommandValidator & Submission Rules
// =========================================================================
Console.WriteLine("\n--- Section 3: UploadSubmissionCommandValidator Tests ---");
var uploadValidator = new UploadSubmissionCommandValidator();
var submitterId = Guid.NewGuid();
var existingArtworkId = Guid.NewGuid();

// 3.1 Empty EventId or SubmitterId fails
var resEmptyEventSub = await uploadValidator.ValidateAsync(new UploadSubmissionCommand(
    EventId: Guid.Empty, SubmitterId: submitterId, Title: "Art", ImageUrl: "https://example.com/art.png"
));
Assert(!resEmptyEventSub.IsValid && resEmptyEventSub.Errors.Any(e => e.PropertyName == "EventId"),
    "Empty EventId on UploadSubmissionCommand must be rejected.");

// 3.2 New Artwork upload: Missing ImageUrl fails
var resMissingImage = await uploadValidator.ValidateAsync(new UploadSubmissionCommand(
    EventId: eventId, SubmitterId: submitterId, ArtworkId: null, Title: "Artwork 1", ImageUrl: null
));
Assert(!resMissingImage.IsValid && resMissingImage.Errors.Any(e => e.PropertyName == "ImageUrl"),
    "Uploading new artwork without ImageUrl must be rejected.");

// 3.3 New Artwork upload: Missing Title fails
var resMissingTitle = await uploadValidator.ValidateAsync(new UploadSubmissionCommand(
    EventId: eventId, SubmitterId: submitterId, ArtworkId: null, Title: "", ImageUrl: "https://example.com/art.png"
));
Assert(!resMissingTitle.IsValid && resMissingTitle.Errors.Any(e => e.PropertyName == "Title"),
    "Uploading new artwork without Title must be rejected.");

// 3.4 New Artwork upload: More than 10 tags fails
var tooManyTags = Enumerable.Range(1, 11).Select(i => $"tag{i}").ToList();
var resTooManyTags = await uploadValidator.ValidateAsync(new UploadSubmissionCommand(
    EventId: eventId, SubmitterId: submitterId, ArtworkId: null, Title: "Art", ImageUrl: "https://example.com/art.png",
    Tags: tooManyTags
));
Assert(!resTooManyTags.IsValid && resTooManyTags.Errors.Any(e => e.ErrorMessage.Contains("up to 10 tags")),
    "Assigning more than 10 tags must be rejected.");

// 3.5 Existing Artwork upload: Title can be null/empty
var resExistingArtwork = await uploadValidator.ValidateAsync(new UploadSubmissionCommand(
    EventId: eventId, SubmitterId: submitterId, ArtworkId: existingArtworkId, Title: null, ImageUrl: null
));
Assert(resExistingArtwork.IsValid,
    "Uploading existing ArtworkId without Title or ImageUrl must be valid.");

// 3.6 Description > 2000 chars fails
var resLongDesc = await uploadValidator.ValidateAsync(new UploadSubmissionCommand(
    EventId: eventId, SubmitterId: submitterId, ArtworkId: existingArtworkId, Description: new string('D', 2001)
));
Assert(!resLongDesc.IsValid && resLongDesc.Errors.Any(e => e.PropertyName == "Description"),
    "Submission description > 2000 chars must be rejected.");


// =========================================================================
// SECTION 4: CreateVoteCommandValidator & Voting Domain Logic
// =========================================================================
Console.WriteLine("\n--- Section 4: CreateVoteCommandValidator & Voting Domain Logic ---");
var voteValidator = new CreateVoteCommandValidator();

// 4.1 Empty SubmissionId or VoterId fails
var resEmptySubVote = await voteValidator.ValidateAsync(new CreateVoteCommand(SubmissionId: Guid.Empty, VoterId: Guid.NewGuid()));
Assert(!resEmptySubVote.IsValid && resEmptySubVote.Errors.Any(e => e.PropertyName == "SubmissionId"),
    "Empty SubmissionId on CreateVoteCommand must be rejected.");

var resEmptyVoter = await voteValidator.ValidateAsync(new CreateVoteCommand(SubmissionId: Guid.NewGuid(), VoterId: Guid.Empty));
Assert(!resEmptyVoter.IsValid && resEmptyVoter.Errors.Any(e => e.PropertyName == "VoterId"),
    "Empty VoterId on CreateVoteCommand must be rejected.");

// 4.2 Valid vote command passes validator
var resValidVote = await voteValidator.ValidateAsync(new CreateVoteCommand(SubmissionId: Guid.NewGuid(), VoterId: Guid.NewGuid()));
Assert(resValidVote.IsValid, "Valid CreateVoteCommand must pass validator.");


// =========================================================================
// SECTION 5: SubmitJuryScoreCommandValidator
// =========================================================================
Console.WriteLine("\n--- Section 5: SubmitJuryScoreCommandValidator Tests ---");
var juryScoreValidator = new SubmitJuryScoreCommandValidator();
var subId = Guid.NewGuid();
var juryUserId = Guid.NewGuid();
var critId = Guid.NewGuid();

// 5.1 Empty SubmissionId / UserId fails
var resEmptySubScore = await juryScoreValidator.ValidateAsync(new SubmitJuryScoreCommand(
    SubmissionId: Guid.Empty, UserId: juryUserId, Dto: new SubmitScoreDto { TotalScore = 10 }
));
Assert(!resEmptySubScore.IsValid && resEmptySubScore.Errors.Any(e => e.PropertyName == "SubmissionId"),
    "Empty SubmissionId on SubmitJuryScoreCommand must be rejected.");

// 5.2 Negative TotalScore fails
var resNegTotal = await juryScoreValidator.ValidateAsync(new SubmitJuryScoreCommand(
    SubmissionId: subId, UserId: juryUserId, Dto: new SubmitScoreDto { TotalScore = -1.5m }
));
Assert(!resNegTotal.IsValid && resNegTotal.Errors.Any(e => e.ErrorMessage.Contains("không được là số âm")),
    "Negative TotalScore must be rejected.");

// 5.3 Negative Criterion Score fails
var resNegCrit = await juryScoreValidator.ValidateAsync(new SubmitJuryScoreCommand(
    SubmissionId: subId, UserId: juryUserId, Dto: new SubmitScoreDto
    {
        TotalScore = 5,
        CriteriaScores = new List<CriteriaScoreItemDto>
        {
            new() { CriteriaId = critId, Score = -2.0m }
        }
    }
));
Assert(!resNegCrit.IsValid && resNegCrit.Errors.Any(e => e.ErrorMessage.Contains("không được là số âm")),
    "Negative criterion score must be rejected.");

// 5.4 Null CriteriaId fails
var resEmptyCritId = await juryScoreValidator.ValidateAsync(new SubmitJuryScoreCommand(
    SubmissionId: subId, UserId: juryUserId, Dto: new SubmitScoreDto
    {
        TotalScore = 5,
        CriteriaScores = new List<CriteriaScoreItemDto>
        {
            new() { CriteriaId = null, Score = 5.0m }
        }
    }
));
Assert(!resEmptyCritId.IsValid, "Null CriteriaId must be rejected by validator.");


// =========================================================================
// SECTION 6: Entity Invariants & Default Value Checks
// =========================================================================
Console.WriteLine("\n--- Section 6: Entity Invariants & Default Values ---");

// 6.1 PlatformEvent defaults
var testEvent = new PlatformEvent();
Assert(testEvent.Status == EventStatus.Draft, "PlatformEvent default status must be Draft.");
Assert(testEvent.MaxVote == 1, "PlatformEvent default MaxVote must be 1.");
Assert(!testEvent.IsFeatured, "PlatformEvent default IsFeatured must be false.");
Assert(!testEvent.IsHeadJurySignedOff, "PlatformEvent default IsHeadJurySignedOff must be false.");
Assert(!testEvent.IsDeleted, "PlatformEvent default IsDeleted must be false.");

// 6.2 EventCriteria defaults
var testCriteria = new EventCriteria();
Assert(testCriteria.MaxScore == 10.00m, "EventCriteria default MaxScore must be 10.00.");
Assert(testCriteria.DisplayOrder == 0, "EventCriteria default DisplayOrder must be 0.");

// 6.3 EventSubmission defaults
var testSubmission = new EventSubmission();
Assert(!testSubmission.AiScanPassed, "EventSubmission default AiScanPassed must be false.");
Assert(!testSubmission.IsDisqualified, "EventSubmission default IsDisqualified must be false.");
Assert(testSubmission.VoteCount == 0, "EventSubmission default VoteCount must be 0.");
Assert(testSubmission.Score == null, "EventSubmission default Score must be null.");


// =========================================================================
// SECTION 7: In-Memory DB Command Handler Domain Validations
// =========================================================================
Console.WriteLine("\n--- Section 7: In-Memory DB Command Handler Validations ---");

await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .Options);

// Seed admin & event
var adminUser = new ApplicationUser { Id = adminId, UserName = "admin1", FullName = "Admin User" };
var artistUser = new ApplicationUser { Id = submitterId, UserName = "artist1", FullName = "Artist One" };
db.Users.AddRange(adminUser, artistUser);
await db.SaveChangesAsync();

// 7.1 CreateEventCommandHandler saves event and defaults criteria if none provided
var createHandler = new CreateEventCommandHandler(db);
var createResult = await createHandler.Handle(baseValidCreate, CancellationToken.None);
Assert(createResult.Success, "CreateEventCommandHandler must succeed for valid command.");
Assert(createResult.Data != null, "Created event data must not be null.");

var savedEvent = await db.PlatformEvents.Include(e => e.Criteria).FirstOrDefaultAsync(e => e.Id == createResult.Data!.Id);
Assert(savedEvent != null, "Created event must exist in DB.");
Assert(savedEvent!.Criteria.Count == 3, "Default 3 scoring criteria must be automatically created when none provided.");
Assert(savedEvent.Criteria.Any(c => c.Name == "Sáng tạo và ý tưởng" && c.Weight == 0.40m),
    "Criteria 'Sáng tạo và ý tưởng' with weight 0.40 must be initialized.");

// 7.2 UploadSubmission to Draft event is rejected
var draftEvent = new PlatformEvent
{
    Id = Guid.NewGuid(),
    Title = "Draft Contest",
    Description = "Desc",
    Status = EventStatus.Draft,
    CreatedByAdminId = adminId,
    SubmissionStartAt = now.AddDays(-1),
    SubmissionEndAt = now.AddDays(5),
    JudgingStartAt = now.AddDays(6),
    JudgingEndAt = now.AddDays(10),
    VotingStartAt = now.AddDays(11),
    VotingEndAt = now.AddDays(15),
    ResultAnnouncementAt = now.AddDays(16),
    CreatedAt = now
};
db.PlatformEvents.Add(draftEvent);
await db.SaveChangesAsync();

var uploadHandler = new UploadSubmissionCommandHandler(db, null!);
var uploadDraftRes = await uploadHandler.Handle(new UploadSubmissionCommand(
    EventId: draftEvent.Id,
    SubmitterId: submitterId,
    Title: "My Artwork",
    ImageUrl: "https://example.com/art.png"
), CancellationToken.None);

Assert(!uploadDraftRes.Success, "Submission to Draft event must be rejected.");
Assert(uploadDraftRes.Errors.Any(e => e.Contains("Bản nháp (Draft)")),
    "Rejection error must state event is in Draft status.");

// 7.3 UploadSubmission after SubmissionEndAt is rejected
var endedSubmissionEvent = new PlatformEvent
{
    Id = Guid.NewGuid(),
    Title = "Closed Submission Contest",
    Description = "Desc",
    Status = EventStatus.Open,
    CreatedByAdminId = adminId,
    SubmissionStartAt = now.AddDays(-10),
    SubmissionEndAt = now.AddDays(-1), // ended!
    JudgingStartAt = now.AddDays(1),
    JudgingEndAt = now.AddDays(5),
    VotingStartAt = now.AddDays(6),
    VotingEndAt = now.AddDays(10),
    ResultAnnouncementAt = now.AddDays(11),
    CreatedAt = now.AddDays(-15)
};
db.PlatformEvents.Add(endedSubmissionEvent);
await db.SaveChangesAsync();

var uploadLateRes = await uploadHandler.Handle(new UploadSubmissionCommand(
    EventId: endedSubmissionEvent.Id,
    SubmitterId: submitterId,
    Title: "Late Artwork",
    ImageUrl: "https://example.com/art.png"
), CancellationToken.None);

Assert(!uploadLateRes.Success, "Late submission after SubmissionEndAt must be rejected.");
Assert(uploadLateRes.Errors.Any(e => e.Contains("Thời gian nhận bài thi đã kết thúc")),
    "Rejection error must state submission phase has ended.");

// 7.4 Voting rule: Self-voting is rejected
var votingEvent = new PlatformEvent
{
    Id = Guid.NewGuid(),
    Title = "Voting Contest",
    Description = "Desc",
    Status = EventStatus.Open,
    CreatedByAdminId = adminId,
    SubmissionStartAt = now.AddDays(-10),
    SubmissionEndAt = now.AddDays(-5),
    JudgingStartAt = now.AddDays(-4),
    JudgingEndAt = now.AddDays(-2),
    VotingStartAt = now.AddDays(-1),
    VotingEndAt = now.AddDays(5), // active voting
    ResultAnnouncementAt = now.AddDays(10),
    CreatedAt = now.AddDays(-15)
};
var mySubmission = new EventSubmission
{
    Id = Guid.NewGuid(),
    EventId = votingEvent.Id,
    SubmitterId = submitterId,
    ArtworkId = Guid.NewGuid(),
    Title = "My Own Artwork",
    IsDisqualified = false
};
db.PlatformEvents.Add(votingEvent);
db.EventSubmissions.Add(mySubmission);
await db.SaveChangesAsync();

var voteHandler = new CreateVoteCommandHandler(db);
var selfVoteRes = await voteHandler.Handle(new CreateVoteCommand(mySubmission.Id, submitterId), CancellationToken.None);
Assert(!selfVoteRes.Success, "Self-voting by submission author must be rejected.");
Assert(selfVoteRes.Errors.Any(e => e.Contains("không thể tự bình chọn")),
    "Rejection error must state author cannot vote for own submission.");

// 7.5 Voting rule: Voting on disqualified submission is rejected
mySubmission.IsDisqualified = true;
await db.SaveChangesAsync();

var otherVoterId = Guid.NewGuid();
db.Users.Add(new ApplicationUser { Id = otherVoterId, UserName = "voter2", FullName = "Other Voter" });
await db.SaveChangesAsync();

var disqVoteRes = await voteHandler.Handle(new CreateVoteCommand(mySubmission.Id, otherVoterId), CancellationToken.None);
Assert(!disqVoteRes.Success, "Voting on disqualified submission must be rejected.");
Assert(disqVoteRes.Errors.Any(e => e.Contains("truất quyền thi đấu")),
    "Rejection error must state submission is disqualified.");

// =========================================================================
// SECTION 8: Submission Dispute Rules (Category Priority, Re-dispute, Multi-user)
// =========================================================================
Console.WriteLine("\n--- Section 8: Submission Dispute Rules Tests ---");

var disputeTargetArt = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = Guid.NewGuid(),
    Title = "Dispute Subject Art",
    ImageUrl = "https://example.com/art-v1.png",
    ModerationStatus = "Approved"
};
var activeSub = new EventSubmission
{
    Id = Guid.NewGuid(),
    EventId = votingEvent.Id,
    SubmitterId = submitterId,
    ArtworkId = disputeTargetArt.Id,
    Artwork = disputeTargetArt,
    Title = "Dispute Subject Entry",
    IsDisqualified = false
};
var client1Id = Guid.NewGuid();
var client2Id = Guid.NewGuid();
db.Users.AddRange(
    new ApplicationUser { Id = client1Id, UserName = "client1", FullName = "Client One" },
    new ApplicationUser { Id = client2Id, UserName = "client2", FullName = "Client Two" }
);
db.Artworks.Add(disputeTargetArt);
db.EventSubmissions.Add(activeSub);
await db.SaveChangesAsync();

var createDisputeHandler = new CreateSubmissionDisputeCommandHandler(db);

// 8.1 Server-side priority derivation prevents user from spoofing High priority
var lowCatSpoofHigh = await createDisputeHandler.Handle(new CreateSubmissionDisputeCommand(
    SubmissionId: activeSub.Id,
    RaisedById: client1Id,
    Reason: "Minor caption typo",
    Priority: "High",
    Category: "Other"
), CancellationToken.None);
Assert(lowCatSpoofHigh.Success && lowCatSpoofHigh.DisputeId.HasValue,
    "Valid Other category dispute must succeed.");

var savedLowDispute = await db.SubmissionDisputes.FindAsync(lowCatSpoofHigh.DisputeId!.Value);
Assert(savedLowDispute != null && savedLowDispute.Category == "Other" && savedLowDispute.Priority == "Low",
    "Client-supplied High priority on Other category must be overridden server-side to Low.");

// 8.2 Duplicate pending dispute with SAME category is blocked, DIFFERENT category is allowed
var duplicateSameCat = await createDisputeHandler.Handle(new CreateSubmissionDisputeCommand(
    SubmissionId: activeSub.Id,
    RaisedById: client1Id,
    Reason: "Minor caption typo",
    Category: "Other"
), CancellationToken.None);
Assert(!duplicateSameCat.Success,
    "Duplicate pending dispute in the same category/reason by same user must be rejected.");

var secondDiffCat = await createDisputeHandler.Handle(new CreateSubmissionDisputeCommand(
    SubmissionId: activeSub.Id,
    RaisedById: client1Id,
    Reason: "Looks AI-generated",
    Priority: "Low",
    Category: "AiArt"
), CancellationToken.None);
Assert(secondDiffCat.Success && secondDiffCat.DisputeId.HasValue,
    "Same user must be allowed to raise a 2nd dispute on the same submission for a different category.");
var savedAiDispute = await db.SubmissionDisputes.FindAsync(secondDiffCat.DisputeId!.Value);
Assert(savedAiDispute != null && savedAiDispute.Priority == "High" && savedAiDispute.Category == "AiArt",
    "AiArt dispute must be assigned High priority server-side.");

// 8.3 Another account can also dispute the same submission, and Admin query returns all disputes
var otherAccountDispute = await createDisputeHandler.Handle(new CreateSubmissionDisputeCommand(
    SubmissionId: activeSub.Id,
    RaisedById: client2Id,
    Reason: "Traced from existing illustration",
    Category: "Copyright"
), CancellationToken.None);
Assert(otherAccountDispute.Success && otherAccountDispute.DisputeId.HasValue,
    "Second account must be able to dispute the same submission.");

// 8.4 After dispute is resolved (Dismiss), client1 can re-dispute the same category & query returns all
savedAiDispute!.Status = "Rejected";
savedAiDispute.Resolution = "Dismiss";
await db.SaveChangesAsync();

var reDisputeAfterResolve = await createDisputeHandler.Handle(new CreateSubmissionDisputeCommand(
    SubmissionId: activeSub.Id,
    RaisedById: client1Id,
    Reason: "New evidence of AI generation",
    Category: "AiArt"
), CancellationToken.None);
Assert(reDisputeAfterResolve.Success && reDisputeAfterResolve.DisputeId.HasValue,
    "Client must be able to raise a new dispute on same category after prior dispute is dismissed.");

var getDisputesHandler = new GetSubmissionDisputesQueryHandler(db);
var (allSubDisputes, allSubDisputeCount) = await getDisputesHandler.Handle(
    new GetSubmissionDisputesQuery(SubmissionId: activeSub.Id, PageNumber: 1, PageSize: 20),
    CancellationToken.None);
Assert(allSubDisputeCount == 4 && allSubDisputes.Count == 4,
    $"Admin query must return all 4 disputes across users and post-resolution history (got {allSubDisputeCount}).");

// 8.5 Disqualified submission rejects new disputes
activeSub.IsDisqualified = true;
await db.SaveChangesAsync();
var disqDispute = await createDisputeHandler.Handle(new CreateSubmissionDisputeCommand(
    SubmissionId: activeSub.Id,
    RaisedById: client2Id,
    Reason: "Another issue",
    Category: "VoteFraud"
), CancellationToken.None);
Assert(!disqDispute.Success,
    "Creating dispute on an already disqualified submission must be rejected.");

// =========================================================================
// SECTION 9: Admin Signals, Jury Email, Invitation Errors, Candidates & Re-Accept
// =========================================================================
Console.WriteLine("\n--- Section 9: Admin Jury & Dispute Signal Tests ---");

var stubNotif = new RecordingNotificationPublisher();

var roleAdminId = Guid.NewGuid();
var roleCreatorId = Guid.NewGuid();
var roleClientId = Guid.NewGuid();
db.Roles.AddRange(
    new IdentityRole<Guid> { Id = roleAdminId, Name = UserRoleNames.Administrator, NormalizedName = UserRoleNames.Administrator.ToUpperInvariant() },
    new IdentityRole<Guid> { Id = roleCreatorId, Name = UserRoleNames.Creator, NormalizedName = UserRoleNames.Creator.ToUpperInvariant() },
    new IdentityRole<Guid> { Id = roleClientId, Name = UserRoleNames.Client, NormalizedName = UserRoleNames.Client.ToUpperInvariant() }
);

var section9AdminId = Guid.NewGuid();
var testAdminUser = new ApplicationUser { Id = section9AdminId, UserName = "admin9", FullName = "Platform Admin 9", Email = "admin9@dillustration.vn" };
var testCreatorUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = "jurycreator", FullName = "Jury Creator Real", Email = "real.creator@dillustration.vn" };
var testClientOnlyUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = "clientonly", FullName = "Client Only", Email = "client.only@dillustration.vn" };
db.Users.AddRange(testAdminUser, testCreatorUser, testClientOnlyUser);

db.UserRoles.AddRange(
    new IdentityUserRole<Guid> { UserId = testAdminUser.Id, RoleId = roleAdminId },
    new IdentityUserRole<Guid> { UserId = testCreatorUser.Id, RoleId = roleCreatorId },
    new IdentityUserRole<Guid> { UserId = testClientOnlyUser.Id, RoleId = roleClientId }
);

var testCreatorProfile = new CreatorProfile
{
    Id = Guid.NewGuid(),
    UserId = testCreatorUser.Id,
    DisplayName = "Creator Real Display",
    CreatedAt = now
};
db.CreatorProfiles.Add(testCreatorProfile);

// Create a fresh active submission for notification check
var notifSub = new EventSubmission
{
    Id = Guid.NewGuid(),
    EventId = votingEvent.Id,
    SubmitterId = submitterId,
    ArtworkId = disputeTargetArt.Id,
    Artwork = disputeTargetArt,
    Title = "Notif Subject Entry",
    IsDisqualified = false
};
db.EventSubmissions.Add(notifSub);

// Create an Open event whose JudgingStartAt is in the future so Jury invites are allowed
var inviteEvent = new PlatformEvent
{
    Id = Guid.NewGuid(),
    Title = "Jury Invite Contest",
    Description = "Desc",
    Status = EventStatus.Open,
    CreatedByAdminId = adminId,
    SubmissionStartAt = now.AddDays(-1),
    SubmissionEndAt = now.AddDays(5),
    JudgingStartAt = now.AddDays(6),
    JudgingEndAt = now.AddDays(10),
    VotingStartAt = now.AddDays(11),
    VotingEndAt = now.AddDays(15),
    ResultAnnouncementAt = now.AddDays(16),
    CreatedAt = now.AddDays(-2)
};
db.PlatformEvents.Add(inviteEvent);
await db.SaveChangesAsync();

// 9.1 Admin receives notification when client files submission dispute
var notifDisputeHandler = new CreateSubmissionDisputeCommandHandler(db, stubNotif);
var notifDisputeRes = await notifDisputeHandler.Handle(new CreateSubmissionDisputeCommand(
    SubmissionId: notifSub.Id,
    RaisedById: client1Id,
    Reason: "Suspected AI image generation",
    Category: "AiArt"
), CancellationToken.None);
Assert(notifDisputeRes.Success && notifDisputeRes.DisputeId.HasValue,
    "Dispute creation with notification publisher must succeed.");
Assert(stubNotif.Published.Any(n =>
        n.UserId == section9AdminId &&
        n.NotificationType == NotificationType.SubmissionDisputeCreated &&
        n.RefType == "SubmissionDispute" &&
        n.RefId == notifDisputeRes.DisputeId),
    "Active Administrator must receive SubmissionDisputeCreated notification when dispute is raised.");

// 9.2 Sending invitation to Client returns explicit reason (not generic 400)
var sendInviteHandler = new SendInvitationCommandHandler(db, stubNotif, NullLogger<SendInvitationCommandHandler>.Instance);
var clientInviteRes = await sendInviteHandler.Handle(new SendInvitationCommand(
    EventId: inviteEvent.Id,
    AdminId: section9AdminId,
    Email: "client.only@dillustration.vn",
    IsHeadJury: false
), CancellationToken.None);
Assert(!clientInviteRes.Success && clientInviteRes.Errors.Any(e => e.Contains("Creator", StringComparison.OrdinalIgnoreCase)),
    "Inviting a Client account must return a descriptive Creator-requirement error.");

// 9.3 Candidate picker lists both eligible Creator and ineligible Client with clear reason
var candidatesHandler = new GetInvitationCandidatesQueryHandler(db);
var (allCandidates, allCandidateCount) = await candidatesHandler.Handle(
    new GetInvitationCandidatesQuery(inviteEvent.Id, Search: null, RoleFilter: "All", Page: 1, PageSize: 20),
    CancellationToken.None);
var creatorCand = allCandidates.FirstOrDefault(c => c.UserId == testCreatorUser.Id);
var clientCand = allCandidates.FirstOrDefault(c => c.UserId == testClientOnlyUser.Id);
Assert(creatorCand != null && creatorCand.CanInvite && creatorCand.Email == "real.creator@dillustration.vn",
    "Invitation candidates query must list eligible Creator with real email.");
Assert(clientCand != null && !clientCand.CanInvite && !string.IsNullOrWhiteSpace(clientCand.IneligibleReason),
    "Invitation candidates query must mark Client account ineligible with explicit reason.");

// 9.4 Invite Creator -> Accept -> GetJuriesQuery returns real CreatorEmail -> Duplicate invite fails cleanly
var firstInviteRes = await sendInviteHandler.Handle(new SendInvitationCommand(
    EventId: inviteEvent.Id,
    AdminId: adminId,
    Email: "real.creator@dillustration.vn",
    IsHeadJury: true
), CancellationToken.None);
Assert(firstInviteRes.Success && firstInviteRes.Data != null,
    "Sending invitation to valid Creator must succeed.");

var updateInviteHandler = new UpdateInvitationStatusCommandHandler(db, stubNotif, NullLogger<UpdateInvitationStatusCommandHandler>.Instance);
var firstAcceptRes = await updateInviteHandler.Handle(new UpdateInvitationStatusCommand(
    InvitationId: firstInviteRes.Data!.Id,
    CurrentUserId: testCreatorUser.Id,
    Status: InvitationStatus.Accepted
), CancellationToken.None);
Assert(firstAcceptRes.Success, "Creator accepting first jury invitation must succeed.");

var getJuriesHandler = new GetJuriesQueryHandler(db);
var (eventJuries, _) = await getJuriesHandler.Handle(new GetJuriesQuery(EventId: inviteEvent.Id), CancellationToken.None);
var acceptedJury = eventJuries.FirstOrDefault(j => j.CreatorId == testCreatorProfile.Id);
Assert(acceptedJury != null && acceptedJury.CreatorEmail == "real.creator@dillustration.vn",
    "GetJuriesQuery must return Creator's real account email after accepting invitation.");

var dupJuryInviteRes = await sendInviteHandler.Handle(new SendInvitationCommand(
    EventId: inviteEvent.Id,
    AdminId: adminId,
    Email: "real.creator@dillustration.vn",
    IsHeadJury: false
), CancellationToken.None);
Assert(!dupJuryInviteRes.Success && dupJuryInviteRes.Errors.Any(e => e.Contains("ban giám khảo", StringComparison.OrdinalIgnoreCase)),
    "Re-inviting an already active Jury member must return explicit duplicate-jury error.");

// 9.5 Remove Jury -> Send 2nd invitation -> Creator accepts 2nd invitation succeeds cleanly
var removeJuryHandler = new RemoveJuryCommandHandler(db);
var removeRes = await removeJuryHandler.Handle(RemoveJuryCommand.ByJuryId(acceptedJury!.Id, adminId), CancellationToken.None);
Assert(removeRes.Success, "Removing active Jury member must succeed.");

var removedRow = await db.Juries.FirstOrDefaultAsync(j => j.Id == acceptedJury.Id);
Assert(removedRow != null && removedRow.IsDeleted && !removedRow.IsHeadJury,
    "RemoveJuryCommand must soft-delete row and clear IsHeadJury flag.");

var secondInviteRes = await sendInviteHandler.Handle(new SendInvitationCommand(
    EventId: inviteEvent.Id,
    AdminId: adminId,
    Email: "real.creator@dillustration.vn",
    IsHeadJury: true
), CancellationToken.None);
Assert(secondInviteRes.Success && secondInviteRes.Data != null,
    "Sending 2nd invitation to previously removed Jury Creator must succeed.");

var secondAcceptRes = await updateInviteHandler.Handle(new UpdateInvitationStatusCommand(
    InvitationId: secondInviteRes.Data!.Id,
    CurrentUserId: testCreatorUser.Id,
    Status: InvitationStatus.Accepted
), CancellationToken.None);
Assert(secondAcceptRes.Success,
    "Creator accepting 2nd jury invitation after previous removal must succeed cleanly.");

var (restoredJuries, _) = await getJuriesHandler.Handle(new GetJuriesQuery(EventId: inviteEvent.Id), CancellationToken.None);
Assert(restoredJuries.Count == 1 && restoredJuries[0].CreatorEmail == "real.creator@dillustration.vn" && restoredJuries[0].IsHeadJury,
    "Removed Jury member must be restored as active Jury with real email after accepting 2nd invitation.");


// =========================================================================
// SECTION 10: Creator Regression Checks (Event Artwork Visibility & Dispute History)
// =========================================================================
Console.WriteLine("\n--- Section 10: Creator Event Artwork Visibility & Dispute History Tests ---");

var creatorUser10Id = Guid.NewGuid();
var clientUser10Id = Guid.NewGuid();
var creatorProfile10Id = Guid.NewGuid();
var creatorEvent = new PlatformEvent
{
    Id = Guid.NewGuid(),
    Title = "Creator Regression Event",
    Description = "Open submission window",
    Status = EventStatus.Open,
    CreatedByAdminId = adminId,
    SubmissionStartAt = now.AddDays(-2),
    SubmissionEndAt = now.AddDays(5),
    JudgingStartAt = now.AddDays(6),
    JudgingEndAt = now.AddDays(10),
    VotingStartAt = now.AddDays(11),
    VotingEndAt = now.AddDays(15),
    ResultAnnouncementAt = now.AddDays(16),
    CreatedAt = now,
    IsDeleted = false
};

db.Users.AddRange(
    new ApplicationUser
    {
        Id = creatorUser10Id,
        UserName = "creator10",
        NormalizedUserName = "CREATOR10",
        Email = "creator10@dillustration.vn",
        NormalizedEmail = "CREATOR10@DILLUSTRATION.VN",
        FullName = "Creator Ten"
    },
    new ApplicationUser
    {
        Id = clientUser10Id,
        UserName = "client10",
        NormalizedUserName = "CLIENT10",
        Email = "client10@dillustration.vn",
        NormalizedEmail = "CLIENT10@DILLUSTRATION.VN",
        FullName = "Client Ten"
    });
db.CreatorProfiles.Add(new CreatorProfile
{
    Id = creatorProfile10Id,
    UserId = creatorUser10Id,
    DisplayName = "Creator Ten Studio",
    IsDeleted = false
});
db.PlatformEvents.Add(creatorEvent);

// Artwork uploaded straight from creator computer during submission stays Pending moderation
var pendingArtOld = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = creatorProfile10Id,
    Title = "Uploaded Submission V1",
    Description = "Uploaded from computer",
    ImageUrl = "https://example.com/submission-v1.png",
    ThumbnailUrl = "https://example.com/submission-v1-thumb.png",
    ModerationStatus = "Pending",
    CreatedAt = now,
    IsDeleted = false
};
var pendingArtNew = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = creatorProfile10Id,
    Title = "Uploaded Submission V2",
    Description = "Swapped artwork",
    ImageUrl = "https://example.com/submission-v2.png",
    ThumbnailUrl = "https://example.com/submission-v2-thumb.png",
    ModerationStatus = "Approved",
    CreatedAt = now,
    IsDeleted = false
};
db.Artworks.AddRange(pendingArtOld, pendingArtNew);

var creatorSubmission = new EventSubmission
{
    Id = Guid.NewGuid(),
    EventId = creatorEvent.Id,
    SubmitterId = creatorUser10Id,
    ArtworkId = pendingArtOld.Id,
    Title = "Submission V1 Title",
    Description = "Initial submission",
    VoteCount = 0,
    IsDisqualified = false,
    SubmittedAt = now
};
db.EventSubmissions.Add(creatorSubmission);
await db.SaveChangesAsync();

// 10.1 (Bug I.2) Client & Creator can view detail and interact (like) with Pending event submission artwork without 'artwork not found'
var marketplaceQueryHandler = new MarketplaceQueryHandler(db);
var interactionHandler = new ArtworkInteractionHandler(db);

var clientDetail = await marketplaceQueryHandler.Handle(new GetArtworkDetailQuery(pendingArtOld.Id, clientUser10Id), CancellationToken.None);
Assert(clientDetail is not null && clientDetail.Artwork.Id == pendingArtOld.Id,
    "Client must see GetArtworkDetailQuery result for Pending event submission artwork (no 'artwork not found').");

var creatorDetail = await marketplaceQueryHandler.Handle(new GetArtworkDetailQuery(pendingArtOld.Id, creatorUser10Id), CancellationToken.None);
Assert(creatorDetail is not null && creatorDetail.Artwork.Id == pendingArtOld.Id,
    "Creator must see GetArtworkDetailQuery result for their own Pending event submission artwork.");

var initialInteractions = await interactionHandler.Handle(new GetArtworkInteractionsQuery(pendingArtOld.Id, clientUser10Id), CancellationToken.None);
Assert(initialInteractions is not null && !initialInteractions.IsLiked && initialInteractions.LikeCount == 0,
    "GetArtworkInteractionsQuery must succeed for Pending event submission artwork.");

var likedState = await interactionHandler.Handle(new SetArtworkLikeCommand(pendingArtOld.Id, clientUser10Id, true), CancellationToken.None);
Assert(likedState is not null && likedState.IsLiked && likedState.LikeCount == 1,
    "Client must be able to favorite/like a Pending event submission artwork.");

// 10.2 (Bug I.3) Updating submission preserves old SubmissionDispute history with snapshots of old artwork title/image
var dispute10Res = await createDisputeHandler.Handle(
    new CreateSubmissionDisputeCommand(
        SubmissionId: creatorSubmission.Id,
        RaisedById: clientUser10Id,
        Reason: "Reported on V1 before artwork swap.",
        Category: "Copyright"),
    CancellationToken.None);
Assert(dispute10Res.Success && dispute10Res.DisputeId.HasValue,
    $"Client should be able to raise dispute before submission artwork update (errors={string.Join("; ", dispute10Res.Errors)}).");

var updateSubmissionHandler = new UpdateSubmissionCommandHandler(db, null!);
var (updatedSubOk, updatedSubDto, updateSubErrors) = await updateSubmissionHandler.Handle(
    new UpdateSubmissionCommand(
        SubmissionId: creatorSubmission.Id,
        UserId: creatorUser10Id,
        Title: "Submission V2 Title",
        Description: "Updated submission with new artwork",
        ArtworkId: pendingArtNew.Id),
    CancellationToken.None);
Assert(updatedSubOk && updatedSubDto is not null && updatedSubDto.ArtworkId == pendingArtNew.Id,
    $"UpdateSubmissionCommand must swap to new ArtworkId cleanly (errors={string.Join("; ", updateSubErrors)}).");

var reloadedOldArt = await db.Artworks.IgnoreQueryFilters().FirstAsync(a => a.Id == pendingArtOld.Id);
Assert(!reloadedOldArt.IsDeleted,
    "Old V1 artwork that already received client likes must remain preserved (not soft-deleted) after submission swap.");

var (disputesAfterUpdate, totalDisputesAfterUpdate) = await getDisputesHandler.Handle(
    new GetSubmissionDisputesQuery(SubmissionId: creatorSubmission.Id),
    CancellationToken.None);
Assert(totalDisputesAfterUpdate == 1 && disputesAfterUpdate.Count == 1,
    "Admin moderation queue must still retain old SubmissionDispute after creator updates/swaps submission artwork.");
Assert(disputesAfterUpdate[0].SubmissionTitle == "Submission V1 Title"
    && disputesAfterUpdate[0].ArtworkImageUrl == "https://example.com/submission-v1.png",
    $"Retained dispute must snapshot old submission title & artwork image (got '{disputesAfterUpdate[0].SubmissionTitle}', '{disputesAfterUpdate[0].ArtworkImageUrl}').");


Console.WriteLine("\n=================================================");
Console.WriteLine($"RESULTS: {passed} PASSED, {failed} FAILED.");
Console.WriteLine("ALL EVENT DTO & ENTITY VALIDATION TESTS PASSED!");
Console.WriteLine("=================================================");

sealed record PublishedNotif(Guid UserId, NotificationType NotificationType, string Title, string Body, string? RefType, Guid? RefId);

sealed class RecordingNotificationPublisher : INotificationPublisher
{
    public List<PublishedNotif> Published { get; } = new();

    public Task<NotificationDto?> PublishAsync(
        Guid userId,
        NotificationType notificationType,
        string title,
        string body,
        string? refType = null,
        Guid? refId = null,
        NotificationChannel channel = NotificationChannel.InApp,
        string? dedupKey = null,
        CancellationToken cancellationToken = default)
    {
        Published.Add(new PublishedNotif(userId, notificationType, title, body, refType, refId));
        return Task.FromResult<NotificationDto?>(new NotificationDto(
            Guid.NewGuid(),
            notificationType.ToString(),
            title,
            body,
            refType,
            refId,
            channel.ToString(),
            false,
            null,
            DateTimeOffset.UtcNow));
    }
}
