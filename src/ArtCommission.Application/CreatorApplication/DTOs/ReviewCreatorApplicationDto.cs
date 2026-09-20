using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.CreatorApplication.DTOs;
public class ReviewCreatorApplicationDto 
{
    public ApplicationStatus Status{ get; set; }
    public string? ReviewNote { get; set; }
}