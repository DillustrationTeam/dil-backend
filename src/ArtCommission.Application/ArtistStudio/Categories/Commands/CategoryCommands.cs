using ArtCommission.Application.ArtistStudio.Categories.DTOs;
using ArtCommission.Application.ArtistStudio.Categories.Helpers;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.ArtistStudio;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Categories.Commands;

public record CreateCategoryCommand(
    string Name,
    string? Slug,
    string? Description,
    int DisplayOrder,
    bool IsActive) : IRequest<(bool Success, CategoryDto? Data, string[] Errors)>;

public sealed class CreateCategoryCommandHandler
    : IRequestHandler<CreateCategoryCommand, (bool Success, CategoryDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public CreateCategoryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<(bool Success, CategoryDto? Data, string[] Errors)> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 100)
        {
            return (false, null, new[] { "Tên danh mục phải từ 1 đến 100 ký tự." });
        }

        var slug = SlugHelper.ToSlug(string.IsNullOrWhiteSpace(request.Slug) ? name : request.Slug);
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 120)
        {
            return (false, null, new[] { "Slug danh mục không hợp lệ hoặc quá 120 ký tự." });
        }

        if (request.Description?.Length > 500)
        {
            return (false, null, new[] { "Mô tả danh mục tối đa 500 ký tự." });
        }

        var existingActive = await _db.ArtworkCategories
            .AsNoTracking()
            .AnyAsync(c => !c.IsDeleted && (c.Name == name || c.Slug == slug), cancellationToken);

        if (existingActive)
        {
            return (false, null, new[] { "Danh mục hoặc slug đã tồn tại." });
        }

        // Reuse soft-deleted category matching exact name/slug to satisfy unique indexes
        var softDeleted = await _db.ArtworkCategories
            .FirstOrDefaultAsync(c => c.IsDeleted && (c.Name == name || c.Slug == slug), cancellationToken);

        var now = DateTimeOffset.UtcNow;
        ArtworkCategory entity;
        if (softDeleted is not null)
        {
            softDeleted.Name = name;
            softDeleted.Slug = slug;
            softDeleted.Description = request.Description?.Trim();
            softDeleted.DisplayOrder = request.DisplayOrder;
            softDeleted.IsActive = request.IsActive;
            softDeleted.IsDeleted = false;
            softDeleted.UpdatedAt = now;
            entity = softDeleted;
        }
        else
        {
            entity = new ArtworkCategory
            {
                Name = name,
                Slug = slug,
                Description = request.Description?.Trim(),
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.ArtworkCategories.Add(entity);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return (true, new CategoryDto(entity.Id, entity.Name, entity.Slug, entity.Description, entity.DisplayOrder, entity.IsActive, 0), Array.Empty<string>());
    }
}

public record UpdateCategoryCommand(
    Guid CategoryId,
    string Name,
    string? Slug,
    string? Description,
    int DisplayOrder,
    bool IsActive) : IRequest<(bool Success, CategoryDto? Data, string[] Errors)>;

public sealed class UpdateCategoryCommandHandler
    : IRequestHandler<UpdateCategoryCommand, (bool Success, CategoryDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public UpdateCategoryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<(bool Success, CategoryDto? Data, string[] Errors)> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 100)
        {
            return (false, null, new[] { "Tên danh mục phải từ 1 đến 100 ký tự." });
        }

        var slug = SlugHelper.ToSlug(string.IsNullOrWhiteSpace(request.Slug) ? name : request.Slug);
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 120)
        {
            return (false, null, new[] { "Slug danh mục không hợp lệ hoặc quá 120 ký tự." });
        }

        if (request.Description?.Length > 500)
        {
            return (false, null, new[] { "Mô tả danh mục tối đa 500 ký tự." });
        }

        var category = await _db.ArtworkCategories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && !c.IsDeleted, cancellationToken);

        if (category is null)
        {
            return (false, null, new[] { "Không tìm thấy danh mục." });
        }

        var conflict = await _db.ArtworkCategories
            .AsNoTracking()
            .AnyAsync(c => c.Id != request.CategoryId && (c.Name == name || c.Slug == slug), cancellationToken);

        if (conflict)
        {
            return (false, null, new[] { "Tên hoặc slug danh mục đã được sử dụng." });
        }

        var nameChanged = !string.Equals(category.Name, name, StringComparison.Ordinal);
        category.Name = name;
        category.Slug = slug;
        category.Description = request.Description?.Trim();
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTimeOffset.UtcNow;

        if (nameChanged)
        {
            var artworks = await _db.Artworks
                .Where(a => a.CategoryId == category.Id && !a.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var artwork in artworks)
            {
                artwork.Style = name;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        var count = await _db.Artworks.CountAsync(a => a.CategoryId == category.Id && !a.IsDeleted, cancellationToken);
        return (true, new CategoryDto(category.Id, category.Name, category.Slug, category.Description, category.DisplayOrder, category.IsActive, count), Array.Empty<string>());
    }
}

public record DeleteCategoryCommand(Guid CategoryId) : IRequest<(bool Success, string[] Errors)>;

public sealed class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, (bool Success, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public DeleteCategoryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<(bool Success, string[] Errors)> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _db.ArtworkCategories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && !c.IsDeleted, cancellationToken);

        if (category is null)
        {
            return (false, new[] { "Không tìm thấy danh mục." });
        }

        var linkedArtworks = await _db.Artworks
            .Where(a => a.CategoryId == category.Id)
            .ToListAsync(cancellationToken);

        foreach (var artwork in linkedArtworks)
        {
            artwork.CategoryId = null;
        }

        category.IsDeleted = true;
        category.IsActive = false;
        category.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return (true, Array.Empty<string>());
    }
}

