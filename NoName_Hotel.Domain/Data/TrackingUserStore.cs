using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.Domain.Data;

// Identity changes access-failure counters, lockout state and concurrency stamps.
// Its users must be tracked even though catalogue reads default to no tracking.
public sealed class TrackingUserStore(NoName_hotel_DbContext context, IdentityErrorDescriber errors)
    : UserStore<ApplicationUser, IdentityRole, NoName_hotel_DbContext>(context, errors)
{
    public override IQueryable<ApplicationUser> Users => base.Users.AsTracking();

    public override Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default) =>
        Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
}
