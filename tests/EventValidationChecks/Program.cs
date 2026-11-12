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

// =========================================================================
// SECTION 12: Jury Dispute Isolation, Auto-Raised Score Deviation, Phase Lock & Per-Jury Graded State
// =========================================================================
Console.WriteLine("\n--- Section 12: Jury Dispute Isolation & Auto-Raised Scoring Dispute Checks ---");
{
    var s12AdminId = Guid.NewGuid();
    var s12Jury1UserId = Guid.NewGuid();
    var s12Jury2UserId = Guid.NewGuid();
    var s12SubUserId = Guid.NewGuid();
    var s12EventId = Guid.NewGuid();

    db.Users.AddRange(
        new ApplicationUser { Id = s12Jury1UserId, UserName = "s12jury1", Email = "s12jury1@test.invalid", FullName = "Jury One" },
        new ApplicationUser { Id = s12Jury2UserId, UserName = "s12jury2", Email = "s12jury2@test.invalid", FullName = "Jury Two" },
        new ApplicationUser { Id = s12SubUserId, UserName = "s12sub", Email = "s12sub@test.invalid", FullName = "Submitter" }
    );

    var s12Jury1Creator = new CreatorProfile { Id = Guid.NewGuid(), UserId = s12Jury1UserId, DisplayName = "Head Jury Studio", IsDeleted = false };
    var s12Jury2Creator = new CreatorProfile { Id = Guid.NewGuid(), UserId = s12Jury2UserId, DisplayName = "Panel Jury Studio", IsDeleted = false };
    var s12SubCreator = new CreatorProfile { Id = Guid.NewGuid(), UserId = s12SubUserId, DisplayName = "Candidate Studio", IsDeleted = false };
    db.CreatorProfiles.AddRange(s12Jury1Creator, s12Jury2Creator, s12SubCreator);

    var s12Event = new PlatformEvent
    {
        Id = s12EventId,
        Title = "Section 12 Jury Isolation Contest",
        Description = "Verify dispute isolation and scoring deviation",
        Status = EventStatus.Open,
        CreatedByAdminId = s12AdminId,
        SubmissionStartAt = now.AddMinutes(-30),
        SubmissionEndAt = now.AddMinutes(30),
        JudgingStartAt = now.AddMinutes(40),
        JudgingEndAt = now.AddMinutes(120),
        VotingStartAt = now.AddMinutes(130),
        VotingEndAt = now.AddMinutes(180),
        ResultAnnouncementAt = now.AddMinutes(200),
        CreatedAt = now,
        IsDeleted = false
    };
    db.PlatformEvents.Add(s12Event);

    var s12Crit1 = new EventCriteria { Id = Guid.NewGuid(), EventId = s12EventId, Name = "Creativity", MaxScore = 10m, Weight = 1m, DisplayOrder = 1, CreatedAt = now };
    var s12Crit2 = new EventCriteria { Id = Guid.NewGuid(), EventId = s12EventId, Name = "Color", MaxScore = 10m, Weight = 1m, DisplayOrder = 2, CreatedAt = now };
    db.EventCriteria.AddRange(s12Crit1, s12Crit2);

    db.Juries.AddRange(
        new Jury { Id = Guid.NewGuid(), EventId = s12EventId, CreatorId = s12Jury1Creator.Id, IsHeadJury = true, CreatedAt = now, IsDeleted = false },
        new Jury { Id = Guid.NewGuid(), EventId = s12EventId, CreatorId = s12Jury2Creator.Id, IsHeadJury = false, CreatedAt = now, IsDeleted = false }
    );

    var s12Art = new Artwork
    {
        Id = Guid.NewGuid(),
        CreatorProfileId = s12SubCreator.Id,
        Title = "Horizon Entry",
        ImageUrl = "https://example.com/horizon.png",
        ThumbnailUrl = "https://example.com/horizon-thumb.png",
        ModerationStatus = "Approved",
        IsDeleted = false
    };
    db.Artworks.Add(s12Art);

    var s12Sub = new EventSubmission
    {
        Id = Guid.NewGuid(),
        EventId = s12EventId,
        SubmitterId = s12SubUserId,
        ArtworkId = s12Art.Id,
        Title = "Horizon",
        IsDisqualified = false,
        SubmittedAt = now
    };
    db.EventSubmissions.Add(s12Sub);
    await db.SaveChangesAsync();

    // Moderation dispute raised during Submission phase
    var aiModDispute = await createDisputeHandler.Handle(
        new CreateSubmissionDisputeCommand(s12Sub.Id, s12SubUserId, "AI generated artwork?", Category: "AiArt"),
        CancellationToken.None);
    Assert(aiModDispute.Success, "Creating moderation AiArt dispute must succeed.");

    var headWorkspaceHandler = new GetHeadJuryWorkspaceQueryHandler(db);
    var wsBeforeJudging = await headWorkspaceHandler.Handle(new GetHeadJuryWorkspaceQuery(s12EventId), CancellationToken.None);
    Assert(wsBeforeJudging != null && wsBeforeJudging.ArbitrationCases.Count == 0 && wsBeforeJudging.DisputesResolved,
        $"Moderation AiArt dispute must NOT leak to Head Jury workspace (arbCount={wsBeforeJudging?.ArbitrationCases.Count}, resolved={wsBeforeJudging?.DisputesResolved}).");

    var (juryQueryItems, juryQueryTotal) = await getDisputesHandler.Handle(
        new GetSubmissionDisputesQuery(EventId: s12EventId, Category: "ScoreAppeal"),
        CancellationToken.None);
    Assert(juryQueryTotal == 0 && juryQueryItems.Count == 0,
        "Jury ScoreAppeal dispute query must exclude moderation AiArt disputes.");

    // Attempting to score before JudgingStartAt must fail cleanly even if Status were Judging
    s12Event.Status = EventStatus.Judging;
    await db.SaveChangesAsync();
    var scoreHandler = new SubmitJuryScoreCommandHandler(db);
    var earlyScoreRes = await scoreHandler.Handle(
        new SubmitJuryScoreCommand(s12Sub.Id, s12Jury1UserId, new SubmitScoreDto
        {
            SubmissionId = s12Sub.Id,
            TotalScore = 18m,
            CriteriaScores = [new CriteriaScoreItemDto { CriteriaId = s12Crit1.Id, Score = 9m }, new CriteriaScoreItemDto { CriteriaId = s12Crit2.Id, Score = 9m }]
        }),
        CancellationToken.None);
    Assert(!earlyScoreRes.Success && earlyScoreRes.Errors.Any(e => e.Contains("chưa bắt đầu")),
        "SubmitJuryScoreCommand must reject scoring before JudgingStartAt even if EventStatus is Judging.");

    // Enter active Judging window
    s12Event.JudgingStartAt = now.AddMinutes(-5);
    await db.SaveChangesAsync();

    var j1ScoreRes = await scoreHandler.Handle(
        new SubmitJuryScoreCommand(s12Sub.Id, s12Jury1UserId, new SubmitScoreDto
        {
            SubmissionId = s12Sub.Id,
            TotalScore = 18m,
            CriteriaScores = [new CriteriaScoreItemDto { CriteriaId = s12Crit1.Id, Score = 9m }, new CriteriaScoreItemDto { CriteriaId = s12Crit2.Id, Score = 9m }]
        }),
        CancellationToken.None);
    Assert(j1ScoreRes.Success && j1ScoreRes.Data != null && j1ScoreRes.Data.Score == 18m,
        $"Head Jury scoring inside Judging window must succeed (errors={string.Join("; ", j1ScoreRes.Errors)}).");

    var getSubsHandler = new GetSubmissionsByEventIdQueryHandler(db);
    var (j2VisibleSubs, _) = await getSubsHandler.Handle(
        new GetSubmissionsByEventIdQuery(s12EventId, CurrentUserId: s12Jury2UserId),
        CancellationToken.None);
    Assert(j2VisibleSubs.Count == 1 && j2VisibleSubs[0].CurrentJuryHasGraded == false,
        "Second Jury member must see CurrentJuryHasGraded == false even after Head Jury scored the submission.");

    var (j1VisibleSubs, _) = await getSubsHandler.Handle(
        new GetSubmissionsByEventIdQuery(s12EventId, CurrentUserId: s12Jury1UserId),
        CancellationToken.None);
    Assert(j1VisibleSubs.Count == 1 && j1VisibleSubs[0].CurrentJuryHasGraded == true && j1VisibleSubs[0].CurrentJuryScore == 18m,
        "Head Jury must see their own CurrentJuryHasGraded == true and CurrentJuryScore == 18.");

    // Jury 2 submits divergent score (8/20 vs 18/20 => > 25% deviation) -> auto ScoreAppeal dispute raised
    var j2ScoreRes = await scoreHandler.Handle(
        new SubmitJuryScoreCommand(s12Sub.Id, s12Jury2UserId, new SubmitScoreDto
        {
            SubmissionId = s12Sub.Id,
            TotalScore = 8m,
            CriteriaScores = [new CriteriaScoreItemDto { CriteriaId = s12Crit1.Id, Score = 4m }, new CriteriaScoreItemDto { CriteriaId = s12Crit2.Id, Score = 4m }]
        }),
        CancellationToken.None);
    Assert(j2ScoreRes.Success && j2ScoreRes.Data?.TotalAverageScore == 13m,
        $"Second Jury divergent scoring must succeed and average to 13 (errors={string.Join("; ", j2ScoreRes.Errors)}).");

    var wsAfterDeviation = await headWorkspaceHandler.Handle(new GetHeadJuryWorkspaceQuery(s12EventId), CancellationToken.None);
    Assert(wsAfterDeviation != null && wsAfterDeviation.ArbitrationCases.Count == 1
        && wsAfterDeviation.ArbitrationCases[0].Category == "ScoreAppeal"
        && wsAfterDeviation.ArbitrationCases[0].DeviationPercent > 25m
        && !wsAfterDeviation.DisputesResolved,
        $"Head Jury workspace must automatically surface the >25% scoring dispute without moderation AiArt dispute (count={wsAfterDeviation?.ArbitrationCases.Count}, dev={wsAfterDeviation?.ArbitrationCases.FirstOrDefault()?.DeviationPercent}).");
}

