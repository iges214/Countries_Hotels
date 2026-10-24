using NoName_Hotel.Domain.Entities.Models;
namespace NoName_Hotel.Application.Services;
public static class BookingAvailability
{
    // Check the maximum rooms occupied on any night; check-out does not occupy a room.
    public static bool HasCapacity(int totalRooms, int requestedRooms, DateOnly checkIn,
        DateOnly checkOut, IEnumerable<Booking> overlappingBookings)
    {
        if (requestedRooms < 0 || totalRooms < requestedRooms || checkOut <= checkIn) return false;
        var events = new SortedDictionary<DateOnly, int>();
        foreach (var booking in overlappingBookings)
        {
            var start = booking.CheckIn > checkIn ? booking.CheckIn : checkIn;
            var end = booking.CheckOut < checkOut ? booking.CheckOut : checkOut;
            if (start >= end) continue;
            events[start] = events.GetValueOrDefault(start) + booking.Rooms;
            events[end] = events.GetValueOrDefault(end) - booking.Rooms;
        }
        var occupied = 0;
        foreach (var delta in events.Values)
        {
            occupied += delta;
            if (occupied + requestedRooms > totalRooms) return false;
        }
        return true;
    }
}
