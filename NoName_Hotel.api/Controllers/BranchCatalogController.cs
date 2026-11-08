using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.api.AuthorizationFilters;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.DTO.Offerings;
using NoName_Hotel.Application.DTO.Rooms;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Domain;
using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.api.Controllers;

[ApiController]
[Route("api/v1/hotel")]
public sealed class HotelProfileController : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public IActionResult Get() => Ok(new
    {
        HotelBrand.Name, HotelBrand.Rating, City = HotelBrand.HomeCity, State = HotelBrand.HomeState,
        Country = HotelBrand.HomeCountry,
        TwinRooms = new { Floor = 1, FirstRoom = 160, LastRoom = 200, BedType = "Twin", BedCount = 2, Description = "Two separate single beds in one bedroom" },
        HoneymoonSuites = new[]
        {
            new { Category = "VVIP", Floor = 3, FirstRoom = 350, LastRoom = 400 },
            new { Category = "Platinum", Floor = 4, FirstRoom = 480, LastRoom = 500 }
        },
        RoomCategories = new[]
        {
            new { Name = "Normal", Floor = 1, FirstRoom = 100, LastRoom = 200 },
            new { Name = "VIP", Floor = 2, FirstRoom = 210, LastRoom = 300 },
            new { Name = "VVIP", Floor = 3, FirstRoom = 350, LastRoom = 400 },
            new { Name = "Platinum", Floor = 4, FirstRoom = 480, LastRoom = 500 }
        }
    });
}

public sealed class UploadRoomPhotoForm
{
    [Required] public IFormFile File { get; set; } = null!;
    [EnumDataType(typeof(RoomPhotoKind))] public RoomPhotoKind Kind { get; set; }
    [MaxLength(200)] public string Caption { get; set; } = string.Empty;
}

public sealed class UploadImageForm
{
    [Required] public IFormFile File { get; set; } = null!;
}

public sealed class UpdateRoomPhotoDto
{
    [EnumDataType(typeof(RoomPhotoKind))] public RoomPhotoKind Kind { get; set; }
    [MaxLength(200)] public string Caption { get; set; } = "";
}

[ApiController, Authorize]
[Route("api/v1/branches/{hotelId:int}")]
[Route("api/v1/hotels/{hotelId:int}")]
public sealed class BranchCatalogController(IBranchCatalogService catalog, IPhotoStorage storage, IHotelRepository repository) : BaseApiController
{
    [HttpGet("rooms"), AllowAnonymous]
    public async Task<ActionResult<PageResult<RoomDto>>> SearchRooms(int hotelId, [FromQuery] RoomSearch search, [FromQuery] PaginationParameters page) =>
        ToActionResult(await catalog.SearchRooms(hotelId, search, page));

    [HttpGet("rooms/{roomId:int}"), AllowAnonymous]
    public async Task<ActionResult<RoomDto>> GetRoom(int hotelId, int roomId, [FromQuery] RoomSearch search) =>
        ToActionResult(await catalog.GetPublicRoom(hotelId, roomId, search));

    [HttpGet("admin/rooms"), HotelOrSystemAdmin]
    public async Task<ActionResult<PageResult<RoomDto>>> AdminRooms(int hotelId, [FromQuery] PaginationParameters page) =>
        ToActionResult(await catalog.GetAdminRooms(hotelId, page));

    [HttpPost("rooms"), HotelOrSystemAdmin]
    public async Task<ActionResult<RoomDto>> CreateRoom(int hotelId, SaveRoomDto dto) => ToActionResult(await catalog.SaveRoom(hotelId, null, dto));

    [HttpPut("rooms/{roomId:int}"), HotelOrSystemAdmin]
    public async Task<ActionResult<RoomDto>> UpdateRoom(int hotelId, int roomId, SaveRoomDto dto) => ToActionResult(await catalog.SaveRoom(hotelId, roomId, dto));