Console.WriteLine("\n--- Section 13: Draft Event Creator-Only Visibility & Access Checks ---");
{
    var draftCreatorId = Guid.NewGuid();
    var otherAdminId = Guid.NewGuid();
    var draftNow = DateTimeOffset.UtcNow;

    db.Users.AddRange(
        new ApplicationUser { Id = draftCreatorId, UserName = "draftcreator13", Email = "draftcreator13@test.invalid", FullName = "Draft Creator" },
        new ApplicationUser { Id = otherAdminId, UserName = "otheradmin13", Email = "otheradmin13@test.invalid", FullName = "Other Admin" }
    );

    var s13DraftEvent = new PlatformEvent
    {
        Id = Guid.NewGuid(),
        Title = "Creator Secret Draft",
        Description = "Private draft description",
        Status = EventStatus.Draft,
        CreatedByAdminId = draftCreatorId,
        Prize = "10000000",
        SubmissionStartAt = draftNow.AddMinutes(3),
        SubmissionEndAt = draftNow.AddMinutes(6),
        JudgingStartAt = draftNow.AddMinutes(7),
        JudgingEndAt = draftNow.AddMinutes(12),
        VotingStartAt = draftNow.AddMinutes(13),
        VotingEndAt = draftNow.AddMinutes(16),
        ResultAnnouncementAt = draftNow.AddMinutes(17),
        CreatedAt = draftNow
    };
    db.PlatformEvents.Add(s13DraftEvent);
    await db.SaveChangesAsync();

    var getEventsHandler = new GetEventsQueryHandler(db);
    var (anonList, _) = await getEventsHandler.Handle(new GetEventsQuery(PageSize: 50, CurrentUserId: null, IncludeDrafts: true), CancellationToken.None);
    Assert(!anonList.Any(e => e.Id == s13DraftEvent.Id),
        "Anonymous users must not see Draft events in GetEvents even with IncludeDrafts=true.");

    var (otherList, _) = await getEventsHandler.Handle(new GetEventsQuery(PageSize: 50, CurrentUserId: otherAdminId, IncludeDrafts: true), CancellationToken.None);
    Assert(!otherList.Any(e => e.Id == s13DraftEvent.Id),
        "Other admins/users must not see another creator's Draft event in GetEvents even with IncludeDrafts=true.");

    var (publicCreatorList, _) = await getEventsHandler.Handle(new GetEventsQuery(PageSize: 50, CurrentUserId: draftCreatorId, IncludeDrafts: false), CancellationToken.None);
    Assert(!publicCreatorList.Any(e => e.Id == s13DraftEvent.Id),
        "Public GetEvents (IncludeDrafts=false) must NOT show Draft events even to their creator.");

    var (creatorList, _) = await getEventsHandler.Handle(new GetEventsQuery(PageSize: 50, CurrentUserId: draftCreatorId, IncludeDrafts: true), CancellationToken.None);
    Assert(creatorList.Any(e => e.Id == s13DraftEvent.Id),
        "Admin GetEvents (IncludeDrafts=true) must show Draft event to its creator.");

    var getByIdHandler = new GetEventByIdQueryHandler(db);
    Assert(await getByIdHandler.Handle(new GetEventByIdQuery(s13DraftEvent.Id, CurrentUserId: null), CancellationToken.None) == null,
        "Anonymous GetEventById must return null for Draft event.");
    Assert(await getByIdHandler.Handle(new GetEventByIdQuery(s13DraftEvent.Id, CurrentUserId: otherAdminId), CancellationToken.None) == null,
        "Non-creator GetEventById must return null for Draft event.");
    Assert((await getByIdHandler.Handle(new GetEventByIdQuery(s13DraftEvent.Id, CurrentUserId: draftCreatorId), CancellationToken.None))?.Id == s13DraftEvent.Id,
        "Creator GetEventById must return the Draft event.");

    var statsHandler = new GetEventStatsQueryHandler(db);
    var otherStats = await statsHandler.Handle(new GetEventStatsQuery(CurrentUserId: otherAdminId, IncludeDrafts: true), CancellationToken.None);
    var creatorStats = await statsHandler.Handle(new GetEventStatsQuery(CurrentUserId: draftCreatorId, IncludeDrafts: true), CancellationToken.None);
    Assert(creatorStats.TotalEventsCount == otherStats.TotalEventsCount + 1,
        $"GetEventStats(IncludeDrafts=true) must count the Draft event only for its creator (creator={creatorStats.TotalEventsCount}, other={otherStats.TotalEventsCount}).");

    var leaderboardHandler = new GetEventLeaderboardQueryHandler(db);
    var (_, _, otherLbTitle) = await leaderboardHandler.Handle(new GetEventLeaderboardQuery(s13DraftEvent.Id, CurrentUserId: otherAdminId), CancellationToken.None);
    var (_, _, creatorLbTitle) = await leaderboardHandler.Handle(new GetEventLeaderboardQuery(s13DraftEvent.Id, CurrentUserId: draftCreatorId), CancellationToken.None);
    Assert(otherLbTitle == null && creatorLbTitle == s13DraftEvent.Title,
        "GetEventLeaderboard must hide Draft event from non-creators and allow its creator.");

    var updateHandler = new UpdateEventCommandHandler(db);
    var otherUpdateRes = await updateHandler.Handle(new UpdateEventCommand(
        EventId: s13DraftEvent.Id,
        Title: "Hijacked Draft",
        Description: "Updated",
        BannerUrl: null,
        Rules: null,
        Prize: null,
        MaxVote: 3,
        Status: EventStatus.Open,
        SubmissionStartAt: s13DraftEvent.SubmissionStartAt,
        SubmissionEndAt: s13DraftEvent.SubmissionEndAt,
        JudgingStartAt: s13DraftEvent.JudgingStartAt,
        JudgingEndAt: s13DraftEvent.JudgingEndAt,
        VotingStartAt: s13DraftEvent.VotingStartAt,
        VotingEndAt: s13DraftEvent.VotingEndAt,
        ResultAnnouncementAt: s13DraftEvent.ResultAnnouncementAt,
        AdminId: otherAdminId
    ), CancellationToken.None);
    Assert(!otherUpdateRes.Success && otherUpdateRes.Errors.Contains("Platform event not found."),
        "Non-creator admin must not be able to update another creator's Draft event.");

    var deleteHandler = new DeleteEventCommandHandler(db);
    var otherDeleteRes = await deleteHandler.Handle(new DeleteEventCommand(s13DraftEvent.Id, otherAdminId), CancellationToken.None);
    Assert(!otherDeleteRes.Success && otherDeleteRes.Errors.Contains("Platform event not found."),
        "Non-creator admin must not be able to delete another creator's Draft event.");

    var creatorPublishRes = await updateHandler.Handle(new UpdateEventCommand(
        EventId: s13DraftEvent.Id,
        Title: "Creator Published Draft",
        Description: "Published description",
        BannerUrl: null,
        Rules: null,
        Prize: null,
        MaxVote: 3,
        Status: EventStatus.Open,
        SubmissionStartAt: s13DraftEvent.SubmissionStartAt,
        SubmissionEndAt: s13DraftEvent.SubmissionEndAt,
        JudgingStartAt: s13DraftEvent.JudgingStartAt,
        JudgingEndAt: s13DraftEvent.JudgingEndAt,
        VotingStartAt: s13DraftEvent.VotingStartAt,
        VotingEndAt: s13DraftEvent.VotingEndAt,
        ResultAnnouncementAt: s13DraftEvent.ResultAnnouncementAt,
        AdminId: draftCreatorId
    ), CancellationToken.None);
    Assert(creatorPublishRes.Success && creatorPublishRes.Data?.Status == EventStatus.Upcoming.ToString(),
        $"Creator publishing future Draft event must resolve to Upcoming (errors={string.Join("; ", creatorPublishRes.Errors)}).");

    var (otherAfterPublishList, _) = await getEventsHandler.Handle(new GetEventsQuery(PageSize: 50, CurrentUserId: otherAdminId), CancellationToken.None);
    Assert(otherAfterPublishList.Any(e => e.Id == s13DraftEvent.Id),
        "Once published (Upcoming), other users must be able to see the event.");
}