public record MergeTagsCommand(IReadOnlyList<Guid> SourceTagIds, Guid TargetTagId)
    : IRequest<(bool Success, int AffectedArtworks, string[] Errors)>;

public sealed class MergeTagsCommandHandler
    : IRequestHandler<MergeTagsCommand, (bool Success, int AffectedArtworks, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public MergeTagsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<(bool Success, int AffectedArtworks, string[] Errors)> Handle(MergeTagsCommand request, CancellationToken cancellationToken)
    {
        var sourceIds = (request.SourceTagIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty && id != request.TargetTagId)
            .Distinct()
            .ToList();

        if (sourceIds.Count == 0)
        {
            return (false, 0, new[] { "Vui lòng chọn ít nhất một tag nguồn khác với tag đích." });
        }

        var targetTag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == request.TargetTagId && !t.IsDeleted, cancellationToken);
        if (targetTag is null)
        {
            return (false, 0, new[] { "Không tìm thấy tag đích." });
        }

        var sourceTags = await _db.Tags
            .Where(t => sourceIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        if (sourceTags.Count == 0)
        {
            return (false, 0, new[] { "Không tìm thấy các tag nguồn cần gộp." });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var existingTargetArtworkIds = (await _db.ArtworkTags
            .Where(at => at.TagId == targetTag.Id)
            .Select(at => at.ArtworkId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var sourceLinks = await _db.ArtworkTags
            .Where(at => sourceIds.Contains(at.TagId))
            .ToListAsync(cancellationToken);

        var affectedArtworks = new HashSet<Guid>();
        foreach (var link in sourceLinks)
        {
            affectedArtworks.Add(link.ArtworkId);
            if (existingTargetArtworkIds.Add(link.ArtworkId))
            {
                _db.ArtworkTags.Add(new ArtworkTag
                {
                    ArtworkId = link.ArtworkId,
                    TagId = targetTag.Id
                });
            }

            _db.ArtworkTags.Remove(link);
        }

        _db.Tags.RemoveRange(sourceTags);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (true, affectedArtworks.Count, Array.Empty<string>());
    }
}

public record PromoteTagToCategoryCommand(
    Guid TagId,
    string? CategoryName,
    string? Description,
    int DisplayOrder,
    bool AssignOnlyUnassigned,
    bool DeleteTagAfterPromote) : IRequest<(bool Success, CategoryDto? Category, int AssignedArtworks, string[] Errors)>;

public sealed class PromoteTagToCategoryCommandHandler
    : IRequestHandler<PromoteTagToCategoryCommand, (bool Success, CategoryDto? Category, int AssignedArtworks, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public PromoteTagToCategoryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<(bool Success, CategoryDto? Category, int AssignedArtworks, string[] Errors)> Handle(
        PromoteTagToCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == request.TagId && !t.IsDeleted, cancellationToken);
        if (tag is null)
        {
            return (false, null, 0, new[] { "Không tìm thấy tag cần thăng hạng." });
        }

        var categoryName = string.IsNullOrWhiteSpace(request.CategoryName) ? tag.Name.Trim() : request.CategoryName.Trim();
        if (categoryName.Length is 0 or > 100)
        {
            return (false, null, 0, new[] { "Tên danh mục phải từ 1 đến 100 ký tự." });
        }

        var slug = SlugHelper.ToSlug(categoryName);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return (false, null, 0, new[] { "Không thể tạo slug hợp lệ từ tên danh mục." });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var category = await _db.ArtworkCategories
            .FirstOrDefaultAsync(c => c.Name == categoryName || c.Slug == slug, cancellationToken);

        if (category is null)
        {
            var maxOrder = await _db.ArtworkCategories
                .Where(c => !c.IsDeleted)
                .Select(c => (int?)c.DisplayOrder)
                .MaxAsync(cancellationToken) ?? 0;

            category = new ArtworkCategory
            {
                Name = categoryName,
                Slug = slug,
                Description = request.Description?.Trim(),
                DisplayOrder = request.DisplayOrder > 0 ? request.DisplayOrder : maxOrder + 1,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.ArtworkCategories.Add(category);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else if (category.IsDeleted)
        {
            category.Name = categoryName;
            category.Slug = slug;
            category.Description = request.Description?.Trim() ?? category.Description;
            category.IsActive = true;
            category.IsDeleted = false;
            category.UpdatedAt = now;
        }
        else
        {
            category.IsActive = true;
            category.UpdatedAt = now;
        }

        var taggedArtworkIds = await _db.ArtworkTags
            .Where(at => at.TagId == tag.Id)
            .Select(at => at.ArtworkId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var artworksToUpdate = await _db.Artworks
            .Where(a => taggedArtworkIds.Contains(a.Id) && !a.IsDeleted && (!request.AssignOnlyUnassigned || a.CategoryId == null))
            .ToListAsync(cancellationToken);

        foreach (var artwork in artworksToUpdate)
        {
            artwork.CategoryId = category.Id;
            artwork.Style = category.Name;
            artwork.UpdatedAt = now;
        }

        if (request.DeleteTagAfterPromote)
        {
            var links = await _db.ArtworkTags.Where(at => at.TagId == tag.Id).ToListAsync(cancellationToken);
            _db.ArtworkTags.RemoveRange(links);
            _db.Tags.Remove(tag);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var totalArtworks = await _db.Artworks.CountAsync(a => a.CategoryId == category.Id && !a.IsDeleted, cancellationToken);
        var dto = new CategoryDto(category.Id, category.Name, category.Slug, category.Description, category.DisplayOrder, category.IsActive, totalArtworks);
        return (true, dto, artworksToUpdate.Count, Array.Empty<string>());
    }
}
