using Microsoft.EntityFrameworkCore;
using RideSync.Web.Models;

namespace RideSync.Web.Data;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Ride> Rides => Set<Ride>();
    public DbSet<WalletTransaction> Transactions => Set<WalletTransaction>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<Ride>().HasIndex(r => new { r.Status, r.RequestedAt });
        b.Entity<Ride>().HasIndex(r => r.DriverId);
        b.Entity<Ride>().HasIndex(r => r.RiderId);
        b.Entity<WalletTransaction>().HasIndex(t => new { t.UserId, t.CreatedAt });
    }
}
