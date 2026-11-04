using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RideSync.Web.Models;

public static class Roles { public const string Rider = "Rider", Driver = "Driver"; }

public static class RideStatus
{
    public const string Pending = "pending", Accepted = "accepted", DriverArrived = "driver_arrived",
        InProgress = "in_progress", Completed = "completed", Cancelled = "cancelled";
    public static bool IsActive(string s) => s is Pending or Accepted or DriverArrived or InProgress;
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    [MaxLength(256)] public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Rider;
    public string? VehicleNumber { get; set; }
    public string? VehicleType { get; set; }          // driver's vehicle class: bike | car | xl
    public int RatingSum { get; set; }                // driver rating aggregate (average = sum / count)
    public int RatingCount { get; set; }
    [NotMapped] public double? AvgRating => RatingCount == 0 ? null : Math.Round((double)RatingSum / RatingCount, 2);

    /// Wallet balance in cents: integers avoid float rounding and allow atomic SQL arithmetic.
    public long BalanceCents { get; set; }

    /// SQL Server rowversion: the DB bumps it automatically on every UPDATE. EF adds "WHERE RowVersion = @old" to UPDATEs;
    /// a stale writer updates 0 rows → DbUpdateConcurrencyException.
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public class Ride
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RiderId { get; set; }
    public Guid? DriverId { get; set; }
    public User? Rider { get; set; }
    public User? Driver { get; set; }

    public string Pickup { get; set; } = "";
    public string Drop { get; set; } = "";
    public double PickupLat { get; set; }
    public double PickupLng { get; set; }
    public double DropLat { get; set; }
    public double DropLng { get; set; }
    public double DistanceKm { get; set; }
    public int DurationMin { get; set; }
    [MaxLength(10)] public string VehicleType { get; set; } = "car";
    [MaxLength(10)] public string PaymentMethod { get; set; } = "cash";   // cash | wallet
    public double Surge { get; set; } = 1.0;
    public double Fare { get; set; }
    public int? DriverRating { get; set; }                                 // 1-5, set once by the rider
    [MaxLength(20)] public string Status { get; set; } = RideStatus.Pending;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public class WalletTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? RideId { get; set; }
    public string Type { get; set; } = "credit";   // credit | debit
    public long AmountCents { get; set; }
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
