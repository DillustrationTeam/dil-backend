using ArtCommission.Application.ArtistStudio.Categories.DTOs;
using ArtCommission.Application.ArtistStudio.Categories.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

[Route("api/v1/categories")]
public class CategoriesController : ApiControllerBase
{
    /// <summary>Lấy danh sách các danh mục lớn đang hoạt động cho Creator Studio, Marketplace và /creators.</summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var items = await Mediator.Send(new GetCategoriesQuery(), cancellationToken);
        return OkEnvelope(items);
    }
}
