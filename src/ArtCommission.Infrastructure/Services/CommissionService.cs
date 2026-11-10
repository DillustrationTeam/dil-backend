using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Commission.Validators;
using ArtCommission.Application.Dispute.CommissionDispute.DTOs;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Commission;
using ArtCommission.Domain.Entities.Dispute;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Infrastructure.Services;

public class CommissionService : ICommissionService
{
    private const string CurrentWorkroomTermsVersion = "1.0";
    public const decimal StagedEscrowThresholdVnd = 2_000_000m;
    private readonly AppDbContext _dbContext;
    private readonly IWalletService _walletService;
    private readonly IWatermarkService _watermarkService;
    private readonly IStorageService _storageService;
    private readonly IVoucherCheckService _voucherCheckService;
    private readonly INotificationPublisher? _notifications;
    private readonly ILogger<CommissionService>? _logger;
    private readonly IEmailService? _emailService;

    public CommissionService(AppDbContext dbContext, IWalletService walletService, IWatermarkService watermarkService,
        IStorageService storageService, IVoucherCheckService voucherCheckService,
        INotificationPublisher? notifications = null, ILogger<CommissionService>? logger = null, IEmailService? emailService = null)
    {
        _dbContext = dbContext;
        _walletService = walletService;
        _watermarkService = watermarkService;
        _storageService = storageService;
        _voucherCheckService = voucherCheckService;
        _notifications = notifications;
        _logger = logger;
        _emailService = emailService;
    }

    private static void RequireClient(Commission commission, Guid userId)
    {
        if (userId == Guid.Empty || commission.ClientId != userId)
            throw new UnauthorizedAccessException("You are not the client for this commission.");
    }

    private static void RequireCreator(Commission commission, Guid userId)
    {
        if (userId == Guid.Empty || commission.CreatorId != userId)
            throw new UnauthorizedAccessException("You are not the creator for this commission.");
    }

    private static void RequireParticipant(Commission commission, Guid userId)
    {
        if (userId == Guid.Empty || (commission.ClientId != userId && commission.CreatorId != userId))
            throw new UnauthorizedAccessException("You are not a participant in this commission.");
    }

    private static void RequireFundedWork(Commission commission)
    {
        if (commission.Status != CommissionStatus.InProgress ||
            commission.EscrowStatus is not (EscrowStatus.Deposited or EscrowStatus.PartialReleased) ||
            commission.EscrowHeldAmount <= 0)
            throw new InvalidOperationException("The commission must be accepted and funded before milestone work.");
    }

    private static void RequireCurrentMilestone(Commission commission, Milestone milestone)
    {
        if (milestone.Sequence != commission.CurrentStage)
            throw new InvalidOperationException("Only the current milestone can be changed.");
    }

