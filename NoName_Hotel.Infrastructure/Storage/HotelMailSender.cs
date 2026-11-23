using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using NoName_Hotel.Application.Contracts;

namespace NoName_Hotel.Infrastructure.Storage;

public sealed class HotelMailSender(IConfiguration configuration) : IHotelMailSender
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["HotelMail:Host"]) &&
        !string.IsNullOrWhiteSpace(configuration["HotelMail:From"]);

    public async Task SendReplyAsync(string recipient, string subject, string body, string? replyTo, CancellationToken cancellationToken)
        => await SendMessageAsync(recipient, "Re: " + subject, body, replyTo, cancellationToken);

    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken) =>
        SendMessageAsync(recipient, subject, body, null, cancellationToken);

    private async Task SendMessageAsync(string recipient, string subject, string body, string? replyTo, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new InvalidOperationException("Hotel email delivery is not configured.");
        using var client = new SmtpClient(configuration["HotelMail:Host"], configuration.GetValue("HotelMail:Port", 587))
        {
            EnableSsl = configuration.GetValue("HotelMail:EnableSsl", true),
            Timeout = 20000
        };
        if (!string.IsNullOrWhiteSpace(configuration["HotelMail:Username"]))
            client.Credentials = new NetworkCredential(configuration["HotelMail:Username"], configuration["HotelMail:Password"]);
        using var message = new MailMessage(configuration["HotelMail:From"]!, recipient) { Subject = subject.Replace('\r', ' ').Replace('\n', ' '), Body = body };
        if (!string.IsNullOrWhiteSpace(replyTo)) message.ReplyToList.Add(replyTo);
        await client.SendMailAsync(message, cancellationToken);
    }
}
