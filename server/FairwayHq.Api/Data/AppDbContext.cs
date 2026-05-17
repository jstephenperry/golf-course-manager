using FairwayHq.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FairwayHq.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<TeeTime> TeeTimes => Set<TeeTime>();
    public DbSet<StaffMember> Staff => Set<StaffMember>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<WeeklyTemplate> WeeklyTemplates => Set<WeeklyTemplate>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<MaintenanceTask> Maintenance => Set<MaintenanceTask>();
    public DbSet<PlayerTab> Tabs => Set<PlayerTab>();
    public DbSet<TabLineItem> TabLineItems => Set<TabLineItem>();
    public DbSet<TabPayment> TabPayments => Set<TabPayment>();
    public DbSet<MemberApplication> MemberApplications => Set<MemberApplication>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<PlayerTab>()
            .HasMany(t => t.Items)
            .WithOne()
            .HasForeignKey(li => li.TabId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<PlayerTab>()
            .HasMany(t => t.Payments)
            .WithOne()
            .HasForeignKey(p => p.TabId)
            .OnDelete(DeleteBehavior.Cascade);

        // SQLite stores decimals as TEXT for accuracy
        foreach (var prop in mb.Model.GetEntityTypes()
                     .SelectMany(e => e.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            prop.SetColumnType("TEXT");
        }
    }
}
