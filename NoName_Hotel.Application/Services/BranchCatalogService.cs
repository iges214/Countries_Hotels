using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.DTO.Offerings;
using NoName_Hotel.Application.DTO.Rooms;
using NoName_Hotel.Application.Validation;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Common.Enum;
using NoName_Hotel.Application.Querying;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Common.Results;
using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.Application.Services;

public static class BranchClock
{
    public static DateOnly Today(Hotel hotel) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(hotel.TimeZoneId)));
}

public sealed class BranchCatalogService(IHotelRepository repo) : IBranchCatalogService
{
    private static Result<T> Fail<T>(string code, string text) => Result<T>.Failure(new Error(code, text));

    private async Task<IQueryable<Room>> AvailableRooms(Hotel branch, RoomSearch search)
    {
        var start = search.CheckIn ?? BranchClock.Today(branch);
        var end = search.CheckOut ?? start.AddDays(1);
        var overlaps = await repo.Query<Booking>().Where(b => b.HotelId == branch.Id &&
            b.Status != BookingStatus.Cancelled && b.ActualCheckOutAt == null && b.CheckIn < end && b.CheckOut > start).ToListAsync();
        var capacity = await repo.Query<Room>().CountAsync(r => r.HotelId == branch.Id && r.IsOperational);
        var query = repo.Query<Room>().Where(r => r.HotelId == branch.Id && r.IsPublished && r.IsOperational && r.MaxGuests >= search.Guests);
        // Historical reservations have no room assignments. They still consume branch capacity.
        if (!BookingAvailability.HasCapacity(capacity, 1, start, end, overlaps)) return query.Where(r => false);
        var today = BranchClock.Today(branch);
        query = query.Where(r => !r.Reservations.Any(a => a.Booking!.Status != BookingStatus.Cancelled && a.Booking.ActualCheckOutAt == null &&
            ((a.Booking.CheckIn < end && a.Booking.CheckOut > start) || (a.Booking.ActualCheckInAt != null && start <= today))));
        if (search.IsTwinRoom.HasValue)
            query = search.IsTwinRoom.Value
                ? query.Where(r => r.Number >= 160 && r.Number <= 200)
                : query.Where(r => r.Number < 160 || r.Number > 200);
        if (search.IsHoneymoonSuite.HasValue)
            query = query.Where(r => r.IsHoneymoonSuite == search.IsHoneymoonSuite.Value);
        if (search.Category.HasValue)
            query = search.Category.Value switch
            {
                RoomCategory.Normal => query.Where(r => r.Number >= 100 && r.Number <= 200),
                RoomCategory.Vip => query.Where(r => r.Number >= 210 && r.Number <= 300),
                RoomCategory.Vvip => query.Where(r => r.Number >= 350 && r.Number <= 400),
                RoomCategory.Platinum => query.Where(r => r.Number >= 480 && r.Number <= 500),
                _ => query.Where(r => false)
            };
        return query;
    }

    public async Task<Result<PageResult<RoomDto>>> SearchRooms(int hotelId, RoomSearch search, PaginationParameters page)
    {
        var errors = DtoValidation.Errors(search);
        if (errors.Length > 0) return Result<PageResult<RoomDto>>.BadRequest(errors);
        var branch = await repo.Query<Hotel>().FirstOrDefaultAsync(h => h.Id == hotelId);
        if (branch is null || !branch.IsActive) return Fail<PageResult<RoomDto>>(ErrorCodes.NotFound, "Branch not found.");
        if (search.CheckIn < BranchClock.Today(branch)) return Fail<PageResult<RoomDto>>(ErrorCodes.Validation, "Check-in cannot be in the past in this branch.");
        var query = await AvailableRooms(branch, search);
        return Result<PageResult<RoomDto>>.Success(await RoomPage(query, page, branch.CurrencyCode));
    }

