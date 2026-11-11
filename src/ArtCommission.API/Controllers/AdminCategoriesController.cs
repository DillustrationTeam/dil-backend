using ArtCommission.Application.ArtistStudio.Categories.Commands;
using ArtCommission.Application.ArtistStudio.Categories.DTOs;
using ArtCommission.Application.ArtistStudio.Categories.Queries;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Quản lý Danh mục lớn và Trending Tags (Admin & Moderator).
/// </summary>
[Authorize(Roles = $"{UserRoleNames.Moderator},{UserRoleNames.Administrator}")]
[Route("api/v1/admin/categories")]
public class AdminCategoriesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AdminCategoriesOverviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview([FromQuery] int trendingLimit = 30, CancellationToken cancellationToken = default)
    {
        var overview = await Mediator.Send(new AdminGetCategoriesQuery(trendingLimit), cancellationToken);
        return OkEnvelope(overview);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var (success, data, errors) = await Mediator.Send(
            new CreateCategoryCommand(request.Name, request.Slug, request.Description, request.DisplayOrder, request.IsActive),
            cancellationToken);

        return success && data is not null ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var (success, data, errors) = await Mediator.Send(
            new UpdateCategoryCommand(id, request.Name, request.Slug, request.Description, request.DisplayOrder, request.IsActive),
            cancellationToken);

        return success && data is not null ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken)
    {
        var (success, errors) = await Mediator.Send(new DeleteCategoryCommand(id), cancellationToken);
        return success ? OkEnvelope(new { deleted = true }) : BadRequestEnvelope(errors);
    }

    [HttpPost("tags/merge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MergeTags([FromBody] MergeTagsRequest request, CancellationToken cancellationToken)
    {
        var (success, affectedArtworks, errors) = await Mediator.Send(
            new MergeTagsCommand(request.SourceTagIds, request.TargetTagId),
            cancellationToken);

        return success
            ? OkEnvelope(new { merged = true, affectedArtworks })
            : BadRequestEnvelope(errors);
    }

    [HttpPost("tags/{tagId:guid}/promote")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PromoteTag(Guid tagId, [FromBody] PromoteTagToCategoryRequest request, CancellationToken cancellationToken)
    {
        var (success, category, assignedArtworks, errors) = await Mediator.Send(
            new PromoteTagToCategoryCommand(
                tagId,
                request.CategoryName,
                request.Description,
                request.DisplayOrder,
                request.AssignOnlyUnassigned,
                request.DeleteTagAfterPromote),
            cancellationToken);

        return success && category is not null
            ? OkEnvelope(new { category, assignedArtworks })
            : BadRequestEnvelope(errors);
    }
}
