using NoName_Hotel.Application.DTO.Rooms;
using NoName_Hotel.Application.DTO.Offerings;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Common.Results;
using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.Application.Contracts;

public interface IBranchCatalogService
{
    Task<Result<PageResult<RoomDto>>> SearchRooms(int hotelId, RoomSearch search, PaginationParameters page);
    Task<Result<RoomDto>> GetPublicRoom(int hotelId, int roomId, RoomSearch search);
    Task<Result<PageResult<RoomDto>>> GetAdminRooms(int hotelId, PaginationParameters page);
    Task<Result<RoomDto>> SaveRoom(int hotelId, int? roomId, SaveRoomDto dto);
    Task<Result<RoomPhotoDto>> AddPhoto(int hotelId, int roomId, RoomPhotoKind kind, string url, string caption);
    Task<Result> DeletePhoto(int hotelId, int roomId, int photoId);
    Task<Result<PageResult<OfferingDto>>> GetOfferings(int hotelId, OfferingKind? kind, string? search, PaginationParameters page, bool admin = false);
    Task<Result<OfferingDto>> SaveOffering(int hotelId, int? offeringId, SaveOfferingDto dto);
    Task<Result> DeleteOffering(int hotelId, int offeringId);
}
