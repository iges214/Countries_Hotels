using System.Data;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Domain.Data;
using NoName_Hotel.Domain.Entities.Models;
namespace NoName_Hotel.Application.Services;
internal static class ReservationTransactions
{
    public static Task<T> RunAsync<T>(NoName_hotel_DbContext db, Func<Task<T>> action) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            if (!db.Database.IsRelational()) return await action();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var result = await action();
            await transaction.CommitAsync();
            return result;
        });

    public static Task<Hotel?> LockHotelAsync(NoName_hotel_DbContext db, int id)
    {
        // All inventory-changing operations acquire the same hotel lock before reading bookings.
        var query = db.Database.IsSqlServer()
            ? db.Hotels.FromSqlInterpolated($"SELECT * FROM [Hotels] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
            : db.Hotels.AsQueryable();
        return query.AsTracking().FirstOrDefaultAsync(h => h.Id == id);
    }
}
