using ArtCommission.Application.Event.Commands;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Validators;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Event;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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


Console.WriteLine("\n=================================================");
Console.WriteLine($"RESULTS: {passed} PASSED, {failed} FAILED.");
Console.WriteLine("ALL EVENT DTO & ENTITY VALIDATION TESTS PASSED!");
Console.WriteLine("=================================================");
