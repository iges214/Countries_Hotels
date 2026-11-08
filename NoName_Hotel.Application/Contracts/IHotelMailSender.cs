namespace NoName_Hotel.Application.Contracts;

public interface IHotelMailSender
{
    bool IsConfigured { get; }
    Task SendReplyAsync(string recipient, string subject, string body, string? replyTo, CancellationToken cancellationToken);
}
