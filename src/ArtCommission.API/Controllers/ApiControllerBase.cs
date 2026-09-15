using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected Guid CurrentUserId
    {
        get
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }

    protected IActionResult OkEnvelope<T>(T data, object? meta = null)
    {
        return Ok(new
        {
            data,
            meta,
            error = (object?)null
        });
    }

    protected IActionResult BadRequestEnvelope(params string[] errors)
    {
        return BadRequest(new
        {
            data = (object?)null,
            meta = (object?)null,
            error = new
            {
                title = "Bad Request",
                details = errors
            }
        });
    }

    protected IActionResult UnauthorizedEnvelope(string message = "Unauthorized access.")
    {
        return Unauthorized(new
        {
            data = (object?)null,
            meta = (object?)null,
            error = new
            {
                title = "Unauthorized",
                details = new[] { message }
            }
        });
    }
}
