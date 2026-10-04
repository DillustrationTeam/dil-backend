using ArtCommission.Application.Event.Commands;
using FluentValidation;

namespace ArtCommission.Application.Event.Validators;

public class ExpireInvitationsCommandValidator : AbstractValidator<ExpireInvitationsCommand>
{
    public ExpireInvitationsCommandValidator()
    {
        RuleFor(x => x.BatchSize)
            .InclusiveBetween(1, 500)
            .WithMessage("Batch size must be between 1 and 500.");

        When(x => x.MaxLifetime.HasValue, () =>
        {
            RuleFor(x => x.MaxLifetime!.Value)
                .GreaterThan(TimeSpan.Zero)
                .WithMessage("Max lifetime must be greater than zero.");
        });
    }
}