    [HttpPost("rooms/{roomId:int}/photos"), HotelOrSystemAdmin]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<RoomPhotoDto>> UploadPhoto(int hotelId, int roomId, [FromForm] UploadRoomPhotoForm form, CancellationToken cancellationToken)
    {
        string url;
        try { await using var stream = form.File.OpenReadStream(); url = await storage.SaveAsync(stream, cancellationToken); }
        catch (ArgumentException e) { return BadRequest(new { error = e.Message }); }
        try
        {
            var result = await catalog.AddPhoto(hotelId, roomId, form.Kind, url, form.Caption);
            if (!result.IsSuccess) storage.Delete(url);
            return ToActionResult(result);
        }
        catch { storage.Delete(url); throw; }
    }

    [HttpDelete("rooms/{roomId:int}/photos/{photoId:int}"), HotelOrSystemAdmin]
    public async Task<IActionResult> DeletePhoto(int hotelId, int roomId, int photoId) => ToActionResult(await catalog.DeletePhoto(hotelId, roomId, photoId));

    [HttpPut("rooms/{roomId:int}/photos/{photoId:int}"), HotelOrSystemAdmin]
    public async Task<IActionResult> UpdatePhoto(int hotelId, int roomId, int photoId, UpdateRoomPhotoDto dto) =>
        await repository.ExecuteSerializableAsync<IActionResult>(async () =>
        {
            if (await repository.LockBranchAsync(hotelId) is null) return NotFound();
            var room = await repository.Query<Room>(true).Include(r => r.Photos).FirstOrDefaultAsync(r => r.Id == roomId && r.HotelId == hotelId);
            var photo = room?.Photos.SingleOrDefault(p => p.Id == photoId);
            if (photo is null) return NotFound();
            if (room!.IsPublished && photo.Kind != dto.Kind && room.Photos.Count(p => p.Kind == photo.Kind) == 1)
                return Conflict(new { error = "Unpublish this room before changing its last room or bathroom photo type." });
            photo.Kind = dto.Kind; photo.Caption = dto.Caption.Trim();
            await repository.SaveChangesAsync(); return NoContent();
        });

    [HttpPost("images"), HotelOrSystemAdmin]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadOfferingImage(int hotelId, [FromForm] UploadImageForm form, CancellationToken cancellationToken)
    {
        if (!await repository.Query<Hotel>().AnyAsync(h => h.Id == hotelId, cancellationToken)) return NotFound();
        try
        {
            await using var stream = form.File.OpenReadStream();
            return Ok(new { Url = await storage.SaveAsync(stream, cancellationToken) });
        }
        catch (ArgumentException e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpGet("offerings"), AllowAnonymous]
    public async Task<ActionResult<PageResult<OfferingDto>>> Offerings(int hotelId, [FromQuery] OfferingKind? kind, [FromQuery] string? search, [FromQuery] PaginationParameters page) =>
        ToActionResult(await catalog.GetOfferings(hotelId, kind, search, page));

    [HttpGet("admin/offerings"), HotelOrSystemAdmin]
    public async Task<ActionResult<PageResult<OfferingDto>>> AdminOfferings(int hotelId, [FromQuery] OfferingKind? kind, [FromQuery] string? search, [FromQuery] PaginationParameters page) =>
        ToActionResult(await catalog.GetOfferings(hotelId, kind, search, page, admin: true));

    [HttpPost("offerings"), HotelOrSystemAdmin]
    public async Task<ActionResult<OfferingDto>> CreateOffering(int hotelId, SaveOfferingDto dto) => ToActionResult(await catalog.SaveOffering(hotelId, null, dto));

    [HttpPut("offerings/{offeringId:int}"), HotelOrSystemAdmin]
    public async Task<ActionResult<OfferingDto>> UpdateOffering(int hotelId, int offeringId, SaveOfferingDto dto) => ToActionResult(await catalog.SaveOffering(hotelId, offeringId, dto));

    [HttpDelete("offerings/{offeringId:int}"), HotelOrSystemAdmin]
    public async Task<IActionResult> DeleteOffering(int hotelId, int offeringId) => ToActionResult(await catalog.DeleteOffering(hotelId, offeringId));
}