    public async Task<CommissionDto> CreateCommissionAsync(CreateCommissionRequest request, Guid clientId, CancellationToken cancellationToken = default)
    {
        RequireUser(clientId);
        var validation = new CreateCommissionRequestValidator().Validate(request);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));

        var creator = await _dbContext.CreatorProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => (p.Id == request.CreatorId || p.UserId == request.CreatorId) && !p.IsDeleted, cancellationToken);
        if (creator is null || !creator.IsAcceptingOrders)
            throw new ArgumentException("Creator hiện không nhận đơn.");

        if (string.IsNullOrWhiteSpace(creator.RateCardJson))
            throw new ArgumentException("Creator chưa cấu hình bảng giá.");
        ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard.RateCardPackageDto? selectedPackage;
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard.RateCardIdConverter() } };
            var packages = System.Text.Json.JsonSerializer.Deserialize<List<ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard.RateCardPackageDto>>(creator.RateCardJson, options);
            selectedPackage = packages?.FirstOrDefault(p => p != null && p.Id == request.PackageId);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new ArgumentException("Bảng giá Creator không hợp lệ. Vui lòng liên hệ Creator cập nhật.");
        }
        if (selectedPackage is null)
            throw new ArgumentException("Gói giá không tồn tại hoặc đã bị thay đổi. Vui lòng chọn lại.");
        await EnsurePackageSlotAvailableAsync(creator.Id, creator.RateCardJson, selectedPackage.Id, asArgumentError: true, cancellationToken);
        var stages = selectedPackage.Milestones;
        if (selectedPackage.Price <= 0 || stages is null || stages.Count == 0
            || stages.Any(stage => stage is null || stage.Price <= 0 || stage.MaxRevisions < 0 || stage.MaxRevisions > 20 || string.IsNullOrWhiteSpace(stage.Title))
            || !stages.OrderBy(stage => stage.Sequence).Select(stage => stage.Sequence).SequenceEqual(Enumerable.Range(1, stages.Count))
            || stages.Sum(stage => stage.Price) != selectedPackage.Price)
            throw new ArgumentException("Giá và milestone của gói không hợp lệ. Vui lòng liên hệ Creator cập nhật.");
        var totalPrice = selectedPackage.Price;
        var milestones = stages.OrderBy(stage => stage.Sequence).Select(stage => new Milestone
        {
            Sequence = stage.Sequence, Title = stage.Title, Price = stage.Price, MaxRevisions = stage.MaxRevisions > 0 ? stage.MaxRevisions : (selectedPackage.MaxRevisions > 0 ? selectedPackage.MaxRevisions : 3), Status = MilestoneStatus.Pending
        }).ToList();
        var creatorTerms = await _dbContext.CreatorTerms.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CreatorProfileId == creator.Id && !x.IsDeleted, cancellationToken);
        var multiplier = request.LicenseType == "Commercial" ? creatorTerms?.CommercialLicenseMultiplier ?? 1.5m : 1m;
        totalPrice = Math.Round(totalPrice * multiplier, 2, MidpointRounding.AwayFromZero);
        foreach (var milestone in milestones)
            milestone.Price = Math.Round(milestone.Price * multiplier, 2, MidpointRounding.AwayFromZero);
        milestones[^1].Price += totalPrice - milestones.Sum(x => x.Price);

        var discountAmount = 0.00m;
        Guid? voucherId = null;
        if (!string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            var voucherCheck = await _voucherCheckService.CheckAsync(
                clientId, request.VoucherCode, totalPrice, nameof(Commission), null, cancellationToken);
            if (!voucherCheck.Success || voucherCheck.Voucher is null)
                throw new ArgumentException(voucherCheck.Errors.FirstOrDefault() ?? "Mã giảm giá không hợp lệ.");
            if (voucherCheck.FinalAmount <= 0)
                throw new ArgumentException("Mã giảm giá phải để lại số tiền thanh toán lớn hơn 0.");
            voucherId = voucherCheck.Voucher.Id;
            discountAmount = voucherCheck.DiscountAmount;
        }
        var finalPrice = totalPrice - discountAmount;

        if (request.AddonIds is null || request.AddonIds.Count > 50 || request.AddonIds.Distinct().Count() != request.AddonIds.Count)
            throw new ArgumentException("Danh sách phụ phí không hợp lệ hoặc trùng lặp.");
        var bookingAddons = await _dbContext.Set<CreatorAddon>().AsNoTracking()
            .Where(x => request.AddonIds.Contains(x.Id) && x.CreatorId == creator.Id && x.IsActive && !x.IsDeleted
                && (x.PackageId == null || x.PackageId == request.PackageId)).ToListAsync(cancellationToken);
        if (bookingAddons.Count != request.AddonIds.Count) throw new ArgumentException("Phụ phí đã thay đổi. Vui lòng chọn lại.");
        foreach (var addon in bookingAddons)
        {
            AddonRules.Validate(addon.Title, addon.Price, addon.AllowedMilestone, addon.ExtendDays, addon.Description);
            var milestone = milestones.FirstOrDefault(x => AddonRules.Allows(addon.AllowedMilestone, AddonRules.Stage(x.Sequence, milestones.Count)))
                ?? throw new ArgumentException("Gói không có mốc phù hợp với phụ phí.");
            milestone.Price += addon.Price;
            totalPrice += addon.Price;
            finalPrice += addon.Price;
        }
        var extendDays = bookingAddons.Sum(x => x.ExtendDays);
        if (extendDays > 0 && request.DeadlineAt is null) throw new ArgumentException("Cần chọn deadline khi phụ phí có gia hạn.");

        var commission = new Commission
        {
            Title = request.Title,
            Description = request.Description,
            ClientId = clientId,
            CreatorId = creator.Id,
            PackageId = request.PackageId,
            VoucherId = voucherId,
            LicenseType = request.LicenseType,
            LicenseMultiplierApplied = multiplier,
            CreatorTermsSnapshotJson = ArtCommission.Application.ArtistStudio.DTOs.CreatorTermsHelper.SerializeSnapshot(creatorTerms),
            TotalPrice = totalPrice,
            DiscountAmount = discountAmount,
            FinalPrice = finalPrice,
            EscrowHeldAmount = 0.00m,
            DisbursedAmount = 0.00m,
            EscrowStatus = EscrowStatus.Pending,
            CurrentStage = 1,
            Status = CommissionStatus.PendingAcceptance,
            DeadlineAt = request.DeadlineAt?.AddDays(extendDays)
        };

        foreach (var m in milestones)
        {
            commission.Milestones.Add(m);
        }

        _dbContext.Commissions.Add(commission);
        foreach (var addon in bookingAddons)
        {
            var sequence = milestones.First(x => AddonRules.Allows(addon.AllowedMilestone, AddonRules.Stage(x.Sequence, milestones.Count))).Sequence;
            _dbContext.Add(new CommissionAddon {
                CommissionId = commission.Id, ProposedById = clientId, Title = addon.Title, Description = addon.Description,
                Price = addon.Price, AllowedMilestone = addon.AllowedMilestone, MilestoneSequence = sequence,
                ExtendDays = addon.ExtendDays, IsPreset = addon.PresetKey is not null, Status = "Accepted"
            });
        }
        await _dbContext.SaveChangesAsync(cancellationToken);

        await PublishSafeAsync(
            creator.UserId, NotificationType.CommissionStatusChanged,
            "Bạn có yêu cầu đặt vẽ mới",
            $"Khách hàng đã gửi yêu cầu “{commission.Title}”.",
            commission.Id, $"CommissionCreated:{commission.Id}:{creator.UserId}", cancellationToken,
            refType: "CreatorCommissionRequests");



        return MapToDto(commission);
    }

    public async Task<(List<CommissionDto> Items, int TotalItems)> GetCommissionsAsync(string? status, string? role, Guid userId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        RequireUser(userId);
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentException("Invalid pagination.");
        var creatorProfileId = await CreatorProfileIdAsync(userId, cancellationToken);
        var query = _dbContext.Commissions.AsNoTracking().Where(c => !c.IsDeleted);

        if (string.Equals(role, "Client", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.ClientId == userId);
        }
        else if (string.Equals(role, "Creator", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.CreatorId == creatorProfileId);
        }
        else if (string.IsNullOrWhiteSpace(role))
            query = query.Where(c => c.ClientId == userId || c.CreatorId == creatorProfileId);
        else throw new ArgumentException("Invalid role filter.");

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<CommissionStatus>(status, true, out var parsedStatus))
                throw new ArgumentException("Invalid commission status filter.");
            query = query.Where(c => c.Status == parsedStatus);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var commissionEntities = await query.OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = commissionEntities.Select(c => MapToDto(c)).ToList();
        var clientIds = items.Select(item => item.ClientId).Distinct().ToArray();
        var clientNames = await _dbContext.Users.AsNoTracking()
            .Where(user => clientIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);
        var creatorIds = items.Select(item => item.CreatorId).Distinct().ToArray();
        var creatorNames = await _dbContext.CreatorProfiles.AsNoTracking()
            .Where(profile => creatorIds.Contains(profile.Id) && !profile.IsDeleted)
            .ToDictionaryAsync(profile => profile.Id, profile => profile.DisplayName, cancellationToken);

        foreach (var item in items)
        {
            if (clientNames.TryGetValue(item.ClientId, out var clientName))
            {
                item.ClientName = clientName;
            }
            if (creatorNames.TryGetValue(item.CreatorId, out var creatorName))
            {
                item.CreatorName = creatorName;
            }
        }

        return (items, totalItems);
    }

    public async Task<CommissionDetailDto?> GetCommissionByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        RequireUser(userId);
        var creatorProfileId = await CreatorProfileIdAsync(userId, cancellationToken);
        var commission = await _dbContext.Commissions
            .Include(c => c.Milestones)
            .Include(c => c.Reviews)
            .Include(c => c.Disputes)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted && (c.ClientId == userId || c.CreatorId == creatorProfileId), cancellationToken);

        if (commission == null) return null;

        var detailDto = MapToDetailDto(commission);
        detailDto.CreatorName = await _dbContext.CreatorProfiles.AsNoTracking()
            .Where(profile => profile.Id == commission.CreatorId && !profile.IsDeleted)
            .Select(profile => profile.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
        return detailDto;
    }

    public async Task<CommissionDto> RespondCommissionAsync(Guid id, RespondCommissionRequest request, Guid creatorId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(id, creatorId, creator: true, cancellationToken);
        var validation = new RespondCommissionRequestValidator().Validate(request);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        if (commission.Status != CommissionStatus.PendingAcceptance)
            throw new InvalidOperationException("Commission cannot be changed in this state.");

        if (string.Equals(request.Action, "Accept", StringComparison.OrdinalIgnoreCase))
        {
            await EnsurePackageSlotAvailableAsync(commission.CreatorId, null, commission.PackageId, asArgumentError: false, cancellationToken);
            commission.Status = CommissionStatus.InProgress;
        }
        else if (string.Equals(request.Action, "Reject", StringComparison.OrdinalIgnoreCase))
        {
            commission.Status = CommissionStatus.Cancelled;
        }
        else if (string.Equals(request.Action, "Negotiate", StringComparison.OrdinalIgnoreCase))
        {
            commission.Status = CommissionStatus.Negotiating;
            if (request.NegotiatePrice.HasValue)
            {
                if (request.NegotiatePrice.Value <= 0) throw new ArgumentException("Negotiated price must be positive.");
                var milestones = await _dbContext.Milestones.Where(m => m.CommissionId == id).OrderBy(m => m.Sequence).ToListAsync(cancellationToken);
                if (milestones.Count == 0 || milestones[^1].Price + request.NegotiatePrice.Value - commission.TotalPrice <= 0)
                    throw new ArgumentException("Negotiated price cannot produce an invalid milestone.");
                milestones[^1].Price += request.NegotiatePrice.Value - commission.TotalPrice;
                commission.TotalPrice = request.NegotiatePrice.Value;
                if (commission.VoucherId.HasValue)
                {
                    var voucher = await _dbContext.Vouchers.AsNoTracking()
                        .SingleAsync(v => v.Id == commission.VoucherId.Value, cancellationToken);
                    commission.DiscountAmount = VoucherCheckService.CalculateDiscount(
                        voucher.DiscountType, voucher.DiscountValue, voucher.MaxDiscountAmount,
                        commission.TotalPrice - await BookingAddonTotalAsync(id, cancellationToken));
                }
                commission.FinalPrice = request.NegotiatePrice.Value - commission.DiscountAmount;
                if (commission.FinalPrice <= 0)
                    throw new ArgumentException("Negotiated price must remain positive after discount.");
            }
        }

        commission.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await PublishSafeAsync(
            commission.ClientId, NotificationType.CommissionStatusChanged,
            "Yêu cầu đặt vẽ đã được phản hồi",
            $"Creator đã {request.Action.ToLowerInvariant()} yêu cầu “{commission.Title}”.",
            commission.Id, $"CommissionResponse:{commission.Id}:{commission.Status}:{commission.ClientId}", cancellationToken,
            refType: commission.Status == CommissionStatus.InProgress ? "CommissionCheckout" : "ClientCommissionRequest");

        return MapToDto(commission);
    }

    public async Task<CommissionDto> RespondToCounterofferAsync(Guid id, bool accept, Guid clientId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(id, clientId, creator: false, cancellationToken);
        if (commission.Status != CommissionStatus.Negotiating)
            throw new InvalidOperationException("Commission has no pending counter-offer.");

        if (accept)
        {
            await EnsurePackageSlotAvailableAsync(commission.CreatorId, null, commission.PackageId, asArgumentError: false, cancellationToken);
        }
        commission.Status = accept ? CommissionStatus.InProgress : CommissionStatus.Cancelled;
        commission.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var creatorUserId = await CreatorUserIdAsync(commission.CreatorId, cancellationToken);
        await PublishSafeAsync(
            creatorUserId, NotificationType.CommissionStatusChanged,
            "Khách hàng đã phản hồi đề nghị giá",
            $"Khách hàng đã {(accept ? "chấp nhận" : "từ chối")} đề nghị giá cho “{commission.Title}”.",
            commission.Id, $"CounterofferResponse:{commission.Id}:{accept}:{creatorUserId}", cancellationToken,
            refType: "CreatorCommissionRequests");

        return MapToDto(commission);
    }

    public async Task<CommissionDto> DepositEscrowAsync(Guid id, Guid clientId, string paymentMethod, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(paymentMethod, "Wallet", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only wallet escrow is supported.");
        await using var tx = await BeginTransactionAsync(cancellationToken);
        var commission = await OwnedCommissionAsync(id, clientId, creator: false, cancellationToken);
        await _dbContext.Entry(commission).Collection(c => c.Milestones).LoadAsync(cancellationToken);
        if (commission.ClientTermsAcceptedAt is null || commission.ClientTermsVersion != CurrentWorkroomTermsVersion)
            throw new InvalidOperationException("Client must accept the current Workroom terms before depositing escrow.");
        if (commission.Status != CommissionStatus.InProgress || commission.EscrowStatus != EscrowStatus.Pending)
            throw new InvalidOperationException("Commission is not ready for escrow deposit.");
        var isInitialDeposit = commission.DisbursedAmount == 0m && commission.EscrowHeldAmount == 0m;
        if (isInitialDeposit && commission.VoucherId.HasValue)
        {
            var voucher = await _dbContext.Vouchers.SingleAsync(v => v.Id == commission.VoucherId.Value, cancellationToken);
            var voucherCheck = await _voucherCheckService.CheckAsync(
                clientId, voucher.VoucherCode, commission.TotalPrice - await BookingAddonTotalAsync(id, cancellationToken),
                nameof(Commission), commission.Id, cancellationToken);
            if (!voucherCheck.Success)
                throw new InvalidOperationException(voucherCheck.Errors.FirstOrDefault() ?? "Mã giảm giá không còn hợp lệ.");

            if (_dbContext.Database.IsRelational())
            {
                var affected = await _dbContext.Vouchers
                    .Where(v => v.Id == voucher.Id && v.IsActive
                        && (v.UsageLimit == null || v.UsedCount < v.UsageLimit.Value))
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(v => v.UsedCount, v => v.UsedCount + 1)
                        .SetProperty(v => v.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);
                if (affected == 0) throw new InvalidOperationException("Mã giảm giá đã hết lượt sử dụng.");
            }
            else
            {
                voucher.UsedCount++;
                voucher.UpdatedAt = DateTimeOffset.UtcNow;
            }

            commission.DiscountAmount = voucherCheck.DiscountAmount;
            commission.FinalPrice = commission.TotalPrice - voucherCheck.DiscountAmount;
            _dbContext.VoucherRedemptions.Add(new VoucherRedemption
            {
                VoucherId = voucher.Id,
                UserId = clientId,
                RefType = nameof(Commission),
                RefId = commission.Id,
                DiscountAmount = voucherCheck.DiscountAmount,
                OrderAmount = commission.TotalPrice,
                FinalAmount = commission.FinalPrice
            });
        }
        if (isInitialDeposit)
            await AllocateFundedMilestonesAsync(commission, cancellationToken);
        var amountToHold = ComputeNextEscrowDepositAmount(commission);
        if (amountToHold <= 0m)
            throw new InvalidOperationException("Commission is not ready for escrow deposit.");
        var wallet = await _walletService.GetOrCreateWalletAsync(clientId, cancellationToken);
        if (wallet.Status != WalletStatus.Active) throw new InvalidOperationException("Wallet is inactive.");
        var isStaged = commission.FinalPrice >= StagedEscrowThresholdVnd;
        var currentMilestone = commission.Milestones.FirstOrDefault(m => !m.IsDeleted && m.Sequence == commission.CurrentStage);
        var holdRefType = isStaged && currentMilestone is not null ? nameof(Milestone) : nameof(Commission);
        var holdRefId = isStaged && currentMilestone is not null ? currentMilestone.Id : commission.Id;
        var holdNote = isStaged
            ? $"Commission staged escrow deposit (Stage {commission.CurrentStage})"
            : "Commission escrow deposit";
        await _walletService.HoldFundsAsync(wallet, WalletTransactionType.EscrowHold, amountToHold,
            holdRefType, holdRefId, holdNote, cancellationToken);

        commission.EscrowHeldAmount += amountToHold;
        commission.EscrowStatus = commission.DisbursedAmount > 0m
            ? EscrowStatus.PartialReleased
            : EscrowStatus.Deposited;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null)
            await tx.CommitAsync(cancellationToken);

        var creatorUserId = await CreatorUserIdAsync(commission.CreatorId, cancellationToken);
        var fundedMilestone = commission.Milestones.FirstOrDefault(m => !m.IsDeleted && m.Sequence == commission.CurrentStage);
        var stageSummary = isStaged
            ? $"cho Mốc #{commission.CurrentStage}{(string.IsNullOrWhiteSpace(fundedMilestone?.Title) ? string.Empty : $" ({fundedMilestone.Title})")}"
            : "toàn bộ 100% giá trị đơn hàng";
        await PublishSafeAsync(
            creatorUserId, NotificationType.EscrowStatusChanged,
            isStaged ? $"Đã ký quỹ Mốc #{commission.CurrentStage}" : "Đơn đặt vẽ đã được ký quỹ 100%",
            $"Khách hàng đã ký quỹ {amountToHold:N0} VND {stageSummary} cho “{commission.Title}”. Bạn có thể bắt đầu thực hiện.",
            commission.Id, $"EscrowDeposited:{commission.Id}:{commission.CurrentStage}:{creatorUserId}", cancellationToken);

        // Gửi email biên nhận ký quỹ Escrow cho khách hàng
        try
        {
            var clientUser = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == clientId, cancellationToken);
            if (clientUser?.Email != null && _emailService != null)
            {
                var creatorUser = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == creatorUserId, cancellationToken);
                var clientName = clientUser.FullName ?? "Khách hàng";
                var creatorName = creatorUser?.FullName ?? "Creator";
                var mList = commission.Milestones.OrderBy(m => m.Sequence)
                    .Select(m => (m.Title ?? $"Mốc #{m.Sequence}", commission.FinalPrice > 0 ? (int)Math.Round((double)m.Price / (double)commission.FinalPrice * 100) : 33, m.Price))
                    .ToList();
                await _emailService.SendEscrowReceiptEmailAsync(
                    clientUser.Email, clientName, creatorName, commission.Title,
                    amountToHold, commission.Id.ToString(), commission.LicenseType, mList, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Không thể gửi email biên lai ký quỹ cho commission {CommissionId}", commission.Id);
        }

        return MapToDto(commission);
    }

    public async Task<CommissionDto> AcceptWorkroomTermsAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var commission = await PartyCommissionAsync(id, userId, cancellationToken);
        if (commission.Status is CommissionStatus.Completed or CommissionStatus.Cancelled)
            throw new InvalidOperationException("Workroom terms cannot be accepted for a closed commission.");

        var now = DateTimeOffset.UtcNow;
        if (commission.ClientId == userId)
        {
            commission.ClientTermsAcceptedAt = now;
            commission.ClientTermsVersion = CurrentWorkroomTermsVersion;
        }
        else
        {
            commission.CreatorTermsAcceptedAt = now;
            commission.CreatorTermsVersion = CurrentWorkroomTermsVersion;
        }

        commission.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(commission);
    }

    public async Task<MilestoneDto> SubmitMilestoneWipAsync(Guid commissionId, Guid milestoneId, string? creatorNote, Stream? fileStream, string? contentType, Guid creatorId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(commissionId, creatorId, creator: true, cancellationToken);
        if (commission.CreatorTermsAcceptedAt is null || commission.CreatorTermsVersion != CurrentWorkroomTermsVersion)
            throw new InvalidOperationException("Creator must accept the current Workroom terms before submitting milestone work.");
        if (commission.Status != CommissionStatus.InProgress || commission.EscrowStatus is not (EscrowStatus.Deposited or EscrowStatus.PartialReleased))
            throw new InvalidOperationException("Commission is not funded and in progress.");
        if (fileStream == null || contentType == null) throw new ArgumentException("WIP file is required.");
        var milestone = await _dbContext.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.CommissionId == commissionId, cancellationToken);
        if (milestone == null) throw new KeyNotFoundException("Không tìm thấy cột mốc milestone.");
        if (milestone.Sequence != commission.CurrentStage || milestone.Status is not (MilestoneStatus.Pending or MilestoneStatus.RevisionRequested))
            throw new InvalidOperationException("Milestone cannot be submitted in this state.");
        if (commission.EscrowHeldAmount < milestone.Price)
            throw new InvalidOperationException("Current milestone escrow has not been deposited.");

        // Upload original
        var originalKey = $"commissions/{commissionId}/milestone-{milestone.Sequence}-original-{Guid.NewGuid()}.jpg";
        var originalUrl = await _storageService.UploadPrivateAsync(fileStream, originalKey, contentType, cancellationToken);

        // Reset stream for watermark
        fileStream.Position = 0;
        using var watermarkedStream = await _watermarkService.ApplyWatermarkAsync(fileStream, "PROTECTED DEMO · DO NOT COPY", cancellationToken);

        var watermarkedKey = $"commissions/{commissionId}/milestone-{milestone.Sequence}-watermarked-{Guid.NewGuid()}.png";
        var watermarkedUrl = await _storageService.UploadPublicAsync(watermarkedStream, watermarkedKey, "image/png", cancellationToken);


        milestone.OriginalWipUrl = originalUrl;
        milestone.WipPreviewUrl = watermarkedUrl;
        milestone.WatermarkedUrl = watermarkedUrl;
        milestone.CreatorNote = creatorNote;
        var wasRevision = milestone.Status == MilestoneStatus.RevisionRequested;
        milestone.Status = MilestoneStatus.Submitted;
        milestone.SubmittedAt = DateTimeOffset.UtcNow;
        milestone.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await PublishSafeAsync(
            commission.ClientId, NotificationType.CommissionStatusChanged,
            wasRevision ? $"Creator đã gửi bản chỉnh sửa Mốc #{milestone.Sequence}" : $"Creator đã nộp phác thảo Mốc #{milestone.Sequence}",
            wasRevision
                ? $"Creator đã gửi lại bản chỉnh sửa cho Mốc #{milestone.Sequence} ({milestone.Title}) trên đơn “{commission.Title}”. Vui lòng kiểm tra (tự động duyệt sau 72h nếu chưa có phản hồi)."
                : $"Creator đã nộp bản phác thảo Mốc #{milestone.Sequence} ({milestone.Title}) cho “{commission.Title}”. Vui lòng kiểm tra (tự động duyệt sau 72h nếu chưa có phản hồi).",
            commission.Id, $"MilestoneSubmitted:{milestone.Id}:{milestone.RevisionCount}:{milestone.SubmittedAt:O}", cancellationToken);

        return MapToMilestoneDto(milestone);
    }

    public async Task<MilestoneDto> ApproveMilestoneAsync(Guid commissionId, Guid milestoneId, Guid clientId, CancellationToken cancellationToken = default)
    {
        await using var tx = await BeginTransactionAsync(cancellationToken);
        var commission = await OwnedCommissionAsync(commissionId, clientId, creator: false, cancellationToken);
        await _dbContext.Entry(commission).Collection(c => c.Milestones).LoadAsync(cancellationToken);
        if (commission.Status != CommissionStatus.InProgress || commission.EscrowStatus is not (EscrowStatus.Deposited or EscrowStatus.PartialReleased))
            throw new InvalidOperationException("Commission is not ready for milestone approval.");

        var milestone = commission.Milestones.FirstOrDefault(m => m.Id == milestoneId);
        if (milestone == null) throw new KeyNotFoundException("Không tìm thấy cột mốc milestone.");
        if (milestone.Status != MilestoneStatus.Submitted || milestone.Sequence != commission.CurrentStage || milestone.Price > commission.EscrowHeldAmount)
            throw new InvalidOperationException("Milestone cannot be approved in this state.");

        await ApproveMilestoneCoreAsync(commission, milestone, clientId, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null)
            await tx.CommitAsync(cancellationToken);

        await PublishMilestoneApprovedNotificationsAsync(commission, milestone, isAutoAccepted: false, cancellationToken);

        return MapToMilestoneDto(milestone);
    }

    private async Task ApproveMilestoneCoreAsync(Commission commission, Milestone milestone, Guid clientId, CancellationToken cancellationToken)
    {
        var clientWallet = await _walletService.GetOrCreateWalletAsync(clientId, cancellationToken);
        var creatorUserId = await CreatorUserIdAsync(commission.CreatorId, cancellationToken);
        var creatorWallet = await _walletService.GetOrCreateWalletAsync(creatorUserId, cancellationToken);
        await _walletService.ReleaseFundsAsync(clientWallet, WalletTransactionType.EscrowRelease, milestone.Price,
            nameof(Milestone), milestone.Id, "Commission milestone release", cancellationToken);
        await _walletService.CreditAsync(creatorWallet, WalletTransactionType.EscrowReceive, milestone.Price,
            nameof(Milestone), milestone.Id, "Commission milestone earning", cancellationToken);

        var now = DateTimeOffset.UtcNow;
        milestone.Status = MilestoneStatus.Approved;
        milestone.ApprovedAt = now;
        milestone.UpdatedAt = now;

        commission.DisbursedAmount += milestone.Price;
        commission.EscrowHeldAmount = Math.Max(0m, commission.EscrowHeldAmount - milestone.Price);
        commission.CurrentStage += 1;
        commission.EscrowStatus = commission.EscrowHeldAmount == 0m && commission.DisbursedAmount >= commission.FinalPrice
            ? EscrowStatus.Released
            : commission.EscrowHeldAmount > 0m
                ? EscrowStatus.PartialReleased
                : EscrowStatus.Pending;
        commission.UpdatedAt = now;
    }

    public async Task<MilestoneDto> RequestMilestoneRevisionAsync(Guid commissionId, Guid milestoneId, RequestRevisionRequest request, Guid clientId, CancellationToken cancellationToken = default)
    {
        var validation = new RequestRevisionRequestValidator().Validate(request);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));

        var commission = await OwnedCommissionAsync(commissionId, clientId, creator: false, cancellationToken);
        if (commission.Status != CommissionStatus.InProgress) throw new InvalidOperationException("Commission is not in progress.");
        var milestone = await _dbContext.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.CommissionId == commissionId, cancellationToken);
        if (milestone == null) throw new KeyNotFoundException("Không tìm thấy cột mốc milestone.");
        if (milestone.Status != MilestoneStatus.Submitted || milestone.Sequence != commission.CurrentStage)
            throw new InvalidOperationException("Milestone cannot be revised in this state.");

        if (milestone.RevisionCount >= milestone.MaxRevisions)
            throw new InvalidOperationException("Đã vượt quá số lần sửa tối đa cho cột mốc này.");

        milestone.Status = MilestoneStatus.RevisionRequested;
        milestone.RevisionCount += 1;
        milestone.RevisionFeedback = request.FeedbackComment;
        milestone.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var creatorUserId = await CreatorUserIdAsync(commission.CreatorId, cancellationToken);
        var remainingRevisions = Math.Max(0, milestone.MaxRevisions - milestone.RevisionCount);
        await PublishSafeAsync(
            creatorUserId, NotificationType.CommissionStatusChanged,
            $"Yêu cầu chỉnh sửa Mốc #{milestone.Sequence}",
            $"Khách hàng yêu cầu chỉnh sửa Mốc #{milestone.Sequence} ({milestone.Title}) trên đơn “{commission.Title}”. Còn {remainingRevisions} lượt sửa.",
            commission.Id, $"MilestoneRevision:{milestone.Id}:{milestone.RevisionCount}", cancellationToken);

        return MapToMilestoneDto(milestone);
    }

    public Task<CommissionDto> DeliverFinalWorkAsync(Guid id, Stream fileStream, string contentType, string fileName, Guid creatorId, CancellationToken cancellationToken = default)
    {
        if (fileStream is null) throw new ArgumentException("Final deliverable file is required.");
        var item = new DeliverableUploadItem(fileStream, contentType, fileName, fileStream.CanSeek ? fileStream.Length : 0);
        return DeliverFinalWorkAsync(id, new[] { item }, creatorId, cancellationToken);
    }

    private sealed record StoredDeliverableEntry(
        string Id,
        string Name,
        string StorageKey,
        long SizeBytes,
        string Sha256,
        DateTimeOffset UploadedAt);

    private sealed record StoredDeliverablesManifest(string Version, List<StoredDeliverableEntry> Files);

    private static string MakeUniqueEntryName(string fileName, HashSet<string> usedNames)
    {
        if (usedNames.Add(fileName)) return fileName;
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var idx = 1;
        while (!usedNames.Add($"{stem}_{idx}{ext}")) idx++;
        return $"{stem}_{idx}{ext}";
    }

    private static string ComputeManifestVersion(IEnumerable<StoredDeliverableEntry> entries)
    {
        var raw = string.Join("\n", entries.Select(e => $"{e.Id}|{e.StorageKey}|{e.Sha256}|{e.SizeBytes}"));
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task<StoredDeliverablesManifest> LoadStoredManifestAsync(Milestone? lastMilestone, CancellationToken ct)
    {
        var url = lastMilestone?.FinalDeliverableUrl;
        if (string.IsNullOrWhiteSpace(url))
            return new StoredDeliverablesManifest(string.Empty, []);

        if (url.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            await using var stream = await _storageService.OpenPrivateReadAsync(url, ct);
            if (stream is not null)
            {
                var parsed = await System.Text.Json.JsonSerializer.DeserializeAsync<StoredDeliverablesManifest>(stream, cancellationToken: ct);
                if (parsed is not null)
                {
                    var version = string.IsNullOrWhiteSpace(parsed.Version) ? ComputeManifestVersion(parsed.Files) : parsed.Version;
                    return new StoredDeliverablesManifest(version, parsed.Files);
                }
            }
        }

        var fileName = Path.GetFileName(url);
        if (fileName.Length > 37 && fileName[36] == '-' && Guid.TryParse(fileName[..36], out _))
            fileName = fileName[37..];
        long size = 0;
        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
        await using (var existing = await _storageService.OpenPrivateReadAsync(url, ct))
        {
            if (existing is not null)
            {
                if (existing.CanSeek) size = existing.Length;
                sha = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(existing, ct)).ToLowerInvariant();
            }
        }
        var legacy = new StoredDeliverableEntry("legacy", fileName, url, size, sha, lastMilestone?.SubmittedAt ?? DateTimeOffset.UtcNow);
        return new StoredDeliverablesManifest(ComputeManifestVersion([legacy]), [legacy]);
    }

    private async Task<DeliverablesManifestDto> SaveManifestAsync(
        Guid commissionId,
        Milestone lastMilestone,
        List<StoredDeliverableEntry> entries,
        CancellationToken ct)
    {
        var version = ComputeManifestVersion(entries);
        var manifest = new StoredDeliverablesManifest(version, entries);
        var jsonBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(manifest);
        await using var ms = new MemoryStream(jsonBytes);
        var manifestKey = $"commissions/{commissionId}/final/manifest-{Guid.NewGuid():N}.json";
        lastMilestone.FinalDeliverableUrl = await _storageService.UploadPrivateAsync(ms, manifestKey, "application/json", ct);
        return ToDeliverablesDto(manifest);
    }

    private static DeliverablesManifestDto ToDeliverablesDto(StoredDeliverablesManifest manifest) =>
        new(
            manifest.Version,
            manifest.Files.Sum(f => f.SizeBytes),
            manifest.Files.Select(f => new DeliverableFileDto(f.Id, f.Name, f.SizeBytes, f.Sha256, f.UploadedAt)).ToList());

    public async Task<CommissionDto> DeliverFinalWorkAsync(Guid id, IReadOnlyList<DeliverableUploadItem> files, Guid creatorId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(id, creatorId, creator: true, cancellationToken);
        await _dbContext.Entry(commission).Collection(c => c.Milestones).LoadAsync(cancellationToken);
        var isRedelivery = commission.Status == CommissionStatus.Delivered;
        if (commission.Status is not (CommissionStatus.InProgress or CommissionStatus.Delivered) || commission.Milestones.Count == 0
            || commission.Milestones.Any(m => m.Status != MilestoneStatus.Approved))
            throw new InvalidOperationException("All milestones must be approved before final delivery.");
        if (files is null || files.Count == 0)
            throw new ArgumentException("Final deliverable file is required.");

        var lastMilestone = commission.Milestones.OrderByDescending(m => m.Sequence).First();
        var existingManifest = await LoadStoredManifestAsync(lastMilestone, cancellationToken);
        long totalBytes = existingManifest.Files.Sum(f => f.SizeBytes);

        var validated = new List<(DeliverableUploadItem Item, string SafeName, string Sha256, long Length)>(files.Count);
        foreach (var file in files)
        {
            var safeName = await DeliverableFileValidator.ValidateAndSanitizeAsync(file, cancellationToken);
            totalBytes += file.FileStream.Length;
            if (totalBytes > DeliverableFileValidator.MaxTotalBytes)
                throw new ArgumentException("Total final deliverable size exceeds the 1 GB limit.");

            file.FileStream.Position = 0;
            var sha256 = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(file.FileStream, cancellationToken)).ToLowerInvariant();
            file.FileStream.Position = 0;
            validated.Add((file, safeName, sha256, file.FileStream.Length));
        }

        var entries = new List<StoredDeliverableEntry>(existingManifest.Files);
        var usedNames = new HashSet<string>(entries.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        foreach (var (item, safeName, sha256, length) in validated)
        {
            var uniqueName = MakeUniqueEntryName(safeName, usedNames);
            var key = $"commissions/{id}/final/{Guid.NewGuid()}-{uniqueName}";
            item.FileStream.Position = 0;
            var storedKey = await _storageService.UploadPrivateAsync(item.FileStream, key, item.ContentType, cancellationToken);
            entries.Add(new StoredDeliverableEntry(Guid.NewGuid().ToString("N")[..12], uniqueName, storedKey, length, sha256, now));
        }

        await SaveManifestAsync(id, lastMilestone, entries, cancellationToken);

        commission.Status = CommissionStatus.Delivered;
        commission.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await PublishSafeAsync(
            commission.ClientId, NotificationType.CommissionStatusChanged,
            isRedelivery ? "Creator đã cập nhật tệp bàn giao (HD)" : "Creator đã gửi file hoàn chỉnh (HD)",
            $"Kho bàn giao của “{commission.Title}” vừa được cập nhật ({entries.Count} tệp). Vui lòng tải kiểm tra kỹ trước khi xác nhận hoàn tất.",
            commission.Id, $"FinalDelivered:{commission.Id}:{commission.UpdatedAt:O}", cancellationToken);

        return MapToDto(commission);
    }

    public async Task<DeliverablesManifestDto> GetDeliverablesManifestAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var commission = await PartyCommissionAsync(id, userId, cancellationToken);
        await _dbContext.Entry(commission).Collection(c => c.Milestones).LoadAsync(cancellationToken);
        var lastMilestone = commission.Milestones.OrderByDescending(m => m.Sequence).FirstOrDefault();
        var manifest = await LoadStoredManifestAsync(lastMilestone, cancellationToken);
        return ToDeliverablesDto(manifest);
    }

    public async Task<DeliverablesManifestDto> DeleteDeliverableFileAsync(Guid id, string fileId, Guid creatorId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(id, creatorId, creator: true, cancellationToken);
        if (commission.Status != CommissionStatus.Delivered)
            throw new InvalidOperationException("Deliverables can only be modified while waiting for client completion.");

        await _dbContext.Entry(commission).Collection(c => c.Milestones).LoadAsync(cancellationToken);
        var lastMilestone = commission.Milestones.OrderByDescending(m => m.Sequence).FirstOrDefault();
        var manifest = await LoadStoredManifestAsync(lastMilestone, cancellationToken);
        var target = manifest.Files.FirstOrDefault(f => string.Equals(f.Id, fileId, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException("Deliverable file not found.");

        if (manifest.Files.Count <= 1)
            throw new InvalidOperationException("Phải giữ lại ít nhất 1 tệp bàn giao. Hãy tải tệp mới lên trước khi xóa tệp cũ.");

        var remaining = manifest.Files.Where(f => !string.Equals(f.Id, target.Id, StringComparison.Ordinal)).ToList();
        var updatedDto = await SaveManifestAsync(id, lastMilestone!, remaining, cancellationToken);
        commission.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await PublishSafeAsync(
            commission.ClientId, NotificationType.CommissionStatusChanged,
            "Kho bàn giao vừa được họa sĩ cập nhật",
            $"Họa sĩ đã gỡ tệp “{target.Name}” khỏi “{commission.Title}”. Vui lòng kiểm tra lại danh sách tệp mới nhất trước khi xác nhận.",
            commission.Id, $"DeliverableDeleted:{commission.Id}:{commission.UpdatedAt:O}", cancellationToken);

        return updatedDto;
    }

    public Task<CommissionDto> CompleteCommissionAsync(Guid id, Guid clientId, CancellationToken cancellationToken = default) =>
        CompleteCommissionAsync(id, clientId, null, cancellationToken);

    public async Task<CommissionDto> CompleteCommissionAsync(Guid id, Guid clientId, string? expectedManifestVersion, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(id, clientId, creator: false, cancellationToken);
        await _dbContext.Entry(commission).Collection(c => c.Milestones).LoadAsync(cancellationToken);
        if (commission.Status != CommissionStatus.Delivered || commission.EscrowHeldAmount != 0
            || commission.Milestones.Any(m => m.Status != MilestoneStatus.Approved))
            throw new InvalidOperationException("Commission is not ready for completion.");

        var lastMilestone = commission.Milestones.OrderByDescending(m => m.Sequence).FirstOrDefault();
        var manifest = await LoadStoredManifestAsync(lastMilestone, cancellationToken);
        if (manifest.Files.Count == 0)
            throw new InvalidOperationException("No deliverable files are present for completion.");

        if (!string.IsNullOrWhiteSpace(expectedManifestVersion)
            && !string.Equals(expectedManifestVersion.Trim(), manifest.Version, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Danh sách tệp bàn giao vừa được họa sĩ thay đổi. Vui lòng tải lại và kiểm tra kỹ trước khi xác nhận hoàn tất.");
        }

        commission.Status = CommissionStatus.Completed;
        // Gửi email chứng nhận bản quyền số & nghiệm thu cho khách hàng
        try
        {
            var clientUser = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == clientId, cancellationToken);
            if (clientUser?.Email != null && _emailService != null)
            {
                var creatorUserId = await CreatorUserIdAsync(commission.CreatorId, cancellationToken);
                var creatorUser = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == creatorUserId, cancellationToken);
                var clientName = clientUser.FullName ?? "Khách hàng";
                var creatorName = creatorUser?.FullName ?? "Creator";
                var lastM = commission.Milestones.OrderByDescending(m => m.Sequence).FirstOrDefault();
                await _emailService.SendCertificateCompletedEmailAsync(
                    clientUser.Email, clientName, creatorName, commission.Title,
                    commission.Id.ToString(), commission.LicenseType, lastM?.WipPreviewUrl, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Không thể gửi email chứng nhận hoàn thành cho commission {CommissionId}", commission.Id);
        }

        commission.EscrowStatus = EscrowStatus.Released;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var completedCreatorUserId = await CreatorUserIdAsync(commission.CreatorId, cancellationToken);
        await PublishSafeAsync(
            completedCreatorUserId, NotificationType.CommissionStatusChanged,
            "Đơn hàng đã hoàn tất",
            $"Khách hàng đã xác nhận nghiệm thu file hoàn chỉnh cho “{commission.Title}”.",
            commission.Id, $"CommissionCompleted:{commission.Id}", cancellationToken);

        return MapToDto(commission);
    }

    public Task<FinalDownloadDto> GetFinalDownloadUrlAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        GetFinalDownloadUrlAsync(id, userId, null, cancellationToken);

    public async Task<FinalDownloadDto> GetFinalDownloadUrlAsync(Guid id, Guid userId, string? fileId, CancellationToken cancellationToken = default)
    {
        var commission = await PartyCommissionAsync(id, userId, cancellationToken);
        if (commission.Status != CommissionStatus.Delivered && commission.Status != CommissionStatus.Completed)
            throw new InvalidOperationException("Final files are available after delivery.");

        await _dbContext.Entry(commission).Collection(c => c.Milestones).LoadAsync(cancellationToken);
        var lastMilestone = commission.Milestones.OrderByDescending(m => m.Sequence).FirstOrDefault();
        var manifest = await LoadStoredManifestAsync(lastMilestone, cancellationToken);
        if (manifest.Files.Count == 0)
            throw new InvalidOperationException("Final deliverable not found.");

        string targetStorageKey;
        if (!string.IsNullOrWhiteSpace(fileId))
        {
            var entry = manifest.Files.FirstOrDefault(f => string.Equals(f.Id, fileId, StringComparison.Ordinal))
                ?? throw new KeyNotFoundException("Deliverable file not found.");
            targetStorageKey = entry.StorageKey;
        }
        else if (manifest.Files.Count == 1)
        {
            targetStorageKey = manifest.Files[0].StorageKey;
        }
        else
        {
            var shortVer = manifest.Version.Length >= 16 ? manifest.Version[..16] : manifest.Version;
            var bundleKey = $"commissions/{id}/final/bundle-{shortVer}-all.zip";
            await using var cached = await _storageService.OpenPrivateReadAsync(bundleKey, cancellationToken);
            if (cached is not null)
            {
                targetStorageKey = bundleKey;
            }
            else
            {
                var tempZipPath = Path.Combine(Path.GetTempPath(), $"dil-all-{Guid.NewGuid():N}.zip");
                try
                {
                    await using var zipFs = new FileStream(tempZipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, useAsync: true);
                    using (var zip = new System.IO.Compression.ZipArchive(zipFs, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
                    {
                        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var file in manifest.Files)
                        {
                            await using var src = await _storageService.OpenPrivateReadAsync(file.StorageKey, cancellationToken);
                            if (src is null) continue;
                            var entry = zip.CreateEntry(MakeUniqueEntryName(file.Name, usedNames), System.IO.Compression.CompressionLevel.Fastest);
                            await using var dst = entry.Open();
                            await src.CopyToAsync(dst, cancellationToken);
                        }
                    }
                    await zipFs.FlushAsync(cancellationToken);
                    zipFs.Position = 0;
                    targetStorageKey = await _storageService.UploadPrivateAsync(zipFs, bundleKey, "application/zip", cancellationToken);
                }
                finally
                {
                    try { if (File.Exists(tempZipPath)) File.Delete(tempZipPath); } catch { }
                }
            }
        }

        var expiresIn = TimeSpan.FromMinutes(15);
        var expiresAt = DateTimeOffset.UtcNow.Add(expiresIn);
        var downloadUrl = _storageService.GeneratePresignedDownloadUrl(targetStorageKey, expiresIn);
        return new FinalDownloadDto(downloadUrl, expiresAt);
    }

    public async Task<string> GetMilestoneWipPreviewUrlAsync(Guid commissionId, Guid milestoneId, Guid userId, CancellationToken cancellationToken = default)
    {
        await PartyCommissionAsync(commissionId, userId, cancellationToken);
        var milestone = await _dbContext.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.CommissionId == commissionId, cancellationToken);
        if (milestone == null) throw new KeyNotFoundException("Milestone not found.");

        return milestone.WatermarkedUrl ?? string.Empty;
    }

    public async Task<CommissionDto> CancelCommissionAsync(Guid id, CancelWithPolicyRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CancellationReason) || request.CancellationReason.Length > 500
            || request.Details?.Length > 2000)
            throw new ArgumentException("Cancellation reason is required and cancellation details are too long.");

        await using var tx = await BeginTransactionAsync(cancellationToken);
        var commission = await PartyCommissionAsync(id, userId, cancellationToken);
        if (commission.Status is CommissionStatus.Completed or CommissionStatus.Cancelled or CommissionStatus.Disputed)
            throw new InvalidOperationException("Commission cannot be cancelled in this state.");
        commission.CancellationReason = request.CancellationReason.Trim();
        commission.CancellationDetails = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim();

        if (commission.Status is CommissionStatus.PendingAcceptance or CommissionStatus.Negotiating)
        {
            await FinalizeCancellationAsync(commission, "Cancelled before work started.", cancellationToken);
        }
        else
        {
            if (commission.CancellationRequestStatus == "Pending")
                throw new InvalidOperationException("A cancellation request is already pending.");

            var now = DateTimeOffset.UtcNow;
            var creatorUserId = await CreatorUserIdAsync(commission.CreatorId, cancellationToken);
            var requesterRole = commission.ClientId == userId ? "Client" : "Creator";
            var overdueGraceEndsAt = commission.DeadlineAt?.AddHours(24);

            commission.CancellationRequestedById = userId;
            commission.CancellationRequestedByRole = requesterRole;
            commission.CancellationRequestedAt = now;
            commission.CancellationRequestStatus = "Pending";
            commission.CancellationRequestExpiresAt = requesterRole == "Client"
                && commission.DeadlineAt <= now
                ? overdueGraceEndsAt
                : now.AddHours(48);
            commission.UpdatedAt = now;

            if (requesterRole == "Client" && overdueGraceEndsAt <= now)
            {
                await FinalizeCancellationAsync(commission, "Automatically cancelled after the overdue grace period.", cancellationToken);
            }
            else
            {
                var recipientId = requesterRole == "Client" ? creatorUserId : commission.ClientId;
                await PublishSafeAsync(
                    recipientId, NotificationType.DisputeStatusChanged,
                    "Yêu cầu hủy đơn mới",
                    $"{requesterRole} đã gửi yêu cầu hủy “{commission.Title}”. Vui lòng phản hồi trước {commission.CancellationRequestExpiresAt:dd/MM/yyyy HH:mm} UTC.",
                    commission.Id, $"CancellationRequested:{commission.Id}:{commission.CancellationRequestedAt:O}", cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null)
            await tx.CommitAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<CommissionDto> RespondCancellationRequestAsync(Guid id, bool approve, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var tx = await BeginTransactionAsync(cancellationToken);
        var commission = await PartyCommissionAsync(id, userId, cancellationToken);
        if (commission.CancellationRequestStatus != "Pending" || commission.CancellationRequestedById is null)
            throw new InvalidOperationException("There is no pending cancellation request.");
        if (commission.CancellationRequestedById == userId)
            throw new UnauthorizedAccessException("The requester cannot respond to their own cancellation request.");
        if (commission.CancellationRequestExpiresAt <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("The cancellation request has expired.");

        if (approve)
        {
            commission.CancellationRequestStatus = "Approved";
            await FinalizeCancellationAsync(commission, "Cancellation approved by both participants.", cancellationToken);
        }
        else
        {
            commission.CancellationRequestStatus = "Rejected";
            commission.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null)
            await tx.CommitAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task ProcessCancellationDeadlinesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var dueIds = await _dbContext.Commissions.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CancellationRequestStatus == "Pending"
                && c.CancellationRequestExpiresAt <= now
                && c.Status != CommissionStatus.Cancelled && c.Status != CommissionStatus.Completed
                && c.Status != CommissionStatus.Disputed)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        foreach (var commissionId in dueIds)
        {
            await using var tx = await BeginTransactionAsync(cancellationToken);
            var commission = await _dbContext.Commissions.FirstAsync(c => c.Id == commissionId, cancellationToken);
            if (commission.CancellationRequestStatus != "Pending" || commission.CancellationRequestExpiresAt > now)
                continue;

            var overdueAutoRefund = commission.CancellationRequestedByRole == "Client"
                && commission.DeadlineAt?.AddHours(24) <= now;
            if (commission.CancellationRequestedByRole == "Creator" || overdueAutoRefund)
            {
                await FinalizeCancellationAsync(
                    commission,
                    overdueAutoRefund
                        ? "Automatically cancelled after the overdue grace period."
                        : "Automatically cancelled because the client did not respond within 48 hours.",
                    cancellationToken);
            }
            else
            {
                commission.CancellationRequestStatus = "Expired";
                commission.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            if (tx is not null)
                await tx.CommitAsync(cancellationToken);
        }
    }

    public async Task ProcessMilestoneAutoAcceptDeadlinesAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-72);
        var dueTargets = await _dbContext.Milestones.AsNoTracking()
            .Where(m => !m.IsDeleted
                && m.Status == MilestoneStatus.Submitted
                && m.SubmittedAt != null && m.SubmittedAt <= cutoff
                && !m.Commission.IsDeleted
                && m.Commission.Status == CommissionStatus.InProgress
                && m.Commission.CancellationRequestStatus != "Pending"
                && (m.Commission.EscrowStatus == EscrowStatus.Deposited || m.Commission.EscrowStatus == EscrowStatus.PartialReleased)
                && m.Sequence == m.Commission.CurrentStage
                && m.Price <= m.Commission.EscrowHeldAmount)
            .Select(m => new { m.CommissionId, MilestoneId = m.Id })
            .ToListAsync(cancellationToken);

        foreach (var target in dueTargets)
        {
            await using var tx = await BeginTransactionAsync(cancellationToken);
            if (_dbContext.Database.IsSqlServer())
            {
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT [Id] FROM [Commissions] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {target.CommissionId}",
                    cancellationToken);
            }

            var commission = await _dbContext.Commissions
                .Include(c => c.Milestones)
                .FirstOrDefaultAsync(c => c.Id == target.CommissionId && !c.IsDeleted, cancellationToken);
            var milestone = commission?.Milestones.FirstOrDefault(m => m.Id == target.MilestoneId && !m.IsDeleted);
            if (commission is null || milestone is null
                || commission.Status != CommissionStatus.InProgress
                || commission.CancellationRequestStatus == "Pending"
                || commission.EscrowStatus is not (EscrowStatus.Deposited or EscrowStatus.PartialReleased)
                || milestone.Status != MilestoneStatus.Submitted
                || milestone.SubmittedAt is null || milestone.SubmittedAt > DateTimeOffset.UtcNow.AddHours(-72)
                || milestone.Sequence != commission.CurrentStage
                || milestone.Price > commission.EscrowHeldAmount)
            {
                continue;
            }

            await ApproveMilestoneCoreAsync(commission, milestone, commission.ClientId, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (tx is not null)
                await tx.CommitAsync(cancellationToken);

            await PublishMilestoneApprovedNotificationsAsync(commission, milestone, isAutoAccepted: true, cancellationToken);
        }
    }

    private async Task FinalizeCancellationAsync(Commission commission, string note, CancellationToken cancellationToken)
    {
        if (commission.EscrowHeldAmount > 0)
        {
            var clientWallet = await _walletService.GetOrCreateWalletAsync(commission.ClientId, cancellationToken);
            await _walletService.RefundHeldFundsAsync(
                clientWallet, WalletTransactionType.RefundFromHold, commission.EscrowHeldAmount,
                "Commission", commission.Id, note, cancellationToken);
            commission.EscrowHeldAmount = 0m;
            commission.EscrowStatus = EscrowStatus.Refunded;
        }

        commission.Status = CommissionStatus.Cancelled;
        commission.CancellationRequestStatus ??= "Approved";
        commission.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public async Task<CommissionDisputeDto> CreateDisputeAsync(Guid id, CreateDisputeRequest request, Guid raisedById, CancellationToken cancellationToken = default)
    {
        var validation = new CreateDisputeRequestValidator().Validate(request);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));

        var commission = await PartyCommissionAsync(id, raisedById, cancellationToken);
        if (commission.Status is CommissionStatus.Completed or CommissionStatus.Cancelled or CommissionStatus.Disputed)
            throw new InvalidOperationException("Commission cannot be disputed in this state.");
        if (await _dbContext.Disputes.AnyAsync(d => d.CommissionId == id, cancellationToken))
            throw new InvalidOperationException("Commission already has a dispute.");
        var dispute = new CommissionDispute
        {
            CommissionId = id,
            RaisedById = raisedById,
            Reason = request.Reason,
            EvidenceUrls = request.EvidenceUrls != null ? string.Join(",", request.EvidenceUrls) : null,
            Status = "Pending"
        };

        _dbContext.Disputes.Add(dispute);

        commission.Status = CommissionStatus.Disputed;
        commission.EscrowStatus = EscrowStatus.Disputed;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var creatorUserId = await CreatorUserIdAsync(commission.CreatorId, cancellationToken);
        var recipientId = raisedById == commission.ClientId ? creatorUserId : commission.ClientId;
        await PublishSafeAsync(
            recipientId, NotificationType.DisputeStatusChanged,
            "Đơn đặt vẽ có tranh chấp mới",
            $"Một tranh chấp đã được mở cho “{commission.Title}”.",
            commission.Id, $"DisputeOpened:{dispute.Id}:{recipientId}", cancellationToken);

        return MapToDisputeDto(dispute);
    }

    public async Task<CommissionDisputeDto?> GetDisputeByCommissionIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        await PartyCommissionAsync(id, userId, cancellationToken);
        var dispute = await _dbContext.Disputes.AsNoTracking().OrderByDescending(d => d.CreatedAt).FirstOrDefaultAsync(d => d.CommissionId == id, cancellationToken);
        return dispute == null ? null : MapToDisputeDto(dispute);
    }

    public async Task<ReviewDto> CreateReviewAsync(Guid id, CreateReviewRequest request, Guid reviewerId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(id, reviewerId, creator: false, cancellationToken);
        if (commission.Status != CommissionStatus.Completed) throw new InvalidOperationException("Review requires a completed commission.");
        if (await _dbContext.Reviews.AnyAsync(r => r.CommissionId == id, cancellationToken))
            throw new InvalidOperationException("Commission already has a review.");
        var validation = new CreateReviewRequestValidator().Validate(request);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        var review = new Review
        {
            CommissionId = id,
            ReviewerId = reviewerId,
            Rating = request.Rating,
            Comment = request.Comment,
            AttachedImagesJson = request.AttachedImageUrls != null ? System.Text.Json.JsonSerializer.Serialize(request.AttachedImageUrls) : null
        };

        _dbContext.Reviews.Add(review);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToReviewDto(review);
    }

    public async Task<TipResultDto> SendTipAsync(Guid id, SendTipRequest request, Guid clientId, CancellationToken cancellationToken = default)
    {
        RequireUser(clientId);
        if (request.Amount < 1000 || request.Amount > 100_000_000)
            throw new ArgumentException("Số tiền tip phải từ 1.000 VND đến 100.000.000 VND.");

        var commission = await _dbContext.Commissions
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn hàng.");

        RequireClient(commission, clientId);

        if (commission.Status != CommissionStatus.Completed && commission.Status != CommissionStatus.Delivered)
            throw new InvalidOperationException("Chỉ có thể gửi tip khi đơn hàng đã hoàn thành hoặc đã bàn giao.");

        var clientWallet = await _walletService.GetOrCreateWalletAsync(clientId, cancellationToken);
        if (clientWallet.Balance < request.Amount)
        {
            throw new InvalidOperationException($"Số dư ví không đủ ({clientWallet.Balance:N0} VND). Vui lòng nạp thêm tiền vào ví để gửi tip.");
        }

        var creatorProfile = await _dbContext.CreatorProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == commission.CreatorId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin họa sĩ.");

        var creatorUserId = creatorProfile.UserId;
        var creatorWallet = await _walletService.GetOrCreateWalletAsync(creatorUserId, cancellationToken);

        var tipId = Guid.NewGuid();
        var tipNote = string.IsNullOrWhiteSpace(request.Message)
            ? $"Gửi tip đơn hàng #{commission.Id.ToString()[..8]}"
            : request.Message.Trim();

        await using var tx = await BeginTransactionAsync(cancellationToken);

        // Trừ tiền ví Client
        await _walletService.DebitAsync(
            clientWallet,
            WalletTransactionType.TipSent,
            request.Amount,
            "CommissionTip",
            tipId,
            $"Gửi tip cho họa sĩ {creatorProfile.User?.FullName ?? creatorProfile.DisplayName ?? "Creator"}: {tipNote}",
            cancellationToken);

        // Cộng tiền ví Creator
        await _walletService.CreditAsync(
            creatorWallet,
            WalletTransactionType.TipReceived,
            request.Amount,
            "CommissionTip",
            tipId,
            $"Nhận tip từ khách hàng đơn hàng #{commission.Id.ToString()[..8]}: {tipNote}",
            cancellationToken);

        // Đăng thông báo vào phòng chat nếu có phòng
        try
        {
            var chatRoom = await _dbContext.ChatRooms
                .FirstOrDefaultAsync(r => r.CommissionId == id && !r.IsDeleted, cancellationToken);
            if (chatRoom != null)
            {
                var sysMsg = new ArtCommission.Domain.Entities.Chat.Message
                {
                    RoomId = chatRoom.Id,
                    CommissionId = id,
                    SenderId = clientId,
                    MessageType = MessageTypes.SystemEvent,
                    Body = $"🎁 Khách hàng đã gửi tặng họa sĩ {request.Amount:N0} VND tiền tip! Lời nhắn: \"{tipNote}\""
                };
                _dbContext.Messages.Add(sysMsg);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Không thể ghi tin nhắn hệ thống phòng chat cho tip commission {CommissionId}", id);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null)
            await tx.CommitAsync(cancellationToken);

        // Phát thông báo realtime cho Creator
        var creatorDisplayName = creatorProfile.DisplayName ?? creatorProfile.User?.FullName ?? "Creator";
        await PublishSafeAsync(
            creatorUserId,
            NotificationType.PaymentSucceeded,
            "Bạn nhận được tiền tip!",
            $"Khách hàng vừa gửi tặng bạn {request.Amount:N0} VND tiền tip cho đơn hàng {commission.Title}.",
            id,
            $"CommissionTip:{tipId}",
            cancellationToken);

        return new TipResultDto(
            commission.Id,
            request.Amount,
            clientWallet.Balance,
            creatorDisplayName,
            DateTimeOffset.UtcNow,
            tipNote
        );
    }


    public async Task<ReviewDto> ReplyReviewAsync(Guid id, string replyComment, Guid creatorId, CancellationToken cancellationToken = default)
    {
        var review = await _dbContext.Reviews.FirstOrDefaultAsync(r => r.CommissionId == id || r.Id == id, cancellationToken);
        if (review == null) throw new KeyNotFoundException("Không tìm thấy đánh giá cho đơn hàng này.");
        await OwnedCommissionAsync(review.CommissionId, creatorId, creator: true, cancellationToken);
        if (review.ReviewerReply is not null) throw new InvalidOperationException("Review already has a reply.");

        review.ReviewerReply = replyComment;
        review.RespondedAt = DateTimeOffset.UtcNow;
        review.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToReviewDto(review);
    }

    private static void RequireUser(Guid userId)
    {
        if (userId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
    }

    private Task<decimal> BookingAddonTotalAsync(Guid commissionId, CancellationToken ct) =>
        _dbContext.Set<CommissionAddon>().Where(x => x.CommissionId == commissionId && !x.IsDeleted && x.Status == "Accepted")
            .SumAsync(x => x.Price, ct);

    private async Task AllocateFundedMilestonesAsync(Commission commission, CancellationToken ct)
    {
        await _dbContext.Entry(commission).Collection(x => x.Milestones).LoadAsync(ct);
        var stages = commission.Milestones.Where(x => !x.IsDeleted).OrderBy(x => x.Sequence).ToList();
        if (stages.Sum(x => x.Price) == commission.FinalPrice) return;
        var extras = await _dbContext.Set<CommissionAddon>()
            .Where(x => x.CommissionId == commission.Id && !x.IsDeleted && x.Status == "Accepted")
            .GroupBy(x => x.MilestoneSequence).Select(g => new { Sequence = g.Key, Price = g.Sum(x => x.Price) })
            .ToDictionaryAsync(x => x.Sequence, x => x.Price, ct);
        var baseTotal = stages.Sum(x => x.Price) - extras.Values.Sum();
        var basePayable = commission.FinalPrice - extras.Values.Sum();
        if (stages.Count == 0 || baseTotal <= 0 || basePayable <= 0)
            throw new InvalidOperationException("Giá ký quỹ không hợp lệ cho kế hoạch mốc và phụ phí.");
        var prices = stages.Select(x => Math.Round((x.Price - extras.GetValueOrDefault(x.Sequence)) * basePayable / baseTotal,
            2, MidpointRounding.AwayFromZero) + extras.GetValueOrDefault(x.Sequence)).ToArray();
        prices[^1] += commission.FinalPrice - prices.Sum();
        if (prices.Any(x => x <= 0)) throw new InvalidOperationException("Giá sau giảm không đủ cho từng mốc.");
        for (var i = 0; i < stages.Count; i++) stages[i].Price = prices[i];
    }

    private Task<Guid> CreatorProfileIdAsync(Guid userId, CancellationToken cancellationToken) =>
        _dbContext.CreatorProfiles.Where(p => p.UserId == userId && !p.IsDeleted)
            .Select(p => p.Id).FirstOrDefaultAsync(cancellationToken);

    private Task<Guid> CreatorUserIdAsync(Guid creatorProfileId, CancellationToken cancellationToken) =>
        _dbContext.CreatorProfiles.Where(p => p.Id == creatorProfileId && !p.IsDeleted)
            .Select(p => p.UserId).SingleAsync(cancellationToken);

    private async Task PublishMilestoneApprovedNotificationsAsync(
        Commission commission,
        Milestone milestone,
        bool isAutoAccepted,
        CancellationToken cancellationToken)
    {
        var creatorUserId = await CreatorUserIdAsync(commission.CreatorId, cancellationToken);
        var nextMilestone = commission.Milestones
            .Where(m => !m.IsDeleted && m.Status != MilestoneStatus.Approved)
            .OrderBy(m => m.Sequence)
            .FirstOrDefault();

        string creatorNextStep;
        string clientNextStep;
        if (nextMilestone is null)
        {
            creatorNextStep = "Toàn bộ các mốc đã hoàn tất — bạn có thể gửi file hoàn chỉnh (HD) ngay!";
            clientNextStep = "Toàn bộ các mốc đã hoàn tất, đang chờ Creator gửi file hoàn chỉnh (HD).";
        }
        else if (commission.EscrowStatus == EscrowStatus.Pending)
        {
            creatorNextStep = $"Đang chờ khách hàng nạp ký quỹ Mốc #{nextMilestone.Sequence} ({nextMilestone.Title}) để bạn tiếp tục.";
            clientNextStep = $"Vui lòng nạp ký quỹ Mốc #{nextMilestone.Sequence} ({nextMilestone.Title}) để Creator tiếp tục.";
        }
        else
        {
            creatorNextStep = $"Bạn có thể bắt đầu thực hiện Mốc #{nextMilestone.Sequence} ({nextMilestone.Title}).";
            clientNextStep = $"Creator có thể tiếp tục thực hiện Mốc #{nextMilestone.Sequence} ({nextMilestone.Title}).";
        }

        if (!isAutoAccepted)
        {
            await PublishSafeAsync(
                creatorUserId, NotificationType.EscrowStatusChanged,
                nextMilestone is null
                    ? $"Đã duyệt Mốc #{milestone.Sequence} — Sẵn sàng gửi file hoàn chỉnh"
                    : $"Đã duyệt Mốc #{milestone.Sequence} ({milestone.Price:N0} VND)",
                $"Khách hàng đã duyệt Mốc #{milestone.Sequence} ({milestone.Title}) và giải ngân {milestone.Price:N0} VND cho “{commission.Title}”. {creatorNextStep}",
                commission.Id, $"MilestoneApproved:{milestone.Id}:{creatorUserId}", cancellationToken);
            return;
        }

        try
        {
            var chatRoom = await _dbContext.ChatRooms
                .FirstOrDefaultAsync(r => r.CommissionId == commission.Id && !r.IsDeleted, cancellationToken);
            if (chatRoom != null)
            {
                _dbContext.Messages.Add(new ArtCommission.Domain.Entities.Chat.Message
                {
                    RoomId = chatRoom.Id,
                    CommissionId = commission.Id,
                    SenderId = commission.ClientId,
                    MessageType = MessageTypes.SystemEvent,
                    Body = $"⏱️ Mốc #{milestone.Sequence} ({milestone.Title}) đã được tự động duyệt và giải ngân {milestone.Price:N0} VND sau 72 giờ không phản hồi. {clientNextStep}"
                });
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Không thể ghi tin nhắn tự động duyệt 72h cho commission {CommissionId}", commission.Id);
        }

        await PublishSafeAsync(
            commission.ClientId, NotificationType.CommissionStatusChanged,
            $"Tự động duyệt Mốc #{milestone.Sequence} (72h)",
            $"Mốc #{milestone.Sequence} ({milestone.Title}) trên đơn “{commission.Title}” đã được tự động duyệt sau 72h. {clientNextStep}",
            commission.Id, $"MilestoneAutoAccepted:{milestone.Id}:Client", cancellationToken);
        await PublishSafeAsync(
            creatorUserId, NotificationType.EscrowStatusChanged,
            $"Tự động giải ngân Mốc #{milestone.Sequence} (72h)",
            $"Mốc #{milestone.Sequence} ({milestone.Title}) trên đơn “{commission.Title}” ({milestone.Price:N0} VND) đã được tự động giải ngân sau 72h. {creatorNextStep}",
            commission.Id, $"MilestoneAutoAccepted:{milestone.Id}:Creator", cancellationToken);
    }

    private async Task PublishSafeAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        Guid commissionId,
        string dedupKey,
        CancellationToken cancellationToken,
        string refType = "Commission")
    {
        if (_notifications is null) return;

        try
        {
            await _notifications.PublishAsync(
                userId, type, title, body, refType, commissionId,
                dedupKey: dedupKey, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "Không phát được thông báo commission. UserId={UserId} CommissionId={CommissionId} Type={Type}",
                userId, commissionId, type);
        }
    }

    
    public async Task<RenameCommissionResultDto> RenameCommissionAsync(
        Guid id,
        string newTitle,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var commission = await PartyCommissionAsync(id, userId, cancellationToken);
        var trimmedTitle = newTitle?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedTitle) || trimmedTitle.Length > 200)
        {
            throw new ArgumentException("Tên dự án không được để trống và tối đa 200 ký tự.");
        }

        commission.Title = trimmedTitle;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        var chatRooms = await _dbContext.ChatRooms
            .Where(r => r.CommissionId == id && !r.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var r in chatRooms)
        {
            r.Title = trimmedTitle;
            r.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var isClient = commission.ClientId == userId;
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        var userDisplayName = user?.FullName ?? (isClient ? "Khách hàng" : "Họa sĩ");
        var roleName = isClient ? "Khách hàng" : "Họa sĩ";

        var mainChatRoom = chatRooms.FirstOrDefault();
        ArtCommission.Domain.Entities.Chat.Message? sysMsg = null;
        var now = DateTimeOffset.UtcNow;

        if (mainChatRoom != null)
        {
            sysMsg = new ArtCommission.Domain.Entities.Chat.Message
            {
                RoomId = mainChatRoom.Id,
                CommissionId = id,
                SenderId = userId,
                MessageType = MessageTypes.SystemEvent,
                Body = $"✏️ {roleName} ({userDisplayName}) đã đổi tên dự án thành: \"{trimmedTitle}\"",
                SentAt = now
            };
            _dbContext.Messages.Add(sysMsg);
            mainChatRoom.LastMessageAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RenameCommissionResultDto(
            commission.Id,
            trimmedTitle,
            userDisplayName,
            mainChatRoom?.Id,
            sysMsg?.Id,
            sysMsg?.Body,
            sysMsg?.SentAt
        );
    }

    private async Task EnsurePackageSlotAvailableAsync(
        Guid creatorProfileId,
        string? rateCardJson,
        Guid? packageId,
        bool asArgumentError,
        CancellationToken cancellationToken)
    {
        if (!packageId.HasValue) return;

        rateCardJson ??= await _dbContext.CreatorProfiles.AsNoTracking()
            .Where(p => p.Id == creatorProfileId && !p.IsDeleted)
            .Select(p => p.RateCardJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(rateCardJson)) return;

        List<ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard.RateCardPackageDto>? packages;
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard.RateCardIdConverter() }
            };
            packages = System.Text.Json.JsonSerializer.Deserialize<List<ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard.RateCardPackageDto>>(rateCardJson, options);
        }
        catch (System.Text.Json.JsonException)
        {
            return;
        }

        var selectedPackage = packages?.FirstOrDefault(p => p != null && p.Id == packageId.Value);
        if (selectedPackage?.MaxSlots is not int maxSlots) return;

        var activeCount = await _dbContext.Commissions.AsNoTracking()
            .CountAsync(c => !c.IsDeleted
                && c.CreatorId == creatorProfileId
                && c.PackageId == packageId.Value
                && (c.Status == CommissionStatus.InProgress
                    || c.Status == CommissionStatus.Delivered
                    || c.Status == CommissionStatus.Disputed),
                cancellationToken);

        if (activeCount >= maxSlots)
        {
            var message = $"Gói \"{selectedPackage.Name}\" đã hết slot nhận đơn ({activeCount}/{maxSlots} đơn đang thực hiện).";
            if (asArgumentError) throw new ArgumentException(message);
            throw new InvalidOperationException(message);
        }
    }

    private async Task<Commission> PartyCommissionAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        RequireUser(userId);
        var creatorProfileId = await CreatorProfileIdAsync(userId, cancellationToken);
        return await _dbContext.Commissions.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted
            && (c.ClientId == userId || c.CreatorId == creatorProfileId), cancellationToken)
            ?? throw new KeyNotFoundException("Commission not found.");
    }

    private async Task<Commission> OwnedCommissionAsync(Guid id, Guid userId, bool creator, CancellationToken cancellationToken)
    {
        var commission = await PartyCommissionAsync(id, userId, cancellationToken);
        var creatorProfileId = creator ? await CreatorProfileIdAsync(userId, cancellationToken) : Guid.Empty;
        if (creator ? commission.CreatorId != creatorProfileId : commission.ClientId != userId)
            throw new UnauthorizedAccessException("This commission belongs to another user.");
        return commission;
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

    private static decimal ComputeNextEscrowDepositAmount(Commission c)
    {
        if (c.EscrowStatus != EscrowStatus.Pending) return 0m;
        if (c.FinalPrice < StagedEscrowThresholdVnd) return Math.Max(0m, c.FinalPrice - c.EscrowHeldAmount);
        var current = c.Milestones.FirstOrDefault(m => !m.IsDeleted && m.Sequence == c.CurrentStage);
        if (current is null) return Math.Max(0m, c.FinalPrice - c.DisbursedAmount - c.EscrowHeldAmount);
        var stagesTotal = c.Milestones.Where(m => !m.IsDeleted).Sum(m => m.Price);
        var stagePrice = c.DisbursedAmount == 0m && stagesTotal > 0m && stagesTotal != c.FinalPrice
            ? Math.Round(current.Price * c.FinalPrice / stagesTotal, 2, MidpointRounding.AwayFromZero)
            : current.Price;
        return Math.Max(0m, stagePrice - c.EscrowHeldAmount);



    }

    #region Helper Mappers
    private static CommissionDto MapToDto(Commission c)
    {
        return new CommissionDto
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            ClientId = c.ClientId,
            CreatorId = c.CreatorId,
            PackageId = c.PackageId,
            VoucherId = c.VoucherId,
            LicenseType = c.LicenseType,
            LicenseMultiplierApplied = c.LicenseMultiplierApplied,
            DiscountAmount = c.DiscountAmount,
            TotalPrice = c.TotalPrice,
            FinalPrice = c.FinalPrice,
            EscrowHeldAmount = c.EscrowHeldAmount,
            DisbursedAmount = c.DisbursedAmount,
            IsStagedEscrow = c.FinalPrice >= StagedEscrowThresholdVnd,
            NextEscrowDepositAmount = ComputeNextEscrowDepositAmount(c),
            EscrowStatus = c.EscrowStatus.ToString(),
            CurrentStage = c.CurrentStage,
            Status = c.Status.ToString(),
            DeadlineAt = c.DeadlineAt,
            CancellationReason = c.CancellationReason,
            CancellationDetails = c.CancellationDetails,
            CancellationRequestedById = c.CancellationRequestedById,
            CancellationRequestedByRole = c.CancellationRequestedByRole,
            CancellationRequestStatus = c.CancellationRequestStatus,
            CancellationRequestedAt = c.CancellationRequestedAt,
            CancellationRequestExpiresAt = c.CancellationRequestExpiresAt,
            IsOverdue = c.DeadlineAt < DateTimeOffset.UtcNow
                && c.Status is not (CommissionStatus.Completed or CommissionStatus.Cancelled),
            OverdueGraceEndsAt = c.DeadlineAt?.AddHours(24),
            ClientTermsAcceptedAt = c.ClientTermsAcceptedAt,
            ClientTermsVersion = c.ClientTermsVersion,
            CreatorTermsAcceptedAt = c.CreatorTermsAcceptedAt,
            CreatorTermsVersion = c.CreatorTermsVersion,
            CurrentWorkroomTermsVersion = CurrentWorkroomTermsVersion,
            CreatorTermsSnapshot = ArtCommission.Application.ArtistStudio.DTOs.CreatorTermsHelper.ParseSnapshot(c.CreatorTermsSnapshotJson),
            CreatedAt = c.CreatedAt
        };
    }

    private static CommissionDetailDto MapToDetailDto(Commission c)
    {
        var dto = MapToDto(c);
        return new CommissionDetailDto
        {
            Id = dto.Id,
            Title = dto.Title,
            Description = dto.Description,
            ClientId = dto.ClientId,
            CreatorId = dto.CreatorId,
            PackageId = dto.PackageId,
            VoucherId = dto.VoucherId,
            LicenseType = dto.LicenseType,
            LicenseMultiplierApplied = dto.LicenseMultiplierApplied,
            DiscountAmount = dto.DiscountAmount,
            TotalPrice = dto.TotalPrice,
            FinalPrice = dto.FinalPrice,
            EscrowHeldAmount = dto.EscrowHeldAmount,
            DisbursedAmount = dto.DisbursedAmount,
            IsStagedEscrow = dto.IsStagedEscrow,
            NextEscrowDepositAmount = dto.NextEscrowDepositAmount,
            EscrowStatus = dto.EscrowStatus,
            CurrentStage = dto.CurrentStage,
            Status = dto.Status,
            DeadlineAt = dto.DeadlineAt,
            CancellationReason = dto.CancellationReason,
            CancellationDetails = dto.CancellationDetails,
            CancellationRequestedById = dto.CancellationRequestedById,
            CancellationRequestedByRole = dto.CancellationRequestedByRole,
            CancellationRequestStatus = dto.CancellationRequestStatus,
            CancellationRequestedAt = dto.CancellationRequestedAt,
            CancellationRequestExpiresAt = dto.CancellationRequestExpiresAt,
            IsOverdue = dto.IsOverdue,
            OverdueGraceEndsAt = dto.OverdueGraceEndsAt,
            ClientTermsAcceptedAt = dto.ClientTermsAcceptedAt,
            ClientTermsVersion = dto.ClientTermsVersion,
            CreatorTermsAcceptedAt = dto.CreatorTermsAcceptedAt,
            CreatorTermsVersion = dto.CreatorTermsVersion,
            CurrentWorkroomTermsVersion = dto.CurrentWorkroomTermsVersion,
            CreatorTermsSnapshot = dto.CreatorTermsSnapshot,
            CreatedAt = dto.CreatedAt,
            Milestones = c.Milestones.Select(MapToMilestoneDto).ToList(),
            Review = c.Reviews.FirstOrDefault() != null ? MapToReviewDto(c.Reviews.First()) : null,
            Dispute = c.Disputes.OrderByDescending(d => d.CreatedAt).FirstOrDefault() != null ? MapToDisputeDto(c.Disputes.OrderByDescending(d => d.CreatedAt).First()) : null
        };
    }

    private static MilestoneDto MapToMilestoneDto(Milestone m)
    {
        return new MilestoneDto
        {
            Id = m.Id,
            CommissionId = m.CommissionId,
            Sequence = m.Sequence,
            Title = m.Title,
            Price = m.Price,
            Status = m.Status.ToString(),
            WipPreviewUrl = m.WipPreviewUrl,
            WatermarkedUrl = m.WatermarkedUrl,
            FinalDeliverableUrl = m.FinalDeliverableUrl,
            RevisionCount = m.RevisionCount,
            MaxRevisions = m.MaxRevisions,
            CreatorNote = m.CreatorNote,
            RevisionFeedback = m.RevisionFeedback,
            SubmittedAt = m.SubmittedAt,
            ApprovedAt = m.ApprovedAt
        };
    }

    private static ReviewDto MapToReviewDto(Review r)
    {
        return new ReviewDto
        {
            Id = r.Id,
            CommissionId = r.CommissionId,
            ReviewerId = r.ReviewerId,
            Rating = r.Rating,
            Comment = r.Comment,
            ReviewerReply = r.ReviewerReply,
            AttachedImages = r.AttachedImagesJson != null ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.AttachedImagesJson) : null,
            RespondedAt = r.RespondedAt,
            CreatedAt = r.CreatedAt
        };
    }

    private static CommissionDisputeDto MapToDisputeDto(CommissionDispute d)
    {
        return new CommissionDisputeDto
        {
            Id = d.Id,
            CommissionId = d.CommissionId,
            RaisedById = d.RaisedById,
            Reason = d.Reason,
            EvidenceUrls = d.EvidenceUrls,
            Status = d.Status,
            Resolution = d.Resolution,
            ClientRefundAmount = d.ClientRefundAmount,
            ArtistPayAmount = d.ArtistPayAmount,
            ClientRefundPercent = ParseClientRefundPercent(d.Resolution),
            AdminNote = d.AdminNote,
            ResolvedAt = d.ResolvedAt,
            CreatedAt = d.CreatedAt
        };
    }

    private static decimal? ParseClientRefundPercent(string? resolution)
    {
        if (resolution == "ClientWin100") return 100m;
        if (resolution == "ArtistWin100") return 0m;
        var value = resolution?.Split('_');
        return value is { Length: 3 } && decimal.TryParse(value[1], out var percent) ? percent : null;
    }
    #endregion
}
