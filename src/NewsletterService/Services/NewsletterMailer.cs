using System.Net;
using System.Net.Mail;

namespace NewsletterService.Services;

public sealed class NewsletterMailer(IConfiguration configuration)
{
    public async Task SendAsync(string email, string subject, string body)
    {
        var host = configuration["Smtp:Host"]
            ?? throw new InvalidOperationException("Smtp:Host is missing.");

        using var client = new SmtpClient(host)
        {
            Port = int.TryParse(configuration["Smtp:Port"], out var port) ? port : 25,
            EnableSsl = bool.TryParse(configuration["Smtp:UseSsl"], out var ssl) && ssl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        var username = configuration["Smtp:UserName"];
        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, configuration["Smtp:Password"]);

        using var message = new MailMessage(
            configuration["Smtp:From"] ?? "newsletter@happyheadlines.local",
            email, subject, body);
        await client.SendMailAsync(message);
    }
}
