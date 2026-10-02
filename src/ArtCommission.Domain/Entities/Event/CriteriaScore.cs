namespace ArtCommission.Domain.Entities.Event;

public class CriteriaScore
{
    public Guid EventCriteriaId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid GradedByJuryId { get; set; }
    public decimal Score { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public EventCriteria? EventCriteria { get; set; }
    public EventSubmission? Submission { get; set; }
    public Jury? GradedByJury { get; set; }
}
