using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NoName_Hotel.api.AuthorizationFilters;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.Contracts.AuthContracts;
using NoName_Hotel.Application.DTO.Admin;
using NoName_Hotel.Application.DTO.AuthDto;
using NoName_Hotel.Application.Querying;
using NoName_Hotel.Application.Services;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Common.QuerableXtensions.Paging;
using NoName_Hotel.Common.Results;
using NoName_Hotel.Domain.Entities.Models;

namespace NoName_Hotel.api.Controllers;

[ApiController, Authorize, Route("api/v1/admin")]
public sealed class AdminAccountController(IHotelRepository repo, IUserServices current, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await users.FindByIdAsync(current.UserId);
        if (user is null) return Unauthorized();
        var system = User.IsInRole(RoleNames.Administrator);
        var ids = await repo.Query<HotelAdmin>().Where(a => a.UserId == current.UserId).Select(a => a.HotelId).ToArrayAsync();
        if (!system && ids.Length == 0) return Forbid();
        Response.Headers.CacheControl = "no-store";
        return Ok(new AdminAccessDto(user.Id, user.FirstName, user.LastName, user.Email ?? "", system, ids));
    }

    [HttpPut("account")]
    public async Task<IActionResult> UpdateAccount(UpdateAccountDto dto)
    {
        if (!await HasAccess()) return Forbid();
        return AccountChange(await current.UpdateAccountAsync(dto));
    }

    [HttpPost("password"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Password(ChangePasswordDto dto)
    {
        if (!await HasAccess()) return Forbid();
        return AccountChange(await current.ChangePasswordAsync(dto));
    }

    private IActionResult AccountChange(Result result)
    {
        if (result.IsSuccess) return NoContent();
        if (result.Errors.Any(error => error.Code == ErrorCodes.NotFound)) return Unauthorized();
        return BadRequest(new { error = string.Join(" ", result.Errors.Select(error => error.Description)) });
    }

    private async Task<bool> HasAccess() => User.IsInRole(RoleNames.Administrator) || await repo.Query<HotelAdmin>().AnyAsync(a => a.UserId == current.UserId);
}

[ApiController, Authorize, HotelOrSystemAdmin, Route("api/v1/branches/{hotelId:int}/admin")]
public sealed class AdministrationController(IHotelRepository repo, AdministrationService admin, UserManager<ApplicationUser> users,
    IUserServices current, IHotelMailSender mail, ILogger<AdministrationController> logger) : BaseApiController
{
    [HttpGet("bookings")]
    public async Task<ActionResult<PageResult<AdminBookingDto>>> Bookings(int hotelId, [FromQuery] PaginationParameters page)
    {
        Response.Headers.CacheControl = "no-store";
        return ToActionResult(await admin.Bookings(hotelId, page));
    }

    [HttpPost("bookings/{bookingId:int}/check-in")]
    public async Task<IActionResult> CheckIn(int hotelId, int bookingId, CheckInDto dto) => ToActionResult(await admin.CheckIn(hotelId, bookingId, dto));
    [HttpPost("bookings/{bookingId:int}/check-out")]
    public async Task<IActionResult> CheckOut(int hotelId, int bookingId) => ToActionResult(await admin.CheckOut(hotelId, bookingId));

    [HttpGet("website")]
    public async Task<IActionResult> Website(int hotelId) => Ok(await admin.Website(hotelId, true));
    [HttpPut("website")]
    public async Task<IActionResult> Website(int hotelId, SaveWebsiteDto dto) => ToActionResult(await admin.SaveWebsite(hotelId, dto));

    [HttpGet("administrators")]
    public async Task<IActionResult> Administrators(int hotelId)
    {
        var assignments = await repo.Query<HotelAdmin>().Where(a => 
        a.HotelId == hotelId).Include(a => a.User).OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.UserId, a.User!.FirstName, a.User.LastName, a.User.Email }).ToListAsync();
        return Ok(assignments);
    }

    [HttpPost("administrators"), Authorize(Roles = RoleNames.Administrator)]
    public async Task<IActionResult> Assign(int hotelId, AssignAdministratorDto dto)
    {
        var user = await users.FindByEmailAsync(dto.Email.Trim());
        if (user is null) return BadRequest(new 
        { error = "No registered account has this email address. Ask the team member to register first." }
        );
        return await repo.ExecuteSerializableAsync<IActionResult>(async () =>
        {
            if (await repo.LockBranchAsync(hotelId) is null) return NotFound();
            if (await repo.Query<HotelAdmin>().AnyAsync(a => a.HotelId == hotelId && a.UserId == user.Id)) return Conflict(new { error = "This account already manages this branch." });
            var assignment = new HotelAdmin { HotelId = hotelId, UserId = user.Id };
            repo.Add(assignment); await repo.SaveChangesAsync(); return Ok(new { assignment.Id });
        });
    }

    [HttpDelete("administrators/{assignmentId:int}"), Authorize(Roles = RoleNames.Administrator)]
    public async Task<IActionResult> Revoke(int hotelId, int assignmentId)
    {
        var assignment = await repo.Query<HotelAdmin>(true).FirstOrDefaultAsync(a => a.Id == assignmentId && a.HotelId == hotelId);
        if (assignment is null) return NotFound();
        repo.Remove(assignment); await repo.SaveChangesAsync(); return NoContent();
    }

    [HttpGet("messages")]
    public async Task<IActionResult> Messages(int hotelId, [FromQuery] PaginationParameters page)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await repo.Query<HotelMessage>().Where(m => m.HotelId == hotelId).Include(m => m.Replies)
            .OrderByDescending(m => m.ReceivedAt).ThenBy(m => m.Id).ToPageResultAsync(page);
        return Ok(new { result.Metadata, Data = result.Data.Select(m => new { m.Id, m.SenderName, m.SenderEmail, m.Subject, m.Body, m.ReceivedAt, m.IsRead, m.IsArchived,
            Channel = "Website message", Replies = m.Replies.OrderBy(r => r.CreatedAt).Select(r => new { r.Id, r.Body, r.CreatedAt, r.DeliveryStatus }) }) });
    }

    [HttpGet("mail-status")]
    public IActionResult MailStatus() => Ok(new { ReplyConfigured = mail.IsConfigured, IncomingEmailConnected = false });

    [HttpPut("messages/{messageId:int}")]
    public async Task<IActionResult> MessageState(int hotelId, int messageId, MessageStateDto dto)
    {
        var m = await repo.Query<HotelMessage>(true).FirstOrDefaultAsync(m => m.Id == messageId && m.HotelId == hotelId);
        if (m is null) return NotFound();
        m.IsRead = dto.IsRead; m.IsArchived = dto.IsArchived;
        await repo.SaveChangesAsync(); return NoContent();
    }

    [HttpPost("messages/{messageId:int}/replies"), EnableRateLimiting("perUser")]
    public async Task<IActionResult> Reply(int hotelId, int messageId, ReplyMessageDto dto, CancellationToken cancellationToken)
    {
        if (dto.RequestId == Guid.Empty) return BadRequest(new { error = "A reply request ID is required." });
        if (!mail.IsConfigured) return StatusCode(503, new { error = "Hotel email delivery must be configured before replies can be sent." });
        var message = await repo.Query<HotelMessage>().FirstOrDefaultAsync(m => m.HotelId == hotelId && m.Id == messageId);
        if (message is null) return NotFound();
        // Reserve the unique request before contacting SMTP. Repeated requests never send twice.
        var reserved = await repo.ExecuteSerializableAsync(async () =>
        {
            await repo.LockBranchAsync(hotelId);
            if (await repo.Query<HotelMessageReply>().AnyAsync(r => r.Id == dto.RequestId)) return false;
            repo.Add(new HotelMessageReply { Id = dto.RequestId, HotelMessageId = messageId, Body = dto.Body.Trim(), AdministratorId = current.UserId });
            await repo.SaveChangesAsync(); return true;
        });
        if (!reserved) return Conflict(new { error = "This reply was already submitted. Refresh the conversation to check its delivery state." });
        var reply = await repo.Query<HotelMessageReply>(true).SingleAsync(r => r.Id == dto.RequestId);
        var website = await admin.Website(hotelId, true);
        try
        {
            await mail.SendReplyAsync(message.SenderEmail, message.Subject, reply.Body, website.PublicEmail, cancellationToken);
            reply.DeliveryStatus = "Sent";
        }
        catch (Exception ex) when (ex is System.Net.Mail.SmtpException or OperationCanceledException or InvalidOperationException)
        {
            reply.DeliveryStatus = "Delivery uncertain";
            logger.LogWarning("Reply {ReplyId} delivery could not be confirmed ({ErrorType})", reply.Id, ex.GetType().Name);
        }
        await repo.SaveChangesAsync();
        return Ok(new { reply.Id, reply.DeliveryStatus });
    }
}

[ApiController, Route("api/v1/branches/{hotelId:int}")]
public sealed class WebsiteController(IHotelRepository repo, AdministrationService admin) : ControllerBase
{
    [HttpGet("website"), AllowAnonymous]
    public async Task<IActionResult> Website(int hotelId)
    {
        if (!await repo.Query<Hotel>().AnyAsync(h => h.Id == hotelId)) return NotFound();
        return Ok(await admin.Website(hotelId, false));
    }

    [HttpPost("messages"), AllowAnonymous, EnableRateLimiting("contact")]
    public async Task<IActionResult> Contact(int hotelId, ContactMessageDto dto)
    {
        if (!await repo.Query<Hotel>().AnyAsync(h => h.Id == hotelId)) return NotFound();
        repo.Add(new HotelMessage { HotelId = hotelId, SenderName = dto.SenderName.Trim(), SenderEmail = dto.SenderEmail.Trim(), Subject = dto.Subject.Trim(), Body = dto.Body.Trim() });
        await repo.SaveChangesAsync(); return Accepted(new { message = "Your message has been received by the hotel." });
    }
}
