using System.Security.Claims;
using ArtCommission.API.Common;
using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers.v1;

[ApiController]
[Route("api/v1")]
[Authorize]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public class AddonsController(IAddonService service) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty
        ? id : throw new UnauthorizedAccessException("Authentication required.");
    private IActionResult Envelope<T>(T value) => Ok(new ApiResponse<T>(value));

    [HttpGet("creators/addons/presets"), AllowAnonymous]
    public IActionResult Presets() => Envelope(AddonRules.Presets);

    [HttpGet("creators/{id:guid}/addons"), AllowAnonymous]
    public async Task<IActionResult> CreatorAddons(Guid id, CancellationToken ct) => Envelope(await service.ListCreatorAsync(id, ct));

    [HttpPost("creators/addons"), Authorize(Roles = "Creator")]
    public async Task<IActionResult> Save(SaveCreatorAddonRequest r, CancellationToken ct) => Envelope(await service.SaveAsync(r, UserId, ct));

    [HttpGet("commissions/{id:guid}/addons")]
    public async Task<IActionResult> List(Guid id, CancellationToken ct) => Envelope(await service.ListAsync(id, UserId, ct));

    [HttpPost("commissions/{id:guid}/addons/propose")]
    public async Task<IActionResult> Propose(Guid id, ProposeAddonRequest r, CancellationToken ct) => Envelope(await service.ProposeAsync(id, r, UserId, ct));

    [HttpPost("commissions/{id:guid}/addons/{addonId:guid}/respond")]
    public async Task<IActionResult> Respond(Guid id, Guid addonId, RespondAddonRequest r, CancellationToken ct) => Envelope(await service.RespondAsync(id, addonId, r, UserId, ct));
}
