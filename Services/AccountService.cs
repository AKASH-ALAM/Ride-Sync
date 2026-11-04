using Microsoft.EntityFrameworkCore;
using RideSync.Web.Data;
using RideSync.Web.Models;

namespace RideSync.Web.Services;

public class AccountService(AppDb db)
{
    public async Task<Result<User>> RegisterAsync(string name, string email, string password, string role, string? vehicle, string? vehicleType)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || (password?.Length ?? 0) < 6)
            return Result<User>.Fail(Outcome.Invalid, "Name, email and a 6+ character password are required");
        if (role == Roles.Driver && string.IsNullOrWhiteSpace(vehicle))
            return Result<User>.Fail(Outcome.Invalid, "Vehicle number is required for drivers");

        var user = new User
        {
            Name = name.Trim(), Email = email.Trim().ToLowerInvariant(), Role = role, VehicleNumber = vehicle, VehicleType = role == Roles.Driver ? (vehicleType ?? "car") : null,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        };
        db.Users.Add(user);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) // the unique index is the real guard; check-then-insert would itself be a race
        { return Result<User>.Fail(Outcome.Conflict, "Email already registered"); }
        return Result<User>.Success(user);
    }

    public async Task<User?> LoginAsync(string email, string password)
    {
        var e = email?.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == e);
        return user is not null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) ? user : null;
    }
}
