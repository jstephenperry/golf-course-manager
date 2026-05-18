using FairwayHq.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FairwayHq.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Nine> Nines => Set<Nine>();
    public DbSet<NineTeeSet> NineTeeSets => Set<NineTeeSet>();
    public DbSet<Hole> Holes => Set<Hole>();
    public DbSet<HoleYardage> HoleYardages => Set<HoleYardage>();
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
    public DbSet<MemberLedgerEntry> MemberLedgerEntries => Set<MemberLedgerEntry>();

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

        // Drives the GET /api/members/{id}/ledger list query
        // (PostedAt DESC, Id DESC tiebreaker).
        mb.Entity<MemberLedgerEntry>()
            .HasIndex(e => new { e.MemberId, e.PostedAt });

        // Nine owns its tee sets and holes; deleting a Nine cascades to both.
        mb.Entity<Nine>()
            .HasMany(n => n.TeeSets)
            .WithOne()
            .HasForeignKey(t => t.NineId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Nine>()
            .HasMany(n => n.Holes)
            .WithOne()
            .HasForeignKey(h => h.NineId)
            .OnDelete(DeleteBehavior.Cascade);

        // A Hole owns its per-tee yardage rows. Deleting a tee set also
        // cascades through HoleYardage rows that reference it so the data
        // stays consistent.
        mb.Entity<Hole>()
            .HasMany(h => h.Yardages)
            .WithOne()
            .HasForeignKey(y => y.HoleId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<HoleYardage>()
            .HasOne<NineTeeSet>()
            .WithMany()
            .HasForeignKey(y => y.TeeSetId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<HoleYardage>()
            .HasIndex(y => new { y.HoleId, y.TeeSetId })
            .IsUnique();

        // Courses reference Nines but do not own them; block deletion of a
        // Nine that's still wired up as a front or back so we don't quietly
        // break the course in the booking UI.
        mb.Entity<Course>()
            .HasOne<Nine>()
            .WithMany()
            .HasForeignKey(c => c.FrontNineId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Course>()
            .HasOne<Nine>()
            .WithMany()
            .HasForeignKey(c => c.BackNineId)
            .OnDelete(DeleteBehavior.Restrict);

        // SQLite stores decimals as TEXT for accuracy
        foreach (var prop in mb.Model.GetEntityTypes()
                     .SelectMany(e => e.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            prop.SetColumnType("TEXT");
        }
    }
}
