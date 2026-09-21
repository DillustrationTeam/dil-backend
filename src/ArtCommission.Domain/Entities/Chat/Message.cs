using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.Identity;

namespace ArtCommission.Domain.Entities.Chat;

/// <summary>
/// Thực thể tin nhắn trong phòng làm việc Workroom giữa Client và Creator (UC26 / database.sql).
/// </summary>
public class Message : BaseEntity
{
    public Guid CommissionId { get; set; }
    public Guid SenderId { get; set; }
    public string MessageType { get; set; } = "Text"; // Text, Image, File, SystemEvent, RevisionNotice
    public string Body { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsRead { get; set; }

    public ApplicationUser? Sender { get; set; }
}