Console.WriteLine("\n--- Section 15: Upcoming (Waiting to Open) -> Open Lifecycle Checks ---");
{
    var s15Now = DateTimeOffset.UtcNow;
    var s15AdminId = Guid.NewGuid();
    db.Users.Add(new ApplicationUser { Id = s15AdminId, UserName = "s15admin", Email = "s15admin@test.invalid", FullName = "S15 Admin" });
    await db.SaveChangesAsync();

    var s15CreateHandler = new CreateEventCommandHandler(db);
    var futureCreateRes = await s15CreateHandler.Handle(new CreateEventCommand(
        Title: "S15 Waiting To Open Event",
        Description: "Starts in +3 min",
        BannerUrl: null,
        Rules: null,
        Prize: null,
        MaxVote: 1,
        Status: EventStatus.Open,
        SubmissionStartAt: s15Now.AddMinutes(3),
        SubmissionEndAt: s15Now.AddMinutes(6),
        JudgingStartAt: s15Now.AddMinutes(7),
        JudgingEndAt: s15Now.AddMinutes(12),
        VotingStartAt: s15Now.AddMinutes(13),
        VotingEndAt: s15Now.AddMinutes(16),
        ResultAnnouncementAt: s15Now.AddMinutes(17),
        AdminId: s15AdminId
    ), CancellationToken.None);
    Assert(futureCreateRes.Success && futureCreateRes.Data?.Status == nameof(EventStatus.Upcoming),
        $"Creating a published event before SubmissionStartAt must set status to Upcoming (got {futureCreateRes.Data?.Status}).");

    // Simulate SubmissionStartAt arriving for an Upcoming event
    var dueUpcoming = new PlatformEvent
    {
        Id = Guid.NewGuid(),
        Title = "S15 Due Upcoming Event",
        Description = "SubmissionStartAt just arrived",
        Status = EventStatus.Upcoming,
        CreatedByAdminId = s15AdminId,
        SubmissionStartAt = s15Now.AddMinutes(-1),
        SubmissionEndAt = s15Now.AddMinutes(5),
        JudgingStartAt = s15Now.AddMinutes(6),
        JudgingEndAt = s15Now.AddMinutes(10),
        VotingStartAt = s15Now.AddMinutes(11),
        VotingEndAt = s15Now.AddMinutes(15),
        ResultAnnouncementAt = s15Now.AddMinutes(16),
        CreatedAt = s15Now.AddMinutes(-5)
    };
    // Draft whose SubmissionStartAt has already passed must stay Draft & hidden from public GetEvents
    var pastDueDraft = new PlatformEvent
    {
        Id = Guid.NewGuid(),
        Title = "S15 Past Due Draft Event",
        Description = "Draft stays hidden forever",
        Status = EventStatus.Draft,
        CreatedByAdminId = s15AdminId,
        SubmissionStartAt = s15Now.AddMinutes(-1),
        SubmissionEndAt = s15Now.AddMinutes(5),
        JudgingStartAt = s15Now.AddMinutes(6),
        JudgingEndAt = s15Now.AddMinutes(10),
        VotingStartAt = s15Now.AddMinutes(11),
        VotingEndAt = s15Now.AddMinutes(15),
        ResultAnnouncementAt = s15Now.AddMinutes(16),
        CreatedAt = s15Now.AddMinutes(-5)
    };
    db.PlatformEvents.AddRange(dueUpcoming, pastDueDraft);
    await db.SaveChangesAsync();

    var s15GetEvents = new GetEventsQueryHandler(db);
    var (openItems, _) = await s15GetEvents.Handle(new GetEventsQuery(Status: EventStatus.Open, PageSize: 50, CurrentUserId: s15AdminId), CancellationToken.None);
    var (upcomingItems, _) = await s15GetEvents.Handle(new GetEventsQuery(Status: EventStatus.Upcoming, PageSize: 50, CurrentUserId: s15AdminId), CancellationToken.None);
    Assert(openItems.Any(e => e.Id == dueUpcoming.Id && e.Status == EventStatus.Open),
        "Due Upcoming event must appear as Open once SubmissionStartAt arrives.");
    Assert(!upcomingItems.Any(e => e.Id == dueUpcoming.Id),
        "Due Upcoming event must no longer appear under Upcoming filter.");
    Assert(!openItems.Any(e => e.Id == pastDueDraft.Id),
        "Draft event must never become Open or show in public GetEvents when SubmissionStartAt arrives.");

    var s15Detail = await new GetEventByIdQueryHandler(db).Handle(new GetEventByIdQuery(dueUpcoming.Id), CancellationToken.None);
    Assert(s15Detail != null && s15Detail.Status == nameof(EventStatus.Open) && s15Detail.IsOpenForSubmission,
        "GetEventById must report effective Open status and IsOpenForSubmission=true for due Upcoming event.");
}

