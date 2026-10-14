namespace ArtCommission.Application.Commission.Disputes.DTOs;

/// <summary>
/// DTO biểu diễn một tin nhắn trong đoạn trích lược sử trao đổi Workroom giữa 2 bên tranh chấp (SCR-22 / UC30).
/// </summary>
public record DisputeChatSnapshotMessageDto
{
    public Guid MessageId { get; init; }
    public Guid SenderId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string SenderRole { get; init; } = string.Empty; // "Client" | "Creator" | "System"
    public string MessageType { get; init; } = "Text"; // Text, Image, File, SystemEvent, RevisionNotice
    public string Body { get; init; } = string.Empty;
    public string? AttachmentUrl { get; init; }
    public DateTimeOffset SentAt { get; init; }
}