    public async Task<Result<RoomDto>> GetPublicRoom(int hotelId, int roomId, RoomSearch search)
    {
        var errors = DtoValidation.Errors(search);
        if (errors.Length > 0) return Result<RoomDto>.BadRequest(errors);
        var branch = await repo.Query<Hotel>().FirstOrDefaultAsync(h => h.Id == hotelId);
        if (branch is null || !branch.IsActive) return Fail<RoomDto>(ErrorCodes.NotFound, "Branch not found.");
        if (search.CheckIn < BranchClock.Today(branch)) return Fail<RoomDto>(ErrorCodes.Validation, "Check-in cannot be in the past in this branch.");
        var query = await AvailableRooms(branch, search);
        var room = await query.Include(r => r.Photos).FirstOrDefaultAsync(r => r.Id == roomId);
        return room is null ? Fail<RoomDto>(ErrorCodes.NotFound, "Room is not available for this stay.") : Result<RoomDto>.Success(ToDto(room, branch.CurrencyCode));
    }

    public async Task<Result<PageResult<RoomDto>>> GetAdminRooms(int hotelId, PaginationParameters page)
    {
        var branch = await repo.Query<Hotel>().FirstOrDefaultAsync(h => h.Id == hotelId);
        return branch is null ? Fail<PageResult<RoomDto>>(ErrorCodes.NotFound, "Branch not found.") :
            Result<PageResult<RoomDto>>.Success(await RoomPage(repo.Query<Room>().Where(r => r.HotelId == hotelId), page, branch.CurrencyCode));
    }

    private static async Task<PageResult<RoomDto>> RoomPage(IQueryable<Room> query, PaginationParameters page, string currency)
    {
        var result = await query.Include(r => r.Photos).OrderBy(r => r.Number).ThenBy(r => r.Id).ToPageResultAsync(page);
        return new() { Data = result.Data.Select(r => ToDto(r, currency)).ToList(), Metadata = result.Metadata };
    }

    public async Task<Result<RoomDto>> SaveRoom(int hotelId, int? roomId, SaveRoomDto dto)
    {
        var errors = DtoValidation.Errors(dto);
        if (errors.Length > 0) return Result<RoomDto>.BadRequest(errors);
        return await repo.ExecuteSerializableAsync(async () =>
        {
            var branch = await repo.LockBranchAsync(hotelId);
            if (branch is null) return Fail<RoomDto>(ErrorCodes.NotFound, "Branch not found.");
            var room = roomId.HasValue ? await repo.Query<Room>(true).Include(r => r.Photos)
                .FirstOrDefaultAsync(r => r.Id == roomId && r.HotelId == hotelId) : new Room { HotelId = hotelId };
            if (room is null) return Fail<RoomDto>(ErrorCodes.NotFound, "Room not found.");
            if (await repo.Query<Room>().AnyAsync(r => r.HotelId == hotelId && r.Number == dto.Number && r.Id != (roomId ?? 0)))
                return Fail<RoomDto>(ErrorCodes.Conflict, "This room number already exists in this branch.");
            if (dto.IsPublished && (!room.Photos.Any(p => p.Kind == RoomPhotoKind.Room) || !room.Photos.Any(p => p.Kind == RoomPhotoKind.Bathroom)))
                return Fail<RoomDto>(ErrorCodes.Validation, "Upload room and bathroom photos before publishing.");
            var reserved = roomId.HasValue && await repo.Query<BookingRoom>().AnyAsync(a => a.RoomId == roomId &&
                a.Booking!.Status != BookingStatus.Cancelled && a.Booking.ActualCheckOutAt == null && (a.Booking.CheckOut > BranchClock.Today(branch) || a.Booking.ActualCheckInAt != null));
            if (reserved && (!dto.IsOperational || dto.Number != room.Number || dto.MaxGuests < room.MaxGuests ||
                !string.Equals(dto.BedType.Trim(), room.BedType, StringComparison.OrdinalIgnoreCase) ||
                (room.IsHoneymoonSuite && !dto.IsHoneymoonSuite)))
                return Fail<RoomDto>(ErrorCodes.Conflict, "A room with upcoming reservations cannot be disabled, renumbered, have its bed layout changed, or lose guest capacity or its honeymoon designation.");
            if (roomId.HasValue && room.IsOperational && !dto.IsOperational)
            {
                var capacity = await repo.Query<Room>().CountAsync(r => r.HotelId == hotelId && r.IsOperational) - 1;
                var today = BranchClock.Today(branch);
                var bookings = await repo.Query<Booking>().Where(b => b.HotelId == hotelId && b.Status != BookingStatus.Cancelled && b.CheckOut > today).ToListAsync();
                var lastDay = bookings.Count == 0 ? today.AddDays(1) : bookings.Max(b => b.CheckOut);
                if (!BookingAvailability.HasCapacity(capacity, 0, today, lastDay, bookings))
                    return Fail<RoomDto>(ErrorCodes.Conflict, "Disabling this room would leave insufficient capacity for existing reservations.");
            }
            room.Number = dto.Number; room.Description = dto.Description.Trim(); room.BedType = dto.BedType.Trim();
            if (RoomNumbering.IsTwinRoom(dto.Number)) room.BedType = "Twin";
            room.IsHoneymoonSuite = dto.IsHoneymoonSuite;
            room.Amenities = dto.Amenities.Trim(); room.MaxGuests = dto.MaxGuests;
            room.SizeSquareMetres = dto.SizeSquareMetres; room.PerNightRate = dto.PerNightRate;
            room.IsPublished = dto.IsPublished; room.IsOperational = dto.IsOperational;
            if (!roomId.HasValue) repo.Add(room);
            await repo.SaveChangesAsync();
            branch.TotalRooms = await repo.Query<Room>().CountAsync(r => r.HotelId == hotelId && r.IsOperational);
            await repo.SaveChangesAsync();
            return Result<RoomDto>.Success(ToDto(room, branch.CurrencyCode));
        });
    }

