using System.Security.Claims;
using ArtCommission.API.Common;
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
        return BadRequest(ApiErrors.Create(400, "Bad Request", string.Join(" ", errors), HttpContext.TraceIdentifier));
    }

    protected IActionResult UnauthorizedEnvelope(string message = "Unauthorized access.")
    {
        return Unauthorized(ApiErrors.Create(401, "Unauthorized", message, HttpContext.TraceIdentifier));
    }

    protected IActionResult NotFoundEnvelope(params string[] errors)
    {
        return NotFound(ApiErrors.Create(404, "Not Found", string.Join(" ", errors), HttpContext.TraceIdentifier));
    }
}
