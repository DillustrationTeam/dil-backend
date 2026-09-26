using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.ArtistStudio.Workstation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

[Authorize]
[Route("api/v1")]
public class CreatorWorkstationController : ApiControllerBase
{
    [HttpGet("creator/workstation")]
    public async Task<IActionResult> GetState(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetCreatorWorkstationQuery(CurrentUserId), cancellationToken);
        if (result is null)
        {
            return BadRequestEnvelope("Creator profile is required.");
        }

        return OkEnvelope(result);
    }

    [HttpPost("creator/workstation/terms")]
    public async Task<IActionResult> SaveTerms([FromBody] UpdateCreatorTermsRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreatorWorkstationMutationCommand("terms", CurrentUserId, Guid.Empty, request), cancellationToken);
        return result.Success ? OkEnvelope(result.Data) : BadRequestEnvelope(result.Errors);
    }

    [HttpPost("creator/workstation/auto-reply")]
    public async Task<IActionResult> SaveAutoReply([FromBody] UpdateAutoReplySettingRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreatorWorkstationMutationCommand("auto-reply", CurrentUserId, Guid.Empty, request), cancellationToken);
        return result.Success ? OkEnvelope(result.Data) : BadRequestEnvelope(result.Errors);
    }

    [HttpPost("creator/workstation/faqs")]
    public async Task<IActionResult> SaveFaq([FromBody] UpsertCreatorFaqRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreatorWorkstationMutationCommand("faq", CurrentUserId, Guid.Empty, request), cancellationToken);
        return result.Success ? OkEnvelope(result.Data) : BadRequestEnvelope(result.Errors);
    }

    [HttpPost("creator/workstation/work-items")]
    public async Task<IActionResult> CreateWorkItem([FromBody] CreateWorkItemRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreatorWorkstationMutationCommand("work-item", CurrentUserId, Guid.Empty, request), cancellationToken);
        return result.Success ? OkEnvelope(result.Data) : BadRequestEnvelope(result.Errors);
    }

    [HttpPut("creator/workstation/work-items/{id:guid}/stage")]
    public async Task<IActionResult> UpdateWorkItemStage(Guid id, [FromBody] UpdateWorkItemStageRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreatorWorkstationMutationCommand("work-stage", CurrentUserId, id, request), cancellationToken);
        return result.Success ? OkEnvelope(result.Data) : BadRequestEnvelope(result.Errors);
    }

    [HttpPost("creator/workstation/assets")]
    public async Task<IActionResult> CreateAsset([FromBody] CreateCreatorAssetRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreatorWorkstationMutationCommand("asset", CurrentUserId, Guid.Empty, request), cancellationToken);
        return result.Success ? OkEnvelope(result.Data) : BadRequestEnvelope(result.Errors);
    }

    [HttpDelete("creator/workstation/assets/{id:guid}")]
    public async Task<IActionResult> DeleteAsset(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreatorWorkstationMutationCommand("delete-asset", CurrentUserId, id), cancellationToken);
        return result.Success ? OkEnvelope(result.Data) : BadRequestEnvelope(result.Errors);
    }
}
