using Microsoft.EntityFrameworkCore;
using VoucherTracker.Api.Models;

namespace VoucherTracker.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<RedemptionAttempt> RedemptionAttempts => Set<RedemptionAttempt>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<FraudFlag> FraudFlags => Set<FraudFlag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Voucher>()
            .HasOne(v => v.Sender)
            .WithMany(u => u.Vouchers)
            .HasForeignKey(v => v.SenderId);

        modelBuilder.Entity<RedemptionAttempt>()
            .HasOne(r => r.Voucher)
            .WithMany(v => v.RedemptionAttempts)
            .HasForeignKey(r => r.VoucherId);

        modelBuilder.Entity<FraudFlag>()
            .HasOne(f => f.Voucher)
            .WithMany(v => v.FraudFlags)
            .HasForeignKey(f => f.VoucherId);

        modelBuilder.Entity<Voucher>()
            .Property(v => v.Amount)
            .HasColumnType("decimal(18,2)");
    }
}