using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Commission.Validators;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Commission;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Infrastructure.Services;

public class CommissionService : ICommissionService
{
    private readonly AppDbContext _dbContext;
    private readonly IWalletService _walletService;
    private readonly IWatermarkService _watermarkService;
    private readonly IStorageService _storageService;
    private readonly IVoucherCheckService _voucherCheckService;
    private readonly INotificationPublisher? _notifications;
    private readonly ILogger<CommissionService>? _logger;

    public CommissionService(AppDbContext dbContext, IWalletService walletService, IWatermarkService watermarkService,
        IStorageService storageService, IVoucherCheckService voucherCheckService,
        INotificationPublisher? notifications = null, ILogger<CommissionService>? logger = null)
    {
        _dbContext = dbContext;
        _walletService = walletService;
        _watermarkService = watermarkService;
        _storageService = storageService;
        _voucherCheckService = voucherCheckService;
        _notifications = notifications;
        _logger = logger;
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
        var stages = selectedPackage.Milestones;
        if (selectedPackage.Price <= 0 || stages is null || stages.Count == 0
            || stages.Any(stage => stage is null || stage.Price <= 0 || string.IsNullOrWhiteSpace(stage.Title))
            || !stages.OrderBy(stage => stage.Sequence).Select(stage => stage.Sequence).SequenceEqual(Enumerable.Range(1, stages.Count))
            || stages.Sum(stage => stage.Price) != selectedPackage.Price)
            throw new ArgumentException("Giá và milestone của gói không hợp lệ. Vui lòng liên hệ Creator cập nhật.");
        var totalPrice = selectedPackage.Price;
        var milestones = stages.OrderBy(stage => stage.Sequence).Select(stage => new Milestone
        {
            Sequence = stage.Sequence, Title = stage.Title, Price = stage.Price, Status = MilestoneStatus.Pending
        }).ToList();
        var multiplier = request.LicenseType == "Commercial"
            ? await _dbContext.CreatorTerms.AsNoTracking()
                .Where(x => x.CreatorProfileId == creator.Id && !x.IsDeleted)
                .Select(x => (decimal?)x.CommercialLicenseMultiplier)
                .FirstOrDefaultAsync(cancellationToken) ?? 1.5m
            : 1m;
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

        var commission = new Commission
        {
            Title = request.Title,
            Description = request.Description,
            ClientId = clientId,
            CreatorId = creator.Id,
            VoucherId = voucherId,
            LicenseType = request.LicenseType,
            LicenseMultiplierApplied = multiplier,
            TotalPrice = totalPrice,
            DiscountAmount = discountAmount,
            FinalPrice = finalPrice,
            EscrowHeldAmount = 0.00m,
            DisbursedAmount = 0.00m,
            EscrowStatus = EscrowStatus.Pending,
            CurrentStage = 1,
            Status = CommissionStatus.PendingAcceptance,
            DeadlineAt = request.DeadlineAt
        };

        foreach (var m in milestones)
        {
            commission.Milestones.Add(m);
        }

        _dbContext.Commissions.Add(commission);
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

        foreach (var item in items)
        {
            if (clientNames.TryGetValue(item.ClientId, out var clientName))
            {
                item.ClientName = clientName;
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
                        voucher.DiscountType, voucher.DiscountValue, voucher.MaxDiscountAmount, commission.TotalPrice);
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
        if (commission.Status != CommissionStatus.InProgress || commission.EscrowStatus != EscrowStatus.Pending)
            throw new InvalidOperationException("Commission is not ready for escrow deposit.");
        if (commission.VoucherId.HasValue)
        {
            var voucher = await _dbContext.Vouchers.SingleAsync(v => v.Id == commission.VoucherId.Value, cancellationToken);
            var voucherCheck = await _voucherCheckService.CheckAsync(
                clientId, voucher.VoucherCode, commission.TotalPrice, nameof(Commission), commission.Id, cancellationToken);
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
            commission.FinalPrice = voucherCheck.FinalAmount;
            _dbContext.VoucherRedemptions.Add(new VoucherRedemption
            {
                VoucherId = voucher.Id,
                UserId = clientId,
                RefType = nameof(Commission),
                RefId = commission.Id,
                DiscountAmount = voucherCheck.DiscountAmount,
                OrderAmount = commission.TotalPrice,
                FinalAmount = voucherCheck.FinalAmount
            });
        }
        var wallet = await _walletService.GetOrCreateWalletAsync(clientId, cancellationToken);
        if (wallet.Status != WalletStatus.Active) throw new InvalidOperationException("Wallet is inactive.");
        await _walletService.HoldFundsAsync(wallet, WalletTransactionType.EscrowHold, commission.FinalPrice,
            nameof(Commission), commission.Id, "Commission escrow deposit", cancellationToken);

        commission.EscrowHeldAmount = commission.FinalPrice;
        commission.EscrowStatus = EscrowStatus.Deposited;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null)
            await tx.CommitAsync(cancellationToken);

        var creatorUserId = await CreatorUserIdAsync(commission.CreatorId, cancellationToken);
        await PublishSafeAsync(
            creatorUserId, NotificationType.EscrowStatusChanged,
            "Đơn đặt vẽ đã được ký quỹ",
            $"Khách hàng đã ký quỹ {commission.FinalPrice:N0} VND cho “{commission.Title}”.",
            commission.Id, $"EscrowDeposited:{commission.Id}:{creatorUserId}", cancellationToken);

        return MapToDto(commission);
    }

