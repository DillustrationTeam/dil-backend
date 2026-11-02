using ArtCommission.Application.Event.Commands;
using FluentValidation;

namespace ArtCommission.Application.Event.Validators;

public class SubmitJuryScoreCommandValidator : AbstractValidator<SubmitJuryScoreCommand>
{
    public SubmitJuryScoreCommandValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Mã bài dự thi không được để trống.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Mã giám khảo không được để trống.");

        RuleFor(x => x.Dto)
            .NotNull().WithMessage("Dữ liệu chấm điểm không được để trống.");

        When(x => x.Dto != null, () =>
        {
            RuleFor(x => x.Dto.TotalScore)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Điểm số không được là số âm.");

            RuleForEach(x => x.Dto.CriteriaScores).ChildRules(item =>
            {
                item.RuleFor(c => c.CriteriaId)
                    .NotEmpty()
                    .WithMessage("Mã tiêu chí chấm điểm (CriteriaId) không hợp lệ hoặc thiếu thông tin.");

                item.RuleFor(c => c.Score)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("Điểm số không được là số âm.");
            });
        });
    }
}