    public Task<Result<RoomPhotoDto>> AddPhoto(int hotelId, int roomId, RoomPhotoKind kind, string url, string caption) =>
        repo.ExecuteSerializableAsync(async () =>
        {
            if (!Enum.IsDefined(kind) || !MediaUrls.IsValid(url) || caption.Length > 200)
                return Fail<RoomPhotoDto>(ErrorCodes.Validation, "Invalid photo type, URL or caption.");
            if (await repo.LockBranchAsync(hotelId) is null) return Fail<RoomPhotoDto>(ErrorCodes.NotFound, "Branch not found.");
            if (!await repo.Query<Room>().AnyAsync(r => r.Id == roomId && r.HotelId == hotelId))
                return Fail<RoomPhotoDto>(ErrorCodes.NotFound, "Room not found.");
            if (await repo.Query<RoomPhoto>().CountAsync(p => p.RoomId == roomId) >= 20)
                return Fail<RoomPhotoDto>(ErrorCodes.Conflict, "A room can have at most 20 photos.");
            var photo = new RoomPhoto { RoomId = roomId, Kind = kind, Url = url, Caption = caption.Trim() };
            repo.Add(photo); await repo.SaveChangesAsync();
            return Result<RoomPhotoDto>.Success(new(photo.Id, kind.ToString(), photo.Url, photo.Caption));
        });

    public Task<Result> DeletePhoto(int hotelId, int roomId, int photoId) => repo.ExecuteSerializableAsync(async () =>
    {
        if (await repo.LockBranchAsync(hotelId) is null) return Result.NotFound(new Error(ErrorCodes.NotFound, "Branch not found."));
        var room = await repo.Query<Room>(true).Include(r => r.Photos).FirstOrDefaultAsync(r => r.Id == roomId && r.HotelId == hotelId);
        var photo = room?.Photos.FirstOrDefault(p => p.Id == photoId);
        if (photo is null) return Result.NotFound(new Error(ErrorCodes.NotFound, "Photo not found."));
        if (room!.IsPublished && room.Photos.Count(p => p.Kind == photo.Kind) == 1)
            return Result.Failure(new Error(ErrorCodes.Conflict, "Unpublish the room before removing its last room or bathroom photo."));
        repo.Remove(photo); await repo.SaveChangesAsync(); return Result.Success();
    });

