using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.Application.Contracts;

// A scoped repository/unit of work. Queries are deferred; mutations share one transaction.
public interface IHotelRepository
{
    IQueryable<T> Query<T>(bool tracking = false) where T : class;
    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    void Attach<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<T> ExecuteSerializableAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task<Hotel?> LockBranchAsync(int hotelId, CancellationToken cancellationToken = default);
    Task<bool> LockBookingUserAsync(string userId, CancellationToken cancellationToken = default);
}
