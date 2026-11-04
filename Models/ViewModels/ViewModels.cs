using System.ComponentModel.DataAnnotations;
using RideSync.Web.Services;

namespace RideSync.Web.Models.ViewModels;

public class RegisterVm
{
    [Required] public string Name { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, MinLength(6)] public string Password { get; set; } = "";
    [Required] public string Role { get; set; } = Roles.Rider;
    [Display(Name = "Vehicle number (drivers)")] public string? VehicleNumber { get; set; }
    [Display(Name = "Vehicle type (drivers)")] public string? VehicleType { get; set; } = "car";
}

public class LoginVm
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public class RiderPanelVm
{
    public Ride? Active { get; set; }
    public Ride? Unrated { get; set; }
    public double Balance { get; set; }
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N"); // one per rendered booking form
}

public class DriverPanelVm
{
    public Ride? Active { get; set; }
    public List<AvailableRide> Available { get; set; } = [];
    public double TodayEarnings { get; set; }
    public int TodayTrips { get; set; }
    public double? Rating { get; set; }
    public int RatingCount { get; set; }
}

public class HistoryVm { public List<Ride> Rides { get; set; } = []; }

public class WalletVm
{
    public double Balance { get; set; }
    public List<WalletTransaction> Transactions { get; set; } = [];
}

public class LabVm
{
    public AcceptLabReport? SafeAccept { get; set; }
    public AcceptLabReport? UnsafeAccept { get; set; }
    public WalletLabReport? SafeWallet { get; set; }
    public WalletLabReport? UnsafeWallet { get; set; }
}
