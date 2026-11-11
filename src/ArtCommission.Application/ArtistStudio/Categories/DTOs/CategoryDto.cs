namespace ArtCommission.Application.ArtistStudio.Categories.DTOs;

public record CategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    int ArtworkCount
);

public record CategorySummaryDto(
    Guid Id,
    string Name,
    string Slug
);

public record TrendingTagDto(
    Guid TagId,
    string Name,
    int ArtworkCount,
    bool IsAiGenerated
);

public record AdminCategoriesOverviewDto(
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<TrendingTagDto> TrendingTags
);

public record CreateCategoryRequest(
    string Name,
    string? Slug,
    string? Description,
    int DisplayOrder = 0,
    bool IsActive = true
);

public record UpdateCategoryRequest(
    string Name,
    string? Slug,
    string? Description,
    int DisplayOrder,
    bool IsActive
);

public record MergeTagsRequest(
    IReadOnlyList<Guid> SourceTagIds,
    Guid TargetTagId
);

public record PromoteTagToCategoryRequest(
    string? CategoryName,
    string? Description,
    int DisplayOrder = 0,
    bool AssignOnlyUnassigned = true,
    bool DeleteTagAfterPromote = false
);
