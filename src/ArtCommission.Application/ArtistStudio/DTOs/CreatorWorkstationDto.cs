using System.Text.Json;
using ArtCommission.Domain.Entities.ArtistStudio;

namespace ArtCommission.Application.ArtistStudio.DTOs;

public record CreatorCustomClauseDto(string Title, string Content);

public record CreatorExtendedTermsDto(
    int Version = 1,
    string? WillDraw = null,
    string? WontDraw = null,
    string? CommercialScope = null,
    string? UsageRestrictions = null,
    string? DeliveryProcess = null,
    bool AllowPortfolioDisplay = true,
    bool RequireCredit = true,
    bool ProhibitAiTraining = true,
    bool ProhibitResale = true,
    IReadOnlyList<CreatorCustomClauseDto>? CustomClauses = null
);

public record CreatorTermsDto(
    decimal CommercialLicenseMultiplier,
    string? RevisionPolicy,
    string? CancellationPolicy,
    CreatorExtendedTermsDto? Extended = null,
    DateTimeOffset? UpdatedAt = null
);

public record UpdateCreatorTermsRequest(
    decimal CommercialLicenseMultiplier,
    string? RevisionPolicy,
    string? CancellationPolicy,
    CreatorExtendedTermsDto? Extended = null
);

public static class CreatorTermsHelper
{
    public const string DefaultRevisionPolicy = "02 lần chỉnh sửa miễn phí ở giai đoạn Sketch; chỉnh sửa nhỏ (màu sắc/chi tiết) ở giai đoạn hoàn thiện.";
    public const string DefaultCancellationPolicy = "Hoàn tiền 100% phần mốc chưa thực hiện nếu hủy trước khi bắt đầu. Sau khi đã duyệt Sketch, khoản ký quỹ của mốc đã thực hiện không hoàn lại.";
    public const string DefaultWillDraw = "Nhân vật gốc (OC), fanart anime/game, minh họa chân dung, couple.";
    public const string DefaultWontDraw = "Nội dung 18+/NSFW, thù hận/chính trị, mecha quá phức tạp.";
    public const string DefaultCommercialScope = "Sử dụng trên kênh livestream, YouTube, banner quảng bá, hoặc in ấn lưu niệm theo thỏa thuận thương mại.";
    public const string DefaultUsageRestrictions = "Không chỉnh sửa làm biến dạng tác phẩm gốc khi chưa trao đổi. Tệp nguồn phân lớp (.PSD/.CLIP) chỉ bàn giao nếu có thỏa thuận riêng.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string OrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    public static CreatorExtendedTermsDto ParseExtended(string? json)
    {
        CreatorExtendedTermsDto? raw = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                raw = JsonSerializer.Deserialize<CreatorExtendedTermsDto>(json, JsonOptions);
            }
            catch (JsonException)
            {
                raw = null;
            }
        }

        raw ??= new CreatorExtendedTermsDto();
        var clauses = (raw.CustomClauses ?? Array.Empty<CreatorCustomClauseDto>())
            .Where(c => !string.IsNullOrWhiteSpace(c.Title) || !string.IsNullOrWhiteSpace(c.Content))
            .Select(c => new CreatorCustomClauseDto(
                OrDefault(c.Title, "Điều khoản bổ sung"),
                c.Content?.Trim() ?? string.Empty))
            .ToArray();

        return raw with
        {
            WillDraw = OrDefault(raw.WillDraw, DefaultWillDraw),
            WontDraw = OrDefault(raw.WontDraw, DefaultWontDraw),
            CommercialScope = OrDefault(raw.CommercialScope, DefaultCommercialScope),
            UsageRestrictions = OrDefault(raw.UsageRestrictions, DefaultUsageRestrictions),
            DeliveryProcess = null,
            CustomClauses = clauses
        };
    }

    public static string SerializeExtended(CreatorExtendedTermsDto extended) =>
        JsonSerializer.Serialize(extended, JsonOptions);

    public static CreatorTermsDto ToDto(CreatorTerms? terms)
    {
        if (terms is null)
        {
            return new CreatorTermsDto(
                1.5m,
                null,
                null,
                ParseExtended(null),
                null
            );
        }

        return new CreatorTermsDto(
            terms.CommercialLicenseMultiplier,
            OrDefault(terms.RevisionPolicy, string.Empty) is { Length: > 0 } rev ? rev : null,
            OrDefault(terms.CancellationPolicy, string.Empty) is { Length: > 0 } cancel ? cancel : null,
            ParseExtended(terms.ExtendedTermsJson),
            terms.UpdatedAt ?? terms.CreatedAt
        );
    }

    public static string SerializeSnapshot(CreatorTerms? terms) =>
        JsonSerializer.Serialize(ToDto(terms), JsonOptions);

    public static CreatorTermsDto? ParseSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<CreatorTermsDto>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public record AutoReplySettingDto(bool IsEnabled, string BriefTemplate);
public record UpdateAutoReplySettingRequest(bool IsEnabled, string BriefTemplate);
public record CreatorFaqDto(Guid Id, string Question, string Answer, int DisplayOrder);
public record UpsertCreatorFaqRequest(string Question, string Answer, int DisplayOrder);
public record CreatorWorkItemDto(Guid Id, string ClientName, string Title, string Stage, DateTimeOffset? DueAt, DateTimeOffset CreatedAt);
public record CreateWorkItemRequest(string ClientName, string Title, string Stage, DateTimeOffset? DueAt);
public record UpdateWorkItemStageRequest(string Stage);
public record CreatorAssetDto(Guid Id, string AssetType, string Name, string? AssetUrl, string? MetadataJson, DateTimeOffset CreatedAt);
public record CreateCreatorAssetRequest(string AssetType, string Name, string? AssetUrl, string? MetadataJson);
