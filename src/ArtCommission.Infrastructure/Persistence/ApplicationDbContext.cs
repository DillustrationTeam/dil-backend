using ArtCommission.Domain.Entities.Commission;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Commission> Commissions => Set<Commission>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Dispute> Disputes => Set<Dispute>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
