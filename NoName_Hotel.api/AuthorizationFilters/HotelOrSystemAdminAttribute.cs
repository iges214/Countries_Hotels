using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Domain.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NoName_Hotel.api.AuthorizationFilters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class HotelOrSystemAdminAttribute : TypeFilterAttribute
    {
        public HotelOrSystemAdminAttribute() : base(typeof(HotelOrSystemAdminFilter))
        {
        }
    }

    public class HotelOrSystemAdminFilter(NoName_hotel_DbContext DbContext) : IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var httpUser = context.HttpContext.User;

            if (httpUser?.Identity?.IsAuthenticated == false)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            //if user is a global administrator, allow immediately
            if (httpUser!.IsInRole("Administrator"))
            {
                return;
            }

            var userId = httpUser.FindFirst(JwtRegisteredClaimNames.Sub)?
                .Value ?? httpUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                context.Result = new ForbidResult();
                return;
            }

            // Try to get hotelId from route values
            context.RouteData.Values.TryGetValue("hotelId", out var hotelIdObj);
            int.TryParse(hotelIdObj?.ToString(), out int hotelId);
            if (hotelId == 0)
            {
                context.Result = new ForbidResult();
                return;
            }


            //checking if user is an admin for this specific hotel
            var isHotelAdminUser = await DbContext.HotelAdmins
                .AnyAsync(a => a.UserId == userId && a.HotelId == hotelId);

            if (!isHotelAdminUser)
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }
}