    public async Task<MilestoneDto> SubmitMilestoneWipAsync(Guid commissionId, Guid milestoneId, string? creatorNote, Stream? fileStream, string? contentType, Guid creatorId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(commissionId, creatorId, creator: true, cancellationToken);
        if (commission.Status != CommissionStatus.InProgress || commission.EscrowStatus is not (EscrowStatus.Deposited or EscrowStatus.PartialReleased))
            throw new InvalidOperationException("Commission is not funded and in progress.");
        if (fileStream == null || contentType == null) throw new ArgumentException("WIP file is required.");
        var milestone = await _dbContext.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.CommissionId == commissionId, cancellationToken);
        if (milestone == null) throw new KeyNotFoundException("Không tìm thấy cột mốc milestone.");
        if (milestone.Sequence != commission.CurrentStage || milestone.Status is not (MilestoneStatus.Pending or MilestoneStatus.RevisionRequested))
            throw new InvalidOperationException("Milestone cannot be submitted in this state.");

        // Upload original
        var originalKey = $"commissions/{commissionId}/milestone-{milestone.Sequence}-original-{Guid.NewGuid()}.jpg";
        var originalUrl = await _storageService.UploadPrivateAsync(fileStream, originalKey, contentType, cancellationToken);

        // Reset stream for watermark
        fileStream.Position = 0;
        using var watermarkedStream = await _watermarkService.ApplyWatermarkAsync(fileStream, "PROTECTED DEMO · DO NOT COPY", cancellationToken);

        var watermarkedKey = $"commissions/{commissionId}/milestone-{milestone.Sequence}-watermarked-{Guid.NewGuid()}.jpg";
        var watermarkedUrl = await _storageService.UploadPublicAsync(watermarkedStream, watermarkedKey, "image/jpeg", cancellationToken);

        milestone.OriginalWipUrl = originalUrl;
        milestone.WipPreviewUrl = watermarkedUrl;
        milestone.WatermarkedUrl = watermarkedUrl;
        milestone.CreatorNote = creatorNote;
        milestone.Status = MilestoneStatus.Submitted;
        milestone.SubmittedAt = DateTimeOffset.UtcNow;
        milestone.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

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
        var clientWallet = await _walletService.GetOrCreateWalletAsync(clientId, cancellationToken);
        var creatorUserId = await _dbContext.CreatorProfiles.Where(p => p.Id == commission.CreatorId)
            .Select(p => p.UserId).SingleAsync(cancellationToken);
        var creatorWallet = await _walletService.GetOrCreateWalletAsync(creatorUserId, cancellationToken);
        await _walletService.ReleaseFundsAsync(clientWallet, WalletTransactionType.EscrowRelease, milestone.Price,
            nameof(Milestone), milestone.Id, "Commission milestone release", cancellationToken);
        await _walletService.CreditAsync(creatorWallet, WalletTransactionType.EscrowReceive, milestone.Price,
            nameof(Milestone), milestone.Id, "Commission milestone earning", cancellationToken);

