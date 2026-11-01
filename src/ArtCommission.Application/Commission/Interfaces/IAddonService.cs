using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Domain.Entities.Commission;

namespace ArtCommission.Application.Commission.Interfaces;

public interface IAddonService
{
    Task<List<CreatorAddon>> ListCreatorAsync(Guid id, CancellationToken ct);
    Task<CreatorAddon> SaveAsync(SaveCreatorAddonRequest request, Guid userId, CancellationToken ct);
    Task<List<CommissionAddon>> ListAsync(Guid id, Guid userId, CancellationToken ct);
    Task<CommissionAddon> ProposeAsync(Guid id, ProposeAddonRequest request, Guid userId, CancellationToken ct);
    Task<CommissionAddon> RespondAsync(Guid id, Guid addonId, RespondAddonRequest request, Guid userId, CancellationToken ct);
}