Console.WriteLine("\n--- Section 14: Carousel Top-5 Latest Featured Active Events Query Checks ---");
{
    var s14AdminId = Guid.NewGuid();
    var s14Now = DateTimeOffset.UtcNow;

    db.Users.Add(new ApplicationUser
    {
        Id = s14AdminId,
        UserName = "s14admin",
        Email = "s14admin@test.invalid",
        FullName = "Section 14 Admin"
    });

    // Clear IsFeatured on any earlier test events so Section 14 assertions are isolated
    var prevFeatured = await db.PlatformEvents.Where(e => e.IsFeatured).ToListAsync();
    foreach (var prev in prevFeatured)
    {
        prev.IsFeatured = false;
    }

    PlatformEvent MakeS14Event(string title, EventStatus status, bool featured, DateTimeOffset createdAt, DateTimeOffset resultAnnouncementAt) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Description = "Carousel test event",
        Status = status,
        IsFeatured = featured,
        CreatedByAdminId = s14AdminId,
        SubmissionStartAt = resultAnnouncementAt.AddMinutes(-20),
        SubmissionEndAt = resultAnnouncementAt.AddMinutes(-15),
        JudgingStartAt = resultAnnouncementAt.AddMinutes(-14),
        JudgingEndAt = resultAnnouncementAt.AddMinutes(-8),
        VotingStartAt = resultAnnouncementAt.AddMinutes(-7),
        VotingEndAt = resultAnnouncementAt.AddMinutes(-2),
        ResultAnnouncementAt = resultAnnouncementAt,
        CreatedAt = createdAt
    };

    // Disqualified candidates (even if newer CreatedAt):
    var s14DraftFeatured = MakeS14Event("S14 Draft Featured", EventStatus.Draft, true, s14Now.AddMinutes(10), s14Now.AddHours(2));
    var s14EndedStatus = MakeS14Event("S14 Ended Status", EventStatus.Ended, true, s14Now.AddMinutes(9), s14Now.AddHours(2));
    var s14ExpiredTime = MakeS14Event("S14 Expired ResultTime", EventStatus.Open, true, s14Now.AddMinutes(8), s14Now.AddMinutes(-1));
    var s14NotFeatured = MakeS14Event("S14 Not Featured", EventStatus.Open, false, s14Now.AddMinutes(7), s14Now.AddHours(2));

    // 6 valid active featured events (oldest #1 through newest #6, including Judging)
    var s14Valid1Oldest = MakeS14Event("S14 Valid 1 Oldest", EventStatus.Open, true, s14Now.AddMinutes(1), s14Now.AddHours(1));
    var s14Valid2 = MakeS14Event("S14 Valid 2", EventStatus.Open, true, s14Now.AddMinutes(2), s14Now.AddHours(1));
    var s14Valid3Judging = MakeS14Event("S14 Valid 3 Judging", EventStatus.Judging, true, s14Now.AddMinutes(3), s14Now.AddHours(1));
    var s14Valid4 = MakeS14Event("S14 Valid 4", EventStatus.Open, true, s14Now.AddMinutes(4), s14Now.AddHours(1));
    var s14Valid5 = MakeS14Event("S14 Valid 5", EventStatus.Open, true, s14Now.AddMinutes(5), s14Now.AddHours(1));
    var s14Valid6Newest = MakeS14Event("S14 Valid 6 Newest", EventStatus.Open, true, s14Now.AddMinutes(6), s14Now.AddHours(1));

    db.PlatformEvents.AddRange(
        s14DraftFeatured, s14EndedStatus, s14ExpiredTime, s14NotFeatured,
        s14Valid1Oldest, s14Valid2, s14Valid3Judging, s14Valid4, s14Valid5, s14Valid6Newest
    );
    await db.SaveChangesAsync();

    var carouselHandler = new GetEventsQueryHandler(db);
    var (carouselItems, carouselTotal) = await carouselHandler.Handle(
        new GetEventsQuery(IsFeatured: true, SortBy: "latest", Page: 1, PageSize: 5, CurrentUserId: s14AdminId, ExcludeEnded: true),
        CancellationToken.None);

    Assert(carouselTotal == 6 && carouselItems.Count == 5,
        $"Carousel query must find 6 eligible featured active events and return top 5 (total={carouselTotal}, count={carouselItems.Count}).");
    Assert(carouselItems.Select(x => x.Id).SequenceEqual(new[]
    {
        s14Valid6Newest.Id,
        s14Valid5.Id,
        s14Valid4.Id,
        s14Valid3Judging.Id,
        s14Valid2.Id
    }), "Carousel must return the exact top 5 latest non-ended featured events (including Judging, excluding Draft/Ended/expired/non-featured/6th oldest).");
}


