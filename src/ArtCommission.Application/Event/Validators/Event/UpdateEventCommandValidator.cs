using ArtCommission.Application.Event.Commands;
using FluentValidation;

namespace ArtCommission.Application.Event.Validators;

public class UpdateEventCommandValidator : AbstractValidator<UpdateEventCommand>
{
    public UpdateEventCommandValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage("Event ID must not be empty.");

        RuleFor(x => x.AdminId)
            .NotEmpty().WithMessage("Admin ID must not be empty.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Event title is required.")
            .MaximumLength(200).WithMessage("Event title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Event description is required.");

        RuleFor(x => x.BannerUrl)
            .MaximumLength(500).WithMessage("Banner URL cannot exceed 500 characters.")
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.BannerUrl))
            .WithMessage("Banner URL must be a valid URL.");

        RuleFor(x => x.Prize)
            .MaximumLength(1000).WithMessage("Prize description cannot exceed 1000 characters.");

        RuleFor(x => x.MaxVote)
            .GreaterThan(0).WithMessage("Max vote must be greater than 0.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid event status.");

        RuleFor(x => x.SubmissionStartAt)
            .NotEmpty().WithMessage("Submission start date is required.");

        RuleFor(x => x.SubmissionEndAt)
            .NotEmpty().WithMessage("Submission end date is required.")
            .GreaterThan(x => x.SubmissionStartAt).WithMessage("Submission end date must be after submission start date.");

        RuleFor(x => x.JudgingStartAt)
            .NotEmpty().WithMessage("Judging start date is required.")
            .GreaterThan(x => x.SubmissionEndAt).WithMessage("Judging start date must be after submission end date.");

        RuleFor(x => x.JudgingEndAt)
            .NotEmpty().WithMessage("Judging end date is required.")
            .GreaterThan(x => x.JudgingStartAt).WithMessage("Judging end date must be after judging start date.");

        RuleFor(x => x.VotingStartAt)
            .NotEmpty().WithMessage("Voting start date is required.")
            .GreaterThan(x => x.JudgingEndAt).WithMessage("Voting start date must be after judging end date.");

        RuleFor(x => x.VotingEndAt)
            .NotEmpty().WithMessage("Voting end date is required.")
            .GreaterThan(x => x.VotingStartAt).WithMessage("Voting end date must be after voting start date.");

        RuleFor(x => x.ResultAnnouncementAt)
            .NotEmpty().WithMessage("Result announcement date is required.")
            .GreaterThan(x => x.VotingEndAt).WithMessage("Result announcement date must be after voting end date.");
    }

    public static List<string> ValidateTimelineTransition(
        ArtCommission.Domain.Entities.Event.PlatformEvent existing,
        UpdateEventCommand request,
        DateTimeOffset now)
    {
        var errors = new List<string>();

        // 1. Các mốc đã bắt đầu hoặc đã qua → không được sửa
        if (existing.SubmissionStartAt <= now && !IsSame(request.SubmissionStartAt, existing.SubmissionStartAt))
        {
            errors.Add("Submission start time has already started or passed and cannot be modified.");
        }

        if (existing.SubmissionEndAt <= now && !IsSame(request.SubmissionEndAt, existing.SubmissionEndAt))
        {
            errors.Add("Submission end time has already passed and cannot be modified.");
        }

        if (existing.JudgingStartAt <= now && !IsSame(request.JudgingStartAt, existing.JudgingStartAt))
        {
            errors.Add("Judging start time has already started or passed and cannot be modified.");
        }

        if (existing.JudgingEndAt <= now && !IsSame(request.JudgingEndAt, existing.JudgingEndAt))
        {
            errors.Add("Judging end time has already passed and cannot be modified.");
        }

        if (existing.VotingStartAt <= now && !IsSame(request.VotingStartAt, existing.VotingStartAt))
        {
            errors.Add("Voting start time has already started or passed and cannot be modified.");
        }

        if (existing.VotingEndAt <= now && !IsSame(request.VotingEndAt, existing.VotingEndAt))
        {
            errors.Add("Voting end time has already passed and cannot be modified.");
        }

        if (existing.ResultAnnouncementAt <= now && !IsSame(request.ResultAnnouncementAt, existing.ResultAnnouncementAt))
        {
            errors.Add("Result announcement time has already passed and cannot be modified.");
        }

        // 2. Mốc của giai đoạn đang diễn ra → chỉ được kéo dài, không được rút ngắn
        // Giai đoạn Nộp bài:
        if (existing.SubmissionStartAt <= now && now < existing.SubmissionEndAt)
        {
            if (request.SubmissionEndAt < existing.SubmissionEndAt)
            {
                errors.Add("The submission phase is currently active; its end time can only be extended, not shortened.");
            }
        }

        // Giai đoạn Chấm điểm:
        if (existing.JudgingStartAt <= now && now < existing.JudgingEndAt)
        {
            if (request.JudgingEndAt < existing.JudgingEndAt)
            {
                errors.Add("The judging phase is currently active; its end time can only be extended, not shortened.");
            }
        }

        // Giai đoạn Bình chọn:
        if (existing.VotingStartAt <= now && now < existing.VotingEndAt)
        {
            if (request.VotingEndAt < existing.VotingEndAt)
            {
                errors.Add("The voting phase is currently active; its end time can only be extended, not shortened.");
            }
        }

        // Giai đoạn Chờ công bố kết quả:
        if (existing.VotingEndAt <= now && now < existing.ResultAnnouncementAt)
        {
            if (request.ResultAnnouncementAt < existing.ResultAnnouncementAt)
            {
                errors.Add("The result announcement phase is currently pending; announcement time can only be extended or postponed, not shortened.");
            }
        }

        // 3. Các mốc của giai đoạn tương lai → được thay đổi, nhưng không được lùi về quá khứ và phải giữ đúng thứ tự
        if (existing.SubmissionStartAt > now && request.SubmissionStartAt <= now)
        {
            errors.Add("The new submission start time must be in the future.");
        }

        if (existing.SubmissionEndAt > now && request.SubmissionEndAt <= now)
        {
            errors.Add("The new submission end time must be in the future.");
        }

        if (existing.JudgingStartAt > now && request.JudgingStartAt <= now)
        {
            errors.Add("The new judging start time must be in the future.");
        }

        if (existing.JudgingEndAt > now && request.JudgingEndAt <= now)
        {
            errors.Add("The new judging end time must be in the future.");
        }

        if (existing.VotingStartAt > now && request.VotingStartAt <= now)
        {
            errors.Add("The new voting start time must be in the future.");
        }

        if (existing.VotingEndAt > now && request.VotingEndAt <= now)
        {
            errors.Add("The new voting end time must be in the future.");
        }

        if (existing.ResultAnnouncementAt > now && request.ResultAnnouncementAt <= now)
        {
            errors.Add("The new result announcement time must be in the future.");
        }

        // Đảm bảo thứ tự tuần tự tăng dần giữa các mốc
        if (request.SubmissionEndAt <= request.SubmissionStartAt)
        {
            errors.Add("Submission end time must be after submission start time.");
        }

        if (request.JudgingStartAt <= request.SubmissionEndAt)
        {
            errors.Add("Judging start time must be after submission end time.");
        }

        if (request.JudgingEndAt <= request.JudgingStartAt)
        {
            errors.Add("Judging end time must be after judging start time.");
        }

        if (request.VotingStartAt <= request.JudgingEndAt)
        {
            errors.Add("Voting start time must be after judging end time.");
        }

        if (request.VotingEndAt <= request.VotingStartAt)
        {
            errors.Add("Voting end time must be after voting start time.");
        }

        if (request.ResultAnnouncementAt <= request.VotingEndAt)
        {
            errors.Add("Result announcement time must be after voting end time.");
        }

        return errors;
    }

    private static bool IsSame(DateTimeOffset a, DateTimeOffset b) =>
        Math.Abs((a - b).TotalSeconds) < 1;
}
