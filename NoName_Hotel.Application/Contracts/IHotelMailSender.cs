namespace NoName_Hotel.Application.Contracts;

public interface IHotelMailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken) =>
        SendReplyAsync(recipient, subject, body, null, cancellationToken);
    Task SendReplyAsync(string recipient, string subject, string body, string? replyTo, CancellationToken cancellationToken);
}