        milestone.Status = MilestoneStatus.Approved;
        milestone.ApprovedAt = DateTimeOffset.UtcNow;
        milestone.UpdatedAt = DateTimeOffset.UtcNow;

        commission.DisbursedAmount += milestone.Price;
        commission.EscrowHeldAmount = Math.Max(0, commission.EscrowHeldAmount - milestone.Price);
        commission.CurrentStage += 1;
        commission.EscrowStatus = commission.EscrowHeldAmount == 0 ? EscrowStatus.Released : EscrowStatus.PartialReleased;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null)
            await tx.CommitAsync(cancellationToken);

        return MapToMilestoneDto(milestone);
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

        return MapToMilestoneDto(milestone);
    }

    public async Task<CommissionDto> DeliverFinalWorkAsync(Guid id, Stream fileStream, string contentType, string fileName, Guid creatorId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(id, creatorId, creator: true, cancellationToken);
        await _dbContext.Entry(commission).Collection(c => c.Milestones).LoadAsync(cancellationToken);
        if (commission.Status != CommissionStatus.InProgress || commission.Milestones.Count == 0
            || commission.Milestones.Any(m => m.Status != MilestoneStatus.Approved))
            throw new InvalidOperationException("All milestones must be approved before final delivery.");
        if (fileStream == null) throw new ArgumentException("Final deliverable file is required.");

        var key = $"commissions/{id}/final/{Guid.NewGuid()}-{fileName}";
        var storageKey = await _storageService.UploadPrivateAsync(fileStream, key, contentType, cancellationToken);

        commission.Status = CommissionStatus.Delivered;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        var lastMilestone = commission.Milestones.OrderByDescending(m => m.Sequence).FirstOrDefault();
        if (lastMilestone != null)
        {
            lastMilestone.FinalDeliverableUrl = storageKey;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<CommissionDto> CompleteCommissionAsync(Guid id, Guid clientId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(id, clientId, creator: false, cancellationToken);
        await _dbContext.Entry(commission).Collection(c => c.Milestones).LoadAsync(cancellationToken);
        if (commission.Status != CommissionStatus.Delivered || commission.EscrowHeldAmount != 0
            || commission.Milestones.Any(m => m.Status != MilestoneStatus.Approved))
            throw new InvalidOperationException("Commission is not ready for completion.");

        commission.Status = CommissionStatus.Completed;
        commission.EscrowStatus = EscrowStatus.Released;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<string> GetFinalDownloadUrlAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var commission = await PartyCommissionAsync(id, userId, cancellationToken);
        if (commission.Status != CommissionStatus.Completed)
            throw new InvalidOperationException("Can only download final files when commission is completed.");

        await _dbContext.Entry(commission).Collection(c => c.Milestones).LoadAsync(cancellationToken);
        var lastMilestone = commission.Milestones.OrderByDescending(m => m.Sequence).FirstOrDefault();
        if (lastMilestone?.FinalDeliverableUrl == null)
            throw new InvalidOperationException("Final deliverable not found.");

        return _storageService.GeneratePresignedDownloadUrl(lastMilestone.FinalDeliverableUrl, TimeSpan.FromMinutes(15));
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
        await using var tx = await BeginTransactionAsync(cancellationToken);
        var commission = await PartyCommissionAsync(id, userId, cancellationToken);
        if (commission.Status is CommissionStatus.Completed or CommissionStatus.Cancelled or CommissionStatus.Disputed)
            throw new InvalidOperationException("Commission cannot be cancelled in this state.");
        if (commission.EscrowHeldAmount > 0 || commission.Status == CommissionStatus.Delivered)
            throw new InvalidOperationException("Funded commissions must be resolved through a dispute.");

        commission.Status = CommissionStatus.Cancelled;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null)
            await tx.CommitAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<DisputeDto> CreateDisputeAsync(Guid id, CreateDisputeRequest request, Guid raisedById, CancellationToken cancellationToken = default)
    {
        var validation = new CreateDisputeRequestValidator().Validate(request);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));

        var commission = await PartyCommissionAsync(id, raisedById, cancellationToken);
        if (commission.Status is CommissionStatus.Completed or CommissionStatus.Cancelled or CommissionStatus.Disputed)
            throw new InvalidOperationException("Commission cannot be disputed in this state.");
        if (await _dbContext.Disputes.AnyAsync(d => d.CommissionId == id, cancellationToken))
            throw new InvalidOperationException("Commission already has a dispute.");
        var dispute = new Dispute
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

    public async Task<DisputeDto?> GetDisputeByCommissionIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        await PartyCommissionAsync(id, userId, cancellationToken);
        var dispute = await _dbContext.Disputes.AsNoTracking().FirstOrDefaultAsync(d => d.CommissionId == id, cancellationToken);
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

    public async Task<ReviewDto> ReplyReviewAsync(Guid id, string replyComment, Guid creatorId, CancellationToken cancellationToken = default)
    {
        await OwnedCommissionAsync(id, creatorId, creator: true, cancellationToken);
        var review = await _dbContext.Reviews.FirstOrDefaultAsync(r => r.CommissionId == id, cancellationToken);
        if (review == null) throw new KeyNotFoundException("Không tìm thấy đánh giá cho đơn hàng này.");
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

    private Task<Guid> CreatorProfileIdAsync(Guid userId, CancellationToken cancellationToken) =>
        _dbContext.CreatorProfiles.Where(p => p.UserId == userId && !p.IsDeleted)
            .Select(p => p.Id).FirstOrDefaultAsync(cancellationToken);

    private Task<Guid> CreatorUserIdAsync(Guid creatorProfileId, CancellationToken cancellationToken) =>
        _dbContext.CreatorProfiles.Where(p => p.Id == creatorProfileId && !p.IsDeleted)
            .Select(p => p.UserId).SingleAsync(cancellationToken);

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
            VoucherId = c.VoucherId,
            LicenseType = c.LicenseType,
            LicenseMultiplierApplied = c.LicenseMultiplierApplied,
            DiscountAmount = c.DiscountAmount,
            TotalPrice = c.TotalPrice,
            FinalPrice = c.FinalPrice,
            EscrowHeldAmount = c.EscrowHeldAmount,
            DisbursedAmount = c.DisbursedAmount,
            EscrowStatus = c.EscrowStatus.ToString(),
            CurrentStage = c.CurrentStage,
            Status = c.Status.ToString(),
            DeadlineAt = c.DeadlineAt,
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
            VoucherId = dto.VoucherId,
            LicenseType = dto.LicenseType,
            LicenseMultiplierApplied = dto.LicenseMultiplierApplied,
            DiscountAmount = dto.DiscountAmount,
            TotalPrice = dto.TotalPrice,
            FinalPrice = dto.FinalPrice,
            EscrowHeldAmount = dto.EscrowHeldAmount,
            DisbursedAmount = dto.DisbursedAmount,
            EscrowStatus = dto.EscrowStatus,
            CurrentStage = dto.CurrentStage,
            Status = dto.Status,
            DeadlineAt = dto.DeadlineAt,
            CreatedAt = dto.CreatedAt,
            Milestones = c.Milestones.Select(MapToMilestoneDto).ToList(),
            Review = c.Reviews.FirstOrDefault() != null ? MapToReviewDto(c.Reviews.First()) : null,
            Dispute = c.Disputes.FirstOrDefault() != null ? MapToDisputeDto(c.Disputes.First()) : null
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

    private static DisputeDto MapToDisputeDto(Dispute d)
    {
        return new DisputeDto
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
            AdminNote = d.AdminNote,
            ResolvedAt = d.ResolvedAt,
            CreatedAt = d.CreatedAt
        };
    }
    #endregion
}