    public async Task<Result<PageResult<OfferingDto>>> GetOfferings(int hotelId, OfferingKind? kind, string? search, PaginationParameters page, bool admin = false)
    {
        if (kind.HasValue && !Enum.IsDefined(kind.Value)) return Fail<PageResult<OfferingDto>>(ErrorCodes.Validation, "Invalid offering category.");
        var branch = await repo.Query<Hotel>().FirstOrDefaultAsync(h => h.Id == hotelId);
        if (branch is null || (!admin && !branch.IsActive)) return Fail<PageResult<OfferingDto>>(ErrorCodes.NotFound, "Branch not found.");
        var query = repo.Query<BranchOffering>().Where(o => o.HotelId == hotelId && (admin || o.IsPublished));
        if (kind.HasValue) query = query.Where(o => o.Kind == kind);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(o => o.Name.Contains(search.Trim()) || o.Description.Contains(search.Trim()));
        var result = await query.OrderBy(o => o.Name).ThenBy(o => o.Id).ToPageResultAsync(page);
        return Result<PageResult<OfferingDto>>.Success(new() { Data = result.Data.Select(o => ToDto(o, branch.CurrencyCode)).ToList(), Metadata = result.Metadata });
    }

    public async Task<Result<OfferingDto>> SaveOffering(int hotelId, int? offeringId, SaveOfferingDto dto)
    {
        var errors = DtoValidation.Errors(dto);
        if (errors.Length > 0) return Result<OfferingDto>.BadRequest(errors);
        var branch = await repo.Query<Hotel>().FirstOrDefaultAsync(h => h.Id == hotelId);
        if (branch is null) return Fail<OfferingDto>(ErrorCodes.NotFound, "Branch not found.");
        var offering = offeringId.HasValue ? await repo.Query<BranchOffering>(true).FirstOrDefaultAsync(o => o.Id == offeringId && o.HotelId == hotelId) : new BranchOffering { HotelId = hotelId };
        if (offering is null) return Fail<OfferingDto>(ErrorCodes.NotFound, "Offering not found.");
        offering.Kind = dto.Kind; offering.Name = dto.Name.Trim(); offering.Description = dto.Description.Trim();
        offering.ImageUrl = dto.ImageUrl; offering.OpeningHours = dto.OpeningHours.Trim(); offering.Price = dto.Price; offering.IsPublished = dto.IsPublished;
        if (!offeringId.HasValue) repo.Add(offering);
        await repo.SaveChangesAsync(); return Result<OfferingDto>.Success(ToDto(offering, branch.CurrencyCode));
    }

    public async Task<Result> DeleteOffering(int hotelId, int offeringId)
    {
        var offering = await repo.Query<BranchOffering>(true).FirstOrDefaultAsync(o => o.Id == offeringId && o.HotelId == hotelId);
        if (offering is null) return Result.NotFound(new Error(ErrorCodes.NotFound, "Offering not found."));
        repo.Remove(offering); await repo.SaveChangesAsync(); return Result.Success();
    }

    private static RoomDto ToDto(Room r, string currency) => new(r.Id, r.HotelId, r.Number, r.Category.ToString(), r.Floor,
        r.Description, r.BedType, r.Amenities, r.MaxGuests, r.SizeSquareMetres, r.PerNightRate, currency, r.IsPublished, r.IsOperational,
        r.Photos.OrderBy(p => p.Kind).ThenBy(p => p.Id).Select(p => new RoomPhotoDto(p.Id, p.Kind.ToString(), p.Url, p.Caption)).ToList(),
        r.IsTwinRoom, r.IsHoneymoonSuite);
    private static OfferingDto ToDto(BranchOffering o, string currency) => new(o.Id, o.HotelId, o.Kind.ToString(), o.Name, o.Description,
        o.ImageUrl, o.OpeningHours, o.Price, currency, o.IsPublished);
}
