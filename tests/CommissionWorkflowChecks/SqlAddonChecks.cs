using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Commission;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using ArtCommission.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Uses a fresh, uniquely named local database; deletes only that database in finally.
internal static class SqlAddonChecks
{
    public static async Task RunAsync()
    {
        var connection = new SqlConnectionStringBuilder {
            DataSource = @".\SQLEXPRESS", InitialCatalog = "AddonChecks_" + Guid.NewGuid().ToString("N"),
            IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10
        };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var seed = new AppDbContext(options);
        var checks = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
        try
        {
            await seed.Database.EnsureCreatedAsync();
            var client = new ApplicationUser { Id = Guid.NewGuid(), UserName = "addon-client", NormalizedUserName = "ADDON-CLIENT", FullName = "Addon test client" };
            var creator = new ApplicationUser { Id = Guid.NewGuid(), UserName = "addon-creator", NormalizedUserName = "ADDON-CREATOR", FullName = "Addon test creator" };
            var profile = new CreatorProfile { UserId = creator.Id, DisplayName = "Addon test" };
            var c = new Commission { ClientId = client.Id, CreatorId = profile.Id, Title = "SQL addon checks",
                Status = CommissionStatus.InProgress, EscrowStatus = EscrowStatus.Deposited,
                TotalPrice = 300, FinalPrice = 300, EscrowHeldAmount = 300, DeadlineAt = DateTimeOffset.UtcNow.AddDays(7),
                Milestones = Enumerable.Range(1, 3).Select(i => new Milestone { Sequence = i, Title = "Stage " + i, Price = 100 }).ToList() };
            seed.Users.AddRange(client, creator); seed.CreatorProfiles.Add(profile); seed.Commissions.Add(c);
            await seed.SaveChangesAsync();
            var wallets = new WalletService(seed);
            var wallet = await wallets.GetOrCreateWalletAsync(client.Id);
            await wallets.CreditAsync(wallet, WalletTransactionType.Deposit, 1000, null, null, "Test deposit");
            await wallets.HoldFundsAsync(wallet, WalletTransactionType.EscrowHold, 300, "Commission", c.Id, "Test hold");
            var service = new AddonService(seed, wallets);
            var a = await service.ProposeAsync(c.Id, new ProposeAddonRequest { Title = "SQL extra", Price = 150, ExtendDays = 1 }, creator.Id, default);

            // Throw after SaveChanges has written the wallet/ledger/commission. The transaction must undo every write.
            var failure = new FailAfterSave();
            var failingOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).AddInterceptors(failure).Options;
            await using (var failing = new AppDbContext(failingOptions))
            {
                try { await new AddonService(failing, new WalletService(failing)).RespondAsync(c.Id, a.Id, new("Accept"), client.Id, default); throw new Exception("Failure was not injected"); }
                catch (InjectedFailure) { checks++; }
            }
            seed.ChangeTracker.Clear();
            wallet = await seed.Wallets.SingleAsync(x => x.UserId == client.Id);
            var persisted = await seed.Commissions.SingleAsync(x => x.Id == c.Id);
            Check(wallet.Balance == 700 && wallet.LockedBalance == 300, "Rollback restores both wallet balances");
            Check(persisted.FinalPrice == 300 && persisted.EscrowHeldAmount == 300, "Rollback restores contract and escrow");
            Check(await seed.Set<CommissionAddon>().AnyAsync(x => x.Id == a.Id && x.Status == "Proposed"), "Rollback restores proposal");
            Check(!await seed.WalletTransactions.AnyAsync(x => x.RefId == a.Id), "Rollback removes ledger entry");

            async Task Accept()
            {
                await using var db = new AppDbContext(options);
                await new AddonService(db, new WalletService(db)).RespondAsync(c.Id, a.Id, new("Accept"), client.Id, default);
            }
            await Task.WhenAll(Accept(), Accept());
            seed.ChangeTracker.Clear();
            wallet = await seed.Wallets.SingleAsync(x => x.UserId == client.Id);
            persisted = await seed.Commissions.SingleAsync(x => x.Id == c.Id);
            Check(wallet.Balance == 550 && wallet.LockedBalance == 450, "Concurrent accepts charge exactly once");
            Check(persisted.FinalPrice == 450 && persisted.EscrowHeldAmount == 450, "Concurrent accepts increase contract once");
            Check(await seed.WalletTransactions.CountAsync(x => x.RefId == a.Id) == 1, "Concurrent accepts create one ledger entry");
            Check((await new WalletService(seed).VerifyLedgerConsistencyAsync(wallet.Id)).Count == 0, "Real SQL ledger reconciles");
            Console.WriteLine($"Passed {checks} SQL Server add-on transaction checks.");
        }
        finally
        {
            if (!connection.InitialCatalog.StartsWith("AddonChecks_", StringComparison.Ordinal)) throw new Exception("Unexpected test database");
            await seed.Database.EnsureDeletedAsync();
        }
    }

    private sealed class InjectedFailure : Exception { }
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
            => throw new InjectedFailure();
    }
}