if (args.Contains("--sql"))
{
    Console.WriteLine("\n--- Section 11: Real SQL Server End-to-End Integration Checks (GUID-Scoped Cleanup) ---");
    var sqlConn = args.SkipWhile(a => a != "--connection").Skip(1).FirstOrDefault()
        ?? "Server=.\\SQLEXPRESS;Database=ArtCommissionDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    var sqlOptions = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(sqlConn)
        .Options;

    var suffix = Guid.NewGuid().ToString("N")[..10];
    var sqlAdminId = Guid.NewGuid();
    var sqlCreatorId = Guid.NewGuid();
    var sqlClient1Id = Guid.NewGuid();
    var sqlClient2Id = Guid.NewGuid();
    var sqlCreatorProfileId = Guid.NewGuid();
    var sqlEventId = Guid.NewGuid();
    var sqlArt1Id = Guid.NewGuid();
    var sqlArt2Id = Guid.NewGuid();
    var sqlUserIds = new[] { sqlAdminId, sqlCreatorId, sqlClient1Id, sqlClient2Id };

    await using var sqlDb = new AppDbContext(sqlOptions);
    try
    {

        var creatorRole = await sqlDb.Roles.FirstOrDefaultAsync(r => r.NormalizedName == "CREATOR");
        if (creatorRole == null)
        {
            creatorRole = new IdentityRole<Guid>("Creator") { Id = Guid.NewGuid(), NormalizedName = "CREATOR" };
            sqlDb.Roles.Add(creatorRole);
        }

        var clientRole = await sqlDb.Roles.FirstOrDefaultAsync(r => r.NormalizedName == "CLIENT");
        if (clientRole == null)
        {
            clientRole = new IdentityRole<Guid>(UserRoleNames.Client) { Id = Guid.NewGuid(), NormalizedName = "CLIENT" };
            sqlDb.Roles.Add(clientRole);
        }

        var adminRole = await sqlDb.Roles.FirstOrDefaultAsync(r => r.NormalizedName == "ADMINISTRATOR");
        if (adminRole == null)
        {
            adminRole = new IdentityRole<Guid>(UserRoleNames.Administrator) { Id = Guid.NewGuid(), NormalizedName = "ADMINISTRATOR" };
            sqlDb.Roles.Add(adminRole);
        }

        var sqlAdminUser = new ApplicationUser
        {
            Id = sqlAdminId,
            UserName = $"sqladmin_{suffix}",
            NormalizedUserName = $"SQLADMIN_{suffix}".ToUpperInvariant(),
            Email = $"sqladmin_{suffix}@test.invalid",
            NormalizedEmail = $"SQLADMIN_{suffix}@TEST.INVALID".ToUpperInvariant(),
            FullName = "SQL Admin Tester"
        };
        var sqlCreatorUser = new ApplicationUser
        {
            Id = sqlCreatorId,
            UserName = $"sqlcreator_{suffix}",
            NormalizedUserName = $"SQLCREATOR_{suffix}".ToUpperInvariant(),
            Email = $"sqlcreator_{suffix}@test.invalid",
            NormalizedEmail = $"SQLCREATOR_{suffix}@TEST.INVALID".ToUpperInvariant(),
            FullName = "SQL Creator Tester"
        };
        var sqlClient1User = new ApplicationUser
        {
            Id = sqlClient1Id,
            UserName = $"sqlclient1_{suffix}",
            NormalizedUserName = $"SQLCLIENT1_{suffix}".ToUpperInvariant(),
            Email = $"sqlclient1_{suffix}@test.invalid",
            NormalizedEmail = $"SQLCLIENT1_{suffix}@TEST.INVALID".ToUpperInvariant(),
            FullName = "SQL Client One"
        };
        var sqlClient2User = new ApplicationUser
        {
            Id = sqlClient2Id,
            UserName = $"sqlclient2_{suffix}",
            NormalizedUserName = $"SQLCLIENT2_{suffix}".ToUpperInvariant(),
            Email = $"sqlclient2_{suffix}@test.invalid",
            NormalizedEmail = $"SQLCLIENT2_{suffix}@TEST.INVALID".ToUpperInvariant(),
            FullName = "SQL Client Two"
        };
        sqlDb.Users.AddRange(sqlAdminUser, sqlCreatorUser, sqlClient1User, sqlClient2User);
        sqlDb.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = sqlAdminId, RoleId = adminRole.Id },
            new IdentityUserRole<Guid> { UserId = sqlCreatorId, RoleId = creatorRole.Id },
            new IdentityUserRole<Guid> { UserId = sqlClient1Id, RoleId = clientRole.Id }
        );
        sqlDb.CreatorProfiles.Add(new CreatorProfile
        {
            Id = sqlCreatorProfileId,
            UserId = sqlCreatorId,
            DisplayName = $"SQL Creator Studio {suffix}",
            IsDeleted = false
        });

        var sqlEvent = new PlatformEvent
        {
            Id = sqlEventId,
            Title = $"SQL E2E Event {suffix}",
            Description = "Real SQL Server verification",
            Status = EventStatus.Open,
            CreatedByAdminId = sqlAdminId,
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
        var sqlArt1 = new Artwork
        {
            Id = sqlArt1Id,
            CreatorProfileId = sqlCreatorProfileId,
            Title = "SQL Pending Art V1",
            ImageUrl = "https://example.com/sql-v1.png",
            ThumbnailUrl = "https://example.com/sql-v1-thumb.png",
            ModerationStatus = "Pending",
            IsDeleted = false
        };
        var sqlArt2 = new Artwork
        {
            Id = sqlArt2Id,
            CreatorProfileId = sqlCreatorProfileId,
            Title = "SQL Approved Art V2",
            ImageUrl = "https://example.com/sql-v2.png",
            ThumbnailUrl = "https://example.com/sql-v2-thumb.png",
            ModerationStatus = "Approved",
            IsDeleted = false
        };
        var sqlSub = new EventSubmission
        {
            Id = Guid.NewGuid(),
            EventId = sqlEvent.Id,
            SubmitterId = sqlCreatorId,
            ArtworkId = sqlArt1.Id,
            Title = "SQL Submission V1",
            Description = "Pending art submission",
            IsDisqualified = false,
            SubmittedAt = now
        };
        sqlDb.PlatformEvents.Add(sqlEvent);
        sqlDb.Artworks.AddRange(sqlArt1, sqlArt2);
        sqlDb.EventSubmissions.Add(sqlSub);
        await sqlDb.SaveChangesAsync();

        // SQL Check I.2: Marketplace detail & interactions on Pending event artwork
        var sqlMarketplace = new MarketplaceQueryHandler(sqlDb);
        var sqlInteractions = new ArtworkInteractionHandler(sqlDb);
        var sqlDetail = await sqlMarketplace.Handle(new GetArtworkDetailQuery(sqlArt1.Id, sqlClient1Id), CancellationToken.None);
        Assert(sqlDetail is not null && sqlDetail.Artwork.Id == sqlArt1.Id,
            "[SQL] Client must see GetArtworkDetailQuery for Pending event artwork.");
        var sqlLiked = await sqlInteractions.Handle(new SetArtworkLikeCommand(sqlArt1.Id, sqlClient1Id, true), CancellationToken.None);
        Assert(sqlLiked is { IsLiked: true, LikeCount: 1 },
            "[SQL] Client must be able to like Pending event artwork.");

        // SQL Check II.1, II.2, II.3, III.1, III.2, I.3: Disputes on SQL Server
        var sqlNotifPub = new RecordingNotificationPublisher();
        var sqlCreateDispute = new CreateSubmissionDisputeCommandHandler(sqlDb, sqlNotifPub);
        var sqlGetDisputes = new GetSubmissionDisputesQueryHandler(sqlDb);

        var d1 = await sqlCreateDispute.Handle(new CreateSubmissionDisputeCommand(sqlSub.Id, sqlClient1Id, "Copyright issue on V1", Priority: "High", Category: "Other"), CancellationToken.None);
        var d2 = await sqlCreateDispute.Handle(new CreateSubmissionDisputeCommand(sqlSub.Id, sqlClient2Id, "Suspected AI", Category: "AiArt"), CancellationToken.None);
        Assert(d1.Success && d2.Success, "[SQL] Two clients must both succeed raising disputes on same submission.");
        Assert(sqlNotifPub.Published.Any(n => n.UserId == sqlAdminId && n.NotificationType == NotificationType.SubmissionDisputeCreated),
            "[SQL] Active Administrator must receive SubmissionDisputeCreated notification.");

        var d1Entity = await sqlDb.SubmissionDisputes.FindAsync(d1.DisputeId!.Value);
        Assert(d1Entity != null && d1Entity.Priority == "Low", "[SQL] Server priority for Category=Other must be Low.");
        d1Entity!.Status = "Rejected";
        d1Entity.Resolution = "Dismiss";
        d1Entity.ResolvedAt = now;
        await sqlDb.SaveChangesAsync();

        var d1Retry = await sqlCreateDispute.Handle(new CreateSubmissionDisputeCommand(sqlSub.Id, sqlClient1Id, "Re-raised dispute", Category: "Other"), CancellationToken.None);
        Assert(d1Retry.Success, "[SQL] Same client must be able to raise a new dispute after prior resolution.");

        var sqlUpdateSub = new UpdateSubmissionCommandHandler(sqlDb, null!);
        var sqlUpdRes = await sqlUpdateSub.Handle(new UpdateSubmissionCommand(sqlSub.Id, sqlCreatorId, Title: "SQL Submission V2", ArtworkId: sqlArt2.Id), CancellationToken.None);
        Assert(sqlUpdRes.Success, "[SQL] Creator must succeed swapping submission artwork.");

        var (sqlDisputesList, sqlDisputeCount) = await sqlGetDisputes.Handle(new GetSubmissionDisputesQuery(SubmissionId: sqlSub.Id), CancellationToken.None);
        Assert(sqlDisputeCount == 3 && sqlDisputesList.All(d => d.SubmissionTitle == "SQL Submission V1" && d.ArtworkImageUrl == "https://example.com/sql-v1.png"),
            $"[SQL] All 3 disputes across clients & re-disputes must persist with V1 snapshots after swap (count={sqlDisputeCount}).");

        // SQL Check III.3, III.4, III.5, III.7: Jury filtered unique indexes & Remove -> Re-invite -> Accept
        var sqlCandidates = new GetInvitationCandidatesQueryHandler(sqlDb);
        var sqlSendInvite = new SendInvitationCommandHandler(sqlDb, sqlNotifPub, NullLogger<SendInvitationCommandHandler>.Instance);
        var sqlRespondInvite = new UpdateInvitationStatusCommandHandler(sqlDb, sqlNotifPub, NullLogger<UpdateInvitationStatusCommandHandler>.Instance);
        var sqlGetJuries = new GetJuriesQueryHandler(sqlDb);
        var sqlRemoveJury = new RemoveJuryCommandHandler(sqlDb);

        var (candidateList, _) = await sqlCandidates.Handle(new GetInvitationCandidatesQuery(sqlEvent.Id, Search: suffix, RoleFilter: "All", Page: 1, PageSize: 20), CancellationToken.None);
        Assert(candidateList.Any(c => c.UserId == sqlCreatorId && c.CanInvite && c.Email == $"sqlcreator_{suffix}@test.invalid"),
            "[SQL] GetInvitationCandidatesQuery must translate to T-SQL and return eligible Creator.");
        Assert(candidateList.Any(c => c.UserId == sqlClient1Id && !c.CanInvite && !string.IsNullOrWhiteSpace(c.IneligibleReason)),
            "[SQL] GetInvitationCandidatesQuery must return Client with explicit IneligibleReason.");

        var clientInvFail = await sqlSendInvite.Handle(new SendInvitationCommand(sqlEvent.Id, sqlAdminId, $"sqlclient1_{suffix}@test.invalid", false), CancellationToken.None);
        Assert(!clientInvFail.Success && clientInvFail.Errors.Any(e => e.Contains("Creator", StringComparison.OrdinalIgnoreCase)),
            "[SQL] Inviting Client must fail with explicit Creator-role error.");

        var inv1 = await sqlSendInvite.Handle(new SendInvitationCommand(sqlEvent.Id, sqlAdminId, $"sqlcreator_{suffix}@test.invalid", true), CancellationToken.None);
        Assert(inv1.Success && inv1.Data != null, "[SQL] First invitation to Creator must succeed.");
        var acc1 = await sqlRespondInvite.Handle(new UpdateInvitationStatusCommand(inv1.Data!.Id, sqlCreatorId, InvitationStatus.Accepted), CancellationToken.None);
        Assert(acc1.Success, "[SQL] Creator accepting 1st invitation must succeed on real SQL Server.");

        var (juries1, _) = await sqlGetJuries.Handle(new GetJuriesQuery(EventId: sqlEvent.Id), CancellationToken.None);
        Assert(juries1.Count == 1 && juries1[0].CreatorEmail == $"sqlcreator_{suffix}@test.invalid",
            "[SQL] GetJuriesQuery must return Creator real email.");

        var rem = await sqlRemoveJury.Handle(RemoveJuryCommand.ByJuryId(juries1[0].Id, sqlAdminId), CancellationToken.None);
        Assert(rem.Success, "[SQL] Removing active Jury must succeed.");

        var inv2 = await sqlSendInvite.Handle(new SendInvitationCommand(sqlEvent.Id, sqlAdminId, $"sqlcreator_{suffix}@test.invalid", true), CancellationToken.None);
        Assert(inv2.Success && inv2.Data != null, "[SQL] Sending 2nd invitation after RemoveJury must succeed.");
        var acc2 = await sqlRespondInvite.Handle(new UpdateInvitationStatusCommand(inv2.Data!.Id, sqlCreatorId, InvitationStatus.Accepted), CancellationToken.None);
        Assert(acc2.Success, $"[SQL] Creator accepting 2nd invitation must succeed under SQL filtered indexes (errors={string.Join("; ", acc2.Errors)}).");

        var (juries2, _) = await sqlGetJuries.Handle(new GetJuriesQuery(EventId: sqlEvent.Id), CancellationToken.None);
        Assert(juries2.Count == 1 && juries2[0].CreatorEmail == $"sqlcreator_{suffix}@test.invalid" && juries2[0].IsHeadJury,
            "[SQL] Restored Jury member must be active again with HeadJury role and real email on SQL Server.");

        // SQL Check 11.8: Jury dispute isolation + auto ScoreAppeal + multi-jury SQL scoring
        var sqlHeadWorkspace = new GetHeadJuryWorkspaceQueryHandler(sqlDb);
        var sqlWsIsolated = await sqlHeadWorkspace.Handle(new GetHeadJuryWorkspaceQuery(sqlEvent.Id), CancellationToken.None);
        Assert(sqlWsIsolated != null && sqlWsIsolated.ArbitrationCases.Count == 0 && sqlWsIsolated.DisputesResolved,
            $"[SQL] Moderation disputes on SQL Server must NOT leak into HeadJuryWorkspace (count={sqlWsIsolated?.ArbitrationCases.Count}).");

        // Add 2 dedicated judges (sqlClient1 & sqlClient2 users with CreatorProfiles) & enter Judging window
        var sqlJudge1Prof = new CreatorProfile { Id = Guid.NewGuid(), UserId = sqlClient1Id, DisplayName = $"SQL Judge 1 {suffix}", IsDeleted = false };
        var sqlJudge2Prof = new CreatorProfile { Id = Guid.NewGuid(), UserId = sqlClient2Id, DisplayName = $"SQL Judge 2 {suffix}", IsDeleted = false };
        sqlDb.CreatorProfiles.AddRange(sqlJudge1Prof, sqlJudge2Prof);
        sqlDb.Juries.AddRange(
            new Jury { Id = Guid.NewGuid(), EventId = sqlEvent.Id, CreatorId = sqlJudge1Prof.Id, IsHeadJury = false, CreatedAt = now, IsDeleted = false },
            new Jury { Id = Guid.NewGuid(), EventId = sqlEvent.Id, CreatorId = sqlJudge2Prof.Id, IsHeadJury = false, CreatedAt = now, IsDeleted = false }
        );
        var sqlCrit1 = new EventCriteria { Id = Guid.NewGuid(), EventId = sqlEvent.Id, Name = "Creativity", MaxScore = 10m, Weight = 1m, DisplayOrder = 1, CreatedAt = now };
        var sqlCrit2 = new EventCriteria { Id = Guid.NewGuid(), EventId = sqlEvent.Id, Name = "Color", MaxScore = 10m, Weight = 1m, DisplayOrder = 2, CreatedAt = now };
        sqlDb.EventCriteria.AddRange(sqlCrit1, sqlCrit2);
        await sqlDb.SaveChangesAsync();

        var sqlScoreHandler = new SubmitJuryScoreCommandHandler(sqlDb);
        var sqlEarlyScore = await sqlScoreHandler.Handle(
            new SubmitJuryScoreCommand(sqlSub.Id, sqlClient1Id, new SubmitScoreDto
            {
                SubmissionId = sqlSub.Id,
                TotalScore = 16m,
                CriteriaScores = [new CriteriaScoreItemDto { CriteriaId = sqlCrit1.Id, Score = 8m }, new CriteriaScoreItemDto { CriteriaId = sqlCrit2.Id, Score = 8m }]
            }),
            CancellationToken.None);
        Assert(!sqlEarlyScore.Success && sqlEarlyScore.Errors.Any(e => e.Contains("chưa bắt đầu")),
            "[SQL] SubmitJuryScoreCommand must reject scoring before JudgingStartAt on SQL Server.");

        sqlEvent.SubmissionStartAt = now.AddMinutes(-30);
        sqlEvent.SubmissionEndAt = now.AddMinutes(-10);
        sqlEvent.JudgingStartAt = now.AddMinutes(-5);
        await sqlDb.SaveChangesAsync();

        var sqlJ1Score = await sqlScoreHandler.Handle(
            new SubmitJuryScoreCommand(sqlSub.Id, sqlClient1Id, new SubmitScoreDto
            {
                SubmissionId = sqlSub.Id,
                TotalScore = 16m,
                CriteriaScores = [new CriteriaScoreItemDto { CriteriaId = sqlCrit1.Id, Score = 8m }, new CriteriaScoreItemDto { CriteriaId = sqlCrit2.Id, Score = 8m }]
            }),
            CancellationToken.None);
        Assert(sqlJ1Score.Success && sqlJ1Score.Data?.Score == 16m,
            $"[SQL] Judge 1 scoring during Judging window must succeed on SQL Server (errors={string.Join("; ", sqlJ1Score.Errors)}).");

        var sqlGetSubs = new GetSubmissionsByEventIdQueryHandler(sqlDb);
        var (sqlJ2Subs, _) = await sqlGetSubs.Handle(new GetSubmissionsByEventIdQuery(sqlEvent.Id, CurrentUserId: sqlClient2Id), CancellationToken.None);
        Assert(sqlJ2Subs.Count == 1 && sqlJ2Subs[0].CurrentJuryHasGraded == false,
            "[SQL] Judge 2 must see CurrentJuryHasGraded == false on SQL Server after Judge 1 scored.");

        var sqlJ2Score = await sqlScoreHandler.Handle(
            new SubmitJuryScoreCommand(sqlSub.Id, sqlClient2Id, new SubmitScoreDto
            {
                SubmissionId = sqlSub.Id,
                TotalScore = 6m,
                CriteriaScores = [new CriteriaScoreItemDto { CriteriaId = sqlCrit1.Id, Score = 3m }, new CriteriaScoreItemDto { CriteriaId = sqlCrit2.Id, Score = 3m }]
            }),
            CancellationToken.None);
        Assert(sqlJ2Score.Success && sqlJ2Score.Data?.TotalAverageScore == 11m,
            $"[SQL] Judge 2 divergent scoring must succeed on SQL Server and average to 11 (errors={string.Join("; ", sqlJ2Score.Errors)}).");

        var sqlWsDeviation = await sqlHeadWorkspace.Handle(new GetHeadJuryWorkspaceQuery(sqlEvent.Id), CancellationToken.None);
        Assert(sqlWsDeviation != null && sqlWsDeviation.ArbitrationCases.Count == 1
            && sqlWsDeviation.ArbitrationCases[0].Category == "ScoreAppeal"
            && sqlWsDeviation.ArbitrationCases[0].DeviationPercent > 25m
            && !sqlWsDeviation.DisputesResolved,
            $"[SQL] HeadJuryWorkspace on SQL Server must surface auto ScoreAppeal dispute only (count={sqlWsDeviation?.ArbitrationCases.Count}).");

        if (sqlWsDeviation?.ArbitrationCases.Count == 1)
        {
            var sqlResolveDispute = new ResolveSubmissionDisputeCommandHandler(sqlDb);
            var sqlResolvedRes = await sqlResolveDispute.Handle(
                new ResolveSubmissionDisputeCommand(sqlWsDeviation.ArbitrationCases[0].Id, sqlCreatorId, "Dismiss", "Confirmed final average"),
                CancellationToken.None);
            Assert(sqlResolvedRes.Success,
                $"[SQL] Resolving auto-generated ScoreAppeal dispute must succeed on SQL Server (errors={string.Join("; ", sqlResolvedRes.Errors)}).");
        }

        // Verify T-SQL translation of Upcoming -> Open effective projection, ExcludeEnded, and IncludeDrafts
        var checkNow = DateTimeOffset.UtcNow;
        sqlEvent.Status = EventStatus.Upcoming;
        sqlEvent.SubmissionStartAt = checkNow.AddMinutes(-1);
        sqlEvent.SubmissionEndAt = checkNow.AddHours(1);
        sqlEvent.JudgingStartAt = checkNow.AddHours(2);
        sqlEvent.JudgingEndAt = checkNow.AddHours(3);
        sqlEvent.VotingStartAt = checkNow.AddHours(4);
        sqlEvent.VotingEndAt = checkNow.AddHours(5);
        sqlEvent.ResultAnnouncementAt = checkNow.AddHours(6);
        await sqlDb.SaveChangesAsync();

        var (sqlUpcomingAsOpen, _) = await new GetEventsQueryHandler(sqlDb).Handle(
            new GetEventsQuery(Search: suffix, Status: EventStatus.Open, PageSize: 10, CurrentUserId: sqlAdminId, ExcludeEnded: true, IncludeDrafts: false),
            CancellationToken.None);
        Assert(sqlUpcomingAsOpen.Any(e => e.Id == sqlEvent.Id && e.Status == EventStatus.Open),
            "[SQL] Due Upcoming event query with Status=Open and ExcludeEnded=true must translate to T-SQL and return Open.");

        var sqlDetailCheck = await new GetEventByIdQueryHandler(sqlDb).Handle(new GetEventByIdQuery(sqlEvent.Id), CancellationToken.None);
        Assert(sqlDetailCheck != null && sqlDetailCheck.Status == nameof(EventStatus.Open) && sqlDetailCheck.IsOpenForSubmission,
            "[SQL] GetEventById on due Upcoming event must return Open and IsOpenForSubmission=true.");

        var sqlStatsCheck = await new GetEventStatsQueryHandler(sqlDb).Handle(new GetEventStatsQuery(sqlAdminId, IncludeDrafts: false), CancellationToken.None);
        Assert(sqlStatsCheck.OpenEventsCount >= 1,
            "[SQL] GetEventStats must translate cleanly to SQL Server.");
    }
    finally
    {
        await using var cleanupDb = new AppDbContext(sqlOptions);
        await cleanupDb.CriteriaScores.Where(cs => cs.EventCriteria != null && cs.EventCriteria.EventId == sqlEventId).ExecuteDeleteAsync();
        await cleanupDb.EventCriteria.IgnoreQueryFilters().Where(ec => ec.EventId == sqlEventId).ExecuteDeleteAsync();
        await cleanupDb.Invitations.IgnoreQueryFilters().Where(i => i.EventId == sqlEventId).ExecuteDeleteAsync();
        await cleanupDb.Juries.IgnoreQueryFilters().Where(j => j.EventId == sqlEventId).ExecuteDeleteAsync();
        await cleanupDb.SubmissionDisputes.IgnoreQueryFilters().Where(d => d.EventId == sqlEventId).ExecuteDeleteAsync();
        await cleanupDb.EventSubmissions.IgnoreQueryFilters().Where(s => s.EventId == sqlEventId).ExecuteDeleteAsync();
        await cleanupDb.ArtworkFavorites.IgnoreQueryFilters().Where(f => f.ArtworkId == sqlArt1Id || f.ArtworkId == sqlArt2Id).ExecuteDeleteAsync();
        await cleanupDb.Artworks.IgnoreQueryFilters().Where(a => a.Id == sqlArt1Id || a.Id == sqlArt2Id).ExecuteDeleteAsync();
        await cleanupDb.PlatformEvents.IgnoreQueryFilters().Where(e => e.Id == sqlEventId).ExecuteDeleteAsync();
        await cleanupDb.CreatorProfiles.IgnoreQueryFilters().Where(c => sqlUserIds.Contains(c.UserId)).ExecuteDeleteAsync();
        await cleanupDb.UserRoles.Where(ur => sqlUserIds.Contains(ur.UserId)).ExecuteDeleteAsync();
        await cleanupDb.Users.IgnoreQueryFilters().Where(u => sqlUserIds.Contains(u.Id)).ExecuteDeleteAsync();
        Console.WriteLine("[SQL] Cleaned up isolated integration test fixtures from ArtCommissionDb.");
    }
}


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
