using Microsoft.EntityFrameworkCore;
using RideSync.Web.Data;
using RideSync.Web.Models;

namespace RideSync.Web.Services;

/// <summary>
/// All balance changes are ONE atomic SQL statement, never "read balance → compute in C# → write back".
///
///   NAIVE (race):   user.Balance = user.Balance - 80;  SaveChanges();
///        two parallel withdrawals of 80 from a balance of 100 both read 100, both write 20 → 160 withdrawn (double spend)
///   OURS (atomic):  UPDATE Users SET Balance = Balance - 80 WHERE Id = @id AND Balance >= 80
///        the DB serialises the two statements; the second sees 20, matches 0 rows → "insufficient funds".
/// </summary>
public class WalletService(AppDb db)
{
    /// Starts a transaction only if the caller hasn't already: lets "complete the ride + pay the driver" share ONE transaction.
    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginAsync() =>
        db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync() : null;

    /// Returns the new balance, or null if funds are insufficient.
    public async Task<long?> DebitAsync(Guid userId, long cents, string description, Guid? rideId = null)
    {
        await using var tx = await BeginAsync();
        var rows = await db.Users.Where(u => u.Id == userId && u.BalanceCents >= cents)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.BalanceCents, u => u.BalanceCents - cents));
        if (rows == 0) return null;
        db.Transactions.Add(new WalletTransaction { UserId = userId, RideId = rideId, Type = "debit", AmountCents = cents, Description = description });
        await db.SaveChangesAsync();
        if (tx is not null) await tx.CommitAsync();
        return await db.Users.Where(u => u.Id == userId).Select(u => u.BalanceCents).SingleAsync();
    }

    public async Task<long> CreditAsync(Guid userId, long cents, string description, Guid? rideId = null)
    {
        await using var tx = await BeginAsync();
        await db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.BalanceCents, u => u.BalanceCents + cents));
        db.Transactions.Add(new WalletTransaction { UserId = userId, RideId = rideId, Type = "credit", AmountCents = cents, Description = description });
        await db.SaveChangesAsync();
        if (tx is not null) await tx.CommitAsync();
        return await db.Users.Where(u => u.Id == userId).Select(u => u.BalanceCents).SingleAsync();
    }
}
