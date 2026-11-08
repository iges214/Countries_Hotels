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

    public Task<T> ExecuteSerializableAsync<T>(Func<Task<T>> action) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var result = await action();
            await transaction.CommitAsync();
            return result;
        });

    public Task<Hotel?> LockBranchAsync(int hotelId)
    {
        // Room publishing, booking, cancellation and inventory edits acquire this same branch lock.
        var query = db.Database.IsSqlServer()
            ? db.Hotels.FromSqlInterpolated($"SELECT * FROM [Hotels] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {hotelId}")
            : db.Hotels.AsQueryable();
        return query.AsTracking().FirstOrDefaultAsync(h => h.Id == hotelId);
    }
}
