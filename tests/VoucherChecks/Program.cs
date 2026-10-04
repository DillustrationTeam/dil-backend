using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var creatorId = Guid.NewGuid();
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("voucher-owner-check-" + Guid.NewGuid()).Options);
db.Vouchers.Add(new Voucher
{
    VoucherCode = "OWN10",
    CreatedByUserId = creatorId,
    DiscountType = DiscountType.Percent,
    DiscountValue = 10,
    Scope = VoucherScope.Commission,
    StartDate = VoucherCheckService.TodayInVietnam(),
    EndDate = VoucherCheckService.TodayInVietnam().AddDays(1)
});
await db.SaveChangesAsync();

var result = await new VoucherCheckService(db).CheckAsync(
    creatorId, "OWN10", 100_000m, "Commission", Guid.NewGuid());

if (result.Success || result.Reason != "Bạn không thể sử dụng mã giảm giá do chính mình tạo.")
    throw new Exception("Voucher owner was allowed to use their own voucher.");

Console.WriteLine("Voucher owner check passed.");
