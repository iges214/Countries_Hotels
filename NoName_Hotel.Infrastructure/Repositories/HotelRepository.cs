using System.Data;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Domain.Entities.Models;
using NoName_Hotel.Infrastructure.Data;

namespace NoName_Hotel.Infrastructure.Repositories;

public sealed class HotelRepository(NoName_hotel_DbContext db) : IHotelRepository
{
    public IQueryable<T> Query<T>(bool tracking = false) where T : class =>
        tracking ? db.Set<T>().AsTracking() : db.Set<T>().AsNoTracking();
    public void Add<T>(T entity) where T : class => db.Add(entity);
    public void Remove<T>(T entity) where T : class => db.Remove(entity);
    public void Attach<T>(T entity) where T : class => db.Attach(entity);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);

    public Task<T> ExecuteSerializableAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            db.ChangeTracker.Clear();
            token.ThrowIfCancellationRequested();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var result = await action();
            await transaction.CommitAsync(token);
            return result;
        }, cancellationToken);

    public Task<Hotel?> LockBranchAsync(int hotelId, CancellationToken cancellationToken = default)
    {
        // Room publishing, booking, cancellation and inventory edits acquire this same branch lock.
        var query = db.Database.IsSqlServer()
            ? db.Hotels.FromSqlInterpolated($"SELECT * FROM [Hotels] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {hotelId}")
            : db.Hotels.AsQueryable();
        return query.AsTracking().FirstOrDefaultAsync(h => h.Id == hotelId, cancellationToken);
    }

    public async Task<bool> LockBookingUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Lock the account before the branch so the same request key cannot race
        // across branches. The unique database index remains the final backstop.
        var query = db.Database.IsSqlServer()
            ? db.Users.FromSqlInterpolated($"SELECT * FROM [AspNetUsers] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {userId}")
            : db.Users.Where(user => user.Id == userId);
        return await query.Select(user => user.Id).FirstOrDefaultAsync(cancellationToken) is not null;
    }
}
